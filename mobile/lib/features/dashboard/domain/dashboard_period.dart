import 'package:flutter/foundation.dart';

/// Период Dashboard: месяц или «последние данные» (как у сайта: сервер сам берёт месяц последних данных продаж, а финансы — с начала
/// текущего месяца по сегодня).
@immutable
class DashboardPeriod {
  const DashboardPeriod(int this.year, int this.month);

  const DashboardPeriod.latest()
      : year = null,
        month = null;

  final int? year;
  final int? month;

  bool get isLatest => year == null || month == null;

  String get _mm => month!.toString().padLeft(2, '0');

  /// Ключ кэша.
  String get key => isLatest ? 'latest' : '$year-$_mm';

  Map<String, Object?> get salesQuery => isLatest ? const {} : {'year': year, 'month': month};

  /// Финансы — весь месяц: с 1-го по последний день.
  Map<String, Object?> get financeQuery {
    if (isLatest) return const {};
    final last = DateTime(year!, month! + 1, 0).day;
    return {'from': '$year-$_mm-01', 'to': '$year-$_mm-${last.toString().padLeft(2, '0')}'};
  }

  @override
  bool operator ==(Object other) => other is DashboardPeriod && other.year == year && other.month == month;

  @override
  int get hashCode => Object.hash(year, month);

  @override
  String toString() => 'DashboardPeriod($key)';
}

/// Фильтры Dashboard: период и организация 1С (только для финансовых блоков; null — все организации).
@immutable
class DashboardFilter {
  const DashboardFilter({this.period = const DashboardPeriod.latest(), this.organizationId});

  final DashboardPeriod period;
  final String? organizationId;

  DashboardFilter copyWith({DashboardPeriod? period, String? Function()? organizationId}) => DashboardFilter(
        period: period ?? this.period,
        organizationId: organizationId == null ? this.organizationId : organizationId(),
      );

  @override
  bool operator ==(Object other) =>
      other is DashboardFilter && other.period == period && other.organizationId == organizationId;

  @override
  int get hashCode => Object.hash(period, organizationId);
}

/// Последние [count] месяцев до [now] включительно — выбор периода, когда у пользователя нет продаж (`/api/sales/months` закрыт).
List<DashboardPeriod> recentMonths(DateTime now, {int count = 12}) => [
      for (var i = 0; i < count; i++) DashboardPeriod(DateTime(now.year, now.month - i).year, DateTime(now.year, now.month - i).month),
    ];
