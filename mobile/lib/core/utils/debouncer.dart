import 'dart:async';

/// Отложить действие до паузы во вводе (поиск: запрос уходит, когда пользователь перестал печатать).
class Debouncer {
  Debouncer([this.delay = const Duration(milliseconds: 350)]);

  final Duration delay;
  Timer? _timer;

  void call(void Function() action) {
    _timer?.cancel();
    _timer = Timer(delay, action);
  }

  void cancel() => _timer?.cancel();

  bool get pending => _timer?.isActive ?? false;
}
