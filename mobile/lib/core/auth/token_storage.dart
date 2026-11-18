import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Сохранённый вход: токен OneBase и до какого времени он действителен.
@immutable
class StoredToken {
  const StoredToken(this.token, this.expiresAt);

  final String token;
  final DateTime? expiresAt;

  bool isExpired(DateTime now) => expiresAt != null && !now.isBefore(expiresAt!);
}

/// Где хранится токен входа. В приложении — Keychain (iOS) и Keystore (Android); в тестах — память.
abstract interface class TokenStorage {
  Future<StoredToken?> read();

  Future<void> write(StoredToken token);

  Future<void> clear();
}

/// Защищённое хранилище:
/// - iOS — Keychain, доступ после первой разблокировки, без переноса на другое устройство через резервную копию;
/// - Android — шифрование ключом из Android Keystore.
///
/// Токен не попадает в журнал и в обычные настройки.
class SecureTokenStorage implements TokenStorage {
  SecureTokenStorage([FlutterSecureStorage? storage])
      : _storage = storage ??
            const FlutterSecureStorage(
              iOptions: IOSOptions(accessibility: KeychainAccessibility.first_unlock_this_device),
              aOptions: AndroidOptions(),
            );

  final FlutterSecureStorage _storage;

  static const _tokenKey = 'onebase.access_token';
  static const _expiresKey = 'onebase.access_token_expires';

  @override
  Future<StoredToken?> read() async {
    final token = await _storage.read(key: _tokenKey);
    if (token == null || token.isEmpty) return null;
    final expires = await _storage.read(key: _expiresKey);
    return StoredToken(token, expires == null ? null : DateTime.tryParse(expires));
  }

  @override
  Future<void> write(StoredToken token) async {
    await _storage.write(key: _tokenKey, value: token.token);
    if (token.expiresAt == null) {
      await _storage.delete(key: _expiresKey);
    } else {
      await _storage.write(key: _expiresKey, value: token.expiresAt!.toUtc().toIso8601String());
    }
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _tokenKey);
    await _storage.delete(key: _expiresKey);
  }
}

/// Хранилище в памяти — для тестов.
class MemoryTokenStorage implements TokenStorage {
  MemoryTokenStorage([this.value]);

  StoredToken? value;

  @override
  Future<StoredToken?> read() async => value;

  @override
  Future<void> write(StoredToken token) async => value = token;

  @override
  Future<void> clear() async => value = null;
}
