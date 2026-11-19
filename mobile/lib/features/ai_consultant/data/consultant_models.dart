import 'package:freezed_annotation/freezed_annotation.dart';

part 'consultant_models.freezed.dart';
part 'consultant_models.g.dart';

// AI-консультант OneBase (`/api/ai/*`, право `ai.agents.run`). Ответ — Markdown (`content`) и, у готового ответа, структура из блоков
// (`answer`) и подробности (`details`: источники, проверка чисел, сотрудники). Чаты и ответы на телефоне не сохраняются.

@freezed
abstract class ChatMessage with _$ChatMessage {
  const factory ChatMessage({
    required String id,

    /// user | assistant
    required String role,

    /// completed | failed
    @Default('completed') String status,
    @Default('') String content,
    String? error,
    required String createdAt,
    @Default(0) int durationMs,
    Map<String, Object?>? answer,
    Map<String, Object?>? details,
  }) = _ChatMessage;

  factory ChatMessage.fromJson(Map<String, Object?> json) => _$ChatMessageFromJson(json);
}

@freezed
abstract class ConversationSummary with _$ConversationSummary {
  const factory ConversationSummary({required String id, required String title, required String lastMessageAt, @Default('web') String source}) =
      _ConversationSummary;

  factory ConversationSummary.fromJson(Map<String, Object?> json) => _$ConversationSummaryFromJson(json);
}

@freezed
abstract class ConversationView with _$ConversationView {
  const factory ConversationView({required String id, required String title, @Default(<ChatMessage>[]) List<ChatMessage> messages}) = _ConversationView;

  factory ConversationView.fromJson(Map<String, Object?> json) => _$ConversationViewFromJson(json);
}

@freezed
abstract class AiStatus with _$AiStatus {
  const factory AiStatus({@Default(false) bool ready, String? message}) = _AiStatus;

  factory AiStatus.fromJson(Map<String, Object?> json) => _$AiStatusFromJson(json);
}

/// Шаг ответа (`progress`): этап, сотрудник, статус (Pending, Running, Done, Failed, Skipped), пояснение.
class ProgressStep {
  const ProgressStep({required this.stage, this.agent, this.agentName, required this.status, this.note});

  factory ProgressStep.fromJson(Map<String, Object?> j) => ProgressStep(
        stage: j['stage']! as String,
        agent: j['agent'] as String?,
        agentName: j['agentName'] as String?,
        status: j['status'] as String? ?? 'Running',
        note: j['note'] as String?,
      );

  final String stage;
  final String? agent;
  final String? agentName;
  final String status;
  final String? note;

  String get key => '$stage:${agent ?? ''}';
}

// ───────────── Структура ответа (блоки) ─────────────

sealed class AnswerBlock {
  const AnswerBlock();

  static AnswerBlock? fromJson(Map<String, Object?> j) {
    List<Map<String, Object?>> list(Object? v) => (v as List<Object?>? ?? const []).cast<Map<String, Object?>>();
    return switch (j['type']) {
      'summary' => SummaryBlock(j['text'] as String? ?? '', j['progress'] as Map<String, Object?>?),
      'kpis' => KpisBlock([
          for (final k in list(j['items']))
            (label: k['label'] as String? ?? '', value: k['value'] as String? ?? '', delta: k['delta'] as String?, tone: k['tone'] as String? ?? 'neutral'),
        ]),
      'facts' => FactsBlock(j['title'] as String?, [
          for (final r in list(j['rows']))
            (
              label: r['label'] as String? ?? '',
              value: r['value'] as String?,
              deltas: [
                for (final d in list(r['deltas'])) (value: d['value'] as String? ?? '', caption: d['caption'] as String?, tone: d['tone'] as String? ?? 'neutral'),
              ],
            ),
        ]),
      'deviations' => DeviationsBlock([
          for (final d in list(j['items'])) (title: d['title'] as String? ?? '', text: d['text'] as String? ?? '', high: d['severity'] == 'high'),
        ]),
      'recommendations' => RecommendationsBlock([
          for (final r in list(j['items']))
            (title: r['title'] as String? ?? '', text: r['text'] as String?, owner: r['owner'] as String?, dueDate: r['dueDate'] as String?),
        ]),
      'markdown' => MarkdownBlock(j['text'] as String? ?? ''),
      _ => null,
    };
  }
}

class SummaryBlock extends AnswerBlock {
  const SummaryBlock(this.text, this.progress);

  final String text;

  /// {label, fact, plan, forecast?, unit, forecastLabel?}
  final Map<String, Object?>? progress;
}

class KpisBlock extends AnswerBlock {
  const KpisBlock(this.items);

  final List<({String label, String value, String? delta, String tone})> items;
}

class FactsBlock extends AnswerBlock {
  const FactsBlock(this.title, this.rows);

  final String? title;
  final List<({String label, String? value, List<({String value, String? caption, String tone})> deltas})> rows;
}

class DeviationsBlock extends AnswerBlock {
  const DeviationsBlock(this.items);

  final List<({String title, String text, bool high})> items;
}

class RecommendationsBlock extends AnswerBlock {
  const RecommendationsBlock(this.items);

  final List<({String title, String? text, String? owner, String? dueDate})> items;
}

class MarkdownBlock extends AnswerBlock {
  const MarkdownBlock(this.text);

  final String text;
}

/// Разобранный готовый ответ: блоки, уточнения, сводка, источники, проверка чисел.
class AnswerView {
  const AnswerView({
    required this.blocks,
    required this.followUps,
    required this.facts,
    required this.recommendations,
    required this.sources,
    this.checked,
    this.supported,
  });

  factory AnswerView.of(ChatMessage m) {
    final a = m.answer;
    final d = m.details;
    final meta = a?['meta'] as Map<String, Object?>?;
    final grounding = d?['grounding'] as Map<String, Object?>?;
    return AnswerView(
      blocks: [
        for (final b in (a?['blocks'] as List<Object?>? ?? const []).cast<Map<String, Object?>>()) ?AnswerBlock.fromJson(b),
        if (a == null && m.content.isNotEmpty) MarkdownBlock(m.content),
      ],
      followUps: [for (final f in a?['followUps'] as List<Object?>? ?? const []) f.toString()],
      facts: meta?['facts'] as int? ?? 0,
      recommendations: meta?['recommendations'] as int? ?? 0,
      sources: [
        for (final s in (d?['sources'] as List<Object?>? ?? const []).cast<Map<String, Object?>>())
          (title: s['title'] as String? ?? '', period: s['period'] as String?, href: s['href'] as String?),
      ],
      checked: grounding?['checked'] as int?,
      supported: grounding?['supported'] as int?,
    );
  }

  final List<AnswerBlock> blocks;
  final List<String> followUps;
  final int facts;
  final int recommendations;
  final List<({String title, String? period, String? href})> sources;
  final int? checked;
  final int? supported;
}
