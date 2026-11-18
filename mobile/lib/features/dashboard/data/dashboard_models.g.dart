// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dashboard_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_SalesPeriod _$SalesPeriodFromJson(Map<String, dynamic> json) => _SalesPeriod(
  year: (json['year'] as num).toInt(),
  month: (json['month'] as num).toInt(),
  dataThrough: json['dataThrough'] as String,
  today: json['today'] as String?,
  workedDays: (json['workedDays'] as num?)?.toInt() ?? 0,
  elapsedDays: (json['elapsedDays'] as num?)?.toDouble(),
  daysInMonth: (json['daysInMonth'] as num).toInt(),
  closed: json['closed'] as bool? ?? false,
  previousCutoff: json['previousCutoff'] as String?,
);

Map<String, dynamic> _$SalesPeriodToJson(_SalesPeriod instance) =>
    <String, dynamic>{
      'year': instance.year,
      'month': instance.month,
      'dataThrough': instance.dataThrough,
      'today': ?instance.today,
      'workedDays': instance.workedDays,
      'elapsedDays': ?instance.elapsedDays,
      'daysInMonth': instance.daysInMonth,
      'closed': instance.closed,
      'previousCutoff': ?instance.previousCutoff,
    };

_TargetValue _$TargetValueFromJson(Map<String, dynamic> json) => _TargetValue(
  value: (json['value'] as num?)?.toDouble(),
  target: (json['target'] as num?)?.toDouble() ?? 0,
  ratio: (json['ratio'] as num?)?.toDouble(),
  level: json['level'] as String?,
  forecast: (json['forecast'] as num?)?.toDouble(),
);

Map<String, dynamic> _$TargetValueToJson(_TargetValue instance) =>
    <String, dynamic>{
      'value': ?instance.value,
      'target': instance.target,
      'ratio': ?instance.ratio,
      'level': ?instance.level,
      'forecast': ?instance.forecast,
    };

_SalesPace _$SalesPaceFromJson(Map<String, dynamic> json) => _SalesPace(
  expected: (json['expected'] as num?)?.toDouble(),
  paceKg: (json['paceKg'] as num?)?.toDouble(),
  requiredKg: (json['requiredKg'] as num?)?.toDouble(),
  level: json['level'] as String?,
);

Map<String, dynamic> _$SalesPaceToJson(_SalesPace instance) =>
    <String, dynamic>{
      'expected': ?instance.expected,
      'paceKg': ?instance.paceKg,
      'requiredKg': ?instance.requiredKg,
      'level': ?instance.level,
    };

_SalesKpi _$SalesKpiFromJson(Map<String, dynamic> json) => _SalesKpi(
  factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
  planKg: (json['planKg'] as num?)?.toDouble(),
  execution: (json['execution'] as num?)?.toDouble(),
  forecastKg: (json['forecastKg'] as num?)?.toDouble(),
  forecastExecution: (json['forecastExecution'] as num?)?.toDouble(),
  revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
  akb: (json['akb'] as num?)?.toInt() ?? 0,
  conversion: json['conversion'] == null
      ? null
      : TargetValue.fromJson(json['conversion'] as Map<String, dynamic>),
  revenuePerOutlet: json['revenuePerOutlet'] == null
      ? null
      : TargetValue.fromJson(json['revenuePerOutlet'] as Map<String, dynamic>),
  akbPerAgent: json['akbPerAgent'] == null
      ? null
      : TargetValue.fromJson(json['akbPerAgent'] as Map<String, dynamic>),
  visitsWithoutOrder: (json['visitsWithoutOrder'] as num?)?.toInt() ?? 0,
  visitsDone: (json['visitsDone'] as num?)?.toInt() ?? 0,
  activeAgents: (json['activeAgents'] as num?)?.toInt() ?? 0,
  planSource: json['planSource'] as String?,
  executionLevel: json['executionLevel'] as String?,
  pace: json['pace'] == null
      ? null
      : SalesPace.fromJson(json['pace'] as Map<String, dynamic>),
);

Map<String, dynamic> _$SalesKpiToJson(_SalesKpi instance) => <String, dynamic>{
  'factKg': instance.factKg,
  'planKg': ?instance.planKg,
  'execution': ?instance.execution,
  'forecastKg': ?instance.forecastKg,
  'forecastExecution': ?instance.forecastExecution,
  'revenue': instance.revenue,
  'akb': instance.akb,
  'conversion': ?instance.conversion?.toJson(),
  'revenuePerOutlet': ?instance.revenuePerOutlet?.toJson(),
  'akbPerAgent': ?instance.akbPerAgent?.toJson(),
  'visitsWithoutOrder': instance.visitsWithoutOrder,
  'visitsDone': instance.visitsDone,
  'activeAgents': instance.activeAgents,
  'planSource': ?instance.planSource,
  'executionLevel': ?instance.executionLevel,
  'pace': ?instance.pace?.toJson(),
};

_FlagCounts _$FlagCountsFromJson(Map<String, dynamic> json) => _FlagCounts(
  critical: (json['critical'] as num?)?.toInt() ?? 0,
  risk: (json['risk'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$FlagCountsToJson(_FlagCounts instance) =>
    <String, dynamic>{'critical': instance.critical, 'risk': instance.risk};

_SalesOverview _$SalesOverviewFromJson(Map<String, dynamic> json) =>
    _SalesOverview(
      period: SalesPeriod.fromJson(json['period'] as Map<String, dynamic>),
      kpi: SalesKpi.fromJson(json['kpi'] as Map<String, dynamic>),
      activeAgents: (json['activeAgents'] as num?)?.toInt() ?? 0,
      flags: json['flags'] == null
          ? const FlagCounts()
          : FlagCounts.fromJson(json['flags'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$SalesOverviewToJson(_SalesOverview instance) =>
    <String, dynamic>{
      'period': instance.period.toJson(),
      'kpi': instance.kpi.toJson(),
      'activeAgents': instance.activeAgents,
      'flags': instance.flags.toJson(),
    };

_SalesMonth _$SalesMonthFromJson(Map<String, dynamic> json) => _SalesMonth(
  year: (json['year'] as num).toInt(),
  month: (json['month'] as num).toInt(),
);

Map<String, dynamic> _$SalesMonthToJson(_SalesMonth instance) =>
    <String, dynamic>{'year': instance.year, 'month': instance.month};

_SalesDaily _$SalesDailyFromJson(Map<String, dynamic> json) => _SalesDaily(
  year: (json['year'] as num).toInt(),
  month: (json['month'] as num).toInt(),
  dataThrough: json['dataThrough'] as String,
  today: json['today'] as String?,
  daysInMonth: (json['daysInMonth'] as num).toInt(),
  closed: json['closed'] as bool? ?? false,
  days: (json['days'] as List<dynamic>)
      .map((e) => (e as num?)?.toDouble())
      .toList(),
  sundays:
      (json['sundays'] as List<dynamic>?)?.map((e) => e as bool).toList() ??
      const <bool>[],
  total: (json['total'] as num?)?.toDouble() ?? 0,
  sameNowKg: (json['sameNowKg'] as num?)?.toDouble() ?? 0,
  sameBeforeKg: (json['sameBeforeKg'] as num?)?.toDouble() ?? 0,
  previousCutoff: json['previousCutoff'] as String?,
);

Map<String, dynamic> _$SalesDailyToJson(_SalesDaily instance) =>
    <String, dynamic>{
      'year': instance.year,
      'month': instance.month,
      'dataThrough': instance.dataThrough,
      'today': ?instance.today,
      'daysInMonth': instance.daysInMonth,
      'closed': instance.closed,
      'days': instance.days,
      'sundays': instance.sundays,
      'total': instance.total,
      'sameNowKg': instance.sameNowKg,
      'sameBeforeKg': instance.sameBeforeKg,
      'previousCutoff': ?instance.previousCutoff,
    };

_FinanceMeta _$FinanceMetaFromJson(Map<String, dynamic> json) => _FinanceMeta(
  hasData: json['hasData'] as bool? ?? false,
  demo: json['demo'] as bool? ?? false,
  note: json['note'] as String?,
  dataThrough: json['dataThrough'] as String?,
  syncedAt: json['syncedAt'] as String?,
  organizationName: json['organizationName'] as String?,
  from: json['from'] as String,
  to: json['to'] as String,
);

Map<String, dynamic> _$FinanceMetaToJson(_FinanceMeta instance) =>
    <String, dynamic>{
      'hasData': instance.hasData,
      'demo': instance.demo,
      'note': ?instance.note,
      'dataThrough': ?instance.dataThrough,
      'syncedAt': ?instance.syncedAt,
      'organizationName': ?instance.organizationName,
      'from': instance.from,
      'to': instance.to,
    };

_PnlValue _$PnlValueFromJson(Map<String, dynamic> json) => _PnlValue(
  code: json['code'] as String,
  title: json['title'] as String,
  kind: json['kind'] as String,
  value: (json['value'] as num?)?.toDouble() ?? 0,
  previous: (json['previous'] as num?)?.toDouble(),
);

Map<String, dynamic> _$PnlValueToJson(_PnlValue instance) => <String, dynamic>{
  'code': instance.code,
  'title': instance.title,
  'kind': instance.kind,
  'value': instance.value,
  'previous': ?instance.previous,
};

_MonthPoint _$MonthPointFromJson(Map<String, dynamic> json) => _MonthPoint(
  month: json['month'] as String,
  revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
  expenses: (json['expenses'] as num?)?.toDouble() ?? 0,
  netProfit: (json['netProfit'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$MonthPointToJson(_MonthPoint instance) =>
    <String, dynamic>{
      'month': instance.month,
      'revenue': instance.revenue,
      'expenses': instance.expenses,
      'netProfit': instance.netProfit,
    };

_FinanceOverview _$FinanceOverviewFromJson(Map<String, dynamic> json) =>
    _FinanceOverview(
      meta: FinanceMeta.fromJson(json['meta'] as Map<String, dynamic>),
      pnl:
          (json['pnl'] as List<dynamic>?)
              ?.map((e) => PnlValue.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <PnlValue>[],
      months:
          (json['months'] as List<dynamic>?)
              ?.map((e) => MonthPoint.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <MonthPoint>[],
      cashTotal: (json['cashTotal'] as num?)?.toDouble() ?? 0,
      receivables: (json['receivables'] as num?)?.toDouble() ?? 0,
      payables: (json['payables'] as num?)?.toDouble() ?? 0,
    );

Map<String, dynamic> _$FinanceOverviewToJson(_FinanceOverview instance) =>
    <String, dynamic>{
      'meta': instance.meta.toJson(),
      'pnl': instance.pnl.map((e) => e.toJson()).toList(),
      'months': instance.months.map((e) => e.toJson()).toList(),
      'cashTotal': instance.cashTotal,
      'receivables': instance.receivables,
      'payables': instance.payables,
    };

_FinanceOrganization _$FinanceOrganizationFromJson(Map<String, dynamic> json) =>
    _FinanceOrganization(
      id: json['id'] as String,
      name: json['name'] as String,
    );

Map<String, dynamic> _$FinanceOrganizationToJson(
  _FinanceOrganization instance,
) => <String, dynamic>{'id': instance.id, 'name': instance.name};

_HrDivision _$HrDivisionFromJson(Map<String, dynamic> json) => _HrDivision(
  id: _string(json['id']),
  name: json['name'] as String,
  active: (json['active'] as num?)?.toInt() ?? 0,
  scheduled: (json['scheduled'] as num?)?.toInt() ?? 0,
  present: (json['present'] as num?)?.toInt() ?? 0,
  late: (json['late'] as num?)?.toInt() ?? 0,
  absent: (json['absent'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$HrDivisionToJson(_HrDivision instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'active': instance.active,
      'scheduled': instance.scheduled,
      'present': instance.present,
      'late': instance.late,
      'absent': instance.absent,
    };

_HrCompare _$HrCompareFromJson(Map<String, dynamic> json) => _HrCompare(
  date: json['date'] as String?,
  presentPct: (json['presentPct'] as num?)?.toDouble(),
);

Map<String, dynamic> _$HrCompareToJson(_HrCompare instance) =>
    <String, dynamic>{
      'date': ?instance.date,
      'presentPct': ?instance.presentPct,
    };

_HrSync _$HrSyncFromJson(Map<String, dynamic> json) => _HrSync(
  lastSuccessAt: json['lastSuccessAt'] as String?,
  lastStatus: json['lastStatus'] as String?,
);

Map<String, dynamic> _$HrSyncToJson(_HrSync instance) => <String, dynamic>{
  'lastSuccessAt': ?instance.lastSuccessAt,
  'lastStatus': ?instance.lastStatus,
};

_HrOverview _$HrOverviewFromJson(Map<String, dynamic> json) => _HrOverview(
  date: json['date'] as String,
  active: (json['active'] as num?)?.toInt() ?? 0,
  scheduled: (json['scheduled'] as num?)?.toInt() ?? 0,
  present: (json['present'] as num?)?.toInt() ?? 0,
  late: (json['late'] as num?)?.toInt() ?? 0,
  absent: (json['absent'] as num?)?.toInt() ?? 0,
  onLeave: (json['onLeave'] as num?)?.toInt() ?? 0,
  compare: json['compare'] == null
      ? null
      : HrCompare.fromJson(json['compare'] as Map<String, dynamic>),
  divisions:
      (json['divisions'] as List<dynamic>?)
          ?.map((e) => HrDivision.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <HrDivision>[],
  sync: json['sync'] == null
      ? null
      : HrSync.fromJson(json['sync'] as Map<String, dynamic>),
);

Map<String, dynamic> _$HrOverviewToJson(_HrOverview instance) =>
    <String, dynamic>{
      'date': instance.date,
      'active': instance.active,
      'scheduled': instance.scheduled,
      'present': instance.present,
      'late': instance.late,
      'absent': instance.absent,
      'onLeave': instance.onLeave,
      'compare': ?instance.compare?.toJson(),
      'divisions': instance.divisions.map((e) => e.toJson()).toList(),
      'sync': ?instance.sync?.toJson(),
    };

_WorkDepartment _$WorkDepartmentFromJson(Map<String, dynamic> json) =>
    _WorkDepartment(
      code: json['code'] as String,
      name: json['name'] as String,
      open: (json['open'] as num?)?.toInt() ?? 0,
      overdue: (json['overdue'] as num?)?.toInt() ?? 0,
      review: (json['review'] as num?)?.toInt() ?? 0,
      blocked: (json['blocked'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$WorkDepartmentToJson(_WorkDepartment instance) =>
    <String, dynamic>{
      'code': instance.code,
      'name': instance.name,
      'open': instance.open,
      'overdue': instance.overdue,
      'review': instance.review,
      'blocked': instance.blocked,
    };

_WorkTaskBrief _$WorkTaskBriefFromJson(Map<String, dynamic> json) =>
    _WorkTaskBrief(
      id: json['id'] as String,
      title: json['title'] as String,
      status: json['status'] as String,
      priority: json['priority'] as String? ?? 'Medium',
      dueDate: json['dueDate'] as String?,
      overdue: json['overdue'] as bool? ?? false,
      departmentName: json['departmentName'] as String?,
      assigneeName: json['assigneeName'] as String?,
    );

Map<String, dynamic> _$WorkTaskBriefToJson(_WorkTaskBrief instance) =>
    <String, dynamic>{
      'id': instance.id,
      'title': instance.title,
      'status': instance.status,
      'priority': instance.priority,
      'dueDate': ?instance.dueDate,
      'overdue': instance.overdue,
      'departmentName': ?instance.departmentName,
      'assigneeName': ?instance.assigneeName,
    };

_WorkOverview _$WorkOverviewFromJson(Map<String, dynamic> json) =>
    _WorkOverview(
      open: (json['open'] as num?)?.toInt() ?? 0,
      inProgress: (json['inProgress'] as num?)?.toInt() ?? 0,
      review: (json['review'] as num?)?.toInt() ?? 0,
      overdue: (json['overdue'] as num?)?.toInt() ?? 0,
      blocked: (json['blocked'] as num?)?.toInt() ?? 0,
      dueThisWeek: (json['dueThisWeek'] as num?)?.toInt() ?? 0,
      doneThisMonth: (json['doneThisMonth'] as num?)?.toInt() ?? 0,
      myOpen: (json['myOpen'] as num?)?.toInt() ?? 0,
      myOverdue: (json['myOverdue'] as num?)?.toInt() ?? 0,
      awaitingMyReview: (json['awaitingMyReview'] as num?)?.toInt() ?? 0,
      departments:
          (json['departments'] as List<dynamic>?)
              ?.map((e) => WorkDepartment.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <WorkDepartment>[],
      myUpcoming:
          (json['myUpcoming'] as List<dynamic>?)
              ?.map((e) => WorkTaskBrief.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <WorkTaskBrief>[],
    );

Map<String, dynamic> _$WorkOverviewToJson(_WorkOverview instance) =>
    <String, dynamic>{
      'open': instance.open,
      'inProgress': instance.inProgress,
      'review': instance.review,
      'overdue': instance.overdue,
      'blocked': instance.blocked,
      'dueThisWeek': instance.dueThisWeek,
      'doneThisMonth': instance.doneThisMonth,
      'myOpen': instance.myOpen,
      'myOverdue': instance.myOverdue,
      'awaitingMyReview': instance.awaitingMyReview,
      'departments': instance.departments.map((e) => e.toJson()).toList(),
      'myUpcoming': instance.myUpcoming.map((e) => e.toJson()).toList(),
    };

_NotificationItem _$NotificationItemFromJson(Map<String, dynamic> json) =>
    _NotificationItem(
      id: json['id'] as String,
      category: json['category'] as String?,
      kind: json['kind'] as String?,
      title: json['title'] as String,
      body: json['body'] as String?,
      createdAt: json['createdAt'] as String,
      read: json['read'] as bool? ?? false,
      severity: json['severity'] as String?,
    );

Map<String, dynamic> _$NotificationItemToJson(_NotificationItem instance) =>
    <String, dynamic>{
      'id': instance.id,
      'category': ?instance.category,
      'kind': ?instance.kind,
      'title': instance.title,
      'body': ?instance.body,
      'createdAt': instance.createdAt,
      'read': instance.read,
      'severity': ?instance.severity,
    };

_NotificationPage _$NotificationPageFromJson(Map<String, dynamic> json) =>
    _NotificationPage(
      items:
          (json['items'] as List<dynamic>?)
              ?.map((e) => NotificationItem.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <NotificationItem>[],
      unread: (json['unread'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$NotificationPageToJson(_NotificationPage instance) =>
    <String, dynamic>{
      'items': instance.items.map((e) => e.toJson()).toList(),
      'unread': instance.unread,
    };

_AlertsSummary _$AlertsSummaryFromJson(Map<String, dynamic> json) =>
    _AlertsSummary(
      problems: (json['problems'] as num?)?.toInt() ?? 0,
      critical: (json['critical'] as num?)?.toInt() ?? 0,
      recommendations: (json['recommendations'] as num?)?.toInt() ?? 0,
      opportunities: (json['opportunities'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$AlertsSummaryToJson(_AlertsSummary instance) =>
    <String, dynamic>{
      'problems': instance.problems,
      'critical': instance.critical,
      'recommendations': instance.recommendations,
      'opportunities': instance.opportunities,
    };

_AlertsRun _$AlertsRunFromJson(Map<String, dynamic> json) =>
    _AlertsRun(at: json['at'] as String?);

Map<String, dynamic> _$AlertsRunToJson(_AlertsRun instance) =>
    <String, dynamic>{'at': ?instance.at};

_AiAlert _$AiAlertFromJson(Map<String, dynamic> json) => _AiAlert(
  id: json['id'] as String,
  category: json['category'] as String,
  severity: json['severity'] as String,
  title: json['title'] as String,
  message: json['message'] as String?,
  recommendation: json['recommendation'] as String?,
  detectedAt: json['detectedAt'] as String?,
);

Map<String, dynamic> _$AiAlertToJson(_AiAlert instance) => <String, dynamic>{
  'id': instance.id,
  'category': instance.category,
  'severity': instance.severity,
  'title': instance.title,
  'message': ?instance.message,
  'recommendation': ?instance.recommendation,
  'detectedAt': ?instance.detectedAt,
};

_AiAlerts _$AiAlertsFromJson(Map<String, dynamic> json) => _AiAlerts(
  summary: json['summary'] == null
      ? const AlertsSummary()
      : AlertsSummary.fromJson(json['summary'] as Map<String, dynamic>),
  lastRun: json['lastRun'] == null
      ? null
      : AlertsRun.fromJson(json['lastRun'] as Map<String, dynamic>),
  alerts:
      (json['alerts'] as List<dynamic>?)
          ?.map((e) => AiAlert.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AiAlert>[],
);

Map<String, dynamic> _$AiAlertsToJson(_AiAlerts instance) => <String, dynamic>{
  'summary': instance.summary.toJson(),
  'lastRun': ?instance.lastRun?.toJson(),
  'alerts': instance.alerts.map((e) => e.toJson()).toList(),
};
