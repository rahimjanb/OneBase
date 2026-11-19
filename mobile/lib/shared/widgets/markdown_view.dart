import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:markdown/markdown.dart' as md;

import '../extensions/context_ext.dart';

/// Markdown ответа консультанта в стиле приложения: заголовки, абзацы, жирный, курсив, код, ссылки, списки, цитаты, таблицы (прокрутка
/// по горизонтали), разделители. Без HTML и картинок. [onLink] — переход по ссылке (пути веба → экраны приложения).
final _softBreak = RegExp(r'[ \t]*\n[ \t]*');

class MarkdownView extends StatelessWidget {
  const MarkdownView(this.text, {super.key, this.onLink, this.muted = false});

  final String text;
  final void Function(String href)? onLink;

  /// Приглушённый вид (черновик ответа).
  final bool muted;

  @override
  Widget build(BuildContext context) {
    final doc = md.Document(extensionSet: md.ExtensionSet.gitHubFlavored, encodeHtml: false);
    final nodes = doc.parse(text.replaceAll('\r\n', '\n'));
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [for (final n in nodes) ?_block(context, n)]);
  }

  TextStyle _base(BuildContext context) => TextStyle(fontSize: 14.5, height: 1.5, color: muted ? context.ob.ink3 : context.ob.ink);

  Widget? _block(BuildContext context, md.Node n) {
    final c = context.ob;
    if (n is md.Text) return Text(n.text, style: _base(context));
    if (n is! md.Element) return null;
    switch (n.tag) {
      case 'h1' || 'h2' || 'h3' || 'h4' || 'h5' || 'h6':
        final size = switch (n.tag) { 'h1' => 20.0, 'h2' => 18.0, 'h3' => 16.0, _ => 15.0 };
        return Padding(
          padding: const EdgeInsets.only(top: 10, bottom: 6),
          child: Text.rich(_inline(context, n.children ?? const [], TextStyle(fontSize: size, fontWeight: FontWeight.w700, height: 1.3, color: c.ink))),
        );
      case 'p':
        return Padding(padding: const EdgeInsets.only(bottom: 8), child: Text.rich(_inline(context, n.children ?? const [], _base(context))));
      case 'ul' || 'ol':
        var i = 0;
        return Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            for (final li in (n.children ?? const <md.Node>[]).whereType<md.Element>())
              Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  SizedBox(width: 22, child: Text(n.tag == 'ol' ? '${++i}.' : '•', style: _base(context).copyWith(color: c.ink3))),
                  Expanded(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: _listItem(context, li.children ?? const [])),
                  ),
                ]),
              ),
          ]),
        );
      case 'blockquote':
        return Container(
          margin: const EdgeInsets.only(bottom: 8),
          padding: const EdgeInsets.fromLTRB(12, 8, 10, 4),
          decoration: BoxDecoration(color: c.muted, border: Border(left: BorderSide(color: c.accent, width: 3)), borderRadius: BorderRadius.circular(6)),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [for (final ch in n.children ?? const <md.Node>[]) ?_block(context, ch)]),
        );
      case 'pre':
        return Container(
          margin: const EdgeInsets.only(bottom: 8),
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(color: c.muted, borderRadius: BorderRadius.circular(8)),
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Text(n.textContent, style: TextStyle(fontFamily: 'Menlo', fontSize: 12.5, color: c.ink)),
          ),
        );
      case 'hr':
        return Padding(padding: const EdgeInsets.symmetric(vertical: 8), child: Divider(color: c.line));
      case 'table':
        return _table(context, n);
      default:
        return Text.rich(_inline(context, n.children ?? const [], _base(context)));
    }
  }

  /// Пункт списка: подряд идущие строчные части (текст, жирный, ссылка, перенос) — один абзац; вложенные списки и абзацы — блоками.
  List<Widget> _listItem(BuildContext context, List<md.Node> children) {
    final out = <Widget>[];
    final run = <md.Node>[];
    void flush() {
      if (run.isEmpty) return;
      out.add(Text.rich(_inline(context, List.of(run), _base(context))));
      run.clear();
    }

    for (final child in children) {
      if (child is md.Element && const {'ul', 'ol', 'p', 'blockquote', 'pre', 'table', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6'}.contains(child.tag)) {
        flush();
        final b = _block(context, child);
        if (b != null) out.add(b);
      } else {
        run.add(child);
      }
    }
    flush();
    return out;
  }

  Widget _table(BuildContext context, md.Element table) {
    final c = context.ob;
    final rows = <List<md.Element>>[];
    var header = 0;
    for (final section in (table.children ?? const <md.Node>[]).whereType<md.Element>()) {
      for (final tr in (section.children ?? const <md.Node>[]).whereType<md.Element>()) {
        rows.add((tr.children ?? const <md.Node>[]).whereType<md.Element>().toList());
      }
      if (section.tag == 'thead') header = rows.length;
    }
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(border: Border.all(color: c.line), borderRadius: BorderRadius.circular(10)),
      clipBehavior: Clip.antiAlias,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Table(
          defaultColumnWidth: const IntrinsicColumnWidth(),
          border: TableBorder(horizontalInside: BorderSide(color: c.line)),
          children: [
            for (var r = 0; r < rows.length; r++)
              TableRow(
                decoration: BoxDecoration(color: r < header ? c.muted : null),
                children: [
                  for (final cell in rows[r])
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 220),
                        child: Text.rich(
                          _inline(context, cell.children ?? const [], TextStyle(fontSize: 13, height: 1.35, fontWeight: r < header ? FontWeight.w600 : FontWeight.w400, color: c.ink)),
                          textAlign: cell.attributes['align'] == 'right' ? TextAlign.right : TextAlign.left,
                        ),
                      ),
                    ),
                ],
              ),
          ],
        ),
      ),
    );
  }

  InlineSpan _inline(BuildContext context, List<md.Node> nodes, TextStyle style) {
    final c = context.ob;
    return TextSpan(
      style: style,
      children: [
        for (final n in nodes)
          if (n is md.Text)
            // Мягкий перенос строки в Markdown — пробел (как в браузере); жёсткий — элемент `br`.
            TextSpan(text: n.text.replaceAll(_softBreak, ' '))
          else if (n is md.Element)
            switch (n.tag) {
              'strong' => _inline(context, n.children ?? const [], style.copyWith(fontWeight: FontWeight.w700)),
              'em' => _inline(context, n.children ?? const [], style.copyWith(fontStyle: FontStyle.italic)),
              'del' => _inline(context, n.children ?? const [], style.copyWith(decoration: TextDecoration.lineThrough)),
              'code' => TextSpan(text: n.textContent, style: style.copyWith(fontFamily: 'Menlo', fontSize: (style.fontSize ?? 14) - 1, backgroundColor: c.muted)),
              'br' => const TextSpan(text: '\n'),
              'a' => TextSpan(
                  text: n.textContent,
                  style: style.copyWith(color: c.accentStrong, decoration: TextDecoration.underline, decorationColor: c.accentStrong.withValues(alpha: 0.4)),
                  recognizer: onLink == null || n.attributes['href'] == null ? null : (TapGestureRecognizer()..onTap = () => onLink!(n.attributes['href']!)),
                ),
              _ => _inline(context, n.children ?? const [], style),
            },
      ],
    );
  }
}
