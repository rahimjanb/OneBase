import 'package:freezed_annotation/freezed_annotation.dart';

part 'analytik_models.freezed.dart';
part 'analytik_models.g.dart';

// Сравнение периодов «Аналитика» OneBase (`/api/analytik/{sales|finance|production|hr}?period=…`): показатели и изменение к
// прошлому периоду считает сервер кодом (без AI-модели, бесплатно). value = null — «нет данных», а не ноль.

@freezed
abstract class AnalytikPeriod with _$AnalytikPeriod {
  const factory AnalytikPeriod({
    required String kind,
    required String from,
    required String to,
    String? previousFrom,
    String? previousTo,
    @Default('') String label,
    @Default('') String previousLabel,
    @Default(0) int days,
  }) = _AnalytikPeriod;

  factory AnalytikPeriod.fromJson(Map<String, Object?> json) => _$AnalytikPeriodFromJson(json);
}

@freezed
abstract class AnalytikKpi with _$AnalytikKpi {
  const factory AnalytikKpi({
    required String code,
    required String title,
    double? value,
    @Default('') String unit,
    double? previous,

    /// Изменение в процентах (уже ×100), считает сервер.
    double? changePercent,

    /// fact | calculated | forecast
    @Default('fact') String kind,
    String? href,
    String? note,
  }) = _AnalytikKpi;

  factory AnalytikKpi.fromJson(Map<String, Object?> json) => _$AnalytikKpiFromJson(json);
}

/// Ответ модуля: период, показатели, пометки (неполные данные) и источники.
@freezed
abstract class AnalytikBlock with _$AnalytikBlock {
  const factory AnalytikBlock({
    required AnalytikPeriod period,
    @Default(<AnalytikKpi>[]) List<AnalytikKpi> kpis,
    @Default(true) bool complete,
    String? dataThrough,
    String? scope,
    @Default(<String>[]) List<String> notes,
    @Default(<String>[]) List<String> sources,
  }) = _AnalytikBlock;

  factory AnalytikBlock.fromJson(Map<String, Object?> json) => _$AnalytikBlockFromJson(json);
}

/// `/api/analytik/hr` — два раздела (персонал и задачи), каждый может отсутствовать; телефон показывает их одним списком.
AnalytikBlock analytikOf(Object? json) {
  final j = json! as Map<String, Object?>;
  if (j.containsKey('kpis')) return AnalytikBlock.fromJson(j);
  final parts = [for (final k in const ['hr', 'work']) if (j[k] is Map<String, Object?>) AnalytikBlock.fromJson(j[k]! as Map<String, Object?>)];
  if (parts.isEmpty) return AnalytikBlock.fromJson({'period': j['period'], 'kpis': const <Object?>[]});
  return parts.first.copyWith(
    kpis: [for (final p in parts) ...p.kpis],
    notes: [for (final p in parts) ...p.notes],
    sources: [for (final p in parts) ...p.sources],
  );
}

/// Счётчики находок Аналитика (`/api/analytik/nav`).
@freezed
abstract class AnalytikNav with _$AnalytikNav {
  const factory AnalytikNav({@Default(0) int critical, @Default(0) int warning, @Default(0) int info, @Default(0) int total}) = _AnalytikNav;

  factory AnalytikNav.fromJson(Map<String, Object?> json) => _$AnalytikNavFromJson(json);
}

/// Производство: сводка технолога (`/api/production/technologist`) — для карточки аналитики.
@freezed
abstract class TechnologistBrief with _$TechnologistBrief {
  const factory TechnologistBrief({
    @Default(0) double todayKg,
    @Default(0) double goodKg,
    @Default(0) double planKg,
    double? completionPercent,
    @Default(0) double defectKg,
    double? defectPercent,
    @Default(0) int activeOrders,
    @Default(0) int openShifts,
    double? normDeviationPercent,
    @Default(<TechDay>[]) List<TechDay> days,
  }) = _TechnologistBrief;

  factory TechnologistBrief.fromJson(Map<String, Object?> json) => _$TechnologistBriefFromJson(json);
}

@freezed
abstract class TechDay with _$TechDay {
  const factory TechDay({required String date, @Default(0) double goodKg, @Default(0) double defectKg}) = _TechDay;

  factory TechDay.fromJson(Map<String, Object?> json) => _$TechDayFromJson(json);
}
