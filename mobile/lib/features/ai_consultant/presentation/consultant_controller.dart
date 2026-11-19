import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/api/sse.dart';
import '../../../core/errors/app_failure.dart';
import '../data/consultant_api.dart';
import '../data/consultant_models.dart';

/// Состояние ответа: ждём, идёт поток, готово, ошибка, остановлено, восстанавливаем после обрыва.
enum ChatPhase { idle, sending, streaming, failed, cancelled, recovering }

/// Черновик ответа: пишется, проверяются числа, переписывается.
enum DraftMode { writing, checking, revising }

@immutable
class ChatState {
  const ChatState({
    this.conversationId,
    this.title,
    this.messages = const [],
    this.phase = ChatPhase.idle,
    this.steps = const {},
    this.draft = '',
    this.draftMode = DraftMode.writing,
    this.pendingQuestion,
    this.failure,
    this.loading = false,
  });

  final String? conversationId;
  final String? title;
  final List<ChatMessage> messages;
  final ChatPhase phase;
  final Map<String, ProgressStep> steps;
  final String draft;
  final DraftMode draftMode;

  /// Вопрос, отправленный, но ещё не подтверждённый сервером (`start`).
  final String? pendingQuestion;
  final AppFailure? failure;

  /// Загружается сохранённый чат.
  final bool loading;

  bool get busy => phase == ChatPhase.sending || phase == ChatPhase.streaming || phase == ChatPhase.recovering;

  ChatState copyWith({
    String? conversationId,
    String? title,
    List<ChatMessage>? messages,
    ChatPhase? phase,
    Map<String, ProgressStep>? steps,
    String? draft,
    DraftMode? draftMode,
    String? Function()? pendingQuestion,
    AppFailure? Function()? failure,
    bool? loading,
  }) =>
      ChatState(
        conversationId: conversationId ?? this.conversationId,
        title: title ?? this.title,
        messages: messages ?? this.messages,
        phase: phase ?? this.phase,
        steps: steps ?? this.steps,
        draft: draft ?? this.draft,
        draftMode: draftMode ?? this.draftMode,
        pendingQuestion: pendingQuestion == null ? this.pendingQuestion : pendingQuestion(),
        failure: failure == null ? this.failure : failure(),
        loading: loading ?? this.loading,
      );
}

/// Диалог с консультантом. Вопрос — по нажатию пользователя (вызов модели платный): без автоповтора и без очереди. Обрыв после `start` —
/// сервер дописывает ответ сам; экран опрашивает чат (3, 6, 12, 24 … с, до 6 минут), пока ответ не появится. Ничего не сохраняется
/// на телефоне — история только в памяти и на сервере.
class ConsultantController extends Notifier<ChatState> {
  ConsultantController(this.initialId);

  final String? initialId;
  CancelToken? _cancel;
  Timer? _recover;

  @override
  ChatState build() {
    ref.onDispose(() {
      _cancel?.cancel('Экран закрыт');
      _recover?.cancel();
    });
    if (initialId != null) {
      Future.microtask(() => open(initialId!));
      return ChatState(conversationId: initialId, loading: true);
    }
    return const ChatState();
  }

  ConsultantApi get _api => ref.read(consultantApiProvider);

  Future<void> open(String id) async {
    state = state.copyWith(conversationId: id, loading: true, failure: () => null);
    try {
      final c = await _api.conversation(id, null);
      if (!ref.mounted) return;
      state = state.copyWith(title: c.title, messages: c.messages, loading: false);
    } on Object catch (e) {
      if (ref.mounted) state = state.copyWith(loading: false, failure: () => AppFailure.from(e));
    }
  }

  /// Задать вопрос. [context] — период и отдел экрана (сервер дописывает их к вопросу).
  Future<void> ask(String question, {Map<String, Object?>? context}) async {
    final text = question.trim();
    if (text.isEmpty || text.length > 4000 || state.busy) return;
    _recover?.cancel();
    final cancel = _cancel = CancelToken();
    state = state.copyWith(phase: ChatPhase.sending, steps: const {}, draft: '', draftMode: DraftMode.writing, pendingQuestion: () => text, failure: () => null);
    var started = false;
    try {
      await for (final e in _api.chat(conversationId: state.conversationId, message: text, context: context, cancel: cancel)) {
        if (!ref.mounted || cancel.isCancelled) return;
        started = _apply(e) || started;
      }
      if (ref.mounted && state.phase == ChatPhase.streaming) {
        // Поток закрылся без done/error — дописывается на сервере.
        _startRecovery();
      }
    } on Object catch (e) {
      if (!ref.mounted) return;
      if (cancel.isCancelled) {
        state = state.copyWith(phase: ChatPhase.cancelled);
        if (started) _startRecovery(quiet: true);
        return;
      }
      if (started) {
        _startRecovery();
      } else {
        state = state.copyWith(phase: ChatPhase.failed, failure: () => AppFailure.from(e));
      }
    }
  }

  /// Обработать событие; true — пришёл `start`.
  bool _apply(SseEvent e) {
    final data = jsonDecode(e.data) as Map<String, Object?>;
    switch (e.event) {
      case 'start':
        final user = ChatMessage.fromJson(data['userMessage']! as Map<String, Object?>);
        state = state.copyWith(
          conversationId: data['conversationId'] as String?,
          title: data['title'] as String?,
          messages: [...state.messages, user],
          phase: ChatPhase.streaming,
          pendingQuestion: () => null,
        );
        return true;
      case 'progress':
        final step = ProgressStep.fromJson(data);
        state = state.copyWith(steps: {...state.steps, step.key: step}, phase: ChatPhase.streaming);
      case 'draft':
        switch (data['kind']) {
          case 'text':
            state = state.copyWith(draft: state.draft + (data['text'] as String? ?? ''), draftMode: DraftMode.writing);
          case 'reset':
            state = state.copyWith(draft: '');
          case 'checking':
            state = state.copyWith(draftMode: DraftMode.checking);
          case 'revising':
            state = state.copyWith(draft: '', draftMode: DraftMode.revising);
        }
      case 'done':
        final m = ChatMessage.fromJson(data['message']! as Map<String, Object?>);
        state = state.copyWith(messages: [...state.messages, m], phase: ChatPhase.idle, draft: '', steps: const {});
      case 'error':
        final msg = data['message'];
        state = state.copyWith(
          messages: msg is Map<String, Object?> ? [...state.messages, ChatMessage.fromJson(msg)] : state.messages,
          phase: ChatPhase.failed,
          draft: '',
          failure: () => AppFailure(ApiErrorKind.validation, serverMessage: data['error'] as String?),
        );
    }
    return false;
  }

  /// Остановить ответ: связь закрывается, сервер может дописать ответ сам (он появится в чате при обновлении).
  void stop() => _cancel?.cancel('Остановлено');

  void _startRecovery({bool quiet = false}) {
    final id = state.conversationId;
    if (id == null) return;
    if (!quiet) state = state.copyWith(phase: ChatPhase.recovering);
    var delay = 3;
    var waited = 0;
    final count = state.messages.length;
    Future<void> tick() async {
      try {
        final c = await _api.conversation(id, null);
        if (!ref.mounted) return;
        if (c.messages.length > count && c.messages.last.role == 'assistant') {
          state = state.copyWith(messages: c.messages, phase: ChatPhase.idle, draft: '', steps: const {});
          return;
        }
      } on Object {
        // Сеть — следующая попытка.
      }
      waited += delay;
      if (waited > 360 || !ref.mounted) {
        if (ref.mounted && !quiet) state = state.copyWith(phase: ChatPhase.failed);
        return;
      }
      delay = delay * 2 > 30 ? 30 : delay * 2;
      _recover = Timer(Duration(seconds: delay), tick);
    }

    _recover = Timer(Duration(seconds: delay), tick);
  }

  /// Новый чат.
  void reset() {
    _cancel?.cancel('Новый чат');
    _recover?.cancel();
    state = const ChatState();
  }
}

final consultantProvider = NotifierProvider.autoDispose.family<ConsultantController, ChatState, String?>(ConsultantController.new);
