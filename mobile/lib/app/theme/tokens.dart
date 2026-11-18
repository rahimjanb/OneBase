import 'package:flutter/material.dart';

/// Цвета OneBase — те же токены, что у веба (`frontend/src/app/globals.css`), чтобы веб и приложение выглядели одним продуктом.
@immutable
class ObColors extends ThemeExtension<ObColors> {
  const ObColors({
    required this.page,
    required this.surface,
    required this.muted,
    required this.line,
    required this.lineStrong,
    required this.ink,
    required this.ink2,
    required this.ink3,
    required this.accent,
    required this.accentStrong,
    required this.accentSoft,
    required this.ok,
    required this.okSoft,
    required this.warn,
    required this.warnSoft,
    required this.bad,
    required this.badSoft,
  });

  static const light = ObColors(
    page: Color(0xFFF6F8FB),
    surface: Color(0xFFFFFFFF),
    muted: Color(0xFFF1F4F8),
    line: Color(0xFFE3E8EF),
    lineStrong: Color(0xFFD3D9E3),
    ink: Color(0xFF111B2E),
    ink2: Color(0xFF56657D),
    ink3: Color(0xFF8793A7),
    accent: Color(0xFF3B76F6),
    accentStrong: Color(0xFF2563EB),
    accentSoft: Color(0xFFEAF1FF),
    ok: Color(0xFF1F9D6B),
    okSoft: Color(0xFFE6F6EF),
    warn: Color(0xFFE0922F),
    warnSoft: Color(0xFFFDF2E3),
    bad: Color(0xFFDC4C4C),
    badSoft: Color(0xFFFDECEC),
  );

  static const dark = ObColors(
    page: Color(0xFF0E1422),
    surface: Color(0xFF151D2E),
    muted: Color(0xFF1C2538),
    line: Color(0xFF27324A),
    lineStrong: Color(0xFF34405C),
    ink: Color(0xFFE7EBF3),
    ink2: Color(0xFFA7B2C6),
    ink3: Color(0xFF7A869D),
    accent: Color(0xFF5B8CFF),
    accentStrong: Color(0xFF7AA2FF),
    accentSoft: Color(0xFF1B2A4D),
    ok: Color(0xFF3CC28A),
    okSoft: Color(0xFF13301F),
    warn: Color(0xFFF0A94A),
    warnSoft: Color(0xFF3A2A12),
    bad: Color(0xFFF07070),
    badSoft: Color(0xFF3A1818),
  );

  final Color page;
  final Color surface;
  final Color muted;
  final Color line;
  final Color lineStrong;
  final Color ink;
  final Color ink2;
  final Color ink3;
  final Color accent;
  final Color accentStrong;
  final Color accentSoft;
  final Color ok;
  final Color okSoft;
  final Color warn;
  final Color warnSoft;
  final Color bad;
  final Color badSoft;

  /// Цвет уровня с сервера («Good» / «Warning» / «Bad» — от 90% зелёный, от 60% оранжевый, ниже красный).
  Color level(String? level) => switch (level) {
        'Good' => ok,
        'Warning' => warn,
        'Bad' => bad,
        _ => ink2,
      };

  Color levelSoft(String? level) => switch (level) {
        'Good' => okSoft,
        'Warning' => warnSoft,
        'Bad' => badSoft,
        _ => muted,
      };

  @override
  ObColors copyWith({Color? page, Color? surface, Color? accent}) => ObColors(
        page: page ?? this.page,
        surface: surface ?? this.surface,
        muted: muted,
        line: line,
        lineStrong: lineStrong,
        ink: ink,
        ink2: ink2,
        ink3: ink3,
        accent: accent ?? this.accent,
        accentStrong: accentStrong,
        accentSoft: accentSoft,
        ok: ok,
        okSoft: okSoft,
        warn: warn,
        warnSoft: warnSoft,
        bad: bad,
        badSoft: badSoft,
      );

  @override
  ObColors lerp(ObColors? other, double t) {
    if (other == null) return this;
    Color l(Color a, Color b) => Color.lerp(a, b, t)!;
    return ObColors(
      page: l(page, other.page),
      surface: l(surface, other.surface),
      muted: l(muted, other.muted),
      line: l(line, other.line),
      lineStrong: l(lineStrong, other.lineStrong),
      ink: l(ink, other.ink),
      ink2: l(ink2, other.ink2),
      ink3: l(ink3, other.ink3),
      accent: l(accent, other.accent),
      accentStrong: l(accentStrong, other.accentStrong),
      accentSoft: l(accentSoft, other.accentSoft),
      ok: l(ok, other.ok),
      okSoft: l(okSoft, other.okSoft),
      warn: l(warn, other.warn),
      warnSoft: l(warnSoft, other.warnSoft),
      bad: l(bad, other.bad),
      badSoft: l(badSoft, other.badSoft),
    );
  }
}

/// Скругления и отступы — один шаг на всё приложение.
abstract final class ObRadius {
  static const double sm = 10;
  static const double md = 14;
  static const double lg = 18;
  static const double xl = 24;
}

abstract final class ObSpace {
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 20;
  static const double xxl = 24;

  /// Поля экрана слева и справа.
  static const double page = 16;
}
