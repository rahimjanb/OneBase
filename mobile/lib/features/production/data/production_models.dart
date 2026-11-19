import 'package:freezed_annotation/freezed_annotation.dart';

part 'production_models.freezed.dart';
part 'production_models.g.dart';

// Производство (`/api/production/*`) и склад сырья (`/api/procurement/*`). Права проверяет сервис: кнопки включаются по флагам ответа
// (`can`, `posts`). Складские проводки и закрытие задания — без оптимистичного интерфейса, с `clientKey` (повтор не создаёт дубль).

@freezed
abstract class NamedValue with _$NamedValue {
  const factory NamedValue({required String name, @Default(0) double value, double? extra, String? unit}) = _NamedValue;

  factory NamedValue.fromJson(Map<String, Object?> json) => _$NamedValueFromJson(json);
}

@freezed
abstract class ProdIssue with _$ProdIssue {
  const factory ProdIssue({required String kind, required String title, @Default('') String detail, String? link}) = _ProdIssue;

  factory ProdIssue.fromJson(Map<String, Object?> json) => _$ProdIssueFromJson(json);
}

@freezed
abstract class ProdDay with _$ProdDay {
  const factory ProdDay({required String date, @Default(0) double goodKg, @Default(0) double defectKg}) = _ProdDay;

  factory ProdDay.fromJson(Map<String, Object?> json) => _$ProdDayFromJson(json);
}

@freezed
abstract class ProductionDashboard with _$ProductionDashboard {
  const factory ProductionDashboard({
    @Default(0) double todayKg,
    @Default(0) double goodKg,
    @Default(0) double planKg,
    double? completionPercent,
    @Default(0) double defectKg,
    double? defectPercent,
    double? defectCost,
    @Default(0) int activeOrders,
    @Default(0) int openShifts,
    @Default(0) int closedShifts,
    @Default(0) double normKg,
    @Default(0) double factKg,
    double? normDeviationPercent,
    @Default(<ProdDay>[]) List<ProdDay> days,
    @Default(<NamedValue>[]) List<NamedValue> byProduct,
    @Default(<NamedValue>[]) List<NamedValue> defectsByType,
    @Default(<ProdIssue>[]) List<ProdIssue> issues,
  }) = _ProductionDashboard;

  factory ProductionDashboard.fromJson(Map<String, Object?> json) => _$ProductionDashboardFromJson(json);
}

@freezed
abstract class CountValue with _$CountValue {
  const factory CountValue({@Default(0) int count, @Default(0) double value}) = _CountValue;

  factory CountValue.fromJson(Map<String, Object?> json) => _$CountValueFromJson(json);
}

@freezed
abstract class LowDays with _$LowDays {
  const factory LowDays({required String name, @Default('') String unitCode, @Default(0) double onHand, @Default(0) double perDay, @Default(0) double days}) =
      _LowDays;

  factory LowDays.fromJson(Map<String, Object?> json) => _$LowDaysFromJson(json);
}

@freezed
abstract class WarehouseValue with _$WarehouseValue {
  const factory WarehouseValue({required String name, @Default(0) int items, @Default(0) double value}) = _WarehouseValue;

  factory WarehouseValue.fromJson(Map<String, Object?> json) => _$WarehouseValueFromJson(json);
}

@freezed
abstract class ProductionOverview with _$ProductionOverview {
  const factory ProductionOverview({
    @Default(<WarehouseValue>[]) List<WarehouseValue> stock,
    @Default(0) double stockValue,
    @Default(CountValue()) CountValue expiring7,
    @Default(CountValue()) CountValue expiring30,
    @Default(CountValue()) CountValue expired,
    @Default(CountValue()) CountValue onControl,
    @Default(0) int belowMin,
    @Default(0) int openOrders,
    @Default(0) int overdueOrders,
    @Default(<LowDays>[]) List<LowDays> lowDays,
  }) = _ProductionOverview;

  factory ProductionOverview.fromJson(Map<String, Object?> json) => _$ProductionOverviewFromJson(json);
}

/// Задание на выпуск (строка журнала). Статус: Draft, Released, InProgress, Completed, Cancelled.
@freezed
abstract class ProductionOrderRow with _$ProductionOrderRow {
  const factory ProductionOrderRow({
    required String id,
    required String number,
    @Default('') String recipeName,
    @Default('') String outputUnit,
    @Default(0) double plannedQty,
    double? outputQty,
    required String plannedDate,
    required String status,
    @Default('') String departmentName,
    @Default('') String warehouseName,
    String? responsibleName,
    @Default(false) bool overconsumed,
  }) = _ProductionOrderRow;

  factory ProductionOrderRow.fromJson(Map<String, Object?> json) => _$ProductionOrderRowFromJson(json);
}

@freezed
abstract class OrderLine with _$OrderLine {
  const factory OrderLine({
    required String name,
    @Default('') String sku,
    @Default('') String unitCode,
    @Default(0) double required,
    @Default(0) double issued,
    @Default(0) double returned,
    @Default(0) double left,
    @Default(0) double available,
    double? deviationPercent,
    @Default(false) bool overconsumed,
  }) = _OrderLine;

  factory OrderLine.fromJson(Map<String, Object?> json) => _$OrderLineFromJson(json);
}

@freezed
abstract class OrderCan with _$OrderCan {
  const factory OrderCan({
    @Default(false) bool release,
    @Default(false) bool issue,
    @Default(false) bool complete,
    @Default(false) bool cancel,
    @Default(false) bool receive,
  }) = _OrderCan;

  factory OrderCan.fromJson(Map<String, Object?> json) => _$OrderCanFromJson(json);
}

@freezed
abstract class HistoryEvent with _$HistoryEvent {
  const factory HistoryEvent({required String id, @Default('') String text, String? actorName, required String createdAt, String? comment}) =
      _HistoryEvent;

  factory HistoryEvent.fromJson(Map<String, Object?> json) => _$HistoryEventFromJson(json);
}

@freezed
abstract class ProductionOrderDetail with _$ProductionOrderDetail {
  const factory ProductionOrderDetail({
    required String id,
    required String number,
    @Default('') String recipeName,
    @Default('') String outputUnit,
    @Default('') String departmentName,
    @Default('') String warehouseName,
    @Default(0) double plannedQty,
    required String plannedDate,
    required String status,
    double? outputQty,
    String? responsibleName,
    String? comment,

    /// Версия — отправляется при закрытии (конфликт → 409, экран перечитывает).
    @Default(0) int version,
    @Default(5) double thresholdPercent,
    @Default(<OrderLine>[]) List<OrderLine> lines,
    @Default(<HistoryEvent>[]) List<HistoryEvent> history,
    @Default(OrderCan()) OrderCan can,
    @Default(0) int shiftsOpen,
    double? shiftsGoodOutput,
  }) = _ProductionOrderDetail;

  factory ProductionOrderDetail.fromJson(Map<String, Object?> json) => _$ProductionOrderDetailFromJson(json);
}

@freezed
abstract class ShiftRow with _$ShiftRow {
  const factory ShiftRow({
    required String id,
    required String number,
    @Default('') String orderNumber,
    @Default('') String recipeName,
    @Default('') String outputUnit,
    required String shiftDate,
    @Default(1) int shiftNo,
    @Default('') String operatorName,

    /// Draft, Closed, Approved, Superseded, Cancelled
    required String status,
    double? goodOutput,
    @Default(0) double defectKg,
  }) = _ShiftRow;

  factory ShiftRow.fromJson(Map<String, Object?> json) => _$ShiftRowFromJson(json);
}

// ───────────── Склад ─────────────

@freezed
abstract class StockRow with _$StockRow {
  const factory StockRow({
    required String itemId,
    required String sku,
    required String name,
    @Default('') String categoryName,
    @Default('') String unitCode,
    @Default(0) double onHand,
    @Default(0) double reserved,
    @Default(0) double available,
    @Default(0) double minQty,
    @Default(false) bool low,
    @Default(0) double expired,
    String? nearestExpiry,
    String? lastMovementAt,
    @Default(false) bool tracksBatches,
  }) = _StockRow;

  factory StockRow.fromJson(Map<String, Object?> json) => _$StockRowFromJson(json);
}

@freezed
abstract class StockSummary with _$StockSummary {
  const factory StockSummary({
    @Default(0) int items,
    @Default(0) int low,
    @Default(0) int empty,
    @Default(0) int withReserve,
    @Default(0) int toIssueRequests,
    @Default(0) int expired,
    @Default(0) int expiring,
  }) = _StockSummary;

  factory StockSummary.fromJson(Map<String, Object?> json) => _$StockSummaryFromJson(json);
}

@freezed
abstract class MovementRow with _$MovementRow {
  const factory MovementRow({
    required String id,
    required String createdAt,
    @Default('') String documentNumber,
    @Default('') String itemName,
    @Default('') String sku,
    @Default('') String unitCode,

    /// Receipt, Issue, WriteOff, Adjustment, TransferOut, TransferIn, Reversal, ProductionIssue, ProductionReturn, ProductionOutput…
    required String kind,
    @Default(0) double quantity,
    @Default(0) double balanceAfter,
    String? actorName,
    String? departmentName,
    String? comment,
    String? warehouseName,
    String? reversedBy,
  }) = _MovementRow;

  factory MovementRow.fromJson(Map<String, Object?> json) => _$MovementRowFromJson(json);
}

@freezed
abstract class Reservation with _$Reservation {
  const factory Reservation({required String requestNumber, @Default('') String requestTitle, @Default('') String departmentName, @Default(0) double quantity}) =
      _Reservation;

  factory Reservation.fromJson(Map<String, Object?> json) => _$ReservationFromJson(json);
}

@freezed
abstract class ItemCard with _$ItemCard {
  const factory ItemCard({
    required StockRow item,
    @Default(<MovementRow>[]) List<MovementRow> movements,
    @Default(<Reservation>[]) List<Reservation> reservations,
  }) = _ItemCard;

  factory ItemCard.fromJson(Map<String, Object?> json) => _$ItemCardFromJson(json);
}

@freezed
abstract class Warehouse with _$Warehouse {
  const factory Warehouse({
    required String id,
    required String name,
    @Default(true) bool isActive,
    @Default(false) bool isDefault,

    /// Пользователь может проводить операции на этом складе.
    @Default(false) bool posts,
  }) = _Warehouse;

  factory Warehouse.fromJson(Map<String, Object?> json) => _$WarehouseFromJson(json);
}

@freezed
abstract class Department with _$Department {
  const factory Department({required String id, @Default('') String code, required String name}) = _Department;

  factory Department.fromJson(Map<String, Object?> json) => _$DepartmentFromJson(json);
}

@freezed
abstract class Warehouses with _$Warehouses {
  const factory Warehouses({@Default(<Warehouse>[]) List<Warehouse> warehouses, @Default(<Department>[]) List<Department> departments}) = _Warehouses;

  factory Warehouses.fromJson(Map<String, Object?> json) => _$WarehousesFromJson(json);
}

@freezed
abstract class CatalogItem with _$CatalogItem {
  const factory CatalogItem({required String id, @Default('') String sku, required String name, @Default('') String unitCode, double? available}) =
      _CatalogItem;

  factory CatalogItem.fromJson(Map<String, Object?> json) => _$CatalogItemFromJson(json);
}

// ───────────── Заявки ─────────────

@freezed
abstract class RequestRow with _$RequestRow {
  const factory RequestRow({
    required String id,
    required String number,
    required String title,
    required String status,
    @Default('Normal') String priority,
    @Default('') String departmentName,
    @Default('') String initiatorName,
    @Default(0) int itemsCount,
    String? firstItem,
    @Default(0) double estimatedTotal,
    String? desiredDate,
    required String createdAt,
  }) = _RequestRow;

  factory RequestRow.fromJson(Map<String, Object?> json) => _$RequestRowFromJson(json);
}
