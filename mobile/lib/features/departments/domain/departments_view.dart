import '../../../core/localization/formatters.dart';
import '../../../l10n/gen/app_localizations.dart';

// «Отделы» — перенос `frontend/src/lib/departments-view.ts`: порядок отделов, месяц, карточка каждого отдела (статус, значение с периодом,
// полоса факта и прогноза, строка деталей). Пороги — как у веба: продажи — прогноз выполнения плана (выполнен — «План выполнен», прогноз
// от 100 % — «В норме»), HR — явка, норма 80 %, снабжение — заявки к закупке без просрочки (только на сегодня). У отделов без источника
// данных (производство, финансы, маркетинг) — «Нет данных».

enum DeptStatus { done, ok, attention, none, error }

enum DeptDetailIcon { cart, attendance, info, sales }

/// Ответ API для карточки: данные, нет доступа (раздела или права) или ошибка (у ошибки — «Повторить»).
sealed class Load<D> {
  const Load();
}

class Loaded<D> extends Load<D> {
  const Loaded(this.data);

  final D data;
}

class Forbidden<D> extends Load<D> {
  const Forbidden();
}

class Failed<D> extends Load<D> {
  const Failed();
}

class DeptCard {
  const DeptCard({
    required this.code,
    required this.name,
    this.status = DeptStatus.none,
    this.value,
    this.period,
    required this.label,
    this.bar,
    this.detail,
    this.detailIcon = DeptDetailIcon.info,
  });

  final String code;
  final String name;
  final DeptStatus status;

  /// Крупное значение, %; null — «—».
  final int? value;

  /// Период показателя: «ноябрь 2026».
  final String? period;
  final String label;

  /// Полоса: факт (заливка цветом статуса) и прогноз (бледнее, поверх трека).
  final ({int value, int? forecast})? bar;

  /// Строка под полосой (у продаж — «Факт 62,3 из 437,4 т (14 %) · прогноз 52 %»).
  final String? detail;
  final DeptDetailIcon detailIcon;

  DeptCard copyWith({
    DeptStatus? status,
    int? value,
    String? Function()? period,
    String? label,
    ({int value, int? forecast})? bar,
    String? detail,
    DeptDetailIcon? detailIcon,
  }) => DeptCard(
    code: code,
    name: name,
    status: status ?? this.status,
    value: value ?? this.value,
    period: period == null ? this.period : period(),
    label: label ?? this.label,
    bar: bar ?? this.bar,
    detail: detail ?? this.detail,
    detailIcon: detailIcon ?? this.detailIcon,
  );
}

/// Отделы — в порядке карточек веба.
const departmentCodes = ['production', 'finance', 'sales', 'marketing', 'supply', 'hr'];

String departmentName(AppLocalizations l10n, String code) => switch (code) {
  'production' => l10n.dpProduction,
  'finance' => l10n.dpFinance,
  'sales' => l10n.dpSales,
  'marketing' => l10n.dpMarketing,
  'supply' => l10n.dpSupply,
  _ => l10n.dpHr,
};

String _label(AppLocalizations l10n, String code) => switch (code) {
  'production' => l10n.dpLabelProduction,
  'finance' => l10n.dpLabelFinance,
  'sales' => l10n.dpLabelSalesForecast,
  'marketing' => l10n.dpLabelMarketing,
  'supply' => l10n.dpLabelSupply,
  _ => l10n.dpLabelHrMonthToDate,
};

// ───────────── Месяц ─────────────

class DeptMonth {
  const DeptMonth(this.year, this.month, {required this.current});

  final int year;
  final int month;
  final bool current;

  String get key => '$year-${month.toString().padLeft(2, '0')}';

  @override
  bool operator ==(Object other) => other is DeptMonth && other.year == year && other.month == month;

  @override
  int get hashCode => Object.hash(year, month);
}

/// Месяц из «2026-11»: неверный или будущий — текущий по Ташкенту ([today] — «2026-11-07»).
DeptMonth parseMonth(String? raw, String today) {
  final ny = int.parse(today.substring(0, 4));
  final nm = int.parse(today.substring(5, 7));
  final m = RegExp(r'^(\d{4})-(\d{2})$').firstMatch(raw ?? '');
  final y = m == null ? ny : int.parse(m.group(1)!);
  final mo = m == null ? nm : int.parse(m.group(2)!);
  final valid = m != null && mo >= 1 && mo <= 12 && y * 12 + mo <= ny * 12 + nm && y >= 2020;
  final (py, pm) = valid ? (y, mo) : (ny, nm);
  return DeptMonth(py, pm, current: py == ny && pm == nm);
}

/// Последние [count] месяцев, начиная с текущего.
List<DeptMonth> monthOptions(String today, {int count = 12}) {
  var y = int.parse(today.substring(0, 4));
  var m = int.parse(today.substring(5, 7));
  final out = <DeptMonth>[];
  for (var i = 0; i < count; i++) {
    out.add(DeptMonth(y, m, current: i == 0));
    m -= 1;
    if (m == 0) {
      m = 12;
      y -= 1;
    }
  }
  return out;
}

/// Дни месяца для отчётов: с 1-го по последний; текущий — по сегодня.
({String from, String to}) monthRange(DeptMonth m, String today) {
  final from = '${m.key}-01';
  if (m.current) return (from: from, to: today);
  final last = DateTime.utc(m.year, m.month + 1, 0).day;
  return (from: from, to: '${m.key}-${last.toString().padLeft(2, '0')}');
}

// ───────────── Карточки ─────────────

class DeptCtx {
  const DeptCtx(this.l10n, this.fmt, {required this.month, required this.current});

  final AppLocalizations l10n;
  final Fmt fmt;

  /// Выбранный месяц строчными: «ноябрь 2026».
  final String month;
  final bool current;

  String monthLabel(int year, int month) => fmt.monthYear(year, month).toLowerCase();
  String pct(num v) => '${fmt.decimal(v)}%';
  String tons(num kg) => fmt.decimal(kg / 1000);
}

String _cap(String s) => s.isEmpty ? s : s[0].toUpperCase() + s.substring(1);

DeptCard _base(String code, DeptCtx ctx) =>
    DeptCard(code: code, name: departmentName(ctx.l10n, code), period: ctx.month, label: _label(ctx.l10n, code));

/// Нет источника данных — «Нет данных за ноябрь 2026».
DeptCard noDataCard(String code, DeptCtx ctx) => _base(code, ctx).copyWith(detail: _cap(ctx.l10n.dpNoData(ctx.month)));

/// Показатели закрыты правами — карточка без числа; ошибка ответа — «Не удалось загрузить» с «Повторить».
DeptCard _unavailable(String code, bool error, DeptCtx ctx) =>
    error ? _base(code, ctx).copyWith(status: DeptStatus.error, period: () => null) : _base(code, ctx).copyWith(period: () => null);

typedef SalesData = ({int year, int month, bool closed, double? planKg, double factKg, double? execution, double? forecastExecution});

/// Продажи: крупно — прогноз выполнения плана (у закрытого месяца — выполнение); полоса — факт и прогноз.
DeptCard salesCard(Load<SalesData> load, DeptCtx ctx) {
  final l10n = ctx.l10n;
  switch (load) {
    case Forbidden():
      return _unavailable('sales', false, ctx);
    case Failed():
      return _unavailable('sales', true, ctx);
    case Loaded(:final data):
      final month = ctx.monthLabel(data.year, data.month);
      final card = _base('sales', ctx).copyWith(period: () => month);
      final plan = data.planKg;
      if (plan == null || plan <= 0) {
        return card.copyWith(status: DeptStatus.attention, detail: _cap(l10n.dpNoPlan(month, ctx.tons(data.factKg))));
      }
      final execution = data.execution ?? data.factKg / plan;
      final forecast = data.closed ? null : data.forecastExecution;
      final shown = forecast ?? execution;
      final value = (shown * 100).round();
      final fact = (execution * 100).round();
      final status = execution >= 1
          ? DeptStatus.done
          : shown >= 1
          ? DeptStatus.ok
          : DeptStatus.attention;
      return card.copyWith(
        status: status,
        value: value,
        label: data.closed ? l10n.dpLabelSalesDone : l10n.dpLabelSalesForecast,
        bar: (value: fact.clamp(0, 100), forecast: forecast == null ? null : value.clamp(0, 100)),
        detail: forecast == null
            ? l10n.dpFact(ctx.tons(data.factKg), ctx.tons(plan), ctx.pct(fact))
            : l10n.dpFactForecast(ctx.tons(data.factKg), ctx.tons(plan), ctx.pct(fact), ctx.pct(value)),
        detailIcon: DeptDetailIcon.sales,
      );
  }
}

typedef HrData = ({double? monthRate, double? presentPct});

/// HR: явка за месяц (у текущего месяца без месячной — явка сегодня), норма 80 %. У текущего месяца — «Явка сегодня: 91 %» или
/// «Сегодня отметок пока нет».
DeptCard hrCard(Load<HrData> load, DeptCtx ctx) {
  final l10n = ctx.l10n;
  switch (load) {
    case Forbidden():
      return _unavailable('hr', false, ctx);
    case Failed():
      return _unavailable('hr', true, ctx);
    case Loaded(:final data):
      final today = ctx.current ? data.presentPct : null;
      final score = data.monthRate ?? today;
      if (score == null) return noDataCard('hr', ctx);
      final present = data.presentPct;
      final detail = ctx.current ? (present == null || present <= 0 ? l10n.dpHrNoMarks : l10n.dpHrToday(ctx.pct(present))) : null;
      final v = score.round();
      return DeptCard(
        code: 'hr',
        name: departmentName(l10n, 'hr'),
        status: score >= 80 ? DeptStatus.ok : DeptStatus.attention,
        value: v,
        period: data.monthRate == null ? null : ctx.month,
        label: data.monthRate == null
            ? l10n.dpLabelHrToday
            : ctx.current
            ? l10n.dpLabelHrMonthToDate
            : l10n.dpLabelHrMonth,
        bar: (value: v.clamp(0, 100), forecast: null),
        detail: data.monthRate == null ? null : detail,
        detailIcon: DeptDetailIcon.attendance,
      );
  }
}

typedef SupplyData = ({int? toBuy, int? overdue, int? onApproval});

/// Снабжение: доля заявок к закупке без просрочки (к закупке ничего — 100 %); только на сегодня.
DeptCard supplyCard(Load<SupplyData> load, DeptCtx ctx) {
  final l10n = ctx.l10n;
  if (!ctx.current) return _base('supply', ctx).copyWith(period: () => null, detail: l10n.dpSupplyTodayOnly);
  switch (load) {
    case Forbidden():
      return _unavailable('supply', false, ctx);
    case Failed():
      return _unavailable('supply', true, ctx);
    case Loaded(:final data):
      final toBuy = data.toBuy;
      if (toBuy == null) return _unavailable('supply', false, ctx);
      final overdue = (data.overdue ?? 0).clamp(0, toBuy);
      final onTime = toBuy > 0 ? (100 * (toBuy - overdue) / toBuy).round() : 100;
      final approval = data.onApproval ?? 0;
      return _base('supply', ctx).copyWith(
        status: overdue > 0 ? DeptStatus.attention : DeptStatus.ok,
        value: onTime,
        period: () => null,
        label: toBuy > 0 ? l10n.dpLabelSupplyOnTime : l10n.dpLabelSupply,
        bar: (value: onTime, forecast: null),
        detail: approval > 0 ? l10n.dpSupplyDetailApproval(toBuy, overdue, approval) : l10n.dpSupplyDetail(toBuy, overdue),
        detailIcon: DeptDetailIcon.cart,
      );
  }
}
