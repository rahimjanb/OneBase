import 'package:freezed_annotation/freezed_annotation.dart';

import '../../dashboard/data/dashboard_models.dart';

part 'finance_models.freezed.dart';
part 'finance_models.g.dart';

// Финансы по факту 1С (`/api/finance/*`, право `finance.read`, только чтение — платежей в OneBase нет). Суммы — сум из 1С, телефон их не
// пересчитывает. Нет данных 1С — `meta.hasData = false` и пояснение сервера `meta.note` (показывается вместо нулей).

@freezed
abstract class AmountRow with _$AmountRow {
  const factory AmountRow({
    required String key,
    required String title,
    @Default(0) double amount,
    double? previous,
    double? quantity,
    String? currency,
    double? currencyAmount,
  }) = _AmountRow;

  factory AmountRow.fromJson(Map<String, Object?> json) => _$AmountRowFromJson(json);
}

@freezed
abstract class FinOverview with _$FinOverview {
  const factory FinOverview({
    required FinanceMeta meta,
    @Default(<PnlValue>[]) List<PnlValue> pnl,
    @Default(<MonthPoint>[]) List<MonthPoint> months,
    @Default(0) double cashTotal,
    @Default(<AmountRow>[]) List<AmountRow> cash,
    @Default(0) double receivables,
    @Default(0) double payables,
    @Default(0) double advancesGiven,
    @Default(0) double advancesReceived,
    @Default(<AmountRow>[]) List<AmountRow> inventory,
    String? balancesMonth,
    @Default(<AmountRow>[]) List<AmountRow> expenseGrowth,
  }) = _FinOverview;

  const FinOverview._();

  factory FinOverview.fromJson(Map<String, Object?> json) => _$FinOverviewFromJson(json);

  PnlValue? line(String code) => pnl.where((p) => p.code == code).firstOrNull;
}

@freezed
abstract class PnlLine with _$PnlLine {
  const factory PnlLine({
    required String code,
    required String title,

    /// income | expense | total
    required String kind,
    @Default(<double>[]) List<double> months,
    @Default(0) double total,
    double? previous,
  }) = _PnlLine;

  factory PnlLine.fromJson(Map<String, Object?> json) => _$PnlLineFromJson(json);
}

@freezed
abstract class FinPnl with _$FinPnl {
  const factory FinPnl({
    required FinanceMeta meta,
    @Default(<String>[]) List<String> months,
    @Default(<PnlLine>[]) List<PnlLine> lines,
    @Default(<AmountRow>[]) List<AmountRow> expensesByAccount,
    @Default(<AmountRow>[]) List<AmountRow> expensesByDepartment,
  }) = _FinPnl;

  factory FinPnl.fromJson(Map<String, Object?> json) => _$FinPnlFromJson(json);
}

@freezed
abstract class CashFlow with _$CashFlow {
  const factory CashFlow({required String code, required String title, @Default(false) bool inflow, @Default(0) double total}) = _CashFlow;

  factory CashFlow.fromJson(Map<String, Object?> json) => _$CashFlowFromJson(json);
}

@freezed
abstract class FinCash with _$FinCash {
  const factory FinCash({
    required FinanceMeta meta,
    @Default(<CashFlow>[]) List<CashFlow> flows,
    @Default(0) double inflow,
    @Default(0) double outflow,
    @Default(<AmountRow>[]) List<AmountRow> balances,
    String? balancesMonth,
  }) = _FinCash;

  factory FinCash.fromJson(Map<String, Object?> json) => _$FinCashFromJson(json);
}

@freezed
abstract class SettlementRow with _$SettlementRow {
  const factory SettlementRow({
    required String counterpartyId,
    required String name,
    String? inn,
    @Default(0) double balance,
    @Default(0) double overdue,
    int? maxOverdueDays,
  }) = _SettlementRow;

  factory SettlementRow.fromJson(Map<String, Object?> json) => _$SettlementRowFromJson(json);
}

@freezed
abstract class FinSettlements with _$FinSettlements {
  const factory FinSettlements({
    required FinanceMeta meta,
    @Default('receivables') String kind,
    @Default(0) double total,
    @Default(0) double overdue,

    /// not_due, 1_30, 31_60, 61_90, over_90, no_term
    @Default(<String, double>{}) Map<String, double> buckets,
    @Default(0) double advances,
    @Default(<SettlementRow>[]) List<SettlementRow> rows,
    @Default('') String method,
  }) = _FinSettlements;

  factory FinSettlements.fromJson(Map<String, Object?> json) => _$FinSettlementsFromJson(json);
}

@freezed
abstract class Anomaly with _$Anomaly {
  const factory Anomaly({
    required String kind,
    required String title,
    @Default('') String detail,
    double? amount,
    String? date,

    /// Путь веба: /finance/accounts/{code}, /finance/entries?…, /finance/documents/{id}.
    String? link,

    /// 3 — критично, 2 — внимание, 1 — к сведению.
    @Default(1) int severity,
  }) = _Anomaly;

  factory Anomaly.fromJson(Map<String, Object?> json) => _$AnomalyFromJson(json);
}

@freezed
abstract class FinAnomalies with _$FinAnomalies {
  const factory FinAnomalies({required FinanceMeta meta, @Default(<Anomaly>[]) List<Anomaly> items, @Default(<String>[]) List<String> checks}) =
      _FinAnomalies;

  factory FinAnomalies.fromJson(Map<String, Object?> json) => _$FinAnomaliesFromJson(json);
}

@freezed
abstract class EntryView with _$EntryView {
  const factory EntryView({
    required String id,
    required String date,
    required String debitAccount,
    String? debitName,
    required String creditAccount,
    String? creditName,
    @Default(0) double amount,
    String? content,
    String? counterparty,
    String? documentId,
    @Default('') String registrarType,
    String? registrarNumber,
  }) = _EntryView;

  factory EntryView.fromJson(Map<String, Object?> json) => _$EntryViewFromJson(json);
}

@freezed
abstract class DocLine with _$DocLine {
  const factory DocLine({
    required int line,
    String? item,
    @Default(0) double quantity,
    String? unit,
    double? price,
    @Default(0) double amount,
  }) = _DocLine;

  factory DocLine.fromJson(Map<String, Object?> json) => _$DocLineFromJson(json);
}

@freezed
abstract class FinDocument with _$FinDocument {
  const factory FinDocument({
    required String id,
    required String type,
    @Default('') String sourceType,
    @Default('') String number,
    required String date,
    @Default(false) bool posted,
    String? organization,
    String? counterparty,
    String? contract,
    String? warehouse,
    @Default('UZS') String currency,
    @Default(0) double amount,
    double? vatAmount,
    String? purpose,
    String? comment,
    @Default(<DocLine>[]) List<DocLine> lines,
    @Default(<EntryView>[]) List<EntryView> entries,
  }) = _FinDocument;

  factory FinDocument.fromJson(Map<String, Object?> json) => _$FinDocumentFromJson(json);
}

@freezed
abstract class TrialRow with _$TrialRow {
  const factory TrialRow({
    required String account,
    required String name,
    @Default(0) double openingDebit,
    @Default(0) double openingCredit,
    @Default(0) double turnoverDebit,
    @Default(0) double turnoverCredit,
    @Default(0) double closingDebit,
    @Default(0) double closingCredit,
  }) = _TrialRow;

  factory TrialRow.fromJson(Map<String, Object?> json) => _$TrialRowFromJson(json);
}

@freezed
abstract class FinAccounts with _$FinAccounts {
  const factory FinAccounts({required FinanceMeta meta, @Default(<TrialRow>[]) List<TrialRow> rows, TrialRow? total}) = _FinAccounts;

  factory FinAccounts.fromJson(Map<String, Object?> json) => _$FinAccountsFromJson(json);
}
