// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'home_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$ProcurementRoles {

 bool get director; bool get financier;
/// Create a copy of ProcurementRoles
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProcurementRolesCopyWith<ProcurementRoles> get copyWith => _$ProcurementRolesCopyWithImpl<ProcurementRoles>(this as ProcurementRoles, _$identity);

  /// Serializes this ProcurementRoles to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProcurementRoles;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProcurementRoles&&(identical(other.director, _this.director) || other.director == _this.director)&&(identical(other.financier, _this.financier) || other.financier == _this.financier));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProcurementRoles;
  return Object.hash(runtimeType,_this.director,_this.financier);
}

@override
String toString() {
  final _this = this as ProcurementRoles;
  return 'ProcurementRoles(director: ${_this.director}, financier: ${_this.financier})';
}


}

/// @nodoc
abstract mixin class $ProcurementRolesCopyWith<$Res>  {
  factory $ProcurementRolesCopyWith(ProcurementRoles value, $Res Function(ProcurementRoles) _then) = _$ProcurementRolesCopyWithImpl;
@useResult
$Res call({
 bool director, bool financier
});




}
/// @nodoc
class _$ProcurementRolesCopyWithImpl<$Res>
    implements $ProcurementRolesCopyWith<$Res> {
  _$ProcurementRolesCopyWithImpl(this._self, this._then);

  final ProcurementRoles _self;
  final $Res Function(ProcurementRoles) _then;

/// Create a copy of ProcurementRoles
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? director = null,Object? financier = null,}) {
  return _then(ProcurementRoles(
director: null == director ? _self.director : director // ignore: cast_nullable_to_non_nullable
as bool,financier: null == financier ? _self.financier : financier // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [ProcurementRoles].
extension ProcurementRolesPatterns on ProcurementRoles {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProcurementRoles value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProcurementRoles() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProcurementRoles value)  $default,){
final _that = this;
switch (_that) {
case _ProcurementRoles():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProcurementRoles value)?  $default,){
final _that = this;
switch (_that) {
case _ProcurementRoles() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( bool director,  bool financier)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ProcurementRoles() when $default != null:
return $default(_that.director,_that.financier);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( bool director,  bool financier)  $default,) {final _that = this;
switch (_that) {
case _ProcurementRoles():
return $default(_that.director,_that.financier);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( bool director,  bool financier)?  $default,) {final _that = this;
switch (_that) {
case _ProcurementRoles() when $default != null:
return $default(_that.director,_that.financier);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProcurementRoles implements ProcurementRoles {
  const _ProcurementRoles({this.director = false, this.financier = false});
  factory _ProcurementRoles.fromJson(Map<String, dynamic> json) => _$ProcurementRolesFromJson(json);

@override@JsonKey() final  bool director;
@override@JsonKey() final  bool financier;

/// Create a copy of ProcurementRoles
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProcurementRolesCopyWith<_ProcurementRoles> get copyWith => __$ProcurementRolesCopyWithImpl<_ProcurementRoles>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProcurementRolesToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProcurementRoles&&(identical(other.director, director) || other.director == director)&&(identical(other.financier, financier) || other.financier == financier));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,director,financier);
}

@override
String toString() {
    return 'ProcurementRoles(director: $director, financier: $financier)';
}


}

/// @nodoc
abstract mixin class _$ProcurementRolesCopyWith<$Res> implements $ProcurementRolesCopyWith<$Res> {
  factory _$ProcurementRolesCopyWith(_ProcurementRoles value, $Res Function(_ProcurementRoles) _then) = __$ProcurementRolesCopyWithImpl;
@override @useResult
$Res call({
 bool director, bool financier
});




}
/// @nodoc
class __$ProcurementRolesCopyWithImpl<$Res>
    implements _$ProcurementRolesCopyWith<$Res> {
  __$ProcurementRolesCopyWithImpl(this._self, this._then);

  final _ProcurementRoles _self;
  final $Res Function(_ProcurementRoles) _then;

/// Create a copy of ProcurementRoles
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? director = null,Object? financier = null,}) {
  return _then(_ProcurementRoles(
director: null == director ? _self.director : director // ignore: cast_nullable_to_non_nullable
as bool,financier: null == financier ? _self.financier : financier // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$ApprovalBrief {

 String get id; String get number; String get title; double get amount; String? get department;
/// Create a copy of ApprovalBrief
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ApprovalBriefCopyWith<ApprovalBrief> get copyWith => _$ApprovalBriefCopyWithImpl<ApprovalBrief>(this as ApprovalBrief, _$identity);

  /// Serializes this ApprovalBrief to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ApprovalBrief;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ApprovalBrief&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.number, _this.number) || other.number == _this.number)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.amount, _this.amount) || other.amount == _this.amount)&&(identical(other.department, _this.department) || other.department == _this.department));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ApprovalBrief;
  return Object.hash(runtimeType,_this.id,_this.number,_this.title,_this.amount,_this.department);
}

@override
String toString() {
  final _this = this as ApprovalBrief;
  return 'ApprovalBrief(id: ${_this.id}, number: ${_this.number}, title: ${_this.title}, amount: ${_this.amount}, department: ${_this.department})';
}


}

/// @nodoc
abstract mixin class $ApprovalBriefCopyWith<$Res>  {
  factory $ApprovalBriefCopyWith(ApprovalBrief value, $Res Function(ApprovalBrief) _then) = _$ApprovalBriefCopyWithImpl;
@useResult
$Res call({
 String id, String number, String title, double amount, String? department
});




}
/// @nodoc
class _$ApprovalBriefCopyWithImpl<$Res>
    implements $ApprovalBriefCopyWith<$Res> {
  _$ApprovalBriefCopyWithImpl(this._self, this._then);

  final ApprovalBrief _self;
  final $Res Function(ApprovalBrief) _then;

/// Create a copy of ApprovalBrief
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? number = null,Object? title = null,Object? amount = null,Object? department = freezed,}) {
  return _then(ApprovalBrief(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,department: freezed == department ? _self.department : department // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [ApprovalBrief].
extension ApprovalBriefPatterns on ApprovalBrief {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ApprovalBrief value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ApprovalBrief() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ApprovalBrief value)  $default,){
final _that = this;
switch (_that) {
case _ApprovalBrief():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ApprovalBrief value)?  $default,){
final _that = this;
switch (_that) {
case _ApprovalBrief() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String number,  String title,  double amount,  String? department)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ApprovalBrief() when $default != null:
return $default(_that.id,_that.number,_that.title,_that.amount,_that.department);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String number,  String title,  double amount,  String? department)  $default,) {final _that = this;
switch (_that) {
case _ApprovalBrief():
return $default(_that.id,_that.number,_that.title,_that.amount,_that.department);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String number,  String title,  double amount,  String? department)?  $default,) {final _that = this;
switch (_that) {
case _ApprovalBrief() when $default != null:
return $default(_that.id,_that.number,_that.title,_that.amount,_that.department);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ApprovalBrief implements ApprovalBrief {
  const _ApprovalBrief({required this.id, required this.number, required this.title, this.amount = 0, this.department});
  factory _ApprovalBrief.fromJson(Map<String, dynamic> json) => _$ApprovalBriefFromJson(json);

@override final  String id;
@override final  String number;
@override final  String title;
@override@JsonKey() final  double amount;
@override final  String? department;

/// Create a copy of ApprovalBrief
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ApprovalBriefCopyWith<_ApprovalBrief> get copyWith => __$ApprovalBriefCopyWithImpl<_ApprovalBrief>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ApprovalBriefToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ApprovalBrief&&(identical(other.id, id) || other.id == id)&&(identical(other.number, number) || other.number == number)&&(identical(other.title, title) || other.title == title)&&(identical(other.amount, amount) || other.amount == amount)&&(identical(other.department, department) || other.department == department));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,number,title,amount,department);
}

@override
String toString() {
    return 'ApprovalBrief(id: $id, number: $number, title: $title, amount: $amount, department: $department)';
}


}

/// @nodoc
abstract mixin class _$ApprovalBriefCopyWith<$Res> implements $ApprovalBriefCopyWith<$Res> {
  factory _$ApprovalBriefCopyWith(_ApprovalBrief value, $Res Function(_ApprovalBrief) _then) = __$ApprovalBriefCopyWithImpl;
@override @useResult
$Res call({
 String id, String number, String title, double amount, String? department
});




}
/// @nodoc
class __$ApprovalBriefCopyWithImpl<$Res>
    implements _$ApprovalBriefCopyWith<$Res> {
  __$ApprovalBriefCopyWithImpl(this._self, this._then);

  final _ApprovalBrief _self;
  final $Res Function(_ApprovalBrief) _then;

/// Create a copy of ApprovalBrief
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? number = null,Object? title = null,Object? amount = null,Object? department = freezed,}) {
  return _then(_ApprovalBrief(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,department: freezed == department ? _self.department : department // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$HomeRequests {

 int get awaitingMe; List<ApprovalBrief> get approvals; int get departmentToPickUp; int get mineToPickUp;
/// Create a copy of HomeRequests
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HomeRequestsCopyWith<HomeRequests> get copyWith => _$HomeRequestsCopyWithImpl<HomeRequests>(this as HomeRequests, _$identity);

  /// Serializes this HomeRequests to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HomeRequests;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HomeRequests&&(identical(other.awaitingMe, _this.awaitingMe) || other.awaitingMe == _this.awaitingMe)&&const DeepCollectionEquality().equals(other.approvals, _this.approvals)&&(identical(other.departmentToPickUp, _this.departmentToPickUp) || other.departmentToPickUp == _this.departmentToPickUp)&&(identical(other.mineToPickUp, _this.mineToPickUp) || other.mineToPickUp == _this.mineToPickUp));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HomeRequests;
  return Object.hash(runtimeType,_this.awaitingMe,const DeepCollectionEquality().hash(_this.approvals),_this.departmentToPickUp,_this.mineToPickUp);
}

@override
String toString() {
  final _this = this as HomeRequests;
  return 'HomeRequests(awaitingMe: ${_this.awaitingMe}, approvals: ${_this.approvals}, departmentToPickUp: ${_this.departmentToPickUp}, mineToPickUp: ${_this.mineToPickUp})';
}


}

/// @nodoc
abstract mixin class $HomeRequestsCopyWith<$Res>  {
  factory $HomeRequestsCopyWith(HomeRequests value, $Res Function(HomeRequests) _then) = _$HomeRequestsCopyWithImpl;
@useResult
$Res call({
 int awaitingMe, List<ApprovalBrief> approvals, int departmentToPickUp, int mineToPickUp
});




}
/// @nodoc
class _$HomeRequestsCopyWithImpl<$Res>
    implements $HomeRequestsCopyWith<$Res> {
  _$HomeRequestsCopyWithImpl(this._self, this._then);

  final HomeRequests _self;
  final $Res Function(HomeRequests) _then;

/// Create a copy of HomeRequests
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? awaitingMe = null,Object? approvals = null,Object? departmentToPickUp = null,Object? mineToPickUp = null,}) {
  return _then(HomeRequests(
awaitingMe: null == awaitingMe ? _self.awaitingMe : awaitingMe // ignore: cast_nullable_to_non_nullable
as int,approvals: null == approvals ? _self.approvals : approvals // ignore: cast_nullable_to_non_nullable
as List<ApprovalBrief>,departmentToPickUp: null == departmentToPickUp ? _self.departmentToPickUp : departmentToPickUp // ignore: cast_nullable_to_non_nullable
as int,mineToPickUp: null == mineToPickUp ? _self.mineToPickUp : mineToPickUp // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [HomeRequests].
extension HomeRequestsPatterns on HomeRequests {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HomeRequests value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HomeRequests() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HomeRequests value)  $default,){
final _that = this;
switch (_that) {
case _HomeRequests():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HomeRequests value)?  $default,){
final _that = this;
switch (_that) {
case _HomeRequests() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int awaitingMe,  List<ApprovalBrief> approvals,  int departmentToPickUp,  int mineToPickUp)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HomeRequests() when $default != null:
return $default(_that.awaitingMe,_that.approvals,_that.departmentToPickUp,_that.mineToPickUp);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int awaitingMe,  List<ApprovalBrief> approvals,  int departmentToPickUp,  int mineToPickUp)  $default,) {final _that = this;
switch (_that) {
case _HomeRequests():
return $default(_that.awaitingMe,_that.approvals,_that.departmentToPickUp,_that.mineToPickUp);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int awaitingMe,  List<ApprovalBrief> approvals,  int departmentToPickUp,  int mineToPickUp)?  $default,) {final _that = this;
switch (_that) {
case _HomeRequests() when $default != null:
return $default(_that.awaitingMe,_that.approvals,_that.departmentToPickUp,_that.mineToPickUp);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HomeRequests implements HomeRequests {
  const _HomeRequests({this.awaitingMe = 0,  List<ApprovalBrief> approvals = const <ApprovalBrief>[], this.departmentToPickUp = 0, this.mineToPickUp = 0}): _approvals = approvals;
  factory _HomeRequests.fromJson(Map<String, dynamic> json) => _$HomeRequestsFromJson(json);

@override@JsonKey() final  int awaitingMe;
 final  List<ApprovalBrief> _approvals;
@override@JsonKey() List<ApprovalBrief> get approvals {
  if (_approvals is EqualUnmodifiableListView) return _approvals;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_approvals);
}

@override@JsonKey() final  int departmentToPickUp;
@override@JsonKey() final  int mineToPickUp;

/// Create a copy of HomeRequests
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HomeRequestsCopyWith<_HomeRequests> get copyWith => __$HomeRequestsCopyWithImpl<_HomeRequests>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HomeRequestsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HomeRequests&&(identical(other.awaitingMe, awaitingMe) || other.awaitingMe == awaitingMe)&&const DeepCollectionEquality().equals(other.approvals, _approvals)&&(identical(other.departmentToPickUp, departmentToPickUp) || other.departmentToPickUp == departmentToPickUp)&&(identical(other.mineToPickUp, mineToPickUp) || other.mineToPickUp == mineToPickUp));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,awaitingMe,const DeepCollectionEquality().hash(_approvals),departmentToPickUp,mineToPickUp);
}

@override
String toString() {
    return 'HomeRequests(awaitingMe: $awaitingMe, approvals: $approvals, departmentToPickUp: $departmentToPickUp, mineToPickUp: $mineToPickUp)';
}


}

/// @nodoc
abstract mixin class _$HomeRequestsCopyWith<$Res> implements $HomeRequestsCopyWith<$Res> {
  factory _$HomeRequestsCopyWith(_HomeRequests value, $Res Function(_HomeRequests) _then) = __$HomeRequestsCopyWithImpl;
@override @useResult
$Res call({
 int awaitingMe, List<ApprovalBrief> approvals, int departmentToPickUp, int mineToPickUp
});




}
/// @nodoc
class __$HomeRequestsCopyWithImpl<$Res>
    implements _$HomeRequestsCopyWith<$Res> {
  __$HomeRequestsCopyWithImpl(this._self, this._then);

  final _HomeRequests _self;
  final $Res Function(_HomeRequests) _then;

/// Create a copy of HomeRequests
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? awaitingMe = null,Object? approvals = null,Object? departmentToPickUp = null,Object? mineToPickUp = null,}) {
  return _then(_HomeRequests(
awaitingMe: null == awaitingMe ? _self.awaitingMe : awaitingMe // ignore: cast_nullable_to_non_nullable
as int,approvals: null == approvals ? _self._approvals : approvals // ignore: cast_nullable_to_non_nullable
as List<ApprovalBrief>,departmentToPickUp: null == departmentToPickUp ? _self.departmentToPickUp : departmentToPickUp // ignore: cast_nullable_to_non_nullable
as int,mineToPickUp: null == mineToPickUp ? _self.mineToPickUp : mineToPickUp // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$AcceptedArrival {

 String get id; String get number; String get supplierName; String? get summary; String? get acceptedAt;
/// Create a copy of AcceptedArrival
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AcceptedArrivalCopyWith<AcceptedArrival> get copyWith => _$AcceptedArrivalCopyWithImpl<AcceptedArrival>(this as AcceptedArrival, _$identity);

  /// Serializes this AcceptedArrival to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AcceptedArrival;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AcceptedArrival&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.number, _this.number) || other.number == _this.number)&&(identical(other.supplierName, _this.supplierName) || other.supplierName == _this.supplierName)&&(identical(other.summary, _this.summary) || other.summary == _this.summary)&&(identical(other.acceptedAt, _this.acceptedAt) || other.acceptedAt == _this.acceptedAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AcceptedArrival;
  return Object.hash(runtimeType,_this.id,_this.number,_this.supplierName,_this.summary,_this.acceptedAt);
}

@override
String toString() {
  final _this = this as AcceptedArrival;
  return 'AcceptedArrival(id: ${_this.id}, number: ${_this.number}, supplierName: ${_this.supplierName}, summary: ${_this.summary}, acceptedAt: ${_this.acceptedAt})';
}


}

/// @nodoc
abstract mixin class $AcceptedArrivalCopyWith<$Res>  {
  factory $AcceptedArrivalCopyWith(AcceptedArrival value, $Res Function(AcceptedArrival) _then) = _$AcceptedArrivalCopyWithImpl;
@useResult
$Res call({
 String id, String number, String supplierName, String? summary, String? acceptedAt
});




}
/// @nodoc
class _$AcceptedArrivalCopyWithImpl<$Res>
    implements $AcceptedArrivalCopyWith<$Res> {
  _$AcceptedArrivalCopyWithImpl(this._self, this._then);

  final AcceptedArrival _self;
  final $Res Function(AcceptedArrival) _then;

/// Create a copy of AcceptedArrival
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? number = null,Object? supplierName = null,Object? summary = freezed,Object? acceptedAt = freezed,}) {
  return _then(AcceptedArrival(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,supplierName: null == supplierName ? _self.supplierName : supplierName // ignore: cast_nullable_to_non_nullable
as String,summary: freezed == summary ? _self.summary : summary // ignore: cast_nullable_to_non_nullable
as String?,acceptedAt: freezed == acceptedAt ? _self.acceptedAt : acceptedAt // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [AcceptedArrival].
extension AcceptedArrivalPatterns on AcceptedArrival {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AcceptedArrival value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AcceptedArrival() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AcceptedArrival value)  $default,){
final _that = this;
switch (_that) {
case _AcceptedArrival():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AcceptedArrival value)?  $default,){
final _that = this;
switch (_that) {
case _AcceptedArrival() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String number,  String supplierName,  String? summary,  String? acceptedAt)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AcceptedArrival() when $default != null:
return $default(_that.id,_that.number,_that.supplierName,_that.summary,_that.acceptedAt);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String number,  String supplierName,  String? summary,  String? acceptedAt)  $default,) {final _that = this;
switch (_that) {
case _AcceptedArrival():
return $default(_that.id,_that.number,_that.supplierName,_that.summary,_that.acceptedAt);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String number,  String supplierName,  String? summary,  String? acceptedAt)?  $default,) {final _that = this;
switch (_that) {
case _AcceptedArrival() when $default != null:
return $default(_that.id,_that.number,_that.supplierName,_that.summary,_that.acceptedAt);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AcceptedArrival implements AcceptedArrival {
  const _AcceptedArrival({required this.id, required this.number, required this.supplierName, this.summary, this.acceptedAt});
  factory _AcceptedArrival.fromJson(Map<String, dynamic> json) => _$AcceptedArrivalFromJson(json);

@override final  String id;
@override final  String number;
@override final  String supplierName;
@override final  String? summary;
@override final  String? acceptedAt;

/// Create a copy of AcceptedArrival
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AcceptedArrivalCopyWith<_AcceptedArrival> get copyWith => __$AcceptedArrivalCopyWithImpl<_AcceptedArrival>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AcceptedArrivalToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AcceptedArrival&&(identical(other.id, id) || other.id == id)&&(identical(other.number, number) || other.number == number)&&(identical(other.supplierName, supplierName) || other.supplierName == supplierName)&&(identical(other.summary, summary) || other.summary == summary)&&(identical(other.acceptedAt, acceptedAt) || other.acceptedAt == acceptedAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,number,supplierName,summary,acceptedAt);
}

@override
String toString() {
    return 'AcceptedArrival(id: $id, number: $number, supplierName: $supplierName, summary: $summary, acceptedAt: $acceptedAt)';
}


}

/// @nodoc
abstract mixin class _$AcceptedArrivalCopyWith<$Res> implements $AcceptedArrivalCopyWith<$Res> {
  factory _$AcceptedArrivalCopyWith(_AcceptedArrival value, $Res Function(_AcceptedArrival) _then) = __$AcceptedArrivalCopyWithImpl;
@override @useResult
$Res call({
 String id, String number, String supplierName, String? summary, String? acceptedAt
});




}
/// @nodoc
class __$AcceptedArrivalCopyWithImpl<$Res>
    implements _$AcceptedArrivalCopyWith<$Res> {
  __$AcceptedArrivalCopyWithImpl(this._self, this._then);

  final _AcceptedArrival _self;
  final $Res Function(_AcceptedArrival) _then;

/// Create a copy of AcceptedArrival
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? number = null,Object? supplierName = null,Object? summary = freezed,Object? acceptedAt = freezed,}) {
  return _then(_AcceptedArrival(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,supplierName: null == supplierName ? _self.supplierName : supplierName // ignore: cast_nullable_to_non_nullable
as String,summary: freezed == summary ? _self.summary : summary // ignore: cast_nullable_to_non_nullable
as String?,acceptedAt: freezed == acceptedAt ? _self.acceptedAt : acceptedAt // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$ProcurementHome {

 ProcurementRoles get roles; HomeRequests get requests; List<AcceptedArrival> get accepted; int get acceptedCount; double get acceptedAmount;
/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProcurementHomeCopyWith<ProcurementHome> get copyWith => _$ProcurementHomeCopyWithImpl<ProcurementHome>(this as ProcurementHome, _$identity);

  /// Serializes this ProcurementHome to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProcurementHome;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProcurementHome&&(identical(other.roles, _this.roles) || other.roles == _this.roles)&&(identical(other.requests, _this.requests) || other.requests == _this.requests)&&const DeepCollectionEquality().equals(other.accepted, _this.accepted)&&(identical(other.acceptedCount, _this.acceptedCount) || other.acceptedCount == _this.acceptedCount)&&(identical(other.acceptedAmount, _this.acceptedAmount) || other.acceptedAmount == _this.acceptedAmount));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProcurementHome;
  return Object.hash(runtimeType,_this.roles,_this.requests,const DeepCollectionEquality().hash(_this.accepted),_this.acceptedCount,_this.acceptedAmount);
}

@override
String toString() {
  final _this = this as ProcurementHome;
  return 'ProcurementHome(roles: ${_this.roles}, requests: ${_this.requests}, accepted: ${_this.accepted}, acceptedCount: ${_this.acceptedCount}, acceptedAmount: ${_this.acceptedAmount})';
}


}

/// @nodoc
abstract mixin class $ProcurementHomeCopyWith<$Res>  {
  factory $ProcurementHomeCopyWith(ProcurementHome value, $Res Function(ProcurementHome) _then) = _$ProcurementHomeCopyWithImpl;
@useResult
$Res call({
 ProcurementRoles roles, HomeRequests requests, List<AcceptedArrival> accepted, int acceptedCount, double acceptedAmount
});


$ProcurementRolesCopyWith<$Res> get roles;$HomeRequestsCopyWith<$Res> get requests;

}
/// @nodoc
class _$ProcurementHomeCopyWithImpl<$Res>
    implements $ProcurementHomeCopyWith<$Res> {
  _$ProcurementHomeCopyWithImpl(this._self, this._then);

  final ProcurementHome _self;
  final $Res Function(ProcurementHome) _then;

/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? roles = null,Object? requests = null,Object? accepted = null,Object? acceptedCount = null,Object? acceptedAmount = null,}) {
  return _then(ProcurementHome(
roles: null == roles ? _self.roles : roles // ignore: cast_nullable_to_non_nullable
as ProcurementRoles,requests: null == requests ? _self.requests : requests // ignore: cast_nullable_to_non_nullable
as HomeRequests,accepted: null == accepted ? _self.accepted : accepted // ignore: cast_nullable_to_non_nullable
as List<AcceptedArrival>,acceptedCount: null == acceptedCount ? _self.acceptedCount : acceptedCount // ignore: cast_nullable_to_non_nullable
as int,acceptedAmount: null == acceptedAmount ? _self.acceptedAmount : acceptedAmount // ignore: cast_nullable_to_non_nullable
as double,
  ));
}
/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$ProcurementRolesCopyWith<$Res> get roles {
  
  return $ProcurementRolesCopyWith<$Res>(_self.roles, (value) {
    return _then(_self.copyWith(roles: value));
  });
}/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HomeRequestsCopyWith<$Res> get requests {
  
  return $HomeRequestsCopyWith<$Res>(_self.requests, (value) {
    return _then(_self.copyWith(requests: value));
  });
}
}


/// Adds pattern-matching-related methods to [ProcurementHome].
extension ProcurementHomePatterns on ProcurementHome {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProcurementHome value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProcurementHome() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProcurementHome value)  $default,){
final _that = this;
switch (_that) {
case _ProcurementHome():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProcurementHome value)?  $default,){
final _that = this;
switch (_that) {
case _ProcurementHome() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( ProcurementRoles roles,  HomeRequests requests,  List<AcceptedArrival> accepted,  int acceptedCount,  double acceptedAmount)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ProcurementHome() when $default != null:
return $default(_that.roles,_that.requests,_that.accepted,_that.acceptedCount,_that.acceptedAmount);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( ProcurementRoles roles,  HomeRequests requests,  List<AcceptedArrival> accepted,  int acceptedCount,  double acceptedAmount)  $default,) {final _that = this;
switch (_that) {
case _ProcurementHome():
return $default(_that.roles,_that.requests,_that.accepted,_that.acceptedCount,_that.acceptedAmount);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( ProcurementRoles roles,  HomeRequests requests,  List<AcceptedArrival> accepted,  int acceptedCount,  double acceptedAmount)?  $default,) {final _that = this;
switch (_that) {
case _ProcurementHome() when $default != null:
return $default(_that.roles,_that.requests,_that.accepted,_that.acceptedCount,_that.acceptedAmount);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProcurementHome extends ProcurementHome {
  const _ProcurementHome({this.roles = const ProcurementRoles(), this.requests = const HomeRequests(),  List<AcceptedArrival> accepted = const <AcceptedArrival>[], this.acceptedCount = 0, this.acceptedAmount = 0}): _accepted = accepted,super._();
  factory _ProcurementHome.fromJson(Map<String, dynamic> json) => _$ProcurementHomeFromJson(json);

@override@JsonKey() final  ProcurementRoles roles;
@override@JsonKey() final  HomeRequests requests;
 final  List<AcceptedArrival> _accepted;
@override@JsonKey() List<AcceptedArrival> get accepted {
  if (_accepted is EqualUnmodifiableListView) return _accepted;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_accepted);
}

@override@JsonKey() final  int acceptedCount;
@override@JsonKey() final  double acceptedAmount;

/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProcurementHomeCopyWith<_ProcurementHome> get copyWith => __$ProcurementHomeCopyWithImpl<_ProcurementHome>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProcurementHomeToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProcurementHome&&(identical(other.roles, roles) || other.roles == roles)&&(identical(other.requests, requests) || other.requests == requests)&&const DeepCollectionEquality().equals(other.accepted, _accepted)&&(identical(other.acceptedCount, acceptedCount) || other.acceptedCount == acceptedCount)&&(identical(other.acceptedAmount, acceptedAmount) || other.acceptedAmount == acceptedAmount));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,roles,requests,const DeepCollectionEquality().hash(_accepted),acceptedCount,acceptedAmount);
}

@override
String toString() {
    return 'ProcurementHome(roles: $roles, requests: $requests, accepted: $accepted, acceptedCount: $acceptedCount, acceptedAmount: $acceptedAmount)';
}


}

/// @nodoc
abstract mixin class _$ProcurementHomeCopyWith<$Res> implements $ProcurementHomeCopyWith<$Res> {
  factory _$ProcurementHomeCopyWith(_ProcurementHome value, $Res Function(_ProcurementHome) _then) = __$ProcurementHomeCopyWithImpl;
@override @useResult
$Res call({
 ProcurementRoles roles, HomeRequests requests, List<AcceptedArrival> accepted, int acceptedCount, double acceptedAmount
});


@override $ProcurementRolesCopyWith<$Res> get roles;@override $HomeRequestsCopyWith<$Res> get requests;

}
/// @nodoc
class __$ProcurementHomeCopyWithImpl<$Res>
    implements _$ProcurementHomeCopyWith<$Res> {
  __$ProcurementHomeCopyWithImpl(this._self, this._then);

  final _ProcurementHome _self;
  final $Res Function(_ProcurementHome) _then;

/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? roles = null,Object? requests = null,Object? accepted = null,Object? acceptedCount = null,Object? acceptedAmount = null,}) {
  return _then(_ProcurementHome(
roles: null == roles ? _self.roles : roles // ignore: cast_nullable_to_non_nullable
as ProcurementRoles,requests: null == requests ? _self.requests : requests // ignore: cast_nullable_to_non_nullable
as HomeRequests,accepted: null == accepted ? _self._accepted : accepted // ignore: cast_nullable_to_non_nullable
as List<AcceptedArrival>,acceptedCount: null == acceptedCount ? _self.acceptedCount : acceptedCount // ignore: cast_nullable_to_non_nullable
as int,acceptedAmount: null == acceptedAmount ? _self.acceptedAmount : acceptedAmount // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$ProcurementRolesCopyWith<$Res> get roles {
  
  return $ProcurementRolesCopyWith<$Res>(_self.roles, (value) {
    return _then(_self.copyWith(roles: value));
  });
}/// Create a copy of ProcurementHome
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HomeRequestsCopyWith<$Res> get requests {
  
  return $HomeRequestsCopyWith<$Res>(_self.requests, (value) {
    return _then(_self.copyWith(requests: value));
  });
}
}

// dart format on
