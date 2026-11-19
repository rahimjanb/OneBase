import 'package:flutter/cupertino.dart' show CupertinoPageTransitionsBuilder;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'tokens.dart';

/// Шрифт веба OneBase (Onest, кириллица и латиница; веса 400–800 — статические начертания в `assets/fonts`).
const obFont = 'Onest';

/// Тема OneBase на Material 3 по мобильному дизайну веба: карточки с тонкой рамкой без теней, скругления 20, шрифт Onest,
/// крупные цифры с плотным трекингом. Цвета — из [ObColors].
abstract final class ObTheme {
  static ThemeData light({TargetPlatform? platform}) => _build(ObColors.light, platform);

  static ThemeData dark({TargetPlatform? platform}) => _build(ObColors.dark, platform);

  static ThemeData _build(ObColors c, TargetPlatform? platform) {
    final brightness = c.brightness;
    final isDark = c.isDark;
    final scheme = ColorScheme(
      brightness: brightness,
      primary: c.accentFill,
      onPrimary: Colors.white,
      primaryContainer: c.accentSoft,
      onPrimaryContainer: c.accentStrong,
      secondary: c.accentStrong,
      onSecondary: Colors.white,
      secondaryContainer: c.accentSoft,
      onSecondaryContainer: c.accentStrong,
      tertiary: c.violet,
      onTertiary: Colors.white,
      error: c.bad,
      onError: Colors.white,
      errorContainer: c.badSoft,
      onErrorContainer: c.bad,
      surface: c.surface,
      onSurface: c.ink,
      onSurfaceVariant: c.ink2,
      surfaceContainerLowest: c.surface,
      surfaceContainerLow: c.surface,
      surfaceContainer: c.muted,
      surfaceContainerHigh: c.muted,
      surfaceContainerHighest: c.muted,
      outline: c.lineStrong,
      outlineVariant: c.line,
      shadow: Colors.black,
      scrim: c.nav.overlay,
      inverseSurface: c.popBg,
      onInverseSurface: c.popText,
      inversePrimary: c.accentSoft,
    );

    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      platform: platform,
      fontFamily: obFont,
      visualDensity: VisualDensity.standard,
    );
    final text = _text(base.textTheme, c);
    final isApple = (platform ?? base.platform) == TargetPlatform.iOS || (platform ?? base.platform) == TargetPlatform.macOS;
    final field = OutlineInputBorder(
      borderRadius: BorderRadius.circular(ObRadius.md),
      borderSide: BorderSide(color: c.line),
    );

    return base.copyWith(
      extensions: [c],
      scaffoldBackgroundColor: c.page,
      canvasColor: c.page,
      textTheme: text,
      dividerColor: c.line,
      splashFactory: isApple ? NoSplash.splashFactory : InkSparkle.splashFactory,
      highlightColor: isApple ? c.ink.withValues(alpha: 0.05) : null,
      appBarTheme: AppBarTheme(
        backgroundColor: c.page,
        surfaceTintColor: Colors.transparent,
        foregroundColor: c.ink,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: isApple,
        titleTextStyle: text.titleLarge,
        systemOverlayStyle: isDark ? SystemUiOverlayStyle.light : SystemUiOverlayStyle.dark,
      ),
      cardTheme: CardThemeData(
        color: c.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(ObRadius.lg),
          side: BorderSide(color: c.line),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: c.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
        border: field,
        enabledBorder: field,
        focusedBorder: field.copyWith(borderSide: BorderSide(color: c.accent, width: 1.5)),
        errorBorder: field.copyWith(borderSide: BorderSide(color: c.bad, width: 1.2)),
        focusedErrorBorder: field.copyWith(borderSide: BorderSide(color: c.bad, width: 1.5)),
        hintStyle: text.bodyLarge!.copyWith(color: c.subtle),
        labelStyle: text.bodyMedium!.copyWith(color: c.ink3),
        errorStyle: text.bodySmall!.copyWith(color: c.bad),
        prefixIconColor: c.ink3,
        suffixIconColor: c.ink3,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: c.accentFill,
          foregroundColor: Colors.white,
          disabledBackgroundColor: c.accentFill.withValues(alpha: 0.4),
          disabledForegroundColor: Colors.white.withValues(alpha: 0.8),
          minimumSize: const Size(64, 52),
          textStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w600, fontSize: 16),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.tile)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: c.ink,
          backgroundColor: c.surface,
          minimumSize: const Size(64, 48),
          side: BorderSide(color: c.line),
          textStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.tile)),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: c.accentStrong,
          textStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w600, fontSize: 13),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.sm)),
        ),
      ),
      iconButtonTheme: IconButtonThemeData(style: IconButton.styleFrom(foregroundColor: c.ink2)),
      chipTheme: ChipThemeData(
        backgroundColor: c.surface,
        selectedColor: c.accentSoft,
        side: BorderSide(color: c.line),
        labelStyle: text.labelLarge!.copyWith(color: c.ink),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
      ),
      dividerTheme: DividerThemeData(color: c.line, thickness: 1, space: 1),
      listTileTheme: ListTileThemeData(
        iconColor: c.ink2,
        textColor: c.ink,
        contentPadding: const EdgeInsets.symmetric(horizontal: ObSpace.lg),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md)),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: isDark ? const Color(0xFF0E1629) : c.surface,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        dragHandleColor: c.lineStrong,
        modalBarrierColor: c.nav.overlay,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(ObRadius.sheet))),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: isDark ? const Color(0xFF0E1629) : c.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.xl)),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: c.popBg,
        contentTextStyle: text.bodyMedium!.copyWith(color: c.popText),
        actionTextColor: isDark ? c.accentStrong : const Color(0xFF8DB0FF),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md)),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: c.accent, linearTrackColor: c.track, circularTrackColor: c.track),
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? Colors.white : c.ink3),
        trackColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? c.accentFill : c.muted),
        trackOutlineColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? Colors.transparent : c.lineStrong),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: c.ink,
        unselectedLabelColor: c.ink3,
        indicatorColor: c.accent,
        dividerColor: c.line,
        labelStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w600),
        unselectedLabelStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w500),
      ),
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.macOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
        },
      ),
    );
  }

  /// Шкала текста по мобильному «Обзору» веба: приветствие 26/700, заголовок секции 17/700, основной текст 14–15, подписи 11–12.
  static TextTheme _text(TextTheme base, ObColors c) {
    TextStyle? s(TextStyle? t, double size, FontWeight w, Color color, {double? spacing, double? height}) =>
        t?.copyWith(fontFamily: obFont, fontSize: size, fontWeight: w, color: color, letterSpacing: spacing, height: height);
    return base.copyWith(
      displayLarge: s(base.displayLarge, 44, FontWeight.w700, c.ink, spacing: -1.5, height: 1),
      displayMedium: s(base.displayMedium, 36, FontWeight.w700, c.ink, spacing: -1, height: 1.05),
      displaySmall: s(base.displaySmall, 30, FontWeight.w700, c.ink, spacing: -0.8, height: 1.1),
      headlineMedium: s(base.headlineMedium, 26, FontWeight.w700, c.ink, spacing: -0.5, height: 1.15),
      headlineSmall: s(base.headlineSmall, 22, FontWeight.w700, c.ink, spacing: -0.4, height: 1.2),
      titleLarge: s(base.titleLarge, 17, FontWeight.w700, c.ink, spacing: -0.2, height: 1.25),
      titleMedium: s(base.titleMedium, 15, FontWeight.w600, c.ink, height: 1.3),
      titleSmall: s(base.titleSmall, 14, FontWeight.w600, c.ink, height: 1.3),
      bodyLarge: s(base.bodyLarge, 15, FontWeight.w400, c.ink, height: 1.4),
      bodyMedium: s(base.bodyMedium, 14, FontWeight.w400, c.ink2, height: 1.4),
      bodySmall: s(base.bodySmall, 12, FontWeight.w400, c.ink3, height: 1.35),
      labelLarge: s(base.labelLarge, 13, FontWeight.w600, c.ink),
      labelMedium: s(base.labelMedium, 12, FontWeight.w500, c.ink3),
      labelSmall: s(base.labelSmall, 11, FontWeight.w500, c.ink3, spacing: 0.1),
    );
  }
}

/// Цифры одинаковой ширины — для KPI и таблиц, чтобы числа не «прыгали» при обновлении.
const tabularFigures = [FontFeature.tabularFigures()];
