import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../app/theme/tokens.dart';
import '../extensions/context_ext.dart';
import 'pressable.dart';

/// Чип фильтра как на главной веба: рамка, скругление, значок слева, стрелка вниз; выбран — акцентом.
class FilterPill extends StatelessWidget {
  const FilterPill({super.key, required this.label, required this.onTap, this.icon, this.active = false, this.chevron = true});

  final String label;
  final VoidCallback onTap;
  final IconData? icon;
  final bool active;
  final bool chevron;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fg = active ? c.accentStrong : c.ink;
    return Pressable(
      onTap: onTap,
      haptic: true,
      selected: active,
      child: Container(
        height: 34,
        padding: const EdgeInsets.symmetric(horizontal: 14),
        decoration: BoxDecoration(
          color: active ? c.accentSoft : c.surface,
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: active ? c.accent.withValues(alpha: 0.4) : c.line),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[Icon(icon, size: 15, color: active ? fg : c.ink2), const SizedBox(width: 7)],
            Text(label, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500, color: fg)),
            if (chevron) ...[const SizedBox(width: 6), Icon(LucideIcons.chevronDown, size: 14, color: c.ink3)],
          ],
        ),
      ),
    );
  }
}

/// Тон плашки: успех, внимание, опасность, акцент, фиолетовый, нейтральный.
enum Tone { ok, warn, bad, accent, violet, neutral }

extension ToneColors on ObColors {
  Color toneFg(Tone t) => switch (t) {
        Tone.ok => ok,
        Tone.warn => warn,
        Tone.bad => bad,
        Tone.accent => accentStrong,
        Tone.violet => violet,
        Tone.neutral => ink2,
      };

  Color toneBg(Tone t) => switch (t) {
        Tone.ok => okSoft,
        Tone.warn => warnSoft,
        Tone.bad => badSoft,
        Tone.accent => accentSoft,
        Tone.violet => violetSoft,
        Tone.neutral => muted,
      };

  /// Цвет точки / заливки полосы.
  Color toneSolid(Tone t) => switch (t) {
        Tone.accent => accent,
        Tone.neutral => ink3,
        _ => toneFg(t),
      };
}

/// Плашка статуса словами (не только цветом): мягкая заливка, жирный текст 11–12.
class SoftBadge extends StatelessWidget {
  const SoftBadge({super.key, required this.label, required this.tone, this.icon, this.size = 11});

  final String label;
  final Tone tone;
  final IconData? icon;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
      decoration: BoxDecoration(color: c.toneBg(tone), borderRadius: BorderRadius.circular(ObRadius.xs)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[Icon(icon, size: size + 2, color: c.toneFg(tone)), const SizedBox(width: 4)],
          Text(label, style: TextStyle(fontSize: size, fontWeight: FontWeight.w600, color: c.toneFg(tone))),
        ],
      ),
    );
  }
}

/// Значок в квадрате со скруглением (отделы, разделы): мягкий фон, значок цветом тона.
class IconTile extends StatelessWidget {
  const IconTile({super.key, required this.icon, this.tone = Tone.neutral, this.size = 38, this.radius = 11, this.color});

  final IconData icon;
  final Tone tone;
  final double size;
  final double radius;

  /// Свой цвет значка (цвет раздела); фон — его 12 %.
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final fg = color ?? (tone == Tone.neutral ? c.ink2 : c.toneFg(tone));
    final bg = color?.withValues(alpha: 0.14) ?? c.toneBg(tone);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(radius)),
      alignment: Alignment.center,
      child: Icon(icon, size: size * 0.47, color: fg),
    );
  }
}

/// Круглая кнопка шапки (поиск, уведомления, профиль): рамка, полупрозрачный фон; [badge] — красный счётчик.
class HeaderButton extends StatelessWidget {
  const HeaderButton({super.key, required this.onTap, required this.semanticLabel, this.icon, this.child, this.badge, this.onDark = false});

  final VoidCallback onTap;
  final String semanticLabel;
  final IconData? icon;
  final Widget? child;
  final int? badge;

  /// Кнопка на тёмной шапке (в светлой теме шапка главной тёмная, как в вебе).
  final bool onDark;

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final dark = onDark || c.isDark;
    final fg = dark ? const Color(0xFFEEF1F7) : c.ink;
    final count = badge ?? 0;
    return Pressable(
      onTap: onTap,
      semanticLabel: semanticLabel,
      child: SizedBox(
        width: 44,
        height: 44,
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            Container(
              width: 40,
              height: 40,
              margin: const EdgeInsets.all(2),
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: dark ? const Color(0x0AFFFFFF) : c.surface,
                border: Border.all(color: dark ? const Color(0x1FFFFFFF) : c.line),
              ),
              alignment: Alignment.center,
              child: child ?? Icon(icon, size: 19, color: fg),
            ),
            if (count > 0)
              Positioned(
                right: -4,
                top: -3,
                child: Container(
                  constraints: const BoxConstraints(minWidth: 20),
                  height: 20,
                  padding: const EdgeInsets.symmetric(horizontal: 5),
                  decoration: BoxDecoration(
                    color: const Color(0xFFFF2222),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: dark ? const Color(0xFF060A14) : c.page, width: 2),
                  ),
                  alignment: Alignment.center,
                  child: Text(
                    count > 9 ? '9+' : '$count',
                    style: const TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: Colors.white, height: 1),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
