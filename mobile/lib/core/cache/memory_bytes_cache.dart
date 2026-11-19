import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Кэш картинок вложений в памяти (LRU по объёму). Вложения задач — рабочие документы: на диск не пишутся, сервер отдаёт их с
/// `Cache-Control: private, no-store`; ключ — id вложения (содержимое не меняется — только удалить и загрузить новое).
class MemoryBytesCache {
  MemoryBytesCache({this.maxBytes = 24 * 1024 * 1024});

  final int maxBytes;
  final _items = <String, Uint8List>{};
  int _size = 0;

  int get size => _size;

  Uint8List? get(String key) {
    final v = _items.remove(key);
    if (v != null) _items[key] = v;
    return v;
  }

  void put(String key, Uint8List bytes) {
    if (bytes.length > maxBytes) return;
    final old = _items.remove(key);
    if (old != null) _size -= old.length;
    _items[key] = bytes;
    _size += bytes.length;
    while (_size > maxBytes && _items.isNotEmpty) {
      final first = _items.keys.first;
      _size -= _items.remove(first)!.length;
    }
  }

  void clear() {
    _items.clear();
    _size = 0;
  }
}

final memoryBytesCacheProvider = Provider<MemoryBytesCache>((ref) => MemoryBytesCache());
