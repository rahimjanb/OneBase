import 'package:flutter/foundation.dart';

import '../../l10n/gen/app_localizations.dart';
import '../api/api_exception.dart';

/// Ошибка для экрана: вид и, если есть, текст сервера. Текст выбирается при показе ([describe]) — на текущем языке.
@immutable
class AppFailure {
  const AppFailure(this.kind, {this.serverMessage});

  factory AppFailure.from(Object error) => switch (error) {
        final AppFailure f => f,
        final ApiException e => AppFailure(e.kind, serverMessage: e.message),
        _ => const AppFailure(ApiErrorKind.unknown),
      };

  final ApiErrorKind kind;
  final String? serverMessage;

  /// Есть смысл нажать «Повторить»: сбой сети или сервера, а не отказ в доступе.
  bool get canRetry => switch (kind) {
        ApiErrorKind.forbidden || ApiErrorKind.notFound || ApiErrorKind.unauthorized || ApiErrorKind.validation => false,
        _ => true,
      };

  /// Понятный текст. Для проверки данных, конфликта и запрета сервер присылает свой текст (уже на языке пользователя) — он точнее.
  String describe(AppLocalizations l10n) {
    final server = serverMessage;
    if (server != null &&
        (kind == ApiErrorKind.validation || kind == ApiErrorKind.conflict || kind == ApiErrorKind.forbidden)) {
      return server;
    }
    return switch (kind) {
      ApiErrorKind.network => l10n.errorNetwork,
      ApiErrorKind.timeout => l10n.errorTimeout,
      ApiErrorKind.unauthorized => l10n.errorUnauthorized,
      ApiErrorKind.forbidden => l10n.errorForbidden,
      ApiErrorKind.notFound => l10n.errorNotFound,
      ApiErrorKind.rateLimited => l10n.errorRateLimited,
      ApiErrorKind.server => l10n.errorServer,
      ApiErrorKind.invalidResponse => l10n.errorInvalidResponse,
      ApiErrorKind.validation || ApiErrorKind.conflict => server ?? l10n.errorUnknown,
      ApiErrorKind.cancelled || ApiErrorKind.unknown => l10n.errorUnknown,
    };
  }

  @override
  bool operator ==(Object other) => other is AppFailure && other.kind == kind && other.serverMessage == serverMessage;

  @override
  int get hashCode => Object.hash(kind, serverMessage);

  @override
  String toString() => 'AppFailure($kind)';
}
