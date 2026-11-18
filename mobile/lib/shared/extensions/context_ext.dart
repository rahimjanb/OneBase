import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../core/localization/formatters.dart';
import '../../l10n/gen/app_localizations.dart';

extension ObContext on BuildContext {
  /// Цвета OneBase текущей темы.
  ObColors get ob => Theme.of(this).extension<ObColors>() ?? ObColors.light;

  TextTheme get text => Theme.of(this).textTheme;

  AppLocalizations get l10n => AppLocalizations.of(this);

  /// Форматы чисел и дат на языке интерфейса.
  Fmt get fmt => Fmt(AppLocalizations.of(this));
}
