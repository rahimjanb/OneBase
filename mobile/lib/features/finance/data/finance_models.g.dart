// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'finance_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_AmountRow _$AmountRowFromJson(Map<String, dynamic> json) => _AmountRow(
  key: json['key'] as String,
  title: json['title'] as String,
  amount: (json['amount'] as num?)?.toDouble() ?? 0,
  previous: (json['previous'] as num?)?.toDouble(),
  quantity: (json['quantity'] as num?)?.toDouble(),
  currency: json['currency'] as String?,
  currencyAmount: (json['currencyAmount'] as num?)?.toDouble(),
);

Map<String, dynamic> _$AmountRowToJson(_AmountRow instance) =>
    <String, dynamic>{
      'key': instance.key,
      'title': instance.title,
      'amount': instance.amount,
      'previous': ?instance.previous,
      'quantity': ?instance.quantity,
      'currency': ?instance.currency,
      'currencyAmount': ?instance.currencyAmount,
    };

_FinOverview _$FinOverviewFromJson(Map<String, dynamic> json) => _FinOverview(
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
  cash:
      (json['cash'] as List<dynamic>?)
          ?.map((e) => AmountRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AmountRow>[],
  receivables: (json['receivables'] as num?)?.toDouble() ?? 0,
  payables: (json['payables'] as num?)?.toDouble() ?? 0,
  advancesGiven: (json['advancesGiven'] as num?)?.toDouble() ?? 0,
  advancesReceived: (json['advancesReceived'] as num?)?.toDouble() ?? 0,
  inventory:
      (json['inventory'] as List<dynamic>?)
          ?.map((e) => AmountRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AmountRow>[],
  balancesMonth: json['balancesMonth'] as String?,
  expenseGrowth:
      (json['expenseGrowth'] as List<dynamic>?)
          ?.map((e) => AmountRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AmountRow>[],
);

Map<String, dynamic> _$FinOverviewToJson(_FinOverview instance) =>
    <String, dynamic>{
      'meta': instance.meta.toJson(),
      'pnl': instance.pnl.map((e) => e.toJson()).toList(),
      'months': instance.months.map((e) => e.toJson()).toList(),
      'cashTotal': instance.cashTotal,
      'cash': instance.cash.map((e) => e.toJson()).toList(),
      'receivables': instance.receivables,
      'payables': instance.payables,
      'advancesGiven': instance.advancesGiven,
      'advancesReceived': instance.advancesReceived,
      'inventory': instance.inventory.map((e) => e.toJson()).toList(),
      'balancesMonth': ?instance.balancesMonth,
      'expenseGrowth': instance.expenseGrowth.map((e) => e.toJson()).toList(),
    };

_PnlLine _$PnlLineFromJson(Map<String, dynamic> json) => _PnlLine(
  code: json['code'] as String,
  title: json['title'] as String,
  kind: json['kind'] as String,
  months:
      (json['months'] as List<dynamic>?)
          ?.map((e) => (e as num).toDouble())
          .toList() ??
      const <double>[],
  total: (json['total'] as num?)?.toDouble() ?? 0,
  previous: (json['previous'] as num?)?.toDouble(),
);

Map<String, dynamic> _$PnlLineToJson(_PnlLine instance) => <String, dynamic>{
  'code': instance.code,
  'title': instance.title,
  'kind': instance.kind,
  'months': instance.months,
  'total': instance.total,
  'previous': ?instance.previous,
};

_FinPnl _$FinPnlFromJson(Map<String, dynamic> json) => _FinPnl(
  meta: FinanceMeta.fromJson(json['meta'] as Map<String, dynamic>),
  months:
      (json['months'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const <String>[],
  lines:
      (json['lines'] as List<dynamic>?)
          ?.map((e) => PnlLine.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <PnlLine>[],
  expensesByAccount:
      (json['expensesByAccount'] as List<dynamic>?)
          ?.map((e) => AmountRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AmountRow>[],
  expensesByDepartment:
      (json['expensesByDepartment'] as List<dynamic>?)
          ?.map((e) => AmountRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AmountRow>[],
);

Map<String, dynamic> _$FinPnlToJson(_FinPnl instance) => <String, dynamic>{
  'meta': instance.meta.toJson(),
  'months': instance.months,
  'lines': instance.lines.map((e) => e.toJson()).toList(),
  'expensesByAccount': instance.expensesByAccount
      .map((e) => e.toJson())
      .toList(),
  'expensesByDepartment': instance.expensesByDepartment
      .map((e) => e.toJson())
      .toList(),
};

_CashFlow _$CashFlowFromJson(Map<String, dynamic> json) => _CashFlow(
  code: json['code'] as String,
  title: json['title'] as String,
  inflow: json['inflow'] as bool? ?? false,
  total: (json['total'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$CashFlowToJson(_CashFlow instance) => <String, dynamic>{
  'code': instance.code,
  'title': instance.title,
  'inflow': instance.inflow,
  'total': instance.total,
};

_FinCash _$FinCashFromJson(Map<String, dynamic> json) => _FinCash(
  meta: FinanceMeta.fromJson(json['meta'] as Map<String, dynamic>),
  flows:
      (json['flows'] as List<dynamic>?)
          ?.map((e) => CashFlow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <CashFlow>[],
  inflow: (json['inflow'] as num?)?.toDouble() ?? 0,
  outflow: (json['outflow'] as num?)?.toDouble() ?? 0,
  balances:
      (json['balances'] as List<dynamic>?)
          ?.map((e) => AmountRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <AmountRow>[],
  balancesMonth: json['balancesMonth'] as String?,
);

Map<String, dynamic> _$FinCashToJson(_FinCash instance) => <String, dynamic>{
  'meta': instance.meta.toJson(),
  'flows': instance.flows.map((e) => e.toJson()).toList(),
  'inflow': instance.inflow,
  'outflow': instance.outflow,
  'balances': instance.balances.map((e) => e.toJson()).toList(),
  'balancesMonth': ?instance.balancesMonth,
};

_SettlementRow _$SettlementRowFromJson(Map<String, dynamic> json) =>
    _SettlementRow(
      counterpartyId: json['counterpartyId'] as String,
      name: json['name'] as String,
      inn: json['inn'] as String?,
      balance: (json['balance'] as num?)?.toDouble() ?? 0,
      overdue: (json['overdue'] as num?)?.toDouble() ?? 0,
      maxOverdueDays: (json['maxOverdueDays'] as num?)?.toInt(),
    );

Map<String, dynamic> _$SettlementRowToJson(_SettlementRow instance) =>
    <String, dynamic>{
      'counterpartyId': instance.counterpartyId,
      'name': instance.name,
      'inn': ?instance.inn,
      'balance': instance.balance,
      'overdue': instance.overdue,
      'maxOverdueDays': ?instance.maxOverdueDays,
    };

_FinSettlements _$FinSettlementsFromJson(Map<String, dynamic> json) =>
    _FinSettlements(
      meta: FinanceMeta.fromJson(json['meta'] as Map<String, dynamic>),
      kind: json['kind'] as String? ?? 'receivables',
      total: (json['total'] as num?)?.toDouble() ?? 0,
      overdue: (json['overdue'] as num?)?.toDouble() ?? 0,
      buckets:
          (json['buckets'] as Map<String, dynamic>?)?.map(
            (k, e) => MapEntry(k, (e as num).toDouble()),
          ) ??
          const <String, double>{},
      advances: (json['advances'] as num?)?.toDouble() ?? 0,
      rows:
          (json['rows'] as List<dynamic>?)
              ?.map((e) => SettlementRow.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <SettlementRow>[],
      method: json['method'] as String? ?? '',
    );

Map<String, dynamic> _$FinSettlementsToJson(_FinSettlements instance) =>
    <String, dynamic>{
      'meta': instance.meta.toJson(),
      'kind': instance.kind,
      'total': instance.total,
      'overdue': instance.overdue,
      'buckets': instance.buckets,
      'advances': instance.advances,
      'rows': instance.rows.map((e) => e.toJson()).toList(),
      'method': instance.method,
    };

_Anomaly _$AnomalyFromJson(Map<String, dynamic> json) => _Anomaly(
  kind: json['kind'] as String,
  title: json['title'] as String,
  detail: json['detail'] as String? ?? '',
  amount: (json['amount'] as num?)?.toDouble(),
  date: json['date'] as String?,
  link: json['link'] as String?,
  severity: (json['severity'] as num?)?.toInt() ?? 1,
);

Map<String, dynamic> _$AnomalyToJson(_Anomaly instance) => <String, dynamic>{
  'kind': instance.kind,
  'title': instance.title,
  'detail': instance.detail,
  'amount': ?instance.amount,
  'date': ?instance.date,
  'link': ?instance.link,
  'severity': instance.severity,
};

_FinAnomalies _$FinAnomaliesFromJson(Map<String, dynamic> json) =>
    _FinAnomalies(
      meta: FinanceMeta.fromJson(json['meta'] as Map<String, dynamic>),
      items:
          (json['items'] as List<dynamic>?)
              ?.map((e) => Anomaly.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <Anomaly>[],
      checks:
          (json['checks'] as List<dynamic>?)
              ?.map((e) => e as String)
              .toList() ??
          const <String>[],
    );

Map<String, dynamic> _$FinAnomaliesToJson(_FinAnomalies instance) =>
    <String, dynamic>{
      'meta': instance.meta.toJson(),
      'items': instance.items.map((e) => e.toJson()).toList(),
      'checks': instance.checks,
    };

_EntryView _$EntryViewFromJson(Map<String, dynamic> json) => _EntryView(
  id: json['id'] as String,
  date: json['date'] as String,
  debitAccount: json['debitAccount'] as String,
  debitName: json['debitName'] as String?,
  creditAccount: json['creditAccount'] as String,
  creditName: json['creditName'] as String?,
  amount: (json['amount'] as num?)?.toDouble() ?? 0,
  content: json['content'] as String?,
  counterparty: json['counterparty'] as String?,
  documentId: json['documentId'] as String?,
  registrarType: json['registrarType'] as String? ?? '',
  registrarNumber: json['registrarNumber'] as String?,
);

Map<String, dynamic> _$EntryViewToJson(_EntryView instance) =>
    <String, dynamic>{
      'id': instance.id,
      'date': instance.date,
      'debitAccount': instance.debitAccount,
      'debitName': ?instance.debitName,
      'creditAccount': instance.creditAccount,
      'creditName': ?instance.creditName,
      'amount': instance.amount,
      'content': ?instance.content,
      'counterparty': ?instance.counterparty,
      'documentId': ?instance.documentId,
      'registrarType': instance.registrarType,
      'registrarNumber': ?instance.registrarNumber,
    };

_DocLine _$DocLineFromJson(Map<String, dynamic> json) => _DocLine(
  line: (json['line'] as num).toInt(),
  item: json['item'] as String?,
  quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
  unit: json['unit'] as String?,
  price: (json['price'] as num?)?.toDouble(),
  amount: (json['amount'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$DocLineToJson(_DocLine instance) => <String, dynamic>{
  'line': instance.line,
  'item': ?instance.item,
  'quantity': instance.quantity,
  'unit': ?instance.unit,
  'price': ?instance.price,
  'amount': instance.amount,
};

_FinDocument _$FinDocumentFromJson(Map<String, dynamic> json) => _FinDocument(
  id: json['id'] as String,
  type: json['type'] as String,
  sourceType: json['sourceType'] as String? ?? '',
  number: json['number'] as String? ?? '',
  date: json['date'] as String,
  posted: json['posted'] as bool? ?? false,
  organization: json['organization'] as String?,
  counterparty: json['counterparty'] as String?,
  contract: json['contract'] as String?,
  warehouse: json['warehouse'] as String?,
  currency: json['currency'] as String? ?? 'UZS',
  amount: (json['amount'] as num?)?.toDouble() ?? 0,
  vatAmount: (json['vatAmount'] as num?)?.toDouble(),
  purpose: json['purpose'] as String?,
  comment: json['comment'] as String?,
  lines:
      (json['lines'] as List<dynamic>?)
          ?.map((e) => DocLine.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <DocLine>[],
  entries:
      (json['entries'] as List<dynamic>?)
          ?.map((e) => EntryView.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <EntryView>[],
);

Map<String, dynamic> _$FinDocumentToJson(_FinDocument instance) =>
    <String, dynamic>{
      'id': instance.id,
      'type': instance.type,
      'sourceType': instance.sourceType,
      'number': instance.number,
      'date': instance.date,
      'posted': instance.posted,
      'organization': ?instance.organization,
      'counterparty': ?instance.counterparty,
      'contract': ?instance.contract,
      'warehouse': ?instance.warehouse,
      'currency': instance.currency,
      'amount': instance.amount,
      'vatAmount': ?instance.vatAmount,
      'purpose': ?instance.purpose,
      'comment': ?instance.comment,
      'lines': instance.lines.map((e) => e.toJson()).toList(),
      'entries': instance.entries.map((e) => e.toJson()).toList(),
    };

_TrialRow _$TrialRowFromJson(Map<String, dynamic> json) => _TrialRow(
  account: json['account'] as String,
  name: json['name'] as String,
  openingDebit: (json['openingDebit'] as num?)?.toDouble() ?? 0,
  openingCredit: (json['openingCredit'] as num?)?.toDouble() ?? 0,
  turnoverDebit: (json['turnoverDebit'] as num?)?.toDouble() ?? 0,
  turnoverCredit: (json['turnoverCredit'] as num?)?.toDouble() ?? 0,
  closingDebit: (json['closingDebit'] as num?)?.toDouble() ?? 0,
  closingCredit: (json['closingCredit'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$TrialRowToJson(_TrialRow instance) => <String, dynamic>{
  'account': instance.account,
  'name': instance.name,
  'openingDebit': instance.openingDebit,
  'openingCredit': instance.openingCredit,
  'turnoverDebit': instance.turnoverDebit,
  'turnoverCredit': instance.turnoverCredit,
  'closingDebit': instance.closingDebit,
  'closingCredit': instance.closingCredit,
};

_FinAccounts _$FinAccountsFromJson(Map<String, dynamic> json) => _FinAccounts(
  meta: FinanceMeta.fromJson(json['meta'] as Map<String, dynamic>),
  rows:
      (json['rows'] as List<dynamic>?)
          ?.map((e) => TrialRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <TrialRow>[],
  total: json['total'] == null
      ? null
      : TrialRow.fromJson(json['total'] as Map<String, dynamic>),
);

Map<String, dynamic> _$FinAccountsToJson(_FinAccounts instance) =>
    <String, dynamic>{
      'meta': instance.meta.toJson(),
      'rows': instance.rows.map((e) => e.toJson()).toList(),
      'total': ?instance.total?.toJson(),
    };
