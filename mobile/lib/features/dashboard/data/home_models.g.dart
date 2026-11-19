// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'home_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_ProcurementRoles _$ProcurementRolesFromJson(Map<String, dynamic> json) =>
    _ProcurementRoles(
      director: json['director'] as bool? ?? false,
      financier: json['financier'] as bool? ?? false,
    );

Map<String, dynamic> _$ProcurementRolesToJson(_ProcurementRoles instance) =>
    <String, dynamic>{
      'director': instance.director,
      'financier': instance.financier,
    };

_ApprovalBrief _$ApprovalBriefFromJson(Map<String, dynamic> json) =>
    _ApprovalBrief(
      id: json['id'] as String,
      number: json['number'] as String,
      title: json['title'] as String,
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      department: json['department'] as String?,
    );

Map<String, dynamic> _$ApprovalBriefToJson(_ApprovalBrief instance) =>
    <String, dynamic>{
      'id': instance.id,
      'number': instance.number,
      'title': instance.title,
      'amount': instance.amount,
      'department': ?instance.department,
    };

_HomeRequests _$HomeRequestsFromJson(Map<String, dynamic> json) =>
    _HomeRequests(
      awaitingMe: (json['awaitingMe'] as num?)?.toInt() ?? 0,
      approvals:
          (json['approvals'] as List<dynamic>?)
              ?.map((e) => ApprovalBrief.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <ApprovalBrief>[],
      departmentToPickUp: (json['departmentToPickUp'] as num?)?.toInt() ?? 0,
      mineToPickUp: (json['mineToPickUp'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$HomeRequestsToJson(_HomeRequests instance) =>
    <String, dynamic>{
      'awaitingMe': instance.awaitingMe,
      'approvals': instance.approvals.map((e) => e.toJson()).toList(),
      'departmentToPickUp': instance.departmentToPickUp,
      'mineToPickUp': instance.mineToPickUp,
    };

_AcceptedArrival _$AcceptedArrivalFromJson(Map<String, dynamic> json) =>
    _AcceptedArrival(
      id: json['id'] as String,
      number: json['number'] as String,
      supplierName: json['supplierName'] as String,
      summary: json['summary'] as String?,
      acceptedAt: json['acceptedAt'] as String?,
    );

Map<String, dynamic> _$AcceptedArrivalToJson(_AcceptedArrival instance) =>
    <String, dynamic>{
      'id': instance.id,
      'number': instance.number,
      'supplierName': instance.supplierName,
      'summary': ?instance.summary,
      'acceptedAt': ?instance.acceptedAt,
    };

_ProcurementHome _$ProcurementHomeFromJson(Map<String, dynamic> json) =>
    _ProcurementHome(
      roles: json['roles'] == null
          ? const ProcurementRoles()
          : ProcurementRoles.fromJson(json['roles'] as Map<String, dynamic>),
      requests: json['requests'] == null
          ? const HomeRequests()
          : HomeRequests.fromJson(json['requests'] as Map<String, dynamic>),
      accepted:
          (json['accepted'] as List<dynamic>?)
              ?.map((e) => AcceptedArrival.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <AcceptedArrival>[],
      acceptedCount: (json['acceptedCount'] as num?)?.toInt() ?? 0,
      acceptedAmount: (json['acceptedAmount'] as num?)?.toDouble() ?? 0,
    );

Map<String, dynamic> _$ProcurementHomeToJson(_ProcurementHome instance) =>
    <String, dynamic>{
      'roles': instance.roles.toJson(),
      'requests': instance.requests.toJson(),
      'accepted': instance.accepted.map((e) => e.toJson()).toList(),
      'acceptedCount': instance.acceptedCount,
      'acceptedAmount': instance.acceptedAmount,
    };
