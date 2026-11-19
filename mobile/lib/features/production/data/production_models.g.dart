// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'production_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_NamedValue _$NamedValueFromJson(Map<String, dynamic> json) => _NamedValue(
  name: json['name'] as String,
  value: (json['value'] as num?)?.toDouble() ?? 0,
  extra: (json['extra'] as num?)?.toDouble(),
  unit: json['unit'] as String?,
);

Map<String, dynamic> _$NamedValueToJson(_NamedValue instance) =>
    <String, dynamic>{
      'name': instance.name,
      'value': instance.value,
      'extra': ?instance.extra,
      'unit': ?instance.unit,
    };

_ProdIssue _$ProdIssueFromJson(Map<String, dynamic> json) => _ProdIssue(
  kind: json['kind'] as String,
  title: json['title'] as String,
  detail: json['detail'] as String? ?? '',
  link: json['link'] as String?,
);

Map<String, dynamic> _$ProdIssueToJson(_ProdIssue instance) =>
    <String, dynamic>{
      'kind': instance.kind,
      'title': instance.title,
      'detail': instance.detail,
      'link': ?instance.link,
    };

_ProdDay _$ProdDayFromJson(Map<String, dynamic> json) => _ProdDay(
  date: json['date'] as String,
  goodKg: (json['goodKg'] as num?)?.toDouble() ?? 0,
  defectKg: (json['defectKg'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$ProdDayToJson(_ProdDay instance) => <String, dynamic>{
  'date': instance.date,
  'goodKg': instance.goodKg,
  'defectKg': instance.defectKg,
};

_ProductionDashboard _$ProductionDashboardFromJson(Map<String, dynamic> json) =>
    _ProductionDashboard(
      todayKg: (json['todayKg'] as num?)?.toDouble() ?? 0,
      goodKg: (json['goodKg'] as num?)?.toDouble() ?? 0,
      planKg: (json['planKg'] as num?)?.toDouble() ?? 0,
      completionPercent: (json['completionPercent'] as num?)?.toDouble(),
      defectKg: (json['defectKg'] as num?)?.toDouble() ?? 0,
      defectPercent: (json['defectPercent'] as num?)?.toDouble(),
      defectCost: (json['defectCost'] as num?)?.toDouble(),
      activeOrders: (json['activeOrders'] as num?)?.toInt() ?? 0,
      openShifts: (json['openShifts'] as num?)?.toInt() ?? 0,
      closedShifts: (json['closedShifts'] as num?)?.toInt() ?? 0,
      normKg: (json['normKg'] as num?)?.toDouble() ?? 0,
      factKg: (json['factKg'] as num?)?.toDouble() ?? 0,
      normDeviationPercent: (json['normDeviationPercent'] as num?)?.toDouble(),
      days:
          (json['days'] as List<dynamic>?)
              ?.map((e) => ProdDay.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <ProdDay>[],
      byProduct:
          (json['byProduct'] as List<dynamic>?)
              ?.map((e) => NamedValue.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <NamedValue>[],
      defectsByType:
          (json['defectsByType'] as List<dynamic>?)
              ?.map((e) => NamedValue.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <NamedValue>[],
      issues:
          (json['issues'] as List<dynamic>?)
              ?.map((e) => ProdIssue.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <ProdIssue>[],
    );

Map<String, dynamic> _$ProductionDashboardToJson(
  _ProductionDashboard instance,
) => <String, dynamic>{
  'todayKg': instance.todayKg,
  'goodKg': instance.goodKg,
  'planKg': instance.planKg,
  'completionPercent': ?instance.completionPercent,
  'defectKg': instance.defectKg,
  'defectPercent': ?instance.defectPercent,
  'defectCost': ?instance.defectCost,
  'activeOrders': instance.activeOrders,
  'openShifts': instance.openShifts,
  'closedShifts': instance.closedShifts,
  'normKg': instance.normKg,
  'factKg': instance.factKg,
  'normDeviationPercent': ?instance.normDeviationPercent,
  'days': instance.days.map((e) => e.toJson()).toList(),
  'byProduct': instance.byProduct.map((e) => e.toJson()).toList(),
  'defectsByType': instance.defectsByType.map((e) => e.toJson()).toList(),
  'issues': instance.issues.map((e) => e.toJson()).toList(),
};

_CountValue _$CountValueFromJson(Map<String, dynamic> json) => _CountValue(
  count: (json['count'] as num?)?.toInt() ?? 0,
  value: (json['value'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$CountValueToJson(_CountValue instance) =>
    <String, dynamic>{'count': instance.count, 'value': instance.value};

_LowDays _$LowDaysFromJson(Map<String, dynamic> json) => _LowDays(
  name: json['name'] as String,
  unitCode: json['unitCode'] as String? ?? '',
  onHand: (json['onHand'] as num?)?.toDouble() ?? 0,
  perDay: (json['perDay'] as num?)?.toDouble() ?? 0,
  days: (json['days'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$LowDaysToJson(_LowDays instance) => <String, dynamic>{
  'name': instance.name,
  'unitCode': instance.unitCode,
  'onHand': instance.onHand,
  'perDay': instance.perDay,
  'days': instance.days,
};

_WarehouseValue _$WarehouseValueFromJson(Map<String, dynamic> json) =>
    _WarehouseValue(
      name: json['name'] as String,
      items: (json['items'] as num?)?.toInt() ?? 0,
      value: (json['value'] as num?)?.toDouble() ?? 0,
    );

Map<String, dynamic> _$WarehouseValueToJson(_WarehouseValue instance) =>
    <String, dynamic>{
      'name': instance.name,
      'items': instance.items,
      'value': instance.value,
    };

_ProductionOverview _$ProductionOverviewFromJson(Map<String, dynamic> json) =>
    _ProductionOverview(
      stock:
          (json['stock'] as List<dynamic>?)
              ?.map((e) => WarehouseValue.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <WarehouseValue>[],
      stockValue: (json['stockValue'] as num?)?.toDouble() ?? 0,
      expiring7: json['expiring7'] == null
          ? const CountValue()
          : CountValue.fromJson(json['expiring7'] as Map<String, dynamic>),
      expiring30: json['expiring30'] == null
          ? const CountValue()
          : CountValue.fromJson(json['expiring30'] as Map<String, dynamic>),
      expired: json['expired'] == null
          ? const CountValue()
          : CountValue.fromJson(json['expired'] as Map<String, dynamic>),
      onControl: json['onControl'] == null
          ? const CountValue()
          : CountValue.fromJson(json['onControl'] as Map<String, dynamic>),
      belowMin: (json['belowMin'] as num?)?.toInt() ?? 0,
      openOrders: (json['openOrders'] as num?)?.toInt() ?? 0,
      overdueOrders: (json['overdueOrders'] as num?)?.toInt() ?? 0,
      lowDays:
          (json['lowDays'] as List<dynamic>?)
              ?.map((e) => LowDays.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <LowDays>[],
    );

Map<String, dynamic> _$ProductionOverviewToJson(_ProductionOverview instance) =>
    <String, dynamic>{
      'stock': instance.stock.map((e) => e.toJson()).toList(),
      'stockValue': instance.stockValue,
      'expiring7': instance.expiring7.toJson(),
      'expiring30': instance.expiring30.toJson(),
      'expired': instance.expired.toJson(),
      'onControl': instance.onControl.toJson(),
      'belowMin': instance.belowMin,
      'openOrders': instance.openOrders,
      'overdueOrders': instance.overdueOrders,
      'lowDays': instance.lowDays.map((e) => e.toJson()).toList(),
    };

_ProductionOrderRow _$ProductionOrderRowFromJson(Map<String, dynamic> json) =>
    _ProductionOrderRow(
      id: json['id'] as String,
      number: json['number'] as String,
      recipeName: json['recipeName'] as String? ?? '',
      outputUnit: json['outputUnit'] as String? ?? '',
      plannedQty: (json['plannedQty'] as num?)?.toDouble() ?? 0,
      outputQty: (json['outputQty'] as num?)?.toDouble(),
      plannedDate: json['plannedDate'] as String,
      status: json['status'] as String,
      departmentName: json['departmentName'] as String? ?? '',
      warehouseName: json['warehouseName'] as String? ?? '',
      responsibleName: json['responsibleName'] as String?,
      overconsumed: json['overconsumed'] as bool? ?? false,
    );

Map<String, dynamic> _$ProductionOrderRowToJson(_ProductionOrderRow instance) =>
    <String, dynamic>{
      'id': instance.id,
      'number': instance.number,
      'recipeName': instance.recipeName,
      'outputUnit': instance.outputUnit,
      'plannedQty': instance.plannedQty,
      'outputQty': ?instance.outputQty,
      'plannedDate': instance.plannedDate,
      'status': instance.status,
      'departmentName': instance.departmentName,
      'warehouseName': instance.warehouseName,
      'responsibleName': ?instance.responsibleName,
      'overconsumed': instance.overconsumed,
    };

_OrderLine _$OrderLineFromJson(Map<String, dynamic> json) => _OrderLine(
  name: json['name'] as String,
  sku: json['sku'] as String? ?? '',
  unitCode: json['unitCode'] as String? ?? '',
  required: (json['required'] as num?)?.toDouble() ?? 0,
  issued: (json['issued'] as num?)?.toDouble() ?? 0,
  returned: (json['returned'] as num?)?.toDouble() ?? 0,
  left: (json['left'] as num?)?.toDouble() ?? 0,
  available: (json['available'] as num?)?.toDouble() ?? 0,
  deviationPercent: (json['deviationPercent'] as num?)?.toDouble(),
  overconsumed: json['overconsumed'] as bool? ?? false,
);

Map<String, dynamic> _$OrderLineToJson(_OrderLine instance) =>
    <String, dynamic>{
      'name': instance.name,
      'sku': instance.sku,
      'unitCode': instance.unitCode,
      'required': instance.required,
      'issued': instance.issued,
      'returned': instance.returned,
      'left': instance.left,
      'available': instance.available,
      'deviationPercent': ?instance.deviationPercent,
      'overconsumed': instance.overconsumed,
    };

_OrderCan _$OrderCanFromJson(Map<String, dynamic> json) => _OrderCan(
  release: json['release'] as bool? ?? false,
  issue: json['issue'] as bool? ?? false,
  complete: json['complete'] as bool? ?? false,
  cancel: json['cancel'] as bool? ?? false,
  receive: json['receive'] as bool? ?? false,
);

Map<String, dynamic> _$OrderCanToJson(_OrderCan instance) => <String, dynamic>{
  'release': instance.release,
  'issue': instance.issue,
  'complete': instance.complete,
  'cancel': instance.cancel,
  'receive': instance.receive,
};

_HistoryEvent _$HistoryEventFromJson(Map<String, dynamic> json) =>
    _HistoryEvent(
      id: json['id'] as String,
      text: json['text'] as String? ?? '',
      actorName: json['actorName'] as String?,
      createdAt: json['createdAt'] as String,
      comment: json['comment'] as String?,
    );

Map<String, dynamic> _$HistoryEventToJson(_HistoryEvent instance) =>
    <String, dynamic>{
      'id': instance.id,
      'text': instance.text,
      'actorName': ?instance.actorName,
      'createdAt': instance.createdAt,
      'comment': ?instance.comment,
    };

_ProductionOrderDetail _$ProductionOrderDetailFromJson(
  Map<String, dynamic> json,
) => _ProductionOrderDetail(
  id: json['id'] as String,
  number: json['number'] as String,
  recipeName: json['recipeName'] as String? ?? '',
  outputUnit: json['outputUnit'] as String? ?? '',
  departmentName: json['departmentName'] as String? ?? '',
  warehouseName: json['warehouseName'] as String? ?? '',
  plannedQty: (json['plannedQty'] as num?)?.toDouble() ?? 0,
  plannedDate: json['plannedDate'] as String,
  status: json['status'] as String,
  outputQty: (json['outputQty'] as num?)?.toDouble(),
  responsibleName: json['responsibleName'] as String?,
  comment: json['comment'] as String?,
  version: (json['version'] as num?)?.toInt() ?? 0,
  thresholdPercent: (json['thresholdPercent'] as num?)?.toDouble() ?? 5,
  lines:
      (json['lines'] as List<dynamic>?)
          ?.map((e) => OrderLine.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <OrderLine>[],
  history:
      (json['history'] as List<dynamic>?)
          ?.map((e) => HistoryEvent.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <HistoryEvent>[],
  can: json['can'] == null
      ? const OrderCan()
      : OrderCan.fromJson(json['can'] as Map<String, dynamic>),
  shiftsOpen: (json['shiftsOpen'] as num?)?.toInt() ?? 0,
  shiftsGoodOutput: (json['shiftsGoodOutput'] as num?)?.toDouble(),
);

Map<String, dynamic> _$ProductionOrderDetailToJson(
  _ProductionOrderDetail instance,
) => <String, dynamic>{
  'id': instance.id,
  'number': instance.number,
  'recipeName': instance.recipeName,
  'outputUnit': instance.outputUnit,
  'departmentName': instance.departmentName,
  'warehouseName': instance.warehouseName,
  'plannedQty': instance.plannedQty,
  'plannedDate': instance.plannedDate,
  'status': instance.status,
  'outputQty': ?instance.outputQty,
  'responsibleName': ?instance.responsibleName,
  'comment': ?instance.comment,
  'version': instance.version,
  'thresholdPercent': instance.thresholdPercent,
  'lines': instance.lines.map((e) => e.toJson()).toList(),
  'history': instance.history.map((e) => e.toJson()).toList(),
  'can': instance.can.toJson(),
  'shiftsOpen': instance.shiftsOpen,
  'shiftsGoodOutput': ?instance.shiftsGoodOutput,
};

_ShiftRow _$ShiftRowFromJson(Map<String, dynamic> json) => _ShiftRow(
  id: json['id'] as String,
  number: json['number'] as String,
  orderNumber: json['orderNumber'] as String? ?? '',
  recipeName: json['recipeName'] as String? ?? '',
  outputUnit: json['outputUnit'] as String? ?? '',
  shiftDate: json['shiftDate'] as String,
  shiftNo: (json['shiftNo'] as num?)?.toInt() ?? 1,
  operatorName: json['operatorName'] as String? ?? '',
  status: json['status'] as String,
  goodOutput: (json['goodOutput'] as num?)?.toDouble(),
  defectKg: (json['defectKg'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$ShiftRowToJson(_ShiftRow instance) => <String, dynamic>{
  'id': instance.id,
  'number': instance.number,
  'orderNumber': instance.orderNumber,
  'recipeName': instance.recipeName,
  'outputUnit': instance.outputUnit,
  'shiftDate': instance.shiftDate,
  'shiftNo': instance.shiftNo,
  'operatorName': instance.operatorName,
  'status': instance.status,
  'goodOutput': ?instance.goodOutput,
  'defectKg': instance.defectKg,
};

_StockRow _$StockRowFromJson(Map<String, dynamic> json) => _StockRow(
  itemId: json['itemId'] as String,
  sku: json['sku'] as String,
  name: json['name'] as String,
  categoryName: json['categoryName'] as String? ?? '',
  unitCode: json['unitCode'] as String? ?? '',
  onHand: (json['onHand'] as num?)?.toDouble() ?? 0,
  reserved: (json['reserved'] as num?)?.toDouble() ?? 0,
  available: (json['available'] as num?)?.toDouble() ?? 0,
  minQty: (json['minQty'] as num?)?.toDouble() ?? 0,
  low: json['low'] as bool? ?? false,
  expired: (json['expired'] as num?)?.toDouble() ?? 0,
  nearestExpiry: json['nearestExpiry'] as String?,
  lastMovementAt: json['lastMovementAt'] as String?,
  tracksBatches: json['tracksBatches'] as bool? ?? false,
);

Map<String, dynamic> _$StockRowToJson(_StockRow instance) => <String, dynamic>{
  'itemId': instance.itemId,
  'sku': instance.sku,
  'name': instance.name,
  'categoryName': instance.categoryName,
  'unitCode': instance.unitCode,
  'onHand': instance.onHand,
  'reserved': instance.reserved,
  'available': instance.available,
  'minQty': instance.minQty,
  'low': instance.low,
  'expired': instance.expired,
  'nearestExpiry': ?instance.nearestExpiry,
  'lastMovementAt': ?instance.lastMovementAt,
  'tracksBatches': instance.tracksBatches,
};

_StockSummary _$StockSummaryFromJson(Map<String, dynamic> json) =>
    _StockSummary(
      items: (json['items'] as num?)?.toInt() ?? 0,
      low: (json['low'] as num?)?.toInt() ?? 0,
      empty: (json['empty'] as num?)?.toInt() ?? 0,
      withReserve: (json['withReserve'] as num?)?.toInt() ?? 0,
      toIssueRequests: (json['toIssueRequests'] as num?)?.toInt() ?? 0,
      expired: (json['expired'] as num?)?.toInt() ?? 0,
      expiring: (json['expiring'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$StockSummaryToJson(_StockSummary instance) =>
    <String, dynamic>{
      'items': instance.items,
      'low': instance.low,
      'empty': instance.empty,
      'withReserve': instance.withReserve,
      'toIssueRequests': instance.toIssueRequests,
      'expired': instance.expired,
      'expiring': instance.expiring,
    };

_MovementRow _$MovementRowFromJson(Map<String, dynamic> json) => _MovementRow(
  id: json['id'] as String,
  createdAt: json['createdAt'] as String,
  documentNumber: json['documentNumber'] as String? ?? '',
  itemName: json['itemName'] as String? ?? '',
  sku: json['sku'] as String? ?? '',
  unitCode: json['unitCode'] as String? ?? '',
  kind: json['kind'] as String,
  quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
  balanceAfter: (json['balanceAfter'] as num?)?.toDouble() ?? 0,
  actorName: json['actorName'] as String?,
  departmentName: json['departmentName'] as String?,
  comment: json['comment'] as String?,
  warehouseName: json['warehouseName'] as String?,
  reversedBy: json['reversedBy'] as String?,
);

Map<String, dynamic> _$MovementRowToJson(_MovementRow instance) =>
    <String, dynamic>{
      'id': instance.id,
      'createdAt': instance.createdAt,
      'documentNumber': instance.documentNumber,
      'itemName': instance.itemName,
      'sku': instance.sku,
      'unitCode': instance.unitCode,
      'kind': instance.kind,
      'quantity': instance.quantity,
      'balanceAfter': instance.balanceAfter,
      'actorName': ?instance.actorName,
      'departmentName': ?instance.departmentName,
      'comment': ?instance.comment,
      'warehouseName': ?instance.warehouseName,
      'reversedBy': ?instance.reversedBy,
    };

_Reservation _$ReservationFromJson(Map<String, dynamic> json) => _Reservation(
  requestNumber: json['requestNumber'] as String,
  requestTitle: json['requestTitle'] as String? ?? '',
  departmentName: json['departmentName'] as String? ?? '',
  quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
);

Map<String, dynamic> _$ReservationToJson(_Reservation instance) =>
    <String, dynamic>{
      'requestNumber': instance.requestNumber,
      'requestTitle': instance.requestTitle,
      'departmentName': instance.departmentName,
      'quantity': instance.quantity,
    };

_ItemCard _$ItemCardFromJson(Map<String, dynamic> json) => _ItemCard(
  item: StockRow.fromJson(json['item'] as Map<String, dynamic>),
  movements:
      (json['movements'] as List<dynamic>?)
          ?.map((e) => MovementRow.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <MovementRow>[],
  reservations:
      (json['reservations'] as List<dynamic>?)
          ?.map((e) => Reservation.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <Reservation>[],
);

Map<String, dynamic> _$ItemCardToJson(_ItemCard instance) => <String, dynamic>{
  'item': instance.item.toJson(),
  'movements': instance.movements.map((e) => e.toJson()).toList(),
  'reservations': instance.reservations.map((e) => e.toJson()).toList(),
};

_Warehouse _$WarehouseFromJson(Map<String, dynamic> json) => _Warehouse(
  id: json['id'] as String,
  name: json['name'] as String,
  isActive: json['isActive'] as bool? ?? true,
  isDefault: json['isDefault'] as bool? ?? false,
  posts: json['posts'] as bool? ?? false,
);

Map<String, dynamic> _$WarehouseToJson(_Warehouse instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'isActive': instance.isActive,
      'isDefault': instance.isDefault,
      'posts': instance.posts,
    };

_Department _$DepartmentFromJson(Map<String, dynamic> json) => _Department(
  id: json['id'] as String,
  code: json['code'] as String? ?? '',
  name: json['name'] as String,
);

Map<String, dynamic> _$DepartmentToJson(_Department instance) =>
    <String, dynamic>{
      'id': instance.id,
      'code': instance.code,
      'name': instance.name,
    };

_Warehouses _$WarehousesFromJson(Map<String, dynamic> json) => _Warehouses(
  warehouses:
      (json['warehouses'] as List<dynamic>?)
          ?.map((e) => Warehouse.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <Warehouse>[],
  departments:
      (json['departments'] as List<dynamic>?)
          ?.map((e) => Department.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <Department>[],
);

Map<String, dynamic> _$WarehousesToJson(_Warehouses instance) =>
    <String, dynamic>{
      'warehouses': instance.warehouses.map((e) => e.toJson()).toList(),
      'departments': instance.departments.map((e) => e.toJson()).toList(),
    };

_CatalogItem _$CatalogItemFromJson(Map<String, dynamic> json) => _CatalogItem(
  id: json['id'] as String,
  sku: json['sku'] as String? ?? '',
  name: json['name'] as String,
  unitCode: json['unitCode'] as String? ?? '',
  available: (json['available'] as num?)?.toDouble(),
);

Map<String, dynamic> _$CatalogItemToJson(_CatalogItem instance) =>
    <String, dynamic>{
      'id': instance.id,
      'sku': instance.sku,
      'name': instance.name,
      'unitCode': instance.unitCode,
      'available': ?instance.available,
    };

_RequestRow _$RequestRowFromJson(Map<String, dynamic> json) => _RequestRow(
  id: json['id'] as String,
  number: json['number'] as String,
  title: json['title'] as String,
  status: json['status'] as String,
  priority: json['priority'] as String? ?? 'Normal',
  departmentName: json['departmentName'] as String? ?? '',
  initiatorName: json['initiatorName'] as String? ?? '',
  itemsCount: (json['itemsCount'] as num?)?.toInt() ?? 0,
  firstItem: json['firstItem'] as String?,
  estimatedTotal: (json['estimatedTotal'] as num?)?.toDouble() ?? 0,
  desiredDate: json['desiredDate'] as String?,
  createdAt: json['createdAt'] as String,
);

Map<String, dynamic> _$RequestRowToJson(_RequestRow instance) =>
    <String, dynamic>{
      'id': instance.id,
      'number': instance.number,
      'title': instance.title,
      'status': instance.status,
      'priority': instance.priority,
      'departmentName': instance.departmentName,
      'initiatorName': instance.initiatorName,
      'itemsCount': instance.itemsCount,
      'firstItem': ?instance.firstItem,
      'estimatedTotal': instance.estimatedTotal,
      'desiredDate': ?instance.desiredDate,
      'createdAt': instance.createdAt,
    };
