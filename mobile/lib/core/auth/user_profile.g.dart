// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'user_profile.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_SalesScope _$SalesScopeFromJson(Map<String, dynamic> json) => _SalesScope(
  level: json['level'] as String? ?? 'Organization',
  home: json['home'] as String?,
);

Map<String, dynamic> _$SalesScopeToJson(_SalesScope instance) =>
    <String, dynamic>{'level': instance.level, 'home': ?instance.home};

_UserProfile _$UserProfileFromJson(Map<String, dynamic> json) => _UserProfile(
  id: json['id'] as String,
  login: json['login'] as String,
  email: json['email'] as String?,
  name: json['name'] as String,
  firstName: json['firstName'] as String?,
  lastName: json['lastName'] as String?,
  position: json['position'] as String?,
  departmentName: json['departmentName'] as String?,
  roles:
      (json['roles'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const <String>[],
  home: json['home'] as String? ?? 'overview',
  permissions:
      (json['permissions'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList() ??
      const <String>[],
  canOpenSettings: json['canOpenSettings'] as bool? ?? false,
  hrAccess: json['hrAccess'] as bool? ?? false,
  hiringRequests: json['hiringRequests'] as bool? ?? false,
  salesScope: json['salesScope'] == null
      ? null
      : SalesScope.fromJson(json['salesScope'] as Map<String, dynamic>),
  departments:
      (json['departments'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList() ??
      const <String>[],
);

Map<String, dynamic> _$UserProfileToJson(_UserProfile instance) =>
    <String, dynamic>{
      'id': instance.id,
      'login': instance.login,
      'email': ?instance.email,
      'name': instance.name,
      'firstName': ?instance.firstName,
      'lastName': ?instance.lastName,
      'position': ?instance.position,
      'departmentName': ?instance.departmentName,
      'roles': instance.roles,
      'home': instance.home,
      'permissions': instance.permissions,
      'canOpenSettings': instance.canOpenSettings,
      'hrAccess': instance.hrAccess,
      'hiringRequests': instance.hiringRequests,
      'salesScope': ?instance.salesScope?.toJson(),
      'departments': instance.departments,
    };
