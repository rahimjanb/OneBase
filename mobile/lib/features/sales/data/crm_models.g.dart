// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'crm_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_FieldCustomerRow _$FieldCustomerRowFromJson(Map<String, dynamic> json) =>
    _FieldCustomerRow(
      marketId: (json['marketId'] as num).toInt(),
      name: json['name'] as String,
      address: json['address'] as String?,
      branch: json['branch'] as String?,
      type: json['type'] as String?,
      lat: (json['lat'] as num?)?.toDouble(),
      lon: (json['lon'] as num?)?.toDouble(),
      agentId: json['agentId'] as String?,
      agentName: json['agentName'] as String?,
      priority: json['priority'] as String? ?? 'Medium',
      status: json['status'] as String? ?? 'Active',
      lastVisitDate: json['lastVisitDate'] as String?,
      lastOrderDate: json['lastOrderDate'] as String?,
      sales7: (json['sales7'] as num?)?.toDouble() ?? 0,
      sales30: (json['sales30'] as num?)?.toDouble() ?? 0,
      sales90: (json['sales90'] as num?)?.toDouble() ?? 0,
      trendPct: (json['trendPct'] as num?)?.toDouble(),
      monthlyTarget: (json['monthlyTarget'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$FieldCustomerRowToJson(_FieldCustomerRow instance) =>
    <String, dynamic>{
      'marketId': instance.marketId,
      'name': instance.name,
      'address': ?instance.address,
      'branch': ?instance.branch,
      'type': ?instance.type,
      'lat': ?instance.lat,
      'lon': ?instance.lon,
      'agentId': ?instance.agentId,
      'agentName': ?instance.agentName,
      'priority': instance.priority,
      'status': instance.status,
      'lastVisitDate': ?instance.lastVisitDate,
      'lastOrderDate': ?instance.lastOrderDate,
      'sales7': instance.sales7,
      'sales30': instance.sales30,
      'sales90': instance.sales90,
      'trendPct': ?instance.trendPct,
      'monthlyTarget': ?instance.monthlyTarget,
    };

_CustomerMonth _$CustomerMonthFromJson(Map<String, dynamic> json) =>
    _CustomerMonth(
      year: (json['year'] as num).toInt(),
      month: (json['month'] as num).toInt(),
      sum: (json['sum'] as num?)?.toDouble() ?? 0,
      kg: (json['kg'] as num?)?.toDouble() ?? 0,
      orders: (json['orders'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$CustomerMonthToJson(_CustomerMonth instance) =>
    <String, dynamic>{
      'year': instance.year,
      'month': instance.month,
      'sum': instance.sum,
      'kg': instance.kg,
      'orders': instance.orders,
    };

_CustomerOrder _$CustomerOrderFromJson(Map<String, dynamic> json) =>
    _CustomerOrder(
      id: (json['id'] as num).toInt(),
      date: json['date'] as String,
      acceptedDate: json['acceptedDate'] as String?,
      status: json['status'] as String? ?? '',
      agent: json['agent'] as String?,
      sum: (json['sum'] as num?)?.toDouble() ?? 0,
      kg: (json['kg'] as num?)?.toDouble() ?? 0,
    );

Map<String, dynamic> _$CustomerOrderToJson(_CustomerOrder instance) =>
    <String, dynamic>{
      'id': instance.id,
      'date': instance.date,
      'acceptedDate': ?instance.acceptedDate,
      'status': instance.status,
      'agent': ?instance.agent,
      'sum': instance.sum,
      'kg': instance.kg,
    };

_CustomerVisit _$CustomerVisitFromJson(Map<String, dynamic> json) =>
    _CustomerVisit(
      source: json['source'] as String? ?? 'Linko',
      at: json['at'] as String,
      agent: json['agent'] as String?,
      status: json['status'] as String? ?? '',
      result: json['result'] as String?,
      comment: json['comment'] as String?,
    );

Map<String, dynamic> _$CustomerVisitToJson(_CustomerVisit instance) =>
    <String, dynamic>{
      'source': instance.source,
      'at': instance.at,
      'agent': ?instance.agent,
      'status': instance.status,
      'result': ?instance.result,
      'comment': ?instance.comment,
    };

_FieldCustomerCard _$FieldCustomerCardFromJson(Map<String, dynamic> json) =>
    _FieldCustomerCard(
      customer: FieldCustomerRow.fromJson(
        json['customer'] as Map<String, dynamic>,
      ),
      linkoResponsibleId: (json['linkoResponsibleId'] as num?)?.toInt(),
      linkoResponsibleName: json['linkoResponsibleName'] as String?,
      supervisorName: json['supervisorName'] as String?,
      contactPerson: json['contactPerson'] as String?,
      phone: json['phone'] as String?,
      note: json['note'] as String?,
      monthToDate: (json['monthToDate'] as num?)?.toDouble() ?? 0,
      orders30: (json['orders30'] as num?)?.toInt() ?? 0,
      visits30: (json['visits30'] as num?)?.toInt() ?? 0,
      months:
          (json['months'] as List<dynamic>?)
              ?.map((e) => CustomerMonth.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <CustomerMonth>[],
      orders:
          (json['orders'] as List<dynamic>?)
              ?.map((e) => CustomerOrder.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <CustomerOrder>[],
      visits:
          (json['visits'] as List<dynamic>?)
              ?.map((e) => CustomerVisit.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <CustomerVisit>[],
      canEdit: json['canEdit'] as bool? ?? false,
      canChangeAgent: json['canChangeAgent'] as bool? ?? false,
    );

Map<String, dynamic> _$FieldCustomerCardToJson(_FieldCustomerCard instance) =>
    <String, dynamic>{
      'customer': instance.customer.toJson(),
      'linkoResponsibleId': ?instance.linkoResponsibleId,
      'linkoResponsibleName': ?instance.linkoResponsibleName,
      'supervisorName': ?instance.supervisorName,
      'contactPerson': ?instance.contactPerson,
      'phone': ?instance.phone,
      'note': ?instance.note,
      'monthToDate': instance.monthToDate,
      'orders30': instance.orders30,
      'visits30': instance.visits30,
      'months': instance.months.map((e) => e.toJson()).toList(),
      'orders': instance.orders.map((e) => e.toJson()).toList(),
      'visits': instance.visits.map((e) => e.toJson()).toList(),
      'canEdit': instance.canEdit,
      'canChangeAgent': instance.canChangeAgent,
    };

_AuditEntry _$AuditEntryFromJson(Map<String, dynamic> json) => _AuditEntry(
  id: (json['id'] as num).toInt(),
  timestamp: json['timestamp'] as String,
  actor: json['actor'] as String? ?? '',
  action: json['action'] as String,
  data: json['data'] as String? ?? '',
);

Map<String, dynamic> _$AuditEntryToJson(_AuditEntry instance) =>
    <String, dynamic>{
      'id': instance.id,
      'timestamp': instance.timestamp,
      'actor': instance.actor,
      'action': instance.action,
      'data': instance.data,
    };
