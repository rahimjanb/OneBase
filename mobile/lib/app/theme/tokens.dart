import 'package:flutter/material.dart';

/// Цвета OneBase — токены мобильного дизайна веба 1Base, чтобы веб на телефоне и приложение были одним продуктом:
/// - экраны (`--ts-*`, `frontend/src/components/hr/timesheet/timesheet.module.css`, мобильный «Обзор»);
/// - нижняя панель с веером (`.rnav`, `frontend/src/app/globals.css`);
/// - акцентные карточки главной (`frontend/src/components/dashboard/mobile/mobile.module.css`).
///
/// Тёмная тема — основная (как у бренда OneBase в вебе), светлая — по выбору. Мелкий текст (`subtle`) светлее/темнее макета ради
/// контраста не ниже 4,5:1 — так же, как в вебе у консультанта и уведомлений.
@immutable
class ObColors extends ThemeExtension<ObColors> {
  const ObColors({
    required this.brightness,
    required this.page,
    required this.glow,
    required this.header,
    required this.surface,
    required this.muted,
    required this.line,
    required this.lineStrong,
    required this.ink,
    required this.ink2,
    required this.ink3,
    required this.subtle,
    required this.accent,
    required this.accentFill,
    required this.accentStrong,
    required this.accentSoft,
    required this.ok,
    required this.okSoft,
    required this.warn,
    required this.warnSoft,
    required this.bad,
    required this.badSoft,
    required this.violet,
    required this.violetSoft,
    required this.series1,
    required this.series2,
    required this.track,
    required this.divider,
    required this.rowDivider,
    required this.tile,
    required this.arrowBg,
    required this.arrowFg,
    required this.popBg,
    required this.popText,
    required this.kpi,
    required this.focus,
    required this.report,
    required this.nav,
  });

  static const dark = ObColors(
    brightness: Brightness.dark,
    page: Color(0xFF060A14),
    glow: Color(0x4D2347B0),
    header: Color(0x00060A14),
    surface: Color(0xFF0C1221),
    muted: Color(0xFF121A2D),
    line: Color(0xFF1A2338),
    lineStrong: Color(0xFF26314D),
    ink: Color(0xFFEEF1F7),
    ink2: Color(0xFFC9CFDC),
    ink3: Color(0xFF8D97AE),
    subtle: Color(0xFF7B86A1),
    accent: Color(0xFF3D7BFF),
    accentFill: Color(0xFF2F6CF5),
    accentStrong: Color(0xFF7FA6FF),
    accentSoft: Color(0x1F3D7BFF),
    ok: Color(0xFF2BD47D),
    okSoft: Color(0x1C2BD47D),
    warn: Color(0xFFF6A93B),
    warnSoft: Color(0x1FF6A93B),
    bad: Color(0xFFFF5A6A),
    badSoft: Color(0x1CFF5A6A),
    violet: Color(0xFFA78BFA),
    violetSoft: Color(0x1FA78BFA),
    series1: Color(0xFF5B8CFF),
    series2: Color(0xFFD95926),
    track: Color(0x14FFFFFF),
    divider: Color(0x12FFFFFF),
    rowDivider: Color(0x0FFFFFFF),
    tile: Color(0x14FFFFFF),
    arrowBg: Color(0xFFFFFFFF),
    arrowFg: Color(0xFF0C1221),
    popBg: Color(0xFF18213A),
    popText: Color(0xFFFFFFFF),
    kpi: CardTint(base: Color(0xFF0F1730), from: Color(0x332B55D6), via: null, border: Color(0x333D7BFF), angle: 160, stop: 0.70),
    focus: CardTint(base: Color(0xFF121335), from: Color(0x266D4CF5), via: Color(0x143D7BFF), border: Color(0x40A78BFA), angle: 160, stop: 0.85),
    report: CardTint(base: Color(0xFF16123A), from: Color(0x407C5CFF), via: Color(0x263D7BFF), border: Color(0x40A78BFA), angle: 120, stop: 1),
    nav: NavColors(
      accent: Color(0xFF4D9DFF),
      bar: Color(0xD10E1628),
      edge: Color(0x17FFFFFF),
      shadow: Color(0x8C000000),
      idle: Color(0xFF8E9DBA),
      ring: Color(0xF50E1628),
      item: Color(0xFF18233D),
      itemShadow: Color(0x99000000),
      labelBg: Color(0xE618233D),
      labelInk: Color(0xFFE6ECF7),
      overlay: Color(0x8C030814),
      beam: Color(0x4D6EA5FF),
    ),
  );

  static const light = ObColors(
    brightness: Brightness.light,
    page: Color(0xFFF5F6F8),
    glow: Color(0x00F5F6F8),
    header: Color(0xFF16213A),
    surface: Color(0xFFFFFFFF),
    muted: Color(0xFFF1F3F6),
    line: Color(0xFFE7E9EE),
    lineStrong: Color(0xFFD6DAE2),
    ink: Color(0xFF0E1322),
    ink2: Color(0xFF3A4255),
    ink3: Color(0xFF5D667A),
    subtle: Color(0xFF646C7E),
    accent: Color(0xFF2F6BFF),
    accentFill: Color(0xFF2F6BFF),
    accentStrong: Color(0xFF2F6BFF),
    accentSoft: Color(0x142F6BFF),
    ok: Color(0xFF0B7A3D),
    okSoft: Color(0x1612A150),
    warn: Color(0xFFA35800),
    warnSoft: Color(0x1CF59E0B),
    bad: Color(0xFFC82333),
    badSoft: Color(0x14E5484D),
    violet: Color(0xFF7C3AED),
    violetSoft: Color(0x127C3AED),
    series1: Color(0xFF3B76F6),
    series2: Color(0xFFEB6834),
    track: Color(0xFFF1F3F6),
    divider: Color(0xFFE7E9EE),
    rowDivider: Color(0x1A7C3AED),
    tile: Color(0x147C3AED),
    arrowBg: Color(0xFF0E1322),
    arrowFg: Color(0xFFFFFFFF),
    popBg: Color(0xFF0E1322),
    popText: Color(0xFFFFFFFF),
    kpi: CardTint(base: Color(0xFFFFFFFF), from: Color(0x142F6BFF), via: null, border: Color(0x332F6BFF), angle: 160, stop: 0.65),
    focus: CardTint(base: Color(0xFFFFFFFF), from: Color(0x127C3AED), via: Color(0x0A2F6BFF), border: Color(0x337C3AED), angle: 160, stop: 0.80),
    report: CardTint(base: Color(0xFFFFFFFF), from: Color(0x1A7C3AED), via: Color(0x122F6BFF), border: Color(0x337C3AED), angle: 120, stop: 1),
    nav: NavColors(
      accent: Color(0xFF0878FF),
      bar: Color(0xCCFFFFFF),
      edge: Color(0xF2FFFFFF),
      shadow: Color(0x33142D64),
      idle: Color(0xFF7D8CA8),
      ring: Color(0xF5FFFFFF),
      item: Color(0xFFFFFFFF),
      itemShadow: Color(0x66193C82),
      labelBg: Color(0xE0F4F7FC),
      labelInk: Color(0xFF1D2A40),
      overlay: Color(0x6B34425C),
      beam: Color(0x9EFFFFFF),
    ),
  );

  final Brightness brightness;

  /// Фон экрана и свечение из правого верхнего угла (тёмная тема).
  final Color page;
  final Color glow;

  /// Шапка главной: в вебе на телефоне тёмная в обеих темах.
  final Color header;

  /// Карточки и вторая поверхность (поля, дорожки, неактивные вкладки).
  final Color surface;
  final Color muted;
  final Color line;
  final Color lineStrong;

  /// Текст: основной, второй, приглушённый, мелкие подписи.
  final Color ink;
  final Color ink2;
  final Color ink3;
  final Color subtle;

  /// Акцент: [accent] — линии и столбики, [accentFill] — заливка под белым текстом, [accentStrong] — ссылки и акцентный текст.
  final Color accent;
  final Color accentFill;
  final Color accentStrong;
  final Color accentSoft;

  final Color ok;
  final Color okSoft;
  final Color warn;
  final Color warnSoft;
  final Color bad;
  final Color badSoft;
  final Color violet;
  final Color violetSoft;

  /// Ряды графиков (категориальные слоты 1 и 2). Пара проверена валидатором палитры (dataviz) на поверхности обеих тем.
  /// Статусные цвета (ok / warn / bad) для рядов не используются.
  final Color series1;
  final Color series2;

  /// Дорожки колец и полос, разделители в карточках главной, плитки, стрелка баннера, всплывающая подсказка графика.
  final Color track;
  final Color divider;
  final Color rowDivider;
  final Color tile;
  final Color arrowBg;
  final Color arrowFg;
  final Color popBg;
  final Color popText;

  /// Акцентные карточки главной: главный показатель, «Фокус дня», AI-отчёт.
  final CardTint kpi;
  final CardTint focus;
  final CardTint report;

  /// Нижняя панель с синей кнопкой и веером.
  final NavColors nav;

  bool get isDark => brightness == Brightness.dark;

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
  ObColors copyWith() => this;

  /// Смена темы — без плавного перехода цветов: токенов много, промежуточные оттенки ничего не дают.
  @override
  ObColors lerp(ObColors? other, double t) => other == null || t < 0.5 ? this : other;
}

/// Фон акцентной карточки: градиент поверх основы и рамка.
@immutable
class CardTint {
  const CardTint({required this.base, required this.from, required this.via, required this.border, required this.angle, required this.stop});

  final Color base;
  final Color from;
  final Color? via;
  final Color border;

  /// Направление градиента в градусах, как в CSS (`160deg`).
  final double angle;

  /// Где градиент кончается (доля длины).
  final double stop;
}

@immutable
class NavColors {
  const NavColors({
    required this.accent,
    required this.bar,
    required this.edge,
    required this.shadow,
    required this.idle,
    required this.ring,
    required this.item,
    required this.itemShadow,
    required this.labelBg,
    required this.labelInk,
    required this.overlay,
    required this.beam,
  });

  final Color accent;
  final Color bar;
  final Color edge;
  final Color shadow;
  final Color idle;
  final Color ring;
  final Color item;
  final Color itemShadow;
  final Color labelBg;
  final Color labelInk;
  final Color overlay;
  final Color beam;
}

/// Скругления — как в вебе: карточки 20, главная карточка 22, плитки 16, поля и вкладки 12.
abstract final class ObRadius {
  static const double xs = 6;
  static const double sm = 10;
  static const double md = 12;
  static const double tile = 16;
  static const double lg = 20;
  static const double xl = 22;
  static const double sheet = 28;
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

  /// Отступ между блоками экрана (как `gap-[20px]` мобильного «Обзора»).
  static const double block = 20;
}
