import 'package:freezed_annotation/freezed_annotation.dart';

part 'crm_models.freezed.dart';
part 'crm_models.g.dart';

// Клиенты (точки) — Sales Base (`/api/field/customers*`): единственный на сервере список точек с поиском, статусами, приоритетами,
// правкой и журналом изменений. id точки (`marketId`) общий с продажами (`/api/sales/stores/{id}`). Клиенты, сделки и заказы в OneBase
// не создаются — источник Linko, только чтение; правится «надстройка» Sales Base (статус, приоритет, план, контакты).

@freezed
abstract class FieldCustomerRow with _$FieldCustomerRow {
  const factory FieldCustomerRow({
    required int marketId,
    required String name,
    String? address,
    String? branch,
    String? type,
    double? lat,
    double? lon,
    String? agentId,
    String? agentName,

    /// Low | Medium | High | Urgent
    @Default('Medium') String priority,

    /// Active | Problem | Inactive
    @Default('Active') String status,
    String? lastVisitDate,
    String? lastOrderDate,
    @Default(0) double sales7,
    @Default(0) double sales30,
    @Default(0) double sales90,

    /// Изменение продаж, % (уже ×100).
    double? trendPct,
    double? monthlyTarget,
  }) = _FieldCustomerRow;

  factory FieldCustomerRow.fromJson(Map<String, Object?> json) => _$FieldCustomerRowFromJson(json);
}

@freezed
abstract class CustomerMonth with _$CustomerMonth {
  const factory CustomerMonth({required int year, required int month, @Default(0) double sum, @Default(0) double kg, @Default(0) int orders}) =
      _CustomerMonth;

  factory CustomerMonth.fromJson(Map<String, Object?> json) => _$CustomerMonthFromJson(json);
}

@freezed
abstract class CustomerOrder with _$CustomerOrder {
  const factory CustomerOrder({
    required int id,
    required String date,
    String? acceptedDate,
    @Default('') String status,
    String? agent,
    @Default(0) double sum,
    @Default(0) double kg,
  }) = _CustomerOrder;

  factory CustomerOrder.fromJson(Map<String, Object?> json) => _$CustomerOrderFromJson(json);
}

@freezed
abstract class CustomerVisit with _$CustomerVisit {
  const factory CustomerVisit({
    /// Linko | SalesBase
    @Default('Linko') String source,
    required String at,
    String? agent,
    @Default('') String status,
    String? result,
    String? comment,
  }) = _CustomerVisit;

  factory CustomerVisit.fromJson(Map<String, Object?> json) => _$CustomerVisitFromJson(json);
}

@freezed
abstract class FieldCustomerCard with _$FieldCustomerCard {
  const factory FieldCustomerCard({
    required FieldCustomerRow customer,
    int? linkoResponsibleId,
    String? linkoResponsibleName,
    String? supervisorName,
    String? contactPerson,
    String? phone,
    String? note,
    @Default(0) double monthToDate,
    @Default(0) int orders30,
    @Default(0) int visits30,
    @Default(<CustomerMonth>[]) List<CustomerMonth> months,
    @Default(<CustomerOrder>[]) List<CustomerOrder> orders,
    @Default(<CustomerVisit>[]) List<CustomerVisit> visits,

    /// Статус, приоритет и план меняют СВ/РМ; контакты и заметку — и агент точки.
    @Default(false) bool canEdit,
    @Default(false) bool canChangeAgent,
  }) = _FieldCustomerCard;

  factory FieldCustomerCard.fromJson(Map<String, Object?> json) => _$FieldCustomerCardFromJson(json);
}

/// Запись журнала изменений точки (`/api/field/audit`).
@freezed
abstract class AuditEntry with _$AuditEntry {
  const factory AuditEntry({required int id, required String timestamp, @Default('') String actor, required String action, @Default('') String data}) =
      _AuditEntry;

  factory AuditEntry.fromJson(Map<String, Object?> json) => _$AuditEntryFromJson(json);
}
