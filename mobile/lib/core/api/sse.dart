import 'dart:async';
import 'dart:convert';

/// Событие потока Server-Sent Events: имя (`start`, `progress`, `draft`, `done`, `error`) и данные (строка JSON).
class SseEvent {
  const SseEvent(this.event, this.data);

  final String event;
  final String data;

  @override
  String toString() => 'SseEvent($event, ${data.length} символов)';
}

/// Разбор потока SSE (как `streamChat` веба): байты → UTF-8 с учётом разрыва многобайтных символов между кусками, `\r\n` → `\n`, кадры по
/// пустой строке; `event:` / `data:` (один пробел после двоеточия убирается), несколько `data:` склеиваются через `\n`; строки с `:` —
/// пульс (пропускается, но сообщает, что связь жива); кадр без данных — пропускается.
class SseParser extends StreamTransformerBase<List<int>, SseEvent> {
  const SseParser({this.onPing});

  /// Вызывается на каждый пульс сервера (`: ping`), чтобы экран знал, что связь жива.
  final void Function()? onPing;

  @override
  Stream<SseEvent> bind(Stream<List<int>> stream) async* {
    var buffer = '';
    await for (final chunk in stream.transform(utf8.decoder)) {
      // `\r` в конце куска может оказаться парой к `\n` в начале следующего — нормализуем весь буфер, хвостовой `\r` ждёт.
      buffer = (buffer + chunk).replaceAll('\r\n', '\n');
      while (true) {
        final end = buffer.indexOf('\n\n');
        if (end < 0) break;
        final frame = buffer.substring(0, end);
        buffer = buffer.substring(end + 2);
        final e = _frame(frame);
        if (e != null) yield e;
      }
    }
    final rest = buffer.trim();
    if (rest.isNotEmpty) {
      final e = _frame(rest);
      if (e != null) yield e;
    }
  }

  SseEvent? _frame(String frame) {
    var event = 'message';
    final data = <String>[];
    for (final line in frame.split('\n')) {
      if (line.isEmpty) continue;
      if (line.startsWith(':')) {
        onPing?.call();
        continue;
      }
      final colon = line.indexOf(':');
      final field = colon < 0 ? line : line.substring(0, colon);
      var value = colon < 0 ? '' : line.substring(colon + 1);
      if (value.startsWith(' ')) value = value.substring(1);
      if (field == 'event') event = value;
      if (field == 'data') data.add(value);
    }
    return data.isEmpty ? null : SseEvent(event, data.join('\n'));
  }
}
