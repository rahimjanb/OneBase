import 'package:flutter/cupertino.dart' show CupertinoPageTransitionsBuilder;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'tokens.dart';

/// Тема OneBase на Material 3: сдержанная, «как iOS» — ровные поверхности, тонкие линии вместо теней, системный шрифт
/// (SF Pro / Roboto), крупные цифры. Цвета — из [ObColors].
abstract final class ObTheme {
  static ThemeData light({TargetPlatform? platform}) => _build(ObColors.light, Brightness.light, platform);

  static ThemeData dark({TargetPlatform? platform}) => _build(ObColors.dark, Brightness.dark, platform);

  static ThemeData _build(ObColors c, Brightness brightness, TargetPlatform? platform) {
    final isDark = brightness == Brightness.dark;
    final scheme = ColorScheme(
      brightness: brightness,
      primary: c.accent,
      onPrimary: Colors.white,
      primaryContainer: c.accentSoft,
      onPrimaryContainer: c.accentStrong,
      secondary: c.accentStrong,
      onSecondary: Colors.white,
      secondaryContainer: c.accentSoft,
      onSecondaryContainer: c.accentStrong,
      tertiary: c.ok,
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
      scrim: Colors.black54,
      inverseSurface: c.ink,
      onInverseSurface: c.surface,
      inversePrimary: c.accentSoft,
    );

    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      platform: platform,
      visualDensity: VisualDensity.standard,
    );
    final text = _text(base.textTheme, c);
    final isApple = (platform ?? base.platform) == TargetPlatform.iOS || (platform ?? base.platform) == TargetPlatform.macOS;
    final field = OutlineInputBorder(
      borderRadius: BorderRadius.circular(ObRadius.md),
      borderSide: BorderSide.none,
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
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: c.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        height: 64,
        indicatorColor: c.accentSoft,
        indicatorShape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md)),
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        iconTheme: WidgetStateProperty.resolveWith(
          (s) => IconThemeData(size: 24, color: s.contains(WidgetState.selected) ? c.accentStrong : c.ink3),
        ),
        labelTextStyle: WidgetStateProperty.resolveWith(
          (s) => text.labelSmall!.copyWith(
            fontSize: 11,
            fontWeight: s.contains(WidgetState.selected) ? FontWeight.w600 : FontWeight.w500,
            color: s.contains(WidgetState.selected) ? c.accentStrong : c.ink3,
          ),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: c.muted,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
        border: field,
        enabledBorder: field,
        focusedBorder: field.copyWith(borderSide: BorderSide(color: c.accent, width: 1.5)),
        errorBorder: field.copyWith(borderSide: BorderSide(color: c.bad, width: 1.2)),
        focusedErrorBorder: field.copyWith(borderSide: BorderSide(color: c.bad, width: 1.5)),
        hintStyle: text.bodyLarge!.copyWith(color: c.ink3),
        labelStyle: text.bodyMedium!.copyWith(color: c.ink2),
        errorStyle: text.bodySmall!.copyWith(color: c.bad),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: c.accent,
          foregroundColor: Colors.white,
          disabledBackgroundColor: c.accent.withValues(alpha: 0.4),
          disabledForegroundColor: Colors.white.withValues(alpha: 0.8),
          minimumSize: const Size(64, 52),
          textStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w600, fontSize: 16),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: c.ink,
          minimumSize: const Size(64, 48),
          side: BorderSide(color: c.line),
          textStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md)),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: c.accentStrong,
          textStyle: text.titleSmall!.copyWith(fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.sm)),
        ),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: SegmentedButton.styleFrom(
          backgroundColor: c.muted,
          foregroundColor: c.ink2,
          selectedBackgroundColor: c.surface,
          selectedForegroundColor: c.ink,
          side: BorderSide(color: c.line),
          textStyle: text.labelLarge,
        ),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: c.muted,
        selectedColor: c.accentSoft,
        side: BorderSide.none,
        labelStyle: text.labelLarge!.copyWith(color: c.ink),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.sm)),
      ),
      dividerTheme: DividerThemeData(color: c.line, thickness: 1, space: 1),
      listTileTheme: ListTileThemeData(
        iconColor: c.ink2,
        textColor: c.ink,
        contentPadding: const EdgeInsets.symmetric(horizontal: ObSpace.lg),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md)),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: c.surface,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        dragHandleColor: c.lineStrong,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(ObRadius.xl))),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: c.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.xl)),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: c.ink,
        contentTextStyle: text.bodyMedium!.copyWith(color: c.surface),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ObRadius.md)),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: c.accent, linearTrackColor: c.muted),
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.macOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
        },
      ),
    );
  }

  /// Шкала текста: крупные заголовки с плотным трекингом, спокойный основной текст.
  static TextTheme _text(TextTheme base, ObColors c) {
    TextStyle? s(TextStyle? t, double size, FontWeight w, Color color, {double? spacing, double? height}) =>
        t?.copyWith(fontSize: size, fontWeight: w, color: color, letterSpacing: spacing, height: height);
    return base.copyWith(
      displaySmall: s(base.displaySmall, 34, FontWeight.w700, c.ink, spacing: -0.8, height: 1.1),
      headlineMedium: s(base.headlineMedium, 28, FontWeight.w700, c.ink, spacing: -0.6, height: 1.15),
      headlineSmall: s(base.headlineSmall, 22, FontWeight.w700, c.ink, spacing: -0.4, height: 1.2),
      titleLarge: s(base.titleLarge, 18, FontWeight.w600, c.ink, spacing: -0.2),
      titleMedium: s(base.titleMedium, 16, FontWeight.w600, c.ink, spacing: -0.1),
      titleSmall: s(base.titleSmall, 14, FontWeight.w600, c.ink),
      bodyLarge: s(base.bodyLarge, 16, FontWeight.w400, c.ink, height: 1.35),
      bodyMedium: s(base.bodyMedium, 14, FontWeight.w400, c.ink2, height: 1.35),
      bodySmall: s(base.bodySmall, 12, FontWeight.w400, c.ink3, height: 1.3),
      labelLarge: s(base.labelLarge, 14, FontWeight.w500, c.ink),
      labelMedium: s(base.labelMedium, 12, FontWeight.w500, c.ink2),
      labelSmall: s(base.labelSmall, 11, FontWeight.w500, c.ink3, spacing: 0.1),
    );
  }
}

/// Цифры одинаковой ширины — для KPI и таблиц, чтобы числа не «прыгали» при обновлении.
const tabularFigures = [FontFeature.tabularFigures()];
