// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'user_profile.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$SalesScope {

 String get level; String? get home;
/// Create a copy of SalesScope
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SalesScopeCopyWith<SalesScope> get copyWith => _$SalesScopeCopyWithImpl<SalesScope>(this as SalesScope, _$identity);

  /// Serializes this SalesScope to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SalesScope;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SalesScope&&(identical(other.level, _this.level) || other.level == _this.level)&&(identical(other.home, _this.home) || other.home == _this.home));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SalesScope;
  return Object.hash(runtimeType,_this.level,_this.home);
}

@override
String toString() {
  final _this = this as SalesScope;
  return 'SalesScope(level: ${_this.level}, home: ${_this.home})';
}


}

/// @nodoc
abstract mixin class $SalesScopeCopyWith<$Res>  {
  factory $SalesScopeCopyWith(SalesScope value, $Res Function(SalesScope) _then) = _$SalesScopeCopyWithImpl;
@useResult
$Res call({
 String level, String? home
});




}
/// @nodoc
class _$SalesScopeCopyWithImpl<$Res>
    implements $SalesScopeCopyWith<$Res> {
  _$SalesScopeCopyWithImpl(this._self, this._then);

  final SalesScope _self;
  final $Res Function(SalesScope) _then;

/// Create a copy of SalesScope
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? level = null,Object? home = freezed,}) {
  return _then(SalesScope(
level: null == level ? _self.level : level // ignore: cast_nullable_to_non_nullable
as String,home: freezed == home ? _self.home : home // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [SalesScope].
extension SalesScopePatterns on SalesScope {
/// A variant of `map` that fallback to returning `orElse`.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case _:
///     return orElse();
/// }
/// ```

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SalesScope value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SalesScope() when $default != null:
return $default(_that);case _:
  return orElse();

}
}
/// A `switch`-like method, using callbacks.
///
/// Callbacks receives the raw object, upcasted.
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case final Subclass2 value:
///     return ...;
/// }
/// ```

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SalesScope value)  $default,){
final _that = this;
switch (_that) {
case _SalesScope():
return $default(_that);case _:
  throw StateError('Unexpected subclass');

}
}
/// A variant of `map` that fallback to returning `null`.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case _:
///     return null;
/// }
/// ```

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SalesScope value)?  $default,){
final _that = this;
switch (_that) {
case _SalesScope() when $default != null:
return $default(_that);case _:
  return null;

}
}
/// A variant of `when` that fallback to an `orElse` callback.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case _:
///     return orElse();
/// }
/// ```

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String level,  String? home)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SalesScope() when $default != null:
return $default(_that.level,_that.home);case _:
  return orElse();

}
}
/// A `switch`-like method, using callbacks.
///
/// As opposed to `map`, this offers destructuring.
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case Subclass2(:final field2):
///     return ...;
/// }
/// ```

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String level,  String? home)  $default,) {final _that = this;
switch (_that) {
case _SalesScope():
return $default(_that.level,_that.home);case _:
  throw StateError('Unexpected subclass');

}
}
/// A variant of `when` that fallback to returning `null`
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case _:
///     return null;
/// }
/// ```

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String level,  String? home)?  $default,) {final _that = this;
switch (_that) {
case _SalesScope() when $default != null:
return $default(_that.level,_that.home);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SalesScope implements SalesScope {
  const _SalesScope({this.level = 'Organization', this.home});
  factory _SalesScope.fromJson(Map<String, dynamic> json) => _$SalesScopeFromJson(json);

@override@JsonKey() final  String level;
@override final  String? home;

/// Create a copy of SalesScope
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SalesScopeCopyWith<_SalesScope> get copyWith => __$SalesScopeCopyWithImpl<_SalesScope>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SalesScopeToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SalesScope&&(identical(other.level, level) || other.level == level)&&(identical(other.home, home) || other.home == home));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,level,home);
}

@override
String toString() {
    return 'SalesScope(level: $level, home: $home)';
}


}

/// @nodoc
abstract mixin class _$SalesScopeCopyWith<$Res> implements $SalesScopeCopyWith<$Res> {
  factory _$SalesScopeCopyWith(_SalesScope value, $Res Function(_SalesScope) _then) = __$SalesScopeCopyWithImpl;
@override @useResult
$Res call({
 String level, String? home
});




}
/// @nodoc
class __$SalesScopeCopyWithImpl<$Res>
    implements _$SalesScopeCopyWith<$Res> {
  __$SalesScopeCopyWithImpl(this._self, this._then);

  final _SalesScope _self;
  final $Res Function(_SalesScope) _then;

/// Create a copy of SalesScope
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? level = null,Object? home = freezed,}) {
  return _then(_SalesScope(
level: null == level ? _self.level : level // ignore: cast_nullable_to_non_nullable
as String,home: freezed == home ? _self.home : home // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$UserProfile {

 String get id; String get login; String? get email; String get name; String? get firstName; String? get lastName; String? get position; String? get departmentName; List<String> get roles;/// Главная на сайте: overview — «Обзор» компании, my_day — «Мой день» сотрудника, work — «Рабочий стол» снабжения и склада.
 String get home; List<String> get permissions; bool get canOpenSettings; bool get hrAccess; bool get hiringRequests; SalesScope? get salesScope;/// Отделы, задачи которых открываются пользователю.
 List<String> get departments;
/// Create a copy of UserProfile
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$UserProfileCopyWith<UserProfile> get copyWith => _$UserProfileCopyWithImpl<UserProfile>(this as UserProfile, _$identity);

  /// Serializes this UserProfile to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as UserProfile;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is UserProfile&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.login, _this.login) || other.login == _this.login)&&(identical(other.email, _this.email) || other.email == _this.email)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.firstName, _this.firstName) || other.firstName == _this.firstName)&&(identical(other.lastName, _this.lastName) || other.lastName == _this.lastName)&&(identical(other.position, _this.position) || other.position == _this.position)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&const DeepCollectionEquality().equals(other.roles, _this.roles)&&(identical(other.home, _this.home) || other.home == _this.home)&&const DeepCollectionEquality().equals(other.permissions, _this.permissions)&&(identical(other.canOpenSettings, _this.canOpenSettings) || other.canOpenSettings == _this.canOpenSettings)&&(identical(other.hrAccess, _this.hrAccess) || other.hrAccess == _this.hrAccess)&&(identical(other.hiringRequests, _this.hiringRequests) || other.hiringRequests == _this.hiringRequests)&&(identical(other.salesScope, _this.salesScope) || other.salesScope == _this.salesScope)&&const DeepCollectionEquality().equals(other.departments, _this.departments));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as UserProfile;
  return Object.hash(runtimeType,_this.id,_this.login,_this.email,_this.name,_this.firstName,_this.lastName,_this.position,_this.departmentName,const DeepCollectionEquality().hash(_this.roles),_this.home,const DeepCollectionEquality().hash(_this.permissions),_this.canOpenSettings,_this.hrAccess,_this.hiringRequests,_this.salesScope,const DeepCollectionEquality().hash(_this.departments));
}

@override
String toString() {
  final _this = this as UserProfile;
  return 'UserProfile(id: ${_this.id}, login: ${_this.login}, email: ${_this.email}, name: ${_this.name}, firstName: ${_this.firstName}, lastName: ${_this.lastName}, position: ${_this.position}, departmentName: ${_this.departmentName}, roles: ${_this.roles}, home: ${_this.home}, permissions: ${_this.permissions}, canOpenSettings: ${_this.canOpenSettings}, hrAccess: ${_this.hrAccess}, hiringRequests: ${_this.hiringRequests}, salesScope: ${_this.salesScope}, departments: ${_this.departments})';
}


}

/// @nodoc
abstract mixin class $UserProfileCopyWith<$Res>  {
  factory $UserProfileCopyWith(UserProfile value, $Res Function(UserProfile) _then) = _$UserProfileCopyWithImpl;
@useResult
$Res call({
 String id, String login, String? email, String name, String? firstName, String? lastName, String? position, String? departmentName, List<String> roles, String home, List<String> permissions, bool canOpenSettings, bool hrAccess, bool hiringRequests, SalesScope? salesScope, List<String> departments
});


$SalesScopeCopyWith<$Res>? get salesScope;

}
/// @nodoc
class _$UserProfileCopyWithImpl<$Res>
    implements $UserProfileCopyWith<$Res> {
  _$UserProfileCopyWithImpl(this._self, this._then);

  final UserProfile _self;
  final $Res Function(UserProfile) _then;

/// Create a copy of UserProfile
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? login = null,Object? email = freezed,Object? name = null,Object? firstName = freezed,Object? lastName = freezed,Object? position = freezed,Object? departmentName = freezed,Object? roles = null,Object? home = null,Object? permissions = null,Object? canOpenSettings = null,Object? hrAccess = null,Object? hiringRequests = null,Object? salesScope = freezed,Object? departments = null,}) {
  return _then(UserProfile(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,login: null == login ? _self.login : login // ignore: cast_nullable_to_non_nullable
as String,email: freezed == email ? _self.email : email // ignore: cast_nullable_to_non_nullable
as String?,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,firstName: freezed == firstName ? _self.firstName : firstName // ignore: cast_nullable_to_non_nullable
as String?,lastName: freezed == lastName ? _self.lastName : lastName // ignore: cast_nullable_to_non_nullable
as String?,position: freezed == position ? _self.position : position // ignore: cast_nullable_to_non_nullable
as String?,departmentName: freezed == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String?,roles: null == roles ? _self.roles : roles // ignore: cast_nullable_to_non_nullable
as List<String>,home: null == home ? _self.home : home // ignore: cast_nullable_to_non_nullable
as String,permissions: null == permissions ? _self.permissions : permissions // ignore: cast_nullable_to_non_nullable
as List<String>,canOpenSettings: null == canOpenSettings ? _self.canOpenSettings : canOpenSettings // ignore: cast_nullable_to_non_nullable
as bool,hrAccess: null == hrAccess ? _self.hrAccess : hrAccess // ignore: cast_nullable_to_non_nullable
as bool,hiringRequests: null == hiringRequests ? _self.hiringRequests : hiringRequests // ignore: cast_nullable_to_non_nullable
as bool,salesScope: freezed == salesScope ? _self.salesScope : salesScope // ignore: cast_nullable_to_non_nullable
as SalesScope?,departments: null == departments ? _self.departments : departments // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}
/// Create a copy of UserProfile
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesScopeCopyWith<$Res>? get salesScope {
    if (_self.salesScope == null) {
    return null;
  }

  return $SalesScopeCopyWith<$Res>(_self.salesScope!, (value) {
    return _then(_self.copyWith(salesScope: value));
  });
}
}


/// Adds pattern-matching-related methods to [UserProfile].
extension UserProfilePatterns on UserProfile {
/// A variant of `map` that fallback to returning `orElse`.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case _:
///     return orElse();
/// }
/// ```

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _UserProfile value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _UserProfile() when $default != null:
return $default(_that);case _:
  return orElse();

}
}
/// A `switch`-like method, using callbacks.
///
/// Callbacks receives the raw object, upcasted.
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case final Subclass2 value:
///     return ...;
/// }
/// ```

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _UserProfile value)  $default,){
final _that = this;
switch (_that) {
case _UserProfile():
return $default(_that);case _:
  throw StateError('Unexpected subclass');

}
}
/// A variant of `map` that fallback to returning `null`.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case _:
///     return null;
/// }
/// ```

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _UserProfile value)?  $default,){
final _that = this;
switch (_that) {
case _UserProfile() when $default != null:
return $default(_that);case _:
  return null;

}
}
/// A variant of `when` that fallback to an `orElse` callback.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case _:
///     return orElse();
/// }
/// ```

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String login,  String? email,  String name,  String? firstName,  String? lastName,  String? position,  String? departmentName,  List<String> roles,  String home,  List<String> permissions,  bool canOpenSettings,  bool hrAccess,  bool hiringRequests,  SalesScope? salesScope,  List<String> departments)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _UserProfile() when $default != null:
return $default(_that.id,_that.login,_that.email,_that.name,_that.firstName,_that.lastName,_that.position,_that.departmentName,_that.roles,_that.home,_that.permissions,_that.canOpenSettings,_that.hrAccess,_that.hiringRequests,_that.salesScope,_that.departments);case _:
  return orElse();

}
}
/// A `switch`-like method, using callbacks.
///
/// As opposed to `map`, this offers destructuring.
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case Subclass2(:final field2):
///     return ...;
/// }
/// ```

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String login,  String? email,  String name,  String? firstName,  String? lastName,  String? position,  String? departmentName,  List<String> roles,  String home,  List<String> permissions,  bool canOpenSettings,  bool hrAccess,  bool hiringRequests,  SalesScope? salesScope,  List<String> departments)  $default,) {final _that = this;
switch (_that) {
case _UserProfile():
return $default(_that.id,_that.login,_that.email,_that.name,_that.firstName,_that.lastName,_that.position,_that.departmentName,_that.roles,_that.home,_that.permissions,_that.canOpenSettings,_that.hrAccess,_that.hiringRequests,_that.salesScope,_that.departments);case _:
  throw StateError('Unexpected subclass');

}
}
/// A variant of `when` that fallback to returning `null`
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case _:
///     return null;
/// }
/// ```

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String login,  String? email,  String name,  String? firstName,  String? lastName,  String? position,  String? departmentName,  List<String> roles,  String home,  List<String> permissions,  bool canOpenSettings,  bool hrAccess,  bool hiringRequests,  SalesScope? salesScope,  List<String> departments)?  $default,) {final _that = this;
switch (_that) {
case _UserProfile() when $default != null:
return $default(_that.id,_that.login,_that.email,_that.name,_that.firstName,_that.lastName,_that.position,_that.departmentName,_that.roles,_that.home,_that.permissions,_that.canOpenSettings,_that.hrAccess,_that.hiringRequests,_that.salesScope,_that.departments);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _UserProfile extends UserProfile {
  const _UserProfile({required this.id, required this.login, this.email, required this.name, this.firstName, this.lastName, this.position, this.departmentName,  List<String> roles = const <String>[], this.home = 'overview',  List<String> permissions = const <String>[], this.canOpenSettings = false, this.hrAccess = false, this.hiringRequests = false, this.salesScope,  List<String> departments = const <String>[]}): _roles = roles,_permissions = permissions,_departments = departments,super._();
  factory _UserProfile.fromJson(Map<String, dynamic> json) => _$UserProfileFromJson(json);

@override final  String id;
@override final  String login;
@override final  String? email;
@override final  String name;
@override final  String? firstName;
@override final  String? lastName;
@override final  String? position;
@override final  String? departmentName;
 final  List<String> _roles;
@override@JsonKey() List<String> get roles {
  if (_roles is EqualUnmodifiableListView) return _roles;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_roles);
}

/// Главная на сайте: overview — «Обзор» компании, my_day — «Мой день» сотрудника, work — «Рабочий стол» снабжения и склада.
@override@JsonKey() final  String home;
 final  List<String> _permissions;
@override@JsonKey() List<String> get permissions {
  if (_permissions is EqualUnmodifiableListView) return _permissions;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_permissions);
}

@override@JsonKey() final  bool canOpenSettings;
@override@JsonKey() final  bool hrAccess;
@override@JsonKey() final  bool hiringRequests;
@override final  SalesScope? salesScope;
/// Отделы, задачи которых открываются пользователю.
 final  List<String> _departments;
/// Отделы, задачи которых открываются пользователю.
@override@JsonKey() List<String> get departments {
  if (_departments is EqualUnmodifiableListView) return _departments;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_departments);
}


/// Create a copy of UserProfile
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$UserProfileCopyWith<_UserProfile> get copyWith => __$UserProfileCopyWithImpl<_UserProfile>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$UserProfileToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _UserProfile&&(identical(other.id, id) || other.id == id)&&(identical(other.login, login) || other.login == login)&&(identical(other.email, email) || other.email == email)&&(identical(other.name, name) || other.name == name)&&(identical(other.firstName, firstName) || other.firstName == firstName)&&(identical(other.lastName, lastName) || other.lastName == lastName)&&(identical(other.position, position) || other.position == position)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&const DeepCollectionEquality().equals(other.roles, _roles)&&(identical(other.home, home) || other.home == home)&&const DeepCollectionEquality().equals(other.permissions, _permissions)&&(identical(other.canOpenSettings, canOpenSettings) || other.canOpenSettings == canOpenSettings)&&(identical(other.hrAccess, hrAccess) || other.hrAccess == hrAccess)&&(identical(other.hiringRequests, hiringRequests) || other.hiringRequests == hiringRequests)&&(identical(other.salesScope, salesScope) || other.salesScope == salesScope)&&const DeepCollectionEquality().equals(other.departments, _departments));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,login,email,name,firstName,lastName,position,departmentName,const DeepCollectionEquality().hash(_roles),home,const DeepCollectionEquality().hash(_permissions),canOpenSettings,hrAccess,hiringRequests,salesScope,const DeepCollectionEquality().hash(_departments));
}

@override
String toString() {
    return 'UserProfile(id: $id, login: $login, email: $email, name: $name, firstName: $firstName, lastName: $lastName, position: $position, departmentName: $departmentName, roles: $roles, home: $home, permissions: $permissions, canOpenSettings: $canOpenSettings, hrAccess: $hrAccess, hiringRequests: $hiringRequests, salesScope: $salesScope, departments: $departments)';
}


}

/// @nodoc
abstract mixin class _$UserProfileCopyWith<$Res> implements $UserProfileCopyWith<$Res> {
  factory _$UserProfileCopyWith(_UserProfile value, $Res Function(_UserProfile) _then) = __$UserProfileCopyWithImpl;
@override @useResult
$Res call({
 String id, String login, String? email, String name, String? firstName, String? lastName, String? position, String? departmentName, List<String> roles, String home, List<String> permissions, bool canOpenSettings, bool hrAccess, bool hiringRequests, SalesScope? salesScope, List<String> departments
});


@override $SalesScopeCopyWith<$Res>? get salesScope;

}
/// @nodoc
class __$UserProfileCopyWithImpl<$Res>
    implements _$UserProfileCopyWith<$Res> {
  __$UserProfileCopyWithImpl(this._self, this._then);

  final _UserProfile _self;
  final $Res Function(_UserProfile) _then;

/// Create a copy of UserProfile
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? login = null,Object? email = freezed,Object? name = null,Object? firstName = freezed,Object? lastName = freezed,Object? position = freezed,Object? departmentName = freezed,Object? roles = null,Object? home = null,Object? permissions = null,Object? canOpenSettings = null,Object? hrAccess = null,Object? hiringRequests = null,Object? salesScope = freezed,Object? departments = null,}) {
  return _then(_UserProfile(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,login: null == login ? _self.login : login // ignore: cast_nullable_to_non_nullable
as String,email: freezed == email ? _self.email : email // ignore: cast_nullable_to_non_nullable
as String?,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,firstName: freezed == firstName ? _self.firstName : firstName // ignore: cast_nullable_to_non_nullable
as String?,lastName: freezed == lastName ? _self.lastName : lastName // ignore: cast_nullable_to_non_nullable
as String?,position: freezed == position ? _self.position : position // ignore: cast_nullable_to_non_nullable
as String?,departmentName: freezed == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String?,roles: null == roles ? _self._roles : roles // ignore: cast_nullable_to_non_nullable
as List<String>,home: null == home ? _self.home : home // ignore: cast_nullable_to_non_nullable
as String,permissions: null == permissions ? _self._permissions : permissions // ignore: cast_nullable_to_non_nullable
as List<String>,canOpenSettings: null == canOpenSettings ? _self.canOpenSettings : canOpenSettings // ignore: cast_nullable_to_non_nullable
as bool,hrAccess: null == hrAccess ? _self.hrAccess : hrAccess // ignore: cast_nullable_to_non_nullable
as bool,hiringRequests: null == hiringRequests ? _self.hiringRequests : hiringRequests // ignore: cast_nullable_to_non_nullable
as bool,salesScope: freezed == salesScope ? _self.salesScope : salesScope // ignore: cast_nullable_to_non_nullable
as SalesScope?,departments: null == departments ? _self._departments : departments // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}

/// Create a copy of UserProfile
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesScopeCopyWith<$Res>? get salesScope {
    if (_self.salesScope == null) {
    return null;
  }

  return $SalesScopeCopyWith<$Res>(_self.salesScope!, (value) {
    return _then(_self.copyWith(salesScope: value));
  });
}
}

// dart format on
