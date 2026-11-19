import 'dart:math' as math;
import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../shared/extensions/context_ext.dart';
import '../theme/tokens.dart';
import 'app_tab.dart';
import 'fan_layout.dart';

/// Высота полосы вкладок без отступа под полоску «домой».
const navBarHeight = 74.0;

/// Отступ под полоску «домой», как у веба: `max(safe-area − 10, 6)`.
double navBottomInset(BuildContext context) => math.max(MediaQuery.paddingOf(context).bottom - 10, 6);

/// Нижняя панель как у веба на телефоне (`MobileNav`): стеклянная полоса со скруглённым верхом, три вкладки и «Профиль», синяя
/// кнопка меню по центру. Кнопка раскрывает веер круглых кнопок с остальными разделами (по правам); фон затемняется и размывается.
/// Закрывается повторным нажатием, тапом по фону, кнопкой «назад» или выбором пункта.
class ObNavScaffold extends StatefulWidget {
  const ObNavScaffold({
    super.key,
    required this.body,
    required this.tabs,
    required this.current,
    required this.fan,
    required this.onTab,
    required this.onFan,
    this.currentLocation,
  });

  final Widget body;
  final List<AppTab> tabs;

  /// Открытая вкладка (null — открыт раздел из веера).
  final AppTab? current;
  final List<FanEntry> fan;
  final ValueChanged<AppTab> onTab;
  final ValueChanged<FanEntry> onFan;
  final String? currentLocation;

  @override
  State<ObNavScaffold> createState() => _ObNavScaffoldState();
}

class _ObNavScaffoldState extends State<ObNavScaffold> with SingleTickerProviderStateMixin {
  late final AnimationController _anim = AnimationController(vsync: this, duration: const Duration(milliseconds: 720));
  bool _open = false;

  @override
  void dispose() {
    _anim.dispose();
    super.dispose();
  }

  void _toggle() {
    HapticFeedback.lightImpact();
    setState(() => _open = !_open);
    _open ? _anim.forward(from: 0) : _anim.reverse();
  }

  void _close() {
    if (!_open) return;
    setState(() => _open = false);
    _anim.reverse();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final keyboard = MediaQuery.viewInsetsOf(context).bottom > 0;
    return PopScope(
      canPop: !_open,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _close();
      },
      child: LayoutBuilder(
        builder: (context, box) {
          final bottom = navBottomInset(context);
          final barTotal = navBarHeight + bottom;
          final fabCenterY = box.maxHeight - barTotal + 22;
          return Stack(
            children: [
              Positioned.fill(
                child: Scaffold(
                  extendBody: true,
                  resizeToAvoidBottomInset: true,
                  body: widget.body,
                  bottomNavigationBar: keyboard
                      ? null
                      : _Bar(
                          tabs: widget.tabs,
                          current: widget.current,
                          bottom: bottom,
                          onTab: (t) {
                            _close();
                            HapticFeedback.selectionClick();
                            widget.onTab(t);
                          },
                        ),
                ),
              ),
              if (!keyboard) ...[
                AnimatedBuilder(
                  animation: _anim,
                  builder: (context, _) => _anim.value == 0 && !_open
                      ? const SizedBox.shrink()
                      : _Fan(
                          progress: _anim,
                          open: _open,
                          entries: widget.fan,
                          center: Offset(box.maxWidth / 2, fabCenterY),
                          width: box.maxWidth,
                          currentLocation: widget.currentLocation,
                          onClose: _close,
                          onPick: (e) {
                            _close();
                            HapticFeedback.selectionClick();
                            widget.onFan(e);
                          },
                        ),
                ),
                Positioned(
                  left: box.maxWidth / 2 - 34,
                  top: fabCenterY - 34,
                  child: _Fab(open: _open, onTap: _toggle, ring: c.nav.ring),
                ),
              ],
            ],
          );
        },
      ),
    );
  }
}

class _Bar extends StatelessWidget {
  const _Bar({required this.tabs, required this.current, required this.bottom, required this.onTab});

  final List<AppTab> tabs;
  final AppTab? current;
  final double bottom;
  final ValueChanged<AppTab> onTab;

  @override
  Widget build(BuildContext context) {
    final nav = context.ob.nav;
    final narrow = MediaQuery.sizeOf(context).width < 375;
    final slots = <AppTab?>[
      tabs.isNotEmpty ? tabs[0] : null,
      tabs.length > 1 ? tabs[1] : null,
      null,
      tabs.length > 2 ? tabs[2] : null,
      AppTab.profile,
    ];
    const radius = BorderRadius.vertical(top: Radius.circular(30));
    return Semantics(
      container: true,
      label: context.l10n.navSections,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6),
        child: DecoratedBox(
          decoration: BoxDecoration(
            borderRadius: radius,
            boxShadow: [BoxShadow(color: nav.shadow, offset: const Offset(0, -8), blurRadius: 32, spreadRadius: -10)],
          ),
          child: ClipRRect(
            borderRadius: radius,
            child: BackdropFilter(
              filter: ImageFilter.blur(sigmaX: 20, sigmaY: 20),
              child: Container(
                decoration: BoxDecoration(
                  color: nav.bar,
                  borderRadius: radius,
                  border: Border(
                    top: BorderSide(color: nav.edge),
                    left: BorderSide(color: nav.edge),
                    right: BorderSide(color: nav.edge),
                  ),
                ),
                padding: EdgeInsets.only(left: 4, right: 4, bottom: bottom),
                height: navBarHeight + bottom,
                child: Row(
                  children: [
                    for (var i = 0; i < slots.length; i++)
                      Expanded(
                        child: i == 2 || slots[i] == null
                            ? const SizedBox.shrink()
                            : _TabButton(tab: slots[i]!, active: slots[i] == current, narrow: narrow, onTap: () => onTab(slots[i]!)),
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _TabButton extends StatelessWidget {
  const _TabButton({required this.tab, required this.active, required this.narrow, required this.onTap});

  final AppTab tab;
  final bool active;
  final bool narrow;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final nav = context.ob.nav;
    final color = active ? nav.accent : nav.idle;
    final label = narrow ? tab.short(context.l10n) : tab.label(context.l10n);
    return Semantics(
      button: true,
      selected: active,
      label: tab.label(context.l10n),
      excludeSemantics: true,
      child: InkResponse(
        onTap: onTap,
        radius: 36,
        highlightShape: BoxShape.rectangle,
        containedInkWell: true,
        borderRadius: BorderRadius.circular(18),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(tab.icon, size: 24, color: color, weight: active ? 700 : 400),
            const SizedBox(height: 4),
            Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 11, fontWeight: active ? FontWeight.w600 : FontWeight.w500, color: color, letterSpacing: -0.2),
            ),
          ],
        ),
      ),
    );
  }
}

/// Синяя кнопка: градиент сверху вниз, окантовка цветом панели и голубое свечение; открыта — крупнее, «четыре квадрата» → «×».
class _Fab extends StatelessWidget {
  const _Fab({required this.open, required this.onTap, required this.ring});

  final bool open;
  final VoidCallback onTap;
  final Color ring;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Semantics(
      button: true,
      expanded: open,
      label: open ? l10n.navCloseMenu : l10n.navOpenMenu,
      excludeSemantics: true,
      child: GestureDetector(
        onTap: onTap,
        child: AnimatedScale(
          scale: open ? 1.07 : 1,
          duration: const Duration(milliseconds: 380),
          curve: const Cubic(0.34, 1.3, 0.64, 1),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 380),
            width: 68,
            height: 68,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: const RadialGradient(
                center: Alignment(0, -1),
                radius: 1.2,
                colors: [Color(0xFF5CB2FF), Color(0xFF1A86FF), Color(0xFF0866EE)],
                stops: [0, 0.45, 1],
              ),
              boxShadow: [
                BoxShadow(color: ring, spreadRadius: 5),
                const BoxShadow(color: Color(0xB30878FF), offset: Offset(0, 12), blurRadius: 26, spreadRadius: -8),
                BoxShadow(color: Color(open ? 0x992890FF : 0x590878FF), blurRadius: open ? 44 : 26),
                if (open) const BoxShadow(color: Color(0x522890FF), blurRadius: 90),
              ],
            ),
            alignment: Alignment.center,
            child: AnimatedSwitcher(
              duration: const Duration(milliseconds: 300),
              transitionBuilder: (child, a) => RotationTransition(
                turns: Tween<double>(begin: -0.25, end: 0).animate(a),
                child: ScaleTransition(
                  scale: Tween<double>(begin: 0.6, end: 1).animate(a),
                  child: FadeTransition(opacity: a, child: child),
                ),
              ),
              child: open
                  ? const Icon(LucideIcons.x, key: ValueKey('x'), color: Colors.white, size: 28, weight: 700)
                  : const _GridGlyph(key: ValueKey('grid')),
            ),
          ),
        ),
      ),
    );
  }
}

/// Значок «четыре квадрата» (заливкой, как `LayoutGrid fill` в вебе).
class _GridGlyph extends StatelessWidget {
  const _GridGlyph({super.key});

  @override
  Widget build(BuildContext context) {
    Widget sq() => Container(
      width: 10,
      height: 10,
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(2.6)),
    );
    return SizedBox(
      width: 24,
      height: 24,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [sq(), sq()]),
          Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [sq(), sq()]),
        ],
      ),
    );
  }
}

class _Fan extends StatelessWidget {
  const _Fan({
    required this.progress,
    required this.open,
    required this.entries,
    required this.center,
    required this.width,
    required this.onClose,
    required this.onPick,
    this.currentLocation,
  });

  final Animation<double> progress;
  final bool open;
  final List<FanEntry> entries;
  final Offset center;
  final double width;
  final VoidCallback onClose;
  final ValueChanged<FanEntry> onPick;
  final String? currentLocation;

  static const _total = 720.0;

  /// Доля анимации пункта i: появление с задержкой i·32 мс (440 мс пружиной), закрытие — в обратном порядке по 22 мс.
  double _itemT(int i, int n, double t) {
    final delay = open ? i * 32.0 : (n - 1 - i) * 22.0;
    final start = delay / _total;
    final end = math.min(1.0, (delay + 440) / _total);
    return ((t - start) / (end - start)).clamp(0.0, 1.0);
  }

  @override
  Widget build(BuildContext context) {
    final c = context.ob;
    final l10n = context.l10n;
    final labels = [for (final e in entries) e.label(l10n)];
    final layout = fanLayout(labels, width, center.dx, center.dy, 22);
    final t = progress.value;
    final n = entries.length;
    final overlayT = Curves.easeOut.transform((t / 0.55).clamp(0.0, 1.0));
    const spring = Cubic(0.34, 1.3, 0.64, 1);
    const ease = Cubic(0.22, 1, 0.36, 1);

    return Positioned.fill(
      child: Material(
        type: MaterialType.transparency,
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            // Затемнение с размытием на весь экран.
            Positioned.fill(
              child: GestureDetector(
                onTap: onClose,
                behavior: HitTestBehavior.opaque,
                child: BackdropFilter(
                  filter: ImageFilter.blur(sigmaX: 6 * overlayT, sigmaY: 6 * overlayT),
                  child: ColoredBox(color: c.nav.overlay.withValues(alpha: c.nav.overlay.a * overlayT)),
                ),
              ),
            ),
            // Ореол вокруг кнопки.
            Positioned(
              left: center.dx - 130,
              top: center.dy - 130,
              child: IgnorePointer(
                child: Opacity(
                  opacity: overlayT,
                  child: Transform.scale(scale: 0.5 + 0.5 * ease.transform(overlayT), child: const _Halo()),
                ),
              ),
            ),
            // Лучи света от кнопки к пунктам.
            for (var i = 0; i < n && i < layout.slots.length; i++)
              _Beam(center: center, slot: layout.slots[i], size: layout.size, t: ease.transform(_itemT(i, n, t)), color: c.nav.beam),
            for (var i = 0; i < n && i < layout.slots.length; i++)
              _FanItem(
                entry: entries[i],
                label: labels[i],
                size: layout.size,
                center: center,
                slot: layout.slots[i],
                t: _itemT(i, n, t),
                spring: spring,
                current: currentLocation != null && currentLocation!.startsWith(entries[i].location),
                onTap: () => onPick(entries[i]),
              ),
          ],
        ),
      ),
    );
  }
}

class _Halo extends StatelessWidget {
  const _Halo();

  @override
  Widget build(BuildContext context) => SizedBox(
    width: 260,
    height: 260,
    child: Stack(
      alignment: Alignment.center,
      children: [
        const DecoratedBox(
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            gradient: RadialGradient(
              colors: [Color(0x5C0878FF), Color(0x2B0878FF), Color(0x125AA0FF), Color(0x005AA0FF)],
              stops: [0, 0.28, 0.48, 0.68],
            ),
          ),
          child: SizedBox.expand(),
        ),
        Container(
          width: 260 - 168,
          height: 260 - 168,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: const Color(0x66FFFFFF)),
            boxShadow: const [BoxShadow(color: Color(0x4078B4FF), blurRadius: 18)],
          ),
        ),
        Container(
          width: 260 - 136,
          height: 260 - 136,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: const Color(0x2EFFFFFF)),
          ),
        ),
      ],
    ),
  );
}

class _Beam extends StatelessWidget {
  const _Beam({required this.center, required this.slot, required this.size, required this.t, required this.color});

  final Offset center;
  final FanSlot slot;
  final double size;
  final double t;
  final Color color;

  @override
  Widget build(BuildContext context) {
    if (t <= 0) return const SizedBox.shrink();
    final length = math.max(0.0, math.sqrt(slot.x * slot.x + slot.y * slot.y) - size / 2 + 6);
    final bw = (size * 0.95).roundToDouble();
    final angle = math.atan2(slot.x, -slot.y);
    final scaleY = 0.2 + 0.8 * t;
    return Positioned(
      left: center.dx - bw / 2,
      top: center.dy - length,
      child: IgnorePointer(
        child: Opacity(
          opacity: t.clamp(0.0, 1.0),
          child: Transform(
            alignment: Alignment.bottomCenter,
            transform: Matrix4.rotationZ(angle)..scaleByDouble(1, scaleY, 1, 1),
            child: CustomPaint(size: Size(bw, length), painter: _BeamPainter(color)),
          ),
        ),
      ),
    );
  }
}

class _BeamPainter extends CustomPainter {
  _BeamPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;
    final path = Path()
      ..moveTo(w * 0.44, h)
      ..lineTo(w * 0.56, h)
      ..lineTo(w, 0)
      ..lineTo(0, 0)
      ..close();
    final paint = Paint()
      ..shader = LinearGradient(
        begin: Alignment.bottomCenter,
        end: Alignment.topCenter,
        colors: [color.withValues(alpha: 0), color, color.withValues(alpha: 0)],
        stops: const [0.06, 0.45, 1],
      ).createShader(Offset.zero & size)
      ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 7);
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(_BeamPainter old) => old.color != color;
}

class _FanItem extends StatelessWidget {
  const _FanItem({
    required this.entry,
    required this.label,
    required this.size,
    required this.center,
    required this.slot,
    required this.t,
    required this.spring,
    required this.current,
    required this.onTap,
  });

  final FanEntry entry;
  final String label;
  final double size;
  final Offset center;
  final FanSlot slot;
  final double t;
  final Curve spring;
  final bool current;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final nav = context.ob.nav;
    final p = spring.transform(t);
    final scale = 0.45 + 0.55 * p;
    final dx = slot.x * p;
    final dy = slot.y * p;
    final itemWidth = math.max(size, fanLabelMax) + 12;
    return Positioned(
      left: center.dx + dx - itemWidth / 2,
      top: center.dy + dy - size / 2,
      width: itemWidth,
      child: Opacity(
        opacity: (t / 0.5).clamp(0.0, 1.0),
        child: Transform.scale(
          scale: scale,
          alignment: Alignment.topCenter,
          child: Semantics(
            button: true,
            selected: current,
            label: label,
            excludeSemantics: true,
            child: GestureDetector(
              onTap: onTap,
              behavior: HitTestBehavior.opaque,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: size,
                    height: size,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: nav.item,
                      boxShadow: [
                        if (current) BoxShadow(color: nav.accent, spreadRadius: 2.5),
                        BoxShadow(color: nav.itemShadow, offset: const Offset(0, 12), blurRadius: 28, spreadRadius: -10),
                        BoxShadow(color: nav.itemShadow.withValues(alpha: 0.25), offset: const Offset(0, 2), blurRadius: 6),
                      ],
                    ),
                    alignment: Alignment.center,
                    child: Icon(entry.icon, color: entry.color, size: size < 60 ? 22 : 24),
                  ),
                  const SizedBox(height: 6),
                  Container(
                    constraints: const BoxConstraints(maxWidth: fanLabelMax),
                    padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 3),
                    decoration: BoxDecoration(
                      color: nav.labelBg,
                      borderRadius: BorderRadius.circular(9),
                      boxShadow: const [BoxShadow(color: Color(0x240F1E3C), offset: Offset(0, 2), blurRadius: 8)],
                    ),
                    child: Text(
                      label,
                      textAlign: TextAlign.center,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 11.5, height: 15 / 11.5, fontWeight: FontWeight.w500, color: nav.labelInk),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Отступ снизу для содержимого экранов над нижней панелью (панель стеклянная, содержимое уходит под неё).
double navContentInset(BuildContext context) => navBarHeight + navBottomInset(context) + 12;

/// Мягкое затухание содержимого над панелью — как `.fade` у мобильного «Обзора».
class NavFade extends StatelessWidget {
  const NavFade({super.key});

  @override
  Widget build(BuildContext context) {
    final page = context.ob.page;
    return IgnorePointer(
      child: Container(
        height: 56,
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [page.withValues(alpha: 0), page.withValues(alpha: 0.95)],
          ),
        ),
      ),
    );
  }
}

/// Цвета для проверки в тестах: панель берёт их из темы.
NavColors navColorsOf(BuildContext context) => context.ob.nav;
