// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'hr_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$HrAttention {

 String get employeeId; String get employee; String? get division;/// absent | late | unknown
 String get kind; String? get firstIn; int? get lateMinutes;
/// Create a copy of HrAttention
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrAttentionCopyWith<HrAttention> get copyWith => _$HrAttentionCopyWithImpl<HrAttention>(this as HrAttention, _$identity);

  /// Serializes this HrAttention to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrAttention;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrAttention&&(identical(other.employeeId, _this.employeeId) || other.employeeId == _this.employeeId)&&(identical(other.employee, _this.employee) || other.employee == _this.employee)&&(identical(other.division, _this.division) || other.division == _this.division)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.firstIn, _this.firstIn) || other.firstIn == _this.firstIn)&&(identical(other.lateMinutes, _this.lateMinutes) || other.lateMinutes == _this.lateMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrAttention;
  return Object.hash(runtimeType,_this.employeeId,_this.employee,_this.division,_this.kind,_this.firstIn,_this.lateMinutes);
}

@override
String toString() {
  final _this = this as HrAttention;
  return 'HrAttention(employeeId: ${_this.employeeId}, employee: ${_this.employee}, division: ${_this.division}, kind: ${_this.kind}, firstIn: ${_this.firstIn}, lateMinutes: ${_this.lateMinutes})';
}


}

/// @nodoc
abstract mixin class $HrAttentionCopyWith<$Res>  {
  factory $HrAttentionCopyWith(HrAttention value, $Res Function(HrAttention) _then) = _$HrAttentionCopyWithImpl;
@useResult
$Res call({
 String employeeId, String employee, String? division, String kind, String? firstIn, int? lateMinutes
});




}
/// @nodoc
class _$HrAttentionCopyWithImpl<$Res>
    implements $HrAttentionCopyWith<$Res> {
  _$HrAttentionCopyWithImpl(this._self, this._then);

  final HrAttention _self;
  final $Res Function(HrAttention) _then;

/// Create a copy of HrAttention
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? employeeId = null,Object? employee = null,Object? division = freezed,Object? kind = null,Object? firstIn = freezed,Object? lateMinutes = freezed,}) {
  return _then(HrAttention(
employeeId: null == employeeId ? _self.employeeId : employeeId // ignore: cast_nullable_to_non_nullable
as String,employee: null == employee ? _self.employee : employee // ignore: cast_nullable_to_non_nullable
as String,division: freezed == division ? _self.division : division // ignore: cast_nullable_to_non_nullable
as String?,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,firstIn: freezed == firstIn ? _self.firstIn : firstIn // ignore: cast_nullable_to_non_nullable
as String?,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,
  ));
}

}


/// Adds pattern-matching-related methods to [HrAttention].
extension HrAttentionPatterns on HrAttention {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrAttention value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrAttention() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrAttention value)  $default,){
final _that = this;
switch (_that) {
case _HrAttention():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrAttention value)?  $default,){
final _that = this;
switch (_that) {
case _HrAttention() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String employeeId,  String employee,  String? division,  String kind,  String? firstIn,  int? lateMinutes)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrAttention() when $default != null:
return $default(_that.employeeId,_that.employee,_that.division,_that.kind,_that.firstIn,_that.lateMinutes);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String employeeId,  String employee,  String? division,  String kind,  String? firstIn,  int? lateMinutes)  $default,) {final _that = this;
switch (_that) {
case _HrAttention():
return $default(_that.employeeId,_that.employee,_that.division,_that.kind,_that.firstIn,_that.lateMinutes);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String employeeId,  String employee,  String? division,  String kind,  String? firstIn,  int? lateMinutes)?  $default,) {final _that = this;
switch (_that) {
case _HrAttention() when $default != null:
return $default(_that.employeeId,_that.employee,_that.division,_that.kind,_that.firstIn,_that.lateMinutes);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrAttention implements HrAttention {
  const _HrAttention({required this.employeeId, required this.employee, this.division, required this.kind, this.firstIn, this.lateMinutes});
  factory _HrAttention.fromJson(Map<String, dynamic> json) => _$HrAttentionFromJson(json);

@override final  String employeeId;
@override final  String employee;
@override final  String? division;
/// absent | late | unknown
@override final  String kind;
@override final  String? firstIn;
@override final  int? lateMinutes;

/// Create a copy of HrAttention
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrAttentionCopyWith<_HrAttention> get copyWith => __$HrAttentionCopyWithImpl<_HrAttention>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrAttentionToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrAttention&&(identical(other.employeeId, employeeId) || other.employeeId == employeeId)&&(identical(other.employee, employee) || other.employee == employee)&&(identical(other.division, division) || other.division == division)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.firstIn, firstIn) || other.firstIn == firstIn)&&(identical(other.lateMinutes, lateMinutes) || other.lateMinutes == lateMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,employeeId,employee,division,kind,firstIn,lateMinutes);
}

@override
String toString() {
    return 'HrAttention(employeeId: $employeeId, employee: $employee, division: $division, kind: $kind, firstIn: $firstIn, lateMinutes: $lateMinutes)';
}


}

/// @nodoc
abstract mixin class _$HrAttentionCopyWith<$Res> implements $HrAttentionCopyWith<$Res> {
  factory _$HrAttentionCopyWith(_HrAttention value, $Res Function(_HrAttention) _then) = __$HrAttentionCopyWithImpl;
@override @useResult
$Res call({
 String employeeId, String employee, String? division, String kind, String? firstIn, int? lateMinutes
});




}
/// @nodoc
class __$HrAttentionCopyWithImpl<$Res>
    implements _$HrAttentionCopyWith<$Res> {
  __$HrAttentionCopyWithImpl(this._self, this._then);

  final _HrAttention _self;
  final $Res Function(_HrAttention) _then;

/// Create a copy of HrAttention
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? employeeId = null,Object? employee = null,Object? division = freezed,Object? kind = null,Object? firstIn = freezed,Object? lateMinutes = freezed,}) {
  return _then(_HrAttention(
employeeId: null == employeeId ? _self.employeeId : employeeId // ignore: cast_nullable_to_non_nullable
as String,employee: null == employee ? _self.employee : employee // ignore: cast_nullable_to_non_nullable
as String,division: freezed == division ? _self.division : division // ignore: cast_nullable_to_non_nullable
as String?,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,firstIn: freezed == firstIn ? _self.firstIn : firstIn // ignore: cast_nullable_to_non_nullable
as String?,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,
  ));
}


}


/// @nodoc
mixin _$HrDivisionDay {

 int? get id; String get name; int get active; int get scheduled; int get present; int get late; int get absent;
/// Create a copy of HrDivisionDay
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrDivisionDayCopyWith<HrDivisionDay> get copyWith => _$HrDivisionDayCopyWithImpl<HrDivisionDay>(this as HrDivisionDay, _$identity);

  /// Serializes this HrDivisionDay to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrDivisionDay;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrDivisionDay&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.active, _this.active) || other.active == _this.active)&&(identical(other.scheduled, _this.scheduled) || other.scheduled == _this.scheduled)&&(identical(other.present, _this.present) || other.present == _this.present)&&(identical(other.late, _this.late) || other.late == _this.late)&&(identical(other.absent, _this.absent) || other.absent == _this.absent));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrDivisionDay;
  return Object.hash(runtimeType,_this.id,_this.name,_this.active,_this.scheduled,_this.present,_this.late,_this.absent);
}

@override
String toString() {
  final _this = this as HrDivisionDay;
  return 'HrDivisionDay(id: ${_this.id}, name: ${_this.name}, active: ${_this.active}, scheduled: ${_this.scheduled}, present: ${_this.present}, late: ${_this.late}, absent: ${_this.absent})';
}


}

/// @nodoc
abstract mixin class $HrDivisionDayCopyWith<$Res>  {
  factory $HrDivisionDayCopyWith(HrDivisionDay value, $Res Function(HrDivisionDay) _then) = _$HrDivisionDayCopyWithImpl;
@useResult
$Res call({
 int? id, String name, int active, int scheduled, int present, int late, int absent
});




}
/// @nodoc
class _$HrDivisionDayCopyWithImpl<$Res>
    implements $HrDivisionDayCopyWith<$Res> {
  _$HrDivisionDayCopyWithImpl(this._self, this._then);

  final HrDivisionDay _self;
  final $Res Function(HrDivisionDay) _then;

/// Create a copy of HrDivisionDay
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = freezed,Object? name = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,}) {
  return _then(HrDivisionDay(
id: freezed == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as int?,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,active: null == active ? _self.active : active // ignore: cast_nullable_to_non_nullable
as int,scheduled: null == scheduled ? _self.scheduled : scheduled // ignore: cast_nullable_to_non_nullable
as int,present: null == present ? _self.present : present // ignore: cast_nullable_to_non_nullable
as int,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as int,absent: null == absent ? _self.absent : absent // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [HrDivisionDay].
extension HrDivisionDayPatterns on HrDivisionDay {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrDivisionDay value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrDivisionDay() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrDivisionDay value)  $default,){
final _that = this;
switch (_that) {
case _HrDivisionDay():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrDivisionDay value)?  $default,){
final _that = this;
switch (_that) {
case _HrDivisionDay() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int? id,  String name,  int active,  int scheduled,  int present,  int late,  int absent)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrDivisionDay() when $default != null:
return $default(_that.id,_that.name,_that.active,_that.scheduled,_that.present,_that.late,_that.absent);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int? id,  String name,  int active,  int scheduled,  int present,  int late,  int absent)  $default,) {final _that = this;
switch (_that) {
case _HrDivisionDay():
return $default(_that.id,_that.name,_that.active,_that.scheduled,_that.present,_that.late,_that.absent);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int? id,  String name,  int active,  int scheduled,  int present,  int late,  int absent)?  $default,) {final _that = this;
switch (_that) {
case _HrDivisionDay() when $default != null:
return $default(_that.id,_that.name,_that.active,_that.scheduled,_that.present,_that.late,_that.absent);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrDivisionDay implements HrDivisionDay {
  const _HrDivisionDay({this.id, required this.name, this.active = 0, this.scheduled = 0, this.present = 0, this.late = 0, this.absent = 0});
  factory _HrDivisionDay.fromJson(Map<String, dynamic> json) => _$HrDivisionDayFromJson(json);

@override final  int? id;
@override final  String name;
@override@JsonKey() final  int active;
@override@JsonKey() final  int scheduled;
@override@JsonKey() final  int present;
@override@JsonKey() final  int late;
@override@JsonKey() final  int absent;

/// Create a copy of HrDivisionDay
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrDivisionDayCopyWith<_HrDivisionDay> get copyWith => __$HrDivisionDayCopyWithImpl<_HrDivisionDay>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrDivisionDayToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrDivisionDay&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.active, active) || other.active == active)&&(identical(other.scheduled, scheduled) || other.scheduled == scheduled)&&(identical(other.present, present) || other.present == present)&&(identical(other.late, late) || other.late == late)&&(identical(other.absent, absent) || other.absent == absent));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name,active,scheduled,present,late,absent);
}

@override
String toString() {
    return 'HrDivisionDay(id: $id, name: $name, active: $active, scheduled: $scheduled, present: $present, late: $late, absent: $absent)';
}


}

/// @nodoc
abstract mixin class _$HrDivisionDayCopyWith<$Res> implements $HrDivisionDayCopyWith<$Res> {
  factory _$HrDivisionDayCopyWith(_HrDivisionDay value, $Res Function(_HrDivisionDay) _then) = __$HrDivisionDayCopyWithImpl;
@override @useResult
$Res call({
 int? id, String name, int active, int scheduled, int present, int late, int absent
});




}
/// @nodoc
class __$HrDivisionDayCopyWithImpl<$Res>
    implements _$HrDivisionDayCopyWith<$Res> {
  __$HrDivisionDayCopyWithImpl(this._self, this._then);

  final _HrDivisionDay _self;
  final $Res Function(_HrDivisionDay) _then;

/// Create a copy of HrDivisionDay
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = freezed,Object? name = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,}) {
  return _then(_HrDivisionDay(
id: freezed == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as int?,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,active: null == active ? _self.active : active // ignore: cast_nullable_to_non_nullable
as int,scheduled: null == scheduled ? _self.scheduled : scheduled // ignore: cast_nullable_to_non_nullable
as int,present: null == present ? _self.present : present // ignore: cast_nullable_to_non_nullable
as int,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as int,absent: null == absent ? _self.absent : absent // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$HrDay {

 String get date; int get active; int get scheduled; int get present; int get late; int get absent; int get onLeave; int get expected; int get unknown; String? get avgArrival; List<int> get passesByHour; List<HrAttention> get attention; int get attentionTotal; List<HrDivisionDay> get divisions;
/// Create a copy of HrDay
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrDayCopyWith<HrDay> get copyWith => _$HrDayCopyWithImpl<HrDay>(this as HrDay, _$identity);

  /// Serializes this HrDay to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrDay;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrDay&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.active, _this.active) || other.active == _this.active)&&(identical(other.scheduled, _this.scheduled) || other.scheduled == _this.scheduled)&&(identical(other.present, _this.present) || other.present == _this.present)&&(identical(other.late, _this.late) || other.late == _this.late)&&(identical(other.absent, _this.absent) || other.absent == _this.absent)&&(identical(other.onLeave, _this.onLeave) || other.onLeave == _this.onLeave)&&(identical(other.expected, _this.expected) || other.expected == _this.expected)&&(identical(other.unknown, _this.unknown) || other.unknown == _this.unknown)&&(identical(other.avgArrival, _this.avgArrival) || other.avgArrival == _this.avgArrival)&&const DeepCollectionEquality().equals(other.passesByHour, _this.passesByHour)&&const DeepCollectionEquality().equals(other.attention, _this.attention)&&(identical(other.attentionTotal, _this.attentionTotal) || other.attentionTotal == _this.attentionTotal)&&const DeepCollectionEquality().equals(other.divisions, _this.divisions));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrDay;
  return Object.hash(runtimeType,_this.date,_this.active,_this.scheduled,_this.present,_this.late,_this.absent,_this.onLeave,_this.expected,_this.unknown,_this.avgArrival,const DeepCollectionEquality().hash(_this.passesByHour),const DeepCollectionEquality().hash(_this.attention),_this.attentionTotal,const DeepCollectionEquality().hash(_this.divisions));
}

@override
String toString() {
  final _this = this as HrDay;
  return 'HrDay(date: ${_this.date}, active: ${_this.active}, scheduled: ${_this.scheduled}, present: ${_this.present}, late: ${_this.late}, absent: ${_this.absent}, onLeave: ${_this.onLeave}, expected: ${_this.expected}, unknown: ${_this.unknown}, avgArrival: ${_this.avgArrival}, passesByHour: ${_this.passesByHour}, attention: ${_this.attention}, attentionTotal: ${_this.attentionTotal}, divisions: ${_this.divisions})';
}


}

/// @nodoc
abstract mixin class $HrDayCopyWith<$Res>  {
  factory $HrDayCopyWith(HrDay value, $Res Function(HrDay) _then) = _$HrDayCopyWithImpl;
@useResult
$Res call({
 String date, int active, int scheduled, int present, int late, int absent, int onLeave, int expected, int unknown, String? avgArrival, List<int> passesByHour, List<HrAttention> attention, int attentionTotal, List<HrDivisionDay> divisions
});




}
/// @nodoc
class _$HrDayCopyWithImpl<$Res>
    implements $HrDayCopyWith<$Res> {
  _$HrDayCopyWithImpl(this._self, this._then);

  final HrDay _self;
  final $Res Function(HrDay) _then;

/// Create a copy of HrDay
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? date = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,Object? onLeave = null,Object? expected = null,Object? unknown = null,Object? avgArrival = freezed,Object? passesByHour = null,Object? attention = null,Object? attentionTotal = null,Object? divisions = null,}) {
  return _then(HrDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,active: null == active ? _self.active : active // ignore: cast_nullable_to_non_nullable
as int,scheduled: null == scheduled ? _self.scheduled : scheduled // ignore: cast_nullable_to_non_nullable
as int,present: null == present ? _self.present : present // ignore: cast_nullable_to_non_nullable
as int,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as int,absent: null == absent ? _self.absent : absent // ignore: cast_nullable_to_non_nullable
as int,onLeave: null == onLeave ? _self.onLeave : onLeave // ignore: cast_nullable_to_non_nullable
as int,expected: null == expected ? _self.expected : expected // ignore: cast_nullable_to_non_nullable
as int,unknown: null == unknown ? _self.unknown : unknown // ignore: cast_nullable_to_non_nullable
as int,avgArrival: freezed == avgArrival ? _self.avgArrival : avgArrival // ignore: cast_nullable_to_non_nullable
as String?,passesByHour: null == passesByHour ? _self.passesByHour : passesByHour // ignore: cast_nullable_to_non_nullable
as List<int>,attention: null == attention ? _self.attention : attention // ignore: cast_nullable_to_non_nullable
as List<HrAttention>,attentionTotal: null == attentionTotal ? _self.attentionTotal : attentionTotal // ignore: cast_nullable_to_non_nullable
as int,divisions: null == divisions ? _self.divisions : divisions // ignore: cast_nullable_to_non_nullable
as List<HrDivisionDay>,
  ));
}

}


/// Adds pattern-matching-related methods to [HrDay].
extension HrDayPatterns on HrDay {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrDay value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrDay() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrDay value)  $default,){
final _that = this;
switch (_that) {
case _HrDay():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrDay value)?  $default,){
final _that = this;
switch (_that) {
case _HrDay() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String date,  int active,  int scheduled,  int present,  int late,  int absent,  int onLeave,  int expected,  int unknown,  String? avgArrival,  List<int> passesByHour,  List<HrAttention> attention,  int attentionTotal,  List<HrDivisionDay> divisions)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrDay() when $default != null:
return $default(_that.date,_that.active,_that.scheduled,_that.present,_that.late,_that.absent,_that.onLeave,_that.expected,_that.unknown,_that.avgArrival,_that.passesByHour,_that.attention,_that.attentionTotal,_that.divisions);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String date,  int active,  int scheduled,  int present,  int late,  int absent,  int onLeave,  int expected,  int unknown,  String? avgArrival,  List<int> passesByHour,  List<HrAttention> attention,  int attentionTotal,  List<HrDivisionDay> divisions)  $default,) {final _that = this;
switch (_that) {
case _HrDay():
return $default(_that.date,_that.active,_that.scheduled,_that.present,_that.late,_that.absent,_that.onLeave,_that.expected,_that.unknown,_that.avgArrival,_that.passesByHour,_that.attention,_that.attentionTotal,_that.divisions);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String date,  int active,  int scheduled,  int present,  int late,  int absent,  int onLeave,  int expected,  int unknown,  String? avgArrival,  List<int> passesByHour,  List<HrAttention> attention,  int attentionTotal,  List<HrDivisionDay> divisions)?  $default,) {final _that = this;
switch (_that) {
case _HrDay() when $default != null:
return $default(_that.date,_that.active,_that.scheduled,_that.present,_that.late,_that.absent,_that.onLeave,_that.expected,_that.unknown,_that.avgArrival,_that.passesByHour,_that.attention,_that.attentionTotal,_that.divisions);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrDay implements HrDay {
  const _HrDay({required this.date, this.active = 0, this.scheduled = 0, this.present = 0, this.late = 0, this.absent = 0, this.onLeave = 0, this.expected = 0, this.unknown = 0, this.avgArrival,  List<int> passesByHour = const <int>[],  List<HrAttention> attention = const <HrAttention>[], this.attentionTotal = 0,  List<HrDivisionDay> divisions = const <HrDivisionDay>[]}): _passesByHour = passesByHour,_attention = attention,_divisions = divisions;
  factory _HrDay.fromJson(Map<String, dynamic> json) => _$HrDayFromJson(json);

@override final  String date;
@override@JsonKey() final  int active;
@override@JsonKey() final  int scheduled;
@override@JsonKey() final  int present;
@override@JsonKey() final  int late;
@override@JsonKey() final  int absent;
@override@JsonKey() final  int onLeave;
@override@JsonKey() final  int expected;
@override@JsonKey() final  int unknown;
@override final  String? avgArrival;
 final  List<int> _passesByHour;
@override@JsonKey() List<int> get passesByHour {
  if (_passesByHour is EqualUnmodifiableListView) return _passesByHour;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_passesByHour);
}

 final  List<HrAttention> _attention;
@override@JsonKey() List<HrAttention> get attention {
  if (_attention is EqualUnmodifiableListView) return _attention;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_attention);
}

@override@JsonKey() final  int attentionTotal;
 final  List<HrDivisionDay> _divisions;
@override@JsonKey() List<HrDivisionDay> get divisions {
  if (_divisions is EqualUnmodifiableListView) return _divisions;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_divisions);
}


/// Create a copy of HrDay
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrDayCopyWith<_HrDay> get copyWith => __$HrDayCopyWithImpl<_HrDay>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrDayToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrDay&&(identical(other.date, date) || other.date == date)&&(identical(other.active, active) || other.active == active)&&(identical(other.scheduled, scheduled) || other.scheduled == scheduled)&&(identical(other.present, present) || other.present == present)&&(identical(other.late, late) || other.late == late)&&(identical(other.absent, absent) || other.absent == absent)&&(identical(other.onLeave, onLeave) || other.onLeave == onLeave)&&(identical(other.expected, expected) || other.expected == expected)&&(identical(other.unknown, unknown) || other.unknown == unknown)&&(identical(other.avgArrival, avgArrival) || other.avgArrival == avgArrival)&&const DeepCollectionEquality().equals(other.passesByHour, _passesByHour)&&const DeepCollectionEquality().equals(other.attention, _attention)&&(identical(other.attentionTotal, attentionTotal) || other.attentionTotal == attentionTotal)&&const DeepCollectionEquality().equals(other.divisions, _divisions));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,date,active,scheduled,present,late,absent,onLeave,expected,unknown,avgArrival,const DeepCollectionEquality().hash(_passesByHour),const DeepCollectionEquality().hash(_attention),attentionTotal,const DeepCollectionEquality().hash(_divisions));
}

@override
String toString() {
    return 'HrDay(date: $date, active: $active, scheduled: $scheduled, present: $present, late: $late, absent: $absent, onLeave: $onLeave, expected: $expected, unknown: $unknown, avgArrival: $avgArrival, passesByHour: $passesByHour, attention: $attention, attentionTotal: $attentionTotal, divisions: $divisions)';
}


}

/// @nodoc
abstract mixin class _$HrDayCopyWith<$Res> implements $HrDayCopyWith<$Res> {
  factory _$HrDayCopyWith(_HrDay value, $Res Function(_HrDay) _then) = __$HrDayCopyWithImpl;
@override @useResult
$Res call({
 String date, int active, int scheduled, int present, int late, int absent, int onLeave, int expected, int unknown, String? avgArrival, List<int> passesByHour, List<HrAttention> attention, int attentionTotal, List<HrDivisionDay> divisions
});




}
/// @nodoc
class __$HrDayCopyWithImpl<$Res>
    implements _$HrDayCopyWith<$Res> {
  __$HrDayCopyWithImpl(this._self, this._then);

  final _HrDay _self;
  final $Res Function(_HrDay) _then;

/// Create a copy of HrDay
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? date = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,Object? onLeave = null,Object? expected = null,Object? unknown = null,Object? avgArrival = freezed,Object? passesByHour = null,Object? attention = null,Object? attentionTotal = null,Object? divisions = null,}) {
  return _then(_HrDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,active: null == active ? _self.active : active // ignore: cast_nullable_to_non_nullable
as int,scheduled: null == scheduled ? _self.scheduled : scheduled // ignore: cast_nullable_to_non_nullable
as int,present: null == present ? _self.present : present // ignore: cast_nullable_to_non_nullable
as int,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as int,absent: null == absent ? _self.absent : absent // ignore: cast_nullable_to_non_nullable
as int,onLeave: null == onLeave ? _self.onLeave : onLeave // ignore: cast_nullable_to_non_nullable
as int,expected: null == expected ? _self.expected : expected // ignore: cast_nullable_to_non_nullable
as int,unknown: null == unknown ? _self.unknown : unknown // ignore: cast_nullable_to_non_nullable
as int,avgArrival: freezed == avgArrival ? _self.avgArrival : avgArrival // ignore: cast_nullable_to_non_nullable
as String?,passesByHour: null == passesByHour ? _self._passesByHour : passesByHour // ignore: cast_nullable_to_non_nullable
as List<int>,attention: null == attention ? _self._attention : attention // ignore: cast_nullable_to_non_nullable
as List<HrAttention>,attentionTotal: null == attentionTotal ? _self.attentionTotal : attentionTotal // ignore: cast_nullable_to_non_nullable
as int,divisions: null == divisions ? _self._divisions : divisions // ignore: cast_nullable_to_non_nullable
as List<HrDivisionDay>,
  ));
}


}


/// @nodoc
mixin _$HrEmployeeRow {

 String get id; int get verifixId; String get fullName;/// W — работает, D — уволен, U — не принят.
 String get status; String? get division; String? get job; String? get schedule; String? get lastEventAt;/// Present, Absent, OnLeave, DayOff, Expected, NoSchedule, Unknown, NotEmployed
 String? get today; bool get todayLate;
/// Create a copy of HrEmployeeRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrEmployeeRowCopyWith<HrEmployeeRow> get copyWith => _$HrEmployeeRowCopyWithImpl<HrEmployeeRow>(this as HrEmployeeRow, _$identity);

  /// Serializes this HrEmployeeRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrEmployeeRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrEmployeeRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.verifixId, _this.verifixId) || other.verifixId == _this.verifixId)&&(identical(other.fullName, _this.fullName) || other.fullName == _this.fullName)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.division, _this.division) || other.division == _this.division)&&(identical(other.job, _this.job) || other.job == _this.job)&&(identical(other.schedule, _this.schedule) || other.schedule == _this.schedule)&&(identical(other.lastEventAt, _this.lastEventAt) || other.lastEventAt == _this.lastEventAt)&&(identical(other.today, _this.today) || other.today == _this.today)&&(identical(other.todayLate, _this.todayLate) || other.todayLate == _this.todayLate));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrEmployeeRow;
  return Object.hash(runtimeType,_this.id,_this.verifixId,_this.fullName,_this.status,_this.division,_this.job,_this.schedule,_this.lastEventAt,_this.today,_this.todayLate);
}

@override
String toString() {
  final _this = this as HrEmployeeRow;
  return 'HrEmployeeRow(id: ${_this.id}, verifixId: ${_this.verifixId}, fullName: ${_this.fullName}, status: ${_this.status}, division: ${_this.division}, job: ${_this.job}, schedule: ${_this.schedule}, lastEventAt: ${_this.lastEventAt}, today: ${_this.today}, todayLate: ${_this.todayLate})';
}


}

/// @nodoc
abstract mixin class $HrEmployeeRowCopyWith<$Res>  {
  factory $HrEmployeeRowCopyWith(HrEmployeeRow value, $Res Function(HrEmployeeRow) _then) = _$HrEmployeeRowCopyWithImpl;
@useResult
$Res call({
 String id, int verifixId, String fullName, String status, String? division, String? job, String? schedule, String? lastEventAt, String? today, bool todayLate
});




}
/// @nodoc
class _$HrEmployeeRowCopyWithImpl<$Res>
    implements $HrEmployeeRowCopyWith<$Res> {
  _$HrEmployeeRowCopyWithImpl(this._self, this._then);

  final HrEmployeeRow _self;
  final $Res Function(HrEmployeeRow) _then;

/// Create a copy of HrEmployeeRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? verifixId = null,Object? fullName = null,Object? status = null,Object? division = freezed,Object? job = freezed,Object? schedule = freezed,Object? lastEventAt = freezed,Object? today = freezed,Object? todayLate = null,}) {
  return _then(HrEmployeeRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,verifixId: null == verifixId ? _self.verifixId : verifixId // ignore: cast_nullable_to_non_nullable
as int,fullName: null == fullName ? _self.fullName : fullName // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,division: freezed == division ? _self.division : division // ignore: cast_nullable_to_non_nullable
as String?,job: freezed == job ? _self.job : job // ignore: cast_nullable_to_non_nullable
as String?,schedule: freezed == schedule ? _self.schedule : schedule // ignore: cast_nullable_to_non_nullable
as String?,lastEventAt: freezed == lastEventAt ? _self.lastEventAt : lastEventAt // ignore: cast_nullable_to_non_nullable
as String?,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as String?,todayLate: null == todayLate ? _self.todayLate : todayLate // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [HrEmployeeRow].
extension HrEmployeeRowPatterns on HrEmployeeRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrEmployeeRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrEmployeeRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrEmployeeRow value)  $default,){
final _that = this;
switch (_that) {
case _HrEmployeeRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrEmployeeRow value)?  $default,){
final _that = this;
switch (_that) {
case _HrEmployeeRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  int verifixId,  String fullName,  String status,  String? division,  String? job,  String? schedule,  String? lastEventAt,  String? today,  bool todayLate)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrEmployeeRow() when $default != null:
return $default(_that.id,_that.verifixId,_that.fullName,_that.status,_that.division,_that.job,_that.schedule,_that.lastEventAt,_that.today,_that.todayLate);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  int verifixId,  String fullName,  String status,  String? division,  String? job,  String? schedule,  String? lastEventAt,  String? today,  bool todayLate)  $default,) {final _that = this;
switch (_that) {
case _HrEmployeeRow():
return $default(_that.id,_that.verifixId,_that.fullName,_that.status,_that.division,_that.job,_that.schedule,_that.lastEventAt,_that.today,_that.todayLate);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  int verifixId,  String fullName,  String status,  String? division,  String? job,  String? schedule,  String? lastEventAt,  String? today,  bool todayLate)?  $default,) {final _that = this;
switch (_that) {
case _HrEmployeeRow() when $default != null:
return $default(_that.id,_that.verifixId,_that.fullName,_that.status,_that.division,_that.job,_that.schedule,_that.lastEventAt,_that.today,_that.todayLate);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrEmployeeRow implements HrEmployeeRow {
  const _HrEmployeeRow({required this.id, this.verifixId = 0, required this.fullName, this.status = 'W', this.division, this.job, this.schedule, this.lastEventAt, this.today, this.todayLate = false});
  factory _HrEmployeeRow.fromJson(Map<String, dynamic> json) => _$HrEmployeeRowFromJson(json);

@override final  String id;
@override@JsonKey() final  int verifixId;
@override final  String fullName;
/// W — работает, D — уволен, U — не принят.
@override@JsonKey() final  String status;
@override final  String? division;
@override final  String? job;
@override final  String? schedule;
@override final  String? lastEventAt;
/// Present, Absent, OnLeave, DayOff, Expected, NoSchedule, Unknown, NotEmployed
@override final  String? today;
@override@JsonKey() final  bool todayLate;

/// Create a copy of HrEmployeeRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrEmployeeRowCopyWith<_HrEmployeeRow> get copyWith => __$HrEmployeeRowCopyWithImpl<_HrEmployeeRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrEmployeeRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrEmployeeRow&&(identical(other.id, id) || other.id == id)&&(identical(other.verifixId, verifixId) || other.verifixId == verifixId)&&(identical(other.fullName, fullName) || other.fullName == fullName)&&(identical(other.status, status) || other.status == status)&&(identical(other.division, division) || other.division == division)&&(identical(other.job, job) || other.job == job)&&(identical(other.schedule, schedule) || other.schedule == schedule)&&(identical(other.lastEventAt, lastEventAt) || other.lastEventAt == lastEventAt)&&(identical(other.today, today) || other.today == today)&&(identical(other.todayLate, todayLate) || other.todayLate == todayLate));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,verifixId,fullName,status,division,job,schedule,lastEventAt,today,todayLate);
}

@override
String toString() {
    return 'HrEmployeeRow(id: $id, verifixId: $verifixId, fullName: $fullName, status: $status, division: $division, job: $job, schedule: $schedule, lastEventAt: $lastEventAt, today: $today, todayLate: $todayLate)';
}


}

/// @nodoc
abstract mixin class _$HrEmployeeRowCopyWith<$Res> implements $HrEmployeeRowCopyWith<$Res> {
  factory _$HrEmployeeRowCopyWith(_HrEmployeeRow value, $Res Function(_HrEmployeeRow) _then) = __$HrEmployeeRowCopyWithImpl;
@override @useResult
$Res call({
 String id, int verifixId, String fullName, String status, String? division, String? job, String? schedule, String? lastEventAt, String? today, bool todayLate
});




}
/// @nodoc
class __$HrEmployeeRowCopyWithImpl<$Res>
    implements _$HrEmployeeRowCopyWith<$Res> {
  __$HrEmployeeRowCopyWithImpl(this._self, this._then);

  final _HrEmployeeRow _self;
  final $Res Function(_HrEmployeeRow) _then;

/// Create a copy of HrEmployeeRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? verifixId = null,Object? fullName = null,Object? status = null,Object? division = freezed,Object? job = freezed,Object? schedule = freezed,Object? lastEventAt = freezed,Object? today = freezed,Object? todayLate = null,}) {
  return _then(_HrEmployeeRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,verifixId: null == verifixId ? _self.verifixId : verifixId // ignore: cast_nullable_to_non_nullable
as int,fullName: null == fullName ? _self.fullName : fullName // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,division: freezed == division ? _self.division : division // ignore: cast_nullable_to_non_nullable
as String?,job: freezed == job ? _self.job : job // ignore: cast_nullable_to_non_nullable
as String?,schedule: freezed == schedule ? _self.schedule : schedule // ignore: cast_nullable_to_non_nullable
as String?,lastEventAt: freezed == lastEventAt ? _self.lastEventAt : lastEventAt // ignore: cast_nullable_to_non_nullable
as String?,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as String?,todayLate: null == todayLate ? _self.todayLate : todayLate // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$HrDayResult {

 String get date; String get status; String? get planBegin; String? get planEnd; String? get firstIn; String? get lastOut; bool get late; int? get lateMinutes; bool get earlyLeave; int? get workedMinutes; String? get leaveName; String? get reason;
/// Create a copy of HrDayResult
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrDayResultCopyWith<HrDayResult> get copyWith => _$HrDayResultCopyWithImpl<HrDayResult>(this as HrDayResult, _$identity);

  /// Serializes this HrDayResult to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrDayResult;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrDayResult&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.planBegin, _this.planBegin) || other.planBegin == _this.planBegin)&&(identical(other.planEnd, _this.planEnd) || other.planEnd == _this.planEnd)&&(identical(other.firstIn, _this.firstIn) || other.firstIn == _this.firstIn)&&(identical(other.lastOut, _this.lastOut) || other.lastOut == _this.lastOut)&&(identical(other.late, _this.late) || other.late == _this.late)&&(identical(other.lateMinutes, _this.lateMinutes) || other.lateMinutes == _this.lateMinutes)&&(identical(other.earlyLeave, _this.earlyLeave) || other.earlyLeave == _this.earlyLeave)&&(identical(other.workedMinutes, _this.workedMinutes) || other.workedMinutes == _this.workedMinutes)&&(identical(other.leaveName, _this.leaveName) || other.leaveName == _this.leaveName)&&(identical(other.reason, _this.reason) || other.reason == _this.reason));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrDayResult;
  return Object.hash(runtimeType,_this.date,_this.status,_this.planBegin,_this.planEnd,_this.firstIn,_this.lastOut,_this.late,_this.lateMinutes,_this.earlyLeave,_this.workedMinutes,_this.leaveName,_this.reason);
}

@override
String toString() {
  final _this = this as HrDayResult;
  return 'HrDayResult(date: ${_this.date}, status: ${_this.status}, planBegin: ${_this.planBegin}, planEnd: ${_this.planEnd}, firstIn: ${_this.firstIn}, lastOut: ${_this.lastOut}, late: ${_this.late}, lateMinutes: ${_this.lateMinutes}, earlyLeave: ${_this.earlyLeave}, workedMinutes: ${_this.workedMinutes}, leaveName: ${_this.leaveName}, reason: ${_this.reason})';
}


}

/// @nodoc
abstract mixin class $HrDayResultCopyWith<$Res>  {
  factory $HrDayResultCopyWith(HrDayResult value, $Res Function(HrDayResult) _then) = _$HrDayResultCopyWithImpl;
@useResult
$Res call({
 String date, String status, String? planBegin, String? planEnd, String? firstIn, String? lastOut, bool late, int? lateMinutes, bool earlyLeave, int? workedMinutes, String? leaveName, String? reason
});




}
/// @nodoc
class _$HrDayResultCopyWithImpl<$Res>
    implements $HrDayResultCopyWith<$Res> {
  _$HrDayResultCopyWithImpl(this._self, this._then);

  final HrDayResult _self;
  final $Res Function(HrDayResult) _then;

/// Create a copy of HrDayResult
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? date = null,Object? status = null,Object? planBegin = freezed,Object? planEnd = freezed,Object? firstIn = freezed,Object? lastOut = freezed,Object? late = null,Object? lateMinutes = freezed,Object? earlyLeave = null,Object? workedMinutes = freezed,Object? leaveName = freezed,Object? reason = freezed,}) {
  return _then(HrDayResult(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,planBegin: freezed == planBegin ? _self.planBegin : planBegin // ignore: cast_nullable_to_non_nullable
as String?,planEnd: freezed == planEnd ? _self.planEnd : planEnd // ignore: cast_nullable_to_non_nullable
as String?,firstIn: freezed == firstIn ? _self.firstIn : firstIn // ignore: cast_nullable_to_non_nullable
as String?,lastOut: freezed == lastOut ? _self.lastOut : lastOut // ignore: cast_nullable_to_non_nullable
as String?,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as bool,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,earlyLeave: null == earlyLeave ? _self.earlyLeave : earlyLeave // ignore: cast_nullable_to_non_nullable
as bool,workedMinutes: freezed == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int?,leaveName: freezed == leaveName ? _self.leaveName : leaveName // ignore: cast_nullable_to_non_nullable
as String?,reason: freezed == reason ? _self.reason : reason // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [HrDayResult].
extension HrDayResultPatterns on HrDayResult {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrDayResult value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrDayResult() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrDayResult value)  $default,){
final _that = this;
switch (_that) {
case _HrDayResult():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrDayResult value)?  $default,){
final _that = this;
switch (_that) {
case _HrDayResult() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String date,  String status,  String? planBegin,  String? planEnd,  String? firstIn,  String? lastOut,  bool late,  int? lateMinutes,  bool earlyLeave,  int? workedMinutes,  String? leaveName,  String? reason)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrDayResult() when $default != null:
return $default(_that.date,_that.status,_that.planBegin,_that.planEnd,_that.firstIn,_that.lastOut,_that.late,_that.lateMinutes,_that.earlyLeave,_that.workedMinutes,_that.leaveName,_that.reason);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String date,  String status,  String? planBegin,  String? planEnd,  String? firstIn,  String? lastOut,  bool late,  int? lateMinutes,  bool earlyLeave,  int? workedMinutes,  String? leaveName,  String? reason)  $default,) {final _that = this;
switch (_that) {
case _HrDayResult():
return $default(_that.date,_that.status,_that.planBegin,_that.planEnd,_that.firstIn,_that.lastOut,_that.late,_that.lateMinutes,_that.earlyLeave,_that.workedMinutes,_that.leaveName,_that.reason);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String date,  String status,  String? planBegin,  String? planEnd,  String? firstIn,  String? lastOut,  bool late,  int? lateMinutes,  bool earlyLeave,  int? workedMinutes,  String? leaveName,  String? reason)?  $default,) {final _that = this;
switch (_that) {
case _HrDayResult() when $default != null:
return $default(_that.date,_that.status,_that.planBegin,_that.planEnd,_that.firstIn,_that.lastOut,_that.late,_that.lateMinutes,_that.earlyLeave,_that.workedMinutes,_that.leaveName,_that.reason);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrDayResult implements HrDayResult {
  const _HrDayResult({required this.date, required this.status, this.planBegin, this.planEnd, this.firstIn, this.lastOut, this.late = false, this.lateMinutes, this.earlyLeave = false, this.workedMinutes, this.leaveName, this.reason});
  factory _HrDayResult.fromJson(Map<String, dynamic> json) => _$HrDayResultFromJson(json);

@override final  String date;
@override final  String status;
@override final  String? planBegin;
@override final  String? planEnd;
@override final  String? firstIn;
@override final  String? lastOut;
@override@JsonKey() final  bool late;
@override final  int? lateMinutes;
@override@JsonKey() final  bool earlyLeave;
@override final  int? workedMinutes;
@override final  String? leaveName;
@override final  String? reason;

/// Create a copy of HrDayResult
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrDayResultCopyWith<_HrDayResult> get copyWith => __$HrDayResultCopyWithImpl<_HrDayResult>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrDayResultToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrDayResult&&(identical(other.date, date) || other.date == date)&&(identical(other.status, status) || other.status == status)&&(identical(other.planBegin, planBegin) || other.planBegin == planBegin)&&(identical(other.planEnd, planEnd) || other.planEnd == planEnd)&&(identical(other.firstIn, firstIn) || other.firstIn == firstIn)&&(identical(other.lastOut, lastOut) || other.lastOut == lastOut)&&(identical(other.late, late) || other.late == late)&&(identical(other.lateMinutes, lateMinutes) || other.lateMinutes == lateMinutes)&&(identical(other.earlyLeave, earlyLeave) || other.earlyLeave == earlyLeave)&&(identical(other.workedMinutes, workedMinutes) || other.workedMinutes == workedMinutes)&&(identical(other.leaveName, leaveName) || other.leaveName == leaveName)&&(identical(other.reason, reason) || other.reason == reason));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,date,status,planBegin,planEnd,firstIn,lastOut,late,lateMinutes,earlyLeave,workedMinutes,leaveName,reason);
}

@override
String toString() {
    return 'HrDayResult(date: $date, status: $status, planBegin: $planBegin, planEnd: $planEnd, firstIn: $firstIn, lastOut: $lastOut, late: $late, lateMinutes: $lateMinutes, earlyLeave: $earlyLeave, workedMinutes: $workedMinutes, leaveName: $leaveName, reason: $reason)';
}


}

/// @nodoc
abstract mixin class _$HrDayResultCopyWith<$Res> implements $HrDayResultCopyWith<$Res> {
  factory _$HrDayResultCopyWith(_HrDayResult value, $Res Function(_HrDayResult) _then) = __$HrDayResultCopyWithImpl;
@override @useResult
$Res call({
 String date, String status, String? planBegin, String? planEnd, String? firstIn, String? lastOut, bool late, int? lateMinutes, bool earlyLeave, int? workedMinutes, String? leaveName, String? reason
});




}
/// @nodoc
class __$HrDayResultCopyWithImpl<$Res>
    implements _$HrDayResultCopyWith<$Res> {
  __$HrDayResultCopyWithImpl(this._self, this._then);

  final _HrDayResult _self;
  final $Res Function(_HrDayResult) _then;

/// Create a copy of HrDayResult
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? date = null,Object? status = null,Object? planBegin = freezed,Object? planEnd = freezed,Object? firstIn = freezed,Object? lastOut = freezed,Object? late = null,Object? lateMinutes = freezed,Object? earlyLeave = null,Object? workedMinutes = freezed,Object? leaveName = freezed,Object? reason = freezed,}) {
  return _then(_HrDayResult(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,planBegin: freezed == planBegin ? _self.planBegin : planBegin // ignore: cast_nullable_to_non_nullable
as String?,planEnd: freezed == planEnd ? _self.planEnd : planEnd // ignore: cast_nullable_to_non_nullable
as String?,firstIn: freezed == firstIn ? _self.firstIn : firstIn // ignore: cast_nullable_to_non_nullable
as String?,lastOut: freezed == lastOut ? _self.lastOut : lastOut // ignore: cast_nullable_to_non_nullable
as String?,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as bool,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,earlyLeave: null == earlyLeave ? _self.earlyLeave : earlyLeave // ignore: cast_nullable_to_non_nullable
as bool,workedMinutes: freezed == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int?,leaveName: freezed == leaveName ? _self.leaveName : leaveName // ignore: cast_nullable_to_non_nullable
as String?,reason: freezed == reason ? _self.reason : reason // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$HrSummary {

 int get planDays; int get presentDays; int get lateDays; int get lateMinutes; int get absentDays; int get leaveDays; int get workedMinutes; int get planMinutes;
/// Create a copy of HrSummary
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrSummaryCopyWith<HrSummary> get copyWith => _$HrSummaryCopyWithImpl<HrSummary>(this as HrSummary, _$identity);

  /// Serializes this HrSummary to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrSummary;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrSummary&&(identical(other.planDays, _this.planDays) || other.planDays == _this.planDays)&&(identical(other.presentDays, _this.presentDays) || other.presentDays == _this.presentDays)&&(identical(other.lateDays, _this.lateDays) || other.lateDays == _this.lateDays)&&(identical(other.lateMinutes, _this.lateMinutes) || other.lateMinutes == _this.lateMinutes)&&(identical(other.absentDays, _this.absentDays) || other.absentDays == _this.absentDays)&&(identical(other.leaveDays, _this.leaveDays) || other.leaveDays == _this.leaveDays)&&(identical(other.workedMinutes, _this.workedMinutes) || other.workedMinutes == _this.workedMinutes)&&(identical(other.planMinutes, _this.planMinutes) || other.planMinutes == _this.planMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrSummary;
  return Object.hash(runtimeType,_this.planDays,_this.presentDays,_this.lateDays,_this.lateMinutes,_this.absentDays,_this.leaveDays,_this.workedMinutes,_this.planMinutes);
}

@override
String toString() {
  final _this = this as HrSummary;
  return 'HrSummary(planDays: ${_this.planDays}, presentDays: ${_this.presentDays}, lateDays: ${_this.lateDays}, lateMinutes: ${_this.lateMinutes}, absentDays: ${_this.absentDays}, leaveDays: ${_this.leaveDays}, workedMinutes: ${_this.workedMinutes}, planMinutes: ${_this.planMinutes})';
}


}

/// @nodoc
abstract mixin class $HrSummaryCopyWith<$Res>  {
  factory $HrSummaryCopyWith(HrSummary value, $Res Function(HrSummary) _then) = _$HrSummaryCopyWithImpl;
@useResult
$Res call({
 int planDays, int presentDays, int lateDays, int lateMinutes, int absentDays, int leaveDays, int workedMinutes, int planMinutes
});




}
/// @nodoc
class _$HrSummaryCopyWithImpl<$Res>
    implements $HrSummaryCopyWith<$Res> {
  _$HrSummaryCopyWithImpl(this._self, this._then);

  final HrSummary _self;
  final $Res Function(HrSummary) _then;

/// Create a copy of HrSummary
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? planDays = null,Object? presentDays = null,Object? lateDays = null,Object? lateMinutes = null,Object? absentDays = null,Object? leaveDays = null,Object? workedMinutes = null,Object? planMinutes = null,}) {
  return _then(HrSummary(
planDays: null == planDays ? _self.planDays : planDays // ignore: cast_nullable_to_non_nullable
as int,presentDays: null == presentDays ? _self.presentDays : presentDays // ignore: cast_nullable_to_non_nullable
as int,lateDays: null == lateDays ? _self.lateDays : lateDays // ignore: cast_nullable_to_non_nullable
as int,lateMinutes: null == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int,absentDays: null == absentDays ? _self.absentDays : absentDays // ignore: cast_nullable_to_non_nullable
as int,leaveDays: null == leaveDays ? _self.leaveDays : leaveDays // ignore: cast_nullable_to_non_nullable
as int,workedMinutes: null == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int,planMinutes: null == planMinutes ? _self.planMinutes : planMinutes // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [HrSummary].
extension HrSummaryPatterns on HrSummary {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrSummary value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrSummary() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrSummary value)  $default,){
final _that = this;
switch (_that) {
case _HrSummary():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrSummary value)?  $default,){
final _that = this;
switch (_that) {
case _HrSummary() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int planDays,  int presentDays,  int lateDays,  int lateMinutes,  int absentDays,  int leaveDays,  int workedMinutes,  int planMinutes)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrSummary() when $default != null:
return $default(_that.planDays,_that.presentDays,_that.lateDays,_that.lateMinutes,_that.absentDays,_that.leaveDays,_that.workedMinutes,_that.planMinutes);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int planDays,  int presentDays,  int lateDays,  int lateMinutes,  int absentDays,  int leaveDays,  int workedMinutes,  int planMinutes)  $default,) {final _that = this;
switch (_that) {
case _HrSummary():
return $default(_that.planDays,_that.presentDays,_that.lateDays,_that.lateMinutes,_that.absentDays,_that.leaveDays,_that.workedMinutes,_that.planMinutes);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int planDays,  int presentDays,  int lateDays,  int lateMinutes,  int absentDays,  int leaveDays,  int workedMinutes,  int planMinutes)?  $default,) {final _that = this;
switch (_that) {
case _HrSummary() when $default != null:
return $default(_that.planDays,_that.presentDays,_that.lateDays,_that.lateMinutes,_that.absentDays,_that.leaveDays,_that.workedMinutes,_that.planMinutes);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrSummary implements HrSummary {
  const _HrSummary({this.planDays = 0, this.presentDays = 0, this.lateDays = 0, this.lateMinutes = 0, this.absentDays = 0, this.leaveDays = 0, this.workedMinutes = 0, this.planMinutes = 0});
  factory _HrSummary.fromJson(Map<String, dynamic> json) => _$HrSummaryFromJson(json);

@override@JsonKey() final  int planDays;
@override@JsonKey() final  int presentDays;
@override@JsonKey() final  int lateDays;
@override@JsonKey() final  int lateMinutes;
@override@JsonKey() final  int absentDays;
@override@JsonKey() final  int leaveDays;
@override@JsonKey() final  int workedMinutes;
@override@JsonKey() final  int planMinutes;

/// Create a copy of HrSummary
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrSummaryCopyWith<_HrSummary> get copyWith => __$HrSummaryCopyWithImpl<_HrSummary>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrSummaryToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrSummary&&(identical(other.planDays, planDays) || other.planDays == planDays)&&(identical(other.presentDays, presentDays) || other.presentDays == presentDays)&&(identical(other.lateDays, lateDays) || other.lateDays == lateDays)&&(identical(other.lateMinutes, lateMinutes) || other.lateMinutes == lateMinutes)&&(identical(other.absentDays, absentDays) || other.absentDays == absentDays)&&(identical(other.leaveDays, leaveDays) || other.leaveDays == leaveDays)&&(identical(other.workedMinutes, workedMinutes) || other.workedMinutes == workedMinutes)&&(identical(other.planMinutes, planMinutes) || other.planMinutes == planMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,planDays,presentDays,lateDays,lateMinutes,absentDays,leaveDays,workedMinutes,planMinutes);
}

@override
String toString() {
    return 'HrSummary(planDays: $planDays, presentDays: $presentDays, lateDays: $lateDays, lateMinutes: $lateMinutes, absentDays: $absentDays, leaveDays: $leaveDays, workedMinutes: $workedMinutes, planMinutes: $planMinutes)';
}


}

/// @nodoc
abstract mixin class _$HrSummaryCopyWith<$Res> implements $HrSummaryCopyWith<$Res> {
  factory _$HrSummaryCopyWith(_HrSummary value, $Res Function(_HrSummary) _then) = __$HrSummaryCopyWithImpl;
@override @useResult
$Res call({
 int planDays, int presentDays, int lateDays, int lateMinutes, int absentDays, int leaveDays, int workedMinutes, int planMinutes
});




}
/// @nodoc
class __$HrSummaryCopyWithImpl<$Res>
    implements _$HrSummaryCopyWith<$Res> {
  __$HrSummaryCopyWithImpl(this._self, this._then);

  final _HrSummary _self;
  final $Res Function(_HrSummary) _then;

/// Create a copy of HrSummary
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? planDays = null,Object? presentDays = null,Object? lateDays = null,Object? lateMinutes = null,Object? absentDays = null,Object? leaveDays = null,Object? workedMinutes = null,Object? planMinutes = null,}) {
  return _then(_HrSummary(
planDays: null == planDays ? _self.planDays : planDays // ignore: cast_nullable_to_non_nullable
as int,presentDays: null == presentDays ? _self.presentDays : presentDays // ignore: cast_nullable_to_non_nullable
as int,lateDays: null == lateDays ? _self.lateDays : lateDays // ignore: cast_nullable_to_non_nullable
as int,lateMinutes: null == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int,absentDays: null == absentDays ? _self.absentDays : absentDays // ignore: cast_nullable_to_non_nullable
as int,leaveDays: null == leaveDays ? _self.leaveDays : leaveDays // ignore: cast_nullable_to_non_nullable
as int,workedMinutes: null == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int,planMinutes: null == planMinutes ? _self.planMinutes : planMinutes // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$HrEmployeeCard {

 HrEmployeeRow get employee; String get from; String get to; List<HrDayResult> get days; HrSummary get summary;
/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrEmployeeCardCopyWith<HrEmployeeCard> get copyWith => _$HrEmployeeCardCopyWithImpl<HrEmployeeCard>(this as HrEmployeeCard, _$identity);

  /// Serializes this HrEmployeeCard to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrEmployeeCard;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrEmployeeCard&&(identical(other.employee, _this.employee) || other.employee == _this.employee)&&(identical(other.from, _this.from) || other.from == _this.from)&&(identical(other.to, _this.to) || other.to == _this.to)&&const DeepCollectionEquality().equals(other.days, _this.days)&&(identical(other.summary, _this.summary) || other.summary == _this.summary));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrEmployeeCard;
  return Object.hash(runtimeType,_this.employee,_this.from,_this.to,const DeepCollectionEquality().hash(_this.days),_this.summary);
}

@override
String toString() {
  final _this = this as HrEmployeeCard;
  return 'HrEmployeeCard(employee: ${_this.employee}, from: ${_this.from}, to: ${_this.to}, days: ${_this.days}, summary: ${_this.summary})';
}


}

/// @nodoc
abstract mixin class $HrEmployeeCardCopyWith<$Res>  {
  factory $HrEmployeeCardCopyWith(HrEmployeeCard value, $Res Function(HrEmployeeCard) _then) = _$HrEmployeeCardCopyWithImpl;
@useResult
$Res call({
 HrEmployeeRow employee, String from, String to, List<HrDayResult> days, HrSummary summary
});


$HrEmployeeRowCopyWith<$Res> get employee;$HrSummaryCopyWith<$Res> get summary;

}
/// @nodoc
class _$HrEmployeeCardCopyWithImpl<$Res>
    implements $HrEmployeeCardCopyWith<$Res> {
  _$HrEmployeeCardCopyWithImpl(this._self, this._then);

  final HrEmployeeCard _self;
  final $Res Function(HrEmployeeCard) _then;

/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? employee = null,Object? from = null,Object? to = null,Object? days = null,Object? summary = null,}) {
  return _then(HrEmployeeCard(
employee: null == employee ? _self.employee : employee // ignore: cast_nullable_to_non_nullable
as HrEmployeeRow,from: null == from ? _self.from : from // ignore: cast_nullable_to_non_nullable
as String,to: null == to ? _self.to : to // ignore: cast_nullable_to_non_nullable
as String,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as List<HrDayResult>,summary: null == summary ? _self.summary : summary // ignore: cast_nullable_to_non_nullable
as HrSummary,
  ));
}
/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrEmployeeRowCopyWith<$Res> get employee {
  
  return $HrEmployeeRowCopyWith<$Res>(_self.employee, (value) {
    return _then(_self.copyWith(employee: value));
  });
}/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrSummaryCopyWith<$Res> get summary {
  
  return $HrSummaryCopyWith<$Res>(_self.summary, (value) {
    return _then(_self.copyWith(summary: value));
  });
}
}


/// Adds pattern-matching-related methods to [HrEmployeeCard].
extension HrEmployeeCardPatterns on HrEmployeeCard {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrEmployeeCard value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrEmployeeCard() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrEmployeeCard value)  $default,){
final _that = this;
switch (_that) {
case _HrEmployeeCard():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrEmployeeCard value)?  $default,){
final _that = this;
switch (_that) {
case _HrEmployeeCard() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( HrEmployeeRow employee,  String from,  String to,  List<HrDayResult> days,  HrSummary summary)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrEmployeeCard() when $default != null:
return $default(_that.employee,_that.from,_that.to,_that.days,_that.summary);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( HrEmployeeRow employee,  String from,  String to,  List<HrDayResult> days,  HrSummary summary)  $default,) {final _that = this;
switch (_that) {
case _HrEmployeeCard():
return $default(_that.employee,_that.from,_that.to,_that.days,_that.summary);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( HrEmployeeRow employee,  String from,  String to,  List<HrDayResult> days,  HrSummary summary)?  $default,) {final _that = this;
switch (_that) {
case _HrEmployeeCard() when $default != null:
return $default(_that.employee,_that.from,_that.to,_that.days,_that.summary);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrEmployeeCard implements HrEmployeeCard {
  const _HrEmployeeCard({required this.employee, required this.from, required this.to,  List<HrDayResult> days = const <HrDayResult>[], this.summary = const HrSummary()}): _days = days;
  factory _HrEmployeeCard.fromJson(Map<String, dynamic> json) => _$HrEmployeeCardFromJson(json);

@override final  HrEmployeeRow employee;
@override final  String from;
@override final  String to;
 final  List<HrDayResult> _days;
@override@JsonKey() List<HrDayResult> get days {
  if (_days is EqualUnmodifiableListView) return _days;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_days);
}

@override@JsonKey() final  HrSummary summary;

/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrEmployeeCardCopyWith<_HrEmployeeCard> get copyWith => __$HrEmployeeCardCopyWithImpl<_HrEmployeeCard>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrEmployeeCardToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrEmployeeCard&&(identical(other.employee, employee) || other.employee == employee)&&(identical(other.from, from) || other.from == from)&&(identical(other.to, to) || other.to == to)&&const DeepCollectionEquality().equals(other.days, _days)&&(identical(other.summary, summary) || other.summary == summary));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,employee,from,to,const DeepCollectionEquality().hash(_days),summary);
}

@override
String toString() {
    return 'HrEmployeeCard(employee: $employee, from: $from, to: $to, days: $days, summary: $summary)';
}


}

/// @nodoc
abstract mixin class _$HrEmployeeCardCopyWith<$Res> implements $HrEmployeeCardCopyWith<$Res> {
  factory _$HrEmployeeCardCopyWith(_HrEmployeeCard value, $Res Function(_HrEmployeeCard) _then) = __$HrEmployeeCardCopyWithImpl;
@override @useResult
$Res call({
 HrEmployeeRow employee, String from, String to, List<HrDayResult> days, HrSummary summary
});


@override $HrEmployeeRowCopyWith<$Res> get employee;@override $HrSummaryCopyWith<$Res> get summary;

}
/// @nodoc
class __$HrEmployeeCardCopyWithImpl<$Res>
    implements _$HrEmployeeCardCopyWith<$Res> {
  __$HrEmployeeCardCopyWithImpl(this._self, this._then);

  final _HrEmployeeCard _self;
  final $Res Function(_HrEmployeeCard) _then;

/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? employee = null,Object? from = null,Object? to = null,Object? days = null,Object? summary = null,}) {
  return _then(_HrEmployeeCard(
employee: null == employee ? _self.employee : employee // ignore: cast_nullable_to_non_nullable
as HrEmployeeRow,from: null == from ? _self.from : from // ignore: cast_nullable_to_non_nullable
as String,to: null == to ? _self.to : to // ignore: cast_nullable_to_non_nullable
as String,days: null == days ? _self._days : days // ignore: cast_nullable_to_non_nullable
as List<HrDayResult>,summary: null == summary ? _self.summary : summary // ignore: cast_nullable_to_non_nullable
as HrSummary,
  ));
}

/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrEmployeeRowCopyWith<$Res> get employee {
  
  return $HrEmployeeRowCopyWith<$Res>(_self.employee, (value) {
    return _then(_self.copyWith(employee: value));
  });
}/// Create a copy of HrEmployeeCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrSummaryCopyWith<$Res> get summary {
  
  return $HrSummaryCopyWith<$Res>(_self.summary, (value) {
    return _then(_self.copyWith(summary: value));
  });
}
}

// dart format on
