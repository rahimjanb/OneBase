// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'analytik_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_AnalytikPeriod _$AnalytikPeriodFromJson(Map<String, dynamic> json) =>
    _AnalytikPeriod(
      kind: json['kind'] as String,
      from: json['from'] as String,
      to: json['to'] as String,
      previousFrom: json['previousFrom'] as String?,
      previousTo: json['previousTo'] as String?,
      label: json['label'] as String? ?? '',
      previousLabel: json['previousLabel'] as String? ?? '',
      days: (json['days'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$AnalytikPeriodToJson(_AnalytikPeriod instance) =>
    <String, dynamic>{
      'kind': instance.kind,
      'from': instance.from,
      'to': instance.to,
      'previousFrom': ?instance.previousFrom,
      'previousTo': ?instance.previousTo,
      'label': instance.label,
      'previousLabel': instance.previousLabel,
      'days': instance.days,
    };

_AnalytikKpi _$AnalytikKpiFromJson(Map<String, dynamic> json) => _AnalytikKpi(
  code: json['code'] as String,
  title: json['title'] as String,
  value: (json['value'] as num?)?.toDouble(),
  unit: json['unit'] as String? ?? '',
  previous: (json['previous'] as num?)?.toDouble(),
  changePercent: (json['changePercent'] as num?)?.toDouble(),
  kind: json['kind'] as String? ?? 'fact',
  href: json['href'] as String?,
  note: json['note'] as String?,
);

Map<String, dynamic> _$AnalytikKpiToJson(_AnalytikKpi instance) =>
    <String, dynamic>{
      'code': instance.code,
      'title': instance.title,
      'value': ?instance.value,
      'unit': instance.unit,
      'previous': ?instance.previous,
      'changePercent': ?instance.changePercent,
      'kind': instance.kind,
      'href': ?instance.href,
      'note': ?instance.note,
    };

_AnalytikBlock _$AnalytikBlockFromJson(Map<String, dynamic> json) =>
    _AnalytikBlock(
      period: AnalytikPeriod.fromJson(json['period'] as Map<String, dynamic>),
      kpis:
          (json['kpis'] as List<dynamic>?)
              ?.map((e) => AnalytikKpi.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <AnalytikKpi>[],
      complete: json['complete'] as bool? ?? true,
      dataThrough: json['dataThrough'] as String?,
      scope: json['scope'] as String?,
      notes:
          (json['notes'] as List<dynamic>?)?.map((e) => e as String).toList() ??
          const <String>[],
      sources:
          (json['sources'] as List<dynamic>?)
              ?.map((e) => e as String)
              .toList() ??
          const <String>[],
    );

Map<String, dynamic> _$AnalytikBlockToJson(_AnalytikBlock instance) =>
    <String, dynamic>{
      'period': instance.period.toJson(),
      'kpis': instance.kpis.map((e) => e.toJson()).toList(),
      'complete': instance.complete,
      'dataThrough': ?instance.dataThrough,
      'scope': ?instance.scope,
      'notes': instance.notes,
      'sources': instance.sources,
    };

_AnalytikNav _$AnalytikNavFromJson(Map<String, dynamic> json) => _AnalytikNav(
  critical: (json['critical'] as num?)?.toInt() ?? 0,
  warning: (json['warning'] as num?)?.toInt() ?? 0,
  info: (json['info'] as num?)?.toInt() ?? 0,
  total: (json['total'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$AnalytikNavToJson(_AnalytikNav instance) =>
    <String, dynamic>{
      'critical': instance.critical,
      'warning': instance.warning,
      'info': instance.info,
      'total': instance.total,
    };

_TechnologistBrief _$TechnologistBriefFromJson(Map<String, dynamic> json) =>
    _TechnologistBrief(
      todayKg: (json['todayKg'] as num?)?.toDouble() ?? 0,
      goodKg: (json['goodKg'] as num?)?.toDouble() ?? 0,
      planKg: (json['planKg'] as num?)?.toDouble() ?? 0,
      completionPercent: (json['completionPercent'] as num?)?.toDouble(),
      defectKg: (json['defectKg'] as num?)?.toDouble() ?? 0,
      defectPercent: (json['defectPercent'] as num?)?.toDouble(),
      activeOrders: (json['activeOrders'] as num?)?.toInt() ?? 0,
      openShifts: (json['openShifts'] as num?)?.toInt() ?? 0,
      normDeviationPercent: (json['normDeviationPercent'] as num?)?.toDouble(),
      days:
          (json['days'] as List<dynamic>?)
              ?.map((e) => TechDay.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <TechDay>[],
    );

Map<String, dynamic> _$TechnologistBriefToJson(_TechnologistBrief instance) =>
    <String, dynamic>{
      'todayKg': instance.todayKg,
      'goodKg': instance.goodKg,
      'planKg': instance.planKg,
      'completionPercent': ?instance.completionPercent,
      'defectKg': instance.defectKg,
      'defectPercent': ?instance.defectPercent,
      'activeOrders': instance.activeOrders,
      'openShifts': instance.openShifts,
      'normDeviationPercent': ?instance.normDeviationPercent,
      'days': instance.days.map((e) => e.toJson()).toList(),
    };

_TechDay _$TechDayFromJson(Map<String, dynamic> json) => _TechDay(
  date: json['date'] as String,
  goodKg: (json['goodKg'] as num?)?.toDouble() ?? 0,
  defectKg: (json['defectKg'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$TechDayToJson(_TechDay instance) => <String, dynamic>{
  'date': instance.date,
  'goodKg': instance.goodKg,
  'defectKg': instance.defectKg,
};
