import 'dart:async';

/// Токен текущего входа в памяти: перехватчик берёт его синхронно на каждом запросе. Источник правды — защищённое хранилище
/// (TokenStorage), сюда токен кладёт AuthController.
class TokenHolder {
  String? value;
}

/// Сигнал «сервер ответил 401 на запрос с токеном» — сеанс закрыт на сервере. Слушает AuthController.
class UnauthorizedSignal {
  final _controller = StreamController<void>.broadcast();

  Stream<void> get stream => _controller.stream;

  void emit() {
    if (!_controller.isClosed) _controller.add(null);
  }

  Future<void> close() => _controller.close();
}
