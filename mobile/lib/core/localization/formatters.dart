import 'package:intl/intl.dart';

import '../../l10n/gen/app_localizations.dart';

/// Форматы чисел и дат на языке интерфейса. Минус — типографский «−», разряды — неразрывным пробелом (как в русской и узбекской
/// локали intl).
class Fmt {
  Fmt(this.l10n) : _locale = l10n.localeName;

  final AppLocalizations l10n;
  final String _locale;

  static const _minus = '−';

  String _fix(String s) => s.replaceAll('-', _minus);

  /// Целое с разрядами: 7753 → «7 753».
  String count(num v) => _fix(NumberFormat.decimalPattern(_locale).format(v.round()));

  /// Дробное: не больше [digits] знаков после запятой, без хвостовых нулей (1,2 — а не 1,20).
  String decimal(num v, {int digits = 1}) => _fix(
        (NumberFormat.decimalPattern(_locale)
              ..minimumFractionDigits = 0
              ..maximumFractionDigits = digits)
            .format(v),
      );

  /// Сумма кратко: 4 535 279 646 → «4,54 млрд сум», 584 970 → «585 тыс. сум», 950 → «950 сум».
  String money(num v, {bool withUnit = true}) {
    final s = compact(v);
    return withUnit ? '$s ${l10n.unitSum}' : s;
  }

  /// Число кратко с масштабом (тыс. / млн / млрд): три значащие цифры.
  String compact(num v) {
    final a = v.abs();
    final (double value, String scale) = a >= 1e9
        ? (v / 1e9, l10n.unitBillion)
        : a >= 1e6
            ? (v / 1e6, l10n.unitMillion)
            : a >= 1e3
                ? (v / 1e3, l10n.unitThousand)
                : (v.toDouble(), '');
    if (scale.isEmpty) return count(value);
    final digits = value.abs() >= 100 ? 0 : (value.abs() >= 10 ? 1 : 2);
    return '${decimal(value, digits: digits)} $scale';
  }

  /// Вес: от тонны — в тоннах («120 т», «1,5 т»), меньше — в кг («950 кг»).
  String weight(num kg) {
    final a = kg.abs();
    if (a < 1000) return '${count(kg)} ${l10n.unitKg}';
    final t = kg / 1000;
    final digits = t.abs() >= 100 ? 0 : 1;
    return '${decimal(t, digits: digits)} ${l10n.unitTon}';
  }

  /// Доля 0–1 → «76%», малые — с десятой: 0,042 → «4,2%».
  String percent(double ratio) {
    final p = ratio * 100;
    final digits = p.abs() >= 10 || p == p.roundToDouble() ? 0 : 1;
    return '${decimal(p, digits: digits)}%';
  }

  /// Изменение со знаком: +0,021 → «+2,1%», −0,13 → «−13%».
  String signedPercent(double ratio) {
    final s = percent(ratio);
    return ratio > 0 ? '+$s' : s;
  }

  /// «Ноябрь 2026».
  String monthYear(int year, int month) => _capitalize(DateFormat('LLLL y', _locale).format(DateTime(year, month)));

  /// «Ноябрь».
  String month(int year, int month) => _capitalize(DateFormat('LLLL', _locale).format(DateTime(year, month)));

  /// «нояб.» — подпись оси.
  String monthShort(DateTime d) => DateFormat('LLL', _locale).format(d);

  /// «12 ноября».
  String dayMonth(DateTime d) => DateFormat('d MMMM', _locale).format(d);

  /// «21:31».
  String time(DateTime d) => DateFormat.Hm(_locale).format(d);

  /// Когда обновлено: «только что», «5 мин назад», «2 ч назад», иначе дата и время.
  String synced(DateTime at, DateTime now) {
    final diff = now.difference(at);
    if (diff.inSeconds < 60) return l10n.syncedJustNow;
    if (diff.inMinutes < 60) return l10n.syncedMinutes(diff.inMinutes);
    if (diff.inHours < 12) return l10n.syncedHours(diff.inHours);
    return l10n.syncedAt('${dayMonth(at)}, ${time(at)}');
  }

  static String _capitalize(String s) => s.isEmpty ? s : s[0].toUpperCase() + s.substring(1);
}
