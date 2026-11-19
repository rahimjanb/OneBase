// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'profile_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$WorkdayToday {

/// not_started, at_work, on_break, out, finished, day_off, on_leave, absent, no_schedule, unclear, not_employed
 String get status; String? get shiftStart; String? get shiftEnd; String? get arrival; String? get departure; int? get workedMinutes; int? get plannedMinutes; int? get lateMinutes; String? get leaveName; String? get reason;
/// Create a copy of WorkdayToday
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WorkdayTodayCopyWith<WorkdayToday> get copyWith => _$WorkdayTodayCopyWithImpl<WorkdayToday>(this as WorkdayToday, _$identity);

  /// Serializes this WorkdayToday to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WorkdayToday;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WorkdayToday&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.shiftStart, _this.shiftStart) || other.shiftStart == _this.shiftStart)&&(identical(other.shiftEnd, _this.shiftEnd) || other.shiftEnd == _this.shiftEnd)&&(identical(other.arrival, _this.arrival) || other.arrival == _this.arrival)&&(identical(other.departure, _this.departure) || other.departure == _this.departure)&&(identical(other.workedMinutes, _this.workedMinutes) || other.workedMinutes == _this.workedMinutes)&&(identical(other.plannedMinutes, _this.plannedMinutes) || other.plannedMinutes == _this.plannedMinutes)&&(identical(other.lateMinutes, _this.lateMinutes) || other.lateMinutes == _this.lateMinutes)&&(identical(other.leaveName, _this.leaveName) || other.leaveName == _this.leaveName)&&(identical(other.reason, _this.reason) || other.reason == _this.reason));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WorkdayToday;
  return Object.hash(runtimeType,_this.status,_this.shiftStart,_this.shiftEnd,_this.arrival,_this.departure,_this.workedMinutes,_this.plannedMinutes,_this.lateMinutes,_this.leaveName,_this.reason);
}

@override
String toString() {
  final _this = this as WorkdayToday;
  return 'WorkdayToday(status: ${_this.status}, shiftStart: ${_this.shiftStart}, shiftEnd: ${_this.shiftEnd}, arrival: ${_this.arrival}, departure: ${_this.departure}, workedMinutes: ${_this.workedMinutes}, plannedMinutes: ${_this.plannedMinutes}, lateMinutes: ${_this.lateMinutes}, leaveName: ${_this.leaveName}, reason: ${_this.reason})';
}


}

/// @nodoc
abstract mixin class $WorkdayTodayCopyWith<$Res>  {
  factory $WorkdayTodayCopyWith(WorkdayToday value, $Res Function(WorkdayToday) _then) = _$WorkdayTodayCopyWithImpl;
@useResult
$Res call({
 String status, String? shiftStart, String? shiftEnd, String? arrival, String? departure, int? workedMinutes, int? plannedMinutes, int? lateMinutes, String? leaveName, String? reason
});




}
/// @nodoc
class _$WorkdayTodayCopyWithImpl<$Res>
    implements $WorkdayTodayCopyWith<$Res> {
  _$WorkdayTodayCopyWithImpl(this._self, this._then);

  final WorkdayToday _self;
  final $Res Function(WorkdayToday) _then;

/// Create a copy of WorkdayToday
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? status = null,Object? shiftStart = freezed,Object? shiftEnd = freezed,Object? arrival = freezed,Object? departure = freezed,Object? workedMinutes = freezed,Object? plannedMinutes = freezed,Object? lateMinutes = freezed,Object? leaveName = freezed,Object? reason = freezed,}) {
  return _then(WorkdayToday(
status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,shiftStart: freezed == shiftStart ? _self.shiftStart : shiftStart // ignore: cast_nullable_to_non_nullable
as String?,shiftEnd: freezed == shiftEnd ? _self.shiftEnd : shiftEnd // ignore: cast_nullable_to_non_nullable
as String?,arrival: freezed == arrival ? _self.arrival : arrival // ignore: cast_nullable_to_non_nullable
as String?,departure: freezed == departure ? _self.departure : departure // ignore: cast_nullable_to_non_nullable
as String?,workedMinutes: freezed == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int?,plannedMinutes: freezed == plannedMinutes ? _self.plannedMinutes : plannedMinutes // ignore: cast_nullable_to_non_nullable
as int?,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,leaveName: freezed == leaveName ? _self.leaveName : leaveName // ignore: cast_nullable_to_non_nullable
as String?,reason: freezed == reason ? _self.reason : reason // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [WorkdayToday].
extension WorkdayTodayPatterns on WorkdayToday {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WorkdayToday value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WorkdayToday() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WorkdayToday value)  $default,){
final _that = this;
switch (_that) {
case _WorkdayToday():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WorkdayToday value)?  $default,){
final _that = this;
switch (_that) {
case _WorkdayToday() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String status,  String? shiftStart,  String? shiftEnd,  String? arrival,  String? departure,  int? workedMinutes,  int? plannedMinutes,  int? lateMinutes,  String? leaveName,  String? reason)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WorkdayToday() when $default != null:
return $default(_that.status,_that.shiftStart,_that.shiftEnd,_that.arrival,_that.departure,_that.workedMinutes,_that.plannedMinutes,_that.lateMinutes,_that.leaveName,_that.reason);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String status,  String? shiftStart,  String? shiftEnd,  String? arrival,  String? departure,  int? workedMinutes,  int? plannedMinutes,  int? lateMinutes,  String? leaveName,  String? reason)  $default,) {final _that = this;
switch (_that) {
case _WorkdayToday():
return $default(_that.status,_that.shiftStart,_that.shiftEnd,_that.arrival,_that.departure,_that.workedMinutes,_that.plannedMinutes,_that.lateMinutes,_that.leaveName,_that.reason);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String status,  String? shiftStart,  String? shiftEnd,  String? arrival,  String? departure,  int? workedMinutes,  int? plannedMinutes,  int? lateMinutes,  String? leaveName,  String? reason)?  $default,) {final _that = this;
switch (_that) {
case _WorkdayToday() when $default != null:
return $default(_that.status,_that.shiftStart,_that.shiftEnd,_that.arrival,_that.departure,_that.workedMinutes,_that.plannedMinutes,_that.lateMinutes,_that.leaveName,_that.reason);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WorkdayToday implements WorkdayToday {
  const _WorkdayToday({required this.status, this.shiftStart, this.shiftEnd, this.arrival, this.departure, this.workedMinutes, this.plannedMinutes, this.lateMinutes, this.leaveName, this.reason});
  factory _WorkdayToday.fromJson(Map<String, dynamic> json) => _$WorkdayTodayFromJson(json);

/// not_started, at_work, on_break, out, finished, day_off, on_leave, absent, no_schedule, unclear, not_employed
@override final  String status;
@override final  String? shiftStart;
@override final  String? shiftEnd;
@override final  String? arrival;
@override final  String? departure;
@override final  int? workedMinutes;
@override final  int? plannedMinutes;
@override final  int? lateMinutes;
@override final  String? leaveName;
@override final  String? reason;

/// Create a copy of WorkdayToday
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WorkdayTodayCopyWith<_WorkdayToday> get copyWith => __$WorkdayTodayCopyWithImpl<_WorkdayToday>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WorkdayTodayToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WorkdayToday&&(identical(other.status, status) || other.status == status)&&(identical(other.shiftStart, shiftStart) || other.shiftStart == shiftStart)&&(identical(other.shiftEnd, shiftEnd) || other.shiftEnd == shiftEnd)&&(identical(other.arrival, arrival) || other.arrival == arrival)&&(identical(other.departure, departure) || other.departure == departure)&&(identical(other.workedMinutes, workedMinutes) || other.workedMinutes == workedMinutes)&&(identical(other.plannedMinutes, plannedMinutes) || other.plannedMinutes == plannedMinutes)&&(identical(other.lateMinutes, lateMinutes) || other.lateMinutes == lateMinutes)&&(identical(other.leaveName, leaveName) || other.leaveName == leaveName)&&(identical(other.reason, reason) || other.reason == reason));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,status,shiftStart,shiftEnd,arrival,departure,workedMinutes,plannedMinutes,lateMinutes,leaveName,reason);
}

@override
String toString() {
    return 'WorkdayToday(status: $status, shiftStart: $shiftStart, shiftEnd: $shiftEnd, arrival: $arrival, departure: $departure, workedMinutes: $workedMinutes, plannedMinutes: $plannedMinutes, lateMinutes: $lateMinutes, leaveName: $leaveName, reason: $reason)';
}


}

/// @nodoc
abstract mixin class _$WorkdayTodayCopyWith<$Res> implements $WorkdayTodayCopyWith<$Res> {
  factory _$WorkdayTodayCopyWith(_WorkdayToday value, $Res Function(_WorkdayToday) _then) = __$WorkdayTodayCopyWithImpl;
@override @useResult
$Res call({
 String status, String? shiftStart, String? shiftEnd, String? arrival, String? departure, int? workedMinutes, int? plannedMinutes, int? lateMinutes, String? leaveName, String? reason
});




}
/// @nodoc
class __$WorkdayTodayCopyWithImpl<$Res>
    implements _$WorkdayTodayCopyWith<$Res> {
  __$WorkdayTodayCopyWithImpl(this._self, this._then);

  final _WorkdayToday _self;
  final $Res Function(_WorkdayToday) _then;

/// Create a copy of WorkdayToday
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? status = null,Object? shiftStart = freezed,Object? shiftEnd = freezed,Object? arrival = freezed,Object? departure = freezed,Object? workedMinutes = freezed,Object? plannedMinutes = freezed,Object? lateMinutes = freezed,Object? leaveName = freezed,Object? reason = freezed,}) {
  return _then(_WorkdayToday(
status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,shiftStart: freezed == shiftStart ? _self.shiftStart : shiftStart // ignore: cast_nullable_to_non_nullable
as String?,shiftEnd: freezed == shiftEnd ? _self.shiftEnd : shiftEnd // ignore: cast_nullable_to_non_nullable
as String?,arrival: freezed == arrival ? _self.arrival : arrival // ignore: cast_nullable_to_non_nullable
as String?,departure: freezed == departure ? _self.departure : departure // ignore: cast_nullable_to_non_nullable
as String?,workedMinutes: freezed == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int?,plannedMinutes: freezed == plannedMinutes ? _self.plannedMinutes : plannedMinutes // ignore: cast_nullable_to_non_nullable
as int?,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,leaveName: freezed == leaveName ? _self.leaveName : leaveName // ignore: cast_nullable_to_non_nullable
as String?,reason: freezed == reason ? _self.reason : reason // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$WorkdayDay {

 String get date;/// ok, late, early_leave, absent, leave, day_off, not_started, no_schedule, unclear, not_employed, future
 String get status; int? get lateMinutes; int? get workedMinutes;
/// Create a copy of WorkdayDay
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WorkdayDayCopyWith<WorkdayDay> get copyWith => _$WorkdayDayCopyWithImpl<WorkdayDay>(this as WorkdayDay, _$identity);

  /// Serializes this WorkdayDay to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WorkdayDay;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WorkdayDay&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.lateMinutes, _this.lateMinutes) || other.lateMinutes == _this.lateMinutes)&&(identical(other.workedMinutes, _this.workedMinutes) || other.workedMinutes == _this.workedMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WorkdayDay;
  return Object.hash(runtimeType,_this.date,_this.status,_this.lateMinutes,_this.workedMinutes);
}

@override
String toString() {
  final _this = this as WorkdayDay;
  return 'WorkdayDay(date: ${_this.date}, status: ${_this.status}, lateMinutes: ${_this.lateMinutes}, workedMinutes: ${_this.workedMinutes})';
}


}

/// @nodoc
abstract mixin class $WorkdayDayCopyWith<$Res>  {
  factory $WorkdayDayCopyWith(WorkdayDay value, $Res Function(WorkdayDay) _then) = _$WorkdayDayCopyWithImpl;
@useResult
$Res call({
 String date, String status, int? lateMinutes, int? workedMinutes
});




}
/// @nodoc
class _$WorkdayDayCopyWithImpl<$Res>
    implements $WorkdayDayCopyWith<$Res> {
  _$WorkdayDayCopyWithImpl(this._self, this._then);

  final WorkdayDay _self;
  final $Res Function(WorkdayDay) _then;

/// Create a copy of WorkdayDay
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? date = null,Object? status = null,Object? lateMinutes = freezed,Object? workedMinutes = freezed,}) {
  return _then(WorkdayDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,workedMinutes: freezed == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int?,
  ));
}

}


/// Adds pattern-matching-related methods to [WorkdayDay].
extension WorkdayDayPatterns on WorkdayDay {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WorkdayDay value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WorkdayDay() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WorkdayDay value)  $default,){
final _that = this;
switch (_that) {
case _WorkdayDay():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WorkdayDay value)?  $default,){
final _that = this;
switch (_that) {
case _WorkdayDay() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String date,  String status,  int? lateMinutes,  int? workedMinutes)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WorkdayDay() when $default != null:
return $default(_that.date,_that.status,_that.lateMinutes,_that.workedMinutes);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String date,  String status,  int? lateMinutes,  int? workedMinutes)  $default,) {final _that = this;
switch (_that) {
case _WorkdayDay():
return $default(_that.date,_that.status,_that.lateMinutes,_that.workedMinutes);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String date,  String status,  int? lateMinutes,  int? workedMinutes)?  $default,) {final _that = this;
switch (_that) {
case _WorkdayDay() when $default != null:
return $default(_that.date,_that.status,_that.lateMinutes,_that.workedMinutes);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WorkdayDay implements WorkdayDay {
  const _WorkdayDay({required this.date, required this.status, this.lateMinutes, this.workedMinutes});
  factory _WorkdayDay.fromJson(Map<String, dynamic> json) => _$WorkdayDayFromJson(json);

@override final  String date;
/// ok, late, early_leave, absent, leave, day_off, not_started, no_schedule, unclear, not_employed, future
@override final  String status;
@override final  int? lateMinutes;
@override final  int? workedMinutes;

/// Create a copy of WorkdayDay
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WorkdayDayCopyWith<_WorkdayDay> get copyWith => __$WorkdayDayCopyWithImpl<_WorkdayDay>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WorkdayDayToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WorkdayDay&&(identical(other.date, date) || other.date == date)&&(identical(other.status, status) || other.status == status)&&(identical(other.lateMinutes, lateMinutes) || other.lateMinutes == lateMinutes)&&(identical(other.workedMinutes, workedMinutes) || other.workedMinutes == workedMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,date,status,lateMinutes,workedMinutes);
}

@override
String toString() {
    return 'WorkdayDay(date: $date, status: $status, lateMinutes: $lateMinutes, workedMinutes: $workedMinutes)';
}


}

/// @nodoc
abstract mixin class _$WorkdayDayCopyWith<$Res> implements $WorkdayDayCopyWith<$Res> {
  factory _$WorkdayDayCopyWith(_WorkdayDay value, $Res Function(_WorkdayDay) _then) = __$WorkdayDayCopyWithImpl;
@override @useResult
$Res call({
 String date, String status, int? lateMinutes, int? workedMinutes
});




}
/// @nodoc
class __$WorkdayDayCopyWithImpl<$Res>
    implements _$WorkdayDayCopyWith<$Res> {
  __$WorkdayDayCopyWithImpl(this._self, this._then);

  final _WorkdayDay _self;
  final $Res Function(_WorkdayDay) _then;

/// Create a copy of WorkdayDay
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? date = null,Object? status = null,Object? lateMinutes = freezed,Object? workedMinutes = freezed,}) {
  return _then(_WorkdayDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,lateMinutes: freezed == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int?,workedMinutes: freezed == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int?,
  ));
}


}


/// @nodoc
mixin _$WorkdayTotals {

 int get workedMinutes; int get lateCount; int get lateMinutes;
/// Create a copy of WorkdayTotals
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WorkdayTotalsCopyWith<WorkdayTotals> get copyWith => _$WorkdayTotalsCopyWithImpl<WorkdayTotals>(this as WorkdayTotals, _$identity);

  /// Serializes this WorkdayTotals to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WorkdayTotals;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WorkdayTotals&&(identical(other.workedMinutes, _this.workedMinutes) || other.workedMinutes == _this.workedMinutes)&&(identical(other.lateCount, _this.lateCount) || other.lateCount == _this.lateCount)&&(identical(other.lateMinutes, _this.lateMinutes) || other.lateMinutes == _this.lateMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WorkdayTotals;
  return Object.hash(runtimeType,_this.workedMinutes,_this.lateCount,_this.lateMinutes);
}

@override
String toString() {
  final _this = this as WorkdayTotals;
  return 'WorkdayTotals(workedMinutes: ${_this.workedMinutes}, lateCount: ${_this.lateCount}, lateMinutes: ${_this.lateMinutes})';
}


}

/// @nodoc
abstract mixin class $WorkdayTotalsCopyWith<$Res>  {
  factory $WorkdayTotalsCopyWith(WorkdayTotals value, $Res Function(WorkdayTotals) _then) = _$WorkdayTotalsCopyWithImpl;
@useResult
$Res call({
 int workedMinutes, int lateCount, int lateMinutes
});




}
/// @nodoc
class _$WorkdayTotalsCopyWithImpl<$Res>
    implements $WorkdayTotalsCopyWith<$Res> {
  _$WorkdayTotalsCopyWithImpl(this._self, this._then);

  final WorkdayTotals _self;
  final $Res Function(WorkdayTotals) _then;

/// Create a copy of WorkdayTotals
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? workedMinutes = null,Object? lateCount = null,Object? lateMinutes = null,}) {
  return _then(WorkdayTotals(
workedMinutes: null == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int,lateCount: null == lateCount ? _self.lateCount : lateCount // ignore: cast_nullable_to_non_nullable
as int,lateMinutes: null == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [WorkdayTotals].
extension WorkdayTotalsPatterns on WorkdayTotals {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WorkdayTotals value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WorkdayTotals() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WorkdayTotals value)  $default,){
final _that = this;
switch (_that) {
case _WorkdayTotals():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WorkdayTotals value)?  $default,){
final _that = this;
switch (_that) {
case _WorkdayTotals() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int workedMinutes,  int lateCount,  int lateMinutes)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WorkdayTotals() when $default != null:
return $default(_that.workedMinutes,_that.lateCount,_that.lateMinutes);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int workedMinutes,  int lateCount,  int lateMinutes)  $default,) {final _that = this;
switch (_that) {
case _WorkdayTotals():
return $default(_that.workedMinutes,_that.lateCount,_that.lateMinutes);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int workedMinutes,  int lateCount,  int lateMinutes)?  $default,) {final _that = this;
switch (_that) {
case _WorkdayTotals() when $default != null:
return $default(_that.workedMinutes,_that.lateCount,_that.lateMinutes);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WorkdayTotals implements WorkdayTotals {
  const _WorkdayTotals({this.workedMinutes = 0, this.lateCount = 0, this.lateMinutes = 0});
  factory _WorkdayTotals.fromJson(Map<String, dynamic> json) => _$WorkdayTotalsFromJson(json);

@override@JsonKey() final  int workedMinutes;
@override@JsonKey() final  int lateCount;
@override@JsonKey() final  int lateMinutes;

/// Create a copy of WorkdayTotals
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WorkdayTotalsCopyWith<_WorkdayTotals> get copyWith => __$WorkdayTotalsCopyWithImpl<_WorkdayTotals>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WorkdayTotalsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WorkdayTotals&&(identical(other.workedMinutes, workedMinutes) || other.workedMinutes == workedMinutes)&&(identical(other.lateCount, lateCount) || other.lateCount == lateCount)&&(identical(other.lateMinutes, lateMinutes) || other.lateMinutes == lateMinutes));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,workedMinutes,lateCount,lateMinutes);
}

@override
String toString() {
    return 'WorkdayTotals(workedMinutes: $workedMinutes, lateCount: $lateCount, lateMinutes: $lateMinutes)';
}


}

/// @nodoc
abstract mixin class _$WorkdayTotalsCopyWith<$Res> implements $WorkdayTotalsCopyWith<$Res> {
  factory _$WorkdayTotalsCopyWith(_WorkdayTotals value, $Res Function(_WorkdayTotals) _then) = __$WorkdayTotalsCopyWithImpl;
@override @useResult
$Res call({
 int workedMinutes, int lateCount, int lateMinutes
});




}
/// @nodoc
class __$WorkdayTotalsCopyWithImpl<$Res>
    implements _$WorkdayTotalsCopyWith<$Res> {
  __$WorkdayTotalsCopyWithImpl(this._self, this._then);

  final _WorkdayTotals _self;
  final $Res Function(_WorkdayTotals) _then;

/// Create a copy of WorkdayTotals
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? workedMinutes = null,Object? lateCount = null,Object? lateMinutes = null,}) {
  return _then(_WorkdayTotals(
workedMinutes: null == workedMinutes ? _self.workedMinutes : workedMinutes // ignore: cast_nullable_to_non_nullable
as int,lateCount: null == lateCount ? _self.lateCount : lateCount // ignore: cast_nullable_to_non_nullable
as int,lateMinutes: null == lateMinutes ? _self.lateMinutes : lateMinutes // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$MyWorkday {

 bool get available; String? get unavailableReason; WorkdayToday? get today; List<WorkdayDay> get week; WorkdayTotals? get totals; String? get syncedAt;
/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$MyWorkdayCopyWith<MyWorkday> get copyWith => _$MyWorkdayCopyWithImpl<MyWorkday>(this as MyWorkday, _$identity);

  /// Serializes this MyWorkday to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as MyWorkday;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is MyWorkday&&(identical(other.available, _this.available) || other.available == _this.available)&&(identical(other.unavailableReason, _this.unavailableReason) || other.unavailableReason == _this.unavailableReason)&&(identical(other.today, _this.today) || other.today == _this.today)&&const DeepCollectionEquality().equals(other.week, _this.week)&&(identical(other.totals, _this.totals) || other.totals == _this.totals)&&(identical(other.syncedAt, _this.syncedAt) || other.syncedAt == _this.syncedAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as MyWorkday;
  return Object.hash(runtimeType,_this.available,_this.unavailableReason,_this.today,const DeepCollectionEquality().hash(_this.week),_this.totals,_this.syncedAt);
}

@override
String toString() {
  final _this = this as MyWorkday;
  return 'MyWorkday(available: ${_this.available}, unavailableReason: ${_this.unavailableReason}, today: ${_this.today}, week: ${_this.week}, totals: ${_this.totals}, syncedAt: ${_this.syncedAt})';
}


}

/// @nodoc
abstract mixin class $MyWorkdayCopyWith<$Res>  {
  factory $MyWorkdayCopyWith(MyWorkday value, $Res Function(MyWorkday) _then) = _$MyWorkdayCopyWithImpl;
@useResult
$Res call({
 bool available, String? unavailableReason, WorkdayToday? today, List<WorkdayDay> week, WorkdayTotals? totals, String? syncedAt
});


$WorkdayTodayCopyWith<$Res>? get today;$WorkdayTotalsCopyWith<$Res>? get totals;

}
/// @nodoc
class _$MyWorkdayCopyWithImpl<$Res>
    implements $MyWorkdayCopyWith<$Res> {
  _$MyWorkdayCopyWithImpl(this._self, this._then);

  final MyWorkday _self;
  final $Res Function(MyWorkday) _then;

/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? available = null,Object? unavailableReason = freezed,Object? today = freezed,Object? week = null,Object? totals = freezed,Object? syncedAt = freezed,}) {
  return _then(MyWorkday(
available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as bool,unavailableReason: freezed == unavailableReason ? _self.unavailableReason : unavailableReason // ignore: cast_nullable_to_non_nullable
as String?,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as WorkdayToday?,week: null == week ? _self.week : week // ignore: cast_nullable_to_non_nullable
as List<WorkdayDay>,totals: freezed == totals ? _self.totals : totals // ignore: cast_nullable_to_non_nullable
as WorkdayTotals?,syncedAt: freezed == syncedAt ? _self.syncedAt : syncedAt // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}
/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$WorkdayTodayCopyWith<$Res>? get today {
    if (_self.today == null) {
    return null;
  }

  return $WorkdayTodayCopyWith<$Res>(_self.today!, (value) {
    return _then(_self.copyWith(today: value));
  });
}/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$WorkdayTotalsCopyWith<$Res>? get totals {
    if (_self.totals == null) {
    return null;
  }

  return $WorkdayTotalsCopyWith<$Res>(_self.totals!, (value) {
    return _then(_self.copyWith(totals: value));
  });
}
}


/// Adds pattern-matching-related methods to [MyWorkday].
extension MyWorkdayPatterns on MyWorkday {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _MyWorkday value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _MyWorkday() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _MyWorkday value)  $default,){
final _that = this;
switch (_that) {
case _MyWorkday():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _MyWorkday value)?  $default,){
final _that = this;
switch (_that) {
case _MyWorkday() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( bool available,  String? unavailableReason,  WorkdayToday? today,  List<WorkdayDay> week,  WorkdayTotals? totals,  String? syncedAt)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _MyWorkday() when $default != null:
return $default(_that.available,_that.unavailableReason,_that.today,_that.week,_that.totals,_that.syncedAt);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( bool available,  String? unavailableReason,  WorkdayToday? today,  List<WorkdayDay> week,  WorkdayTotals? totals,  String? syncedAt)  $default,) {final _that = this;
switch (_that) {
case _MyWorkday():
return $default(_that.available,_that.unavailableReason,_that.today,_that.week,_that.totals,_that.syncedAt);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( bool available,  String? unavailableReason,  WorkdayToday? today,  List<WorkdayDay> week,  WorkdayTotals? totals,  String? syncedAt)?  $default,) {final _that = this;
switch (_that) {
case _MyWorkday() when $default != null:
return $default(_that.available,_that.unavailableReason,_that.today,_that.week,_that.totals,_that.syncedAt);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _MyWorkday implements MyWorkday {
  const _MyWorkday({this.available = false, this.unavailableReason, this.today,  List<WorkdayDay> week = const <WorkdayDay>[], this.totals, this.syncedAt}): _week = week;
  factory _MyWorkday.fromJson(Map<String, dynamic> json) => _$MyWorkdayFromJson(json);

@override@JsonKey() final  bool available;
@override final  String? unavailableReason;
@override final  WorkdayToday? today;
 final  List<WorkdayDay> _week;
@override@JsonKey() List<WorkdayDay> get week {
  if (_week is EqualUnmodifiableListView) return _week;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_week);
}

@override final  WorkdayTotals? totals;
@override final  String? syncedAt;

/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$MyWorkdayCopyWith<_MyWorkday> get copyWith => __$MyWorkdayCopyWithImpl<_MyWorkday>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$MyWorkdayToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _MyWorkday&&(identical(other.available, available) || other.available == available)&&(identical(other.unavailableReason, unavailableReason) || other.unavailableReason == unavailableReason)&&(identical(other.today, today) || other.today == today)&&const DeepCollectionEquality().equals(other.week, _week)&&(identical(other.totals, totals) || other.totals == totals)&&(identical(other.syncedAt, syncedAt) || other.syncedAt == syncedAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,available,unavailableReason,today,const DeepCollectionEquality().hash(_week),totals,syncedAt);
}

@override
String toString() {
    return 'MyWorkday(available: $available, unavailableReason: $unavailableReason, today: $today, week: $week, totals: $totals, syncedAt: $syncedAt)';
}


}

/// @nodoc
abstract mixin class _$MyWorkdayCopyWith<$Res> implements $MyWorkdayCopyWith<$Res> {
  factory _$MyWorkdayCopyWith(_MyWorkday value, $Res Function(_MyWorkday) _then) = __$MyWorkdayCopyWithImpl;
@override @useResult
$Res call({
 bool available, String? unavailableReason, WorkdayToday? today, List<WorkdayDay> week, WorkdayTotals? totals, String? syncedAt
});


@override $WorkdayTodayCopyWith<$Res>? get today;@override $WorkdayTotalsCopyWith<$Res>? get totals;

}
/// @nodoc
class __$MyWorkdayCopyWithImpl<$Res>
    implements _$MyWorkdayCopyWith<$Res> {
  __$MyWorkdayCopyWithImpl(this._self, this._then);

  final _MyWorkday _self;
  final $Res Function(_MyWorkday) _then;

/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? available = null,Object? unavailableReason = freezed,Object? today = freezed,Object? week = null,Object? totals = freezed,Object? syncedAt = freezed,}) {
  return _then(_MyWorkday(
available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as bool,unavailableReason: freezed == unavailableReason ? _self.unavailableReason : unavailableReason // ignore: cast_nullable_to_non_nullable
as String?,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as WorkdayToday?,week: null == week ? _self._week : week // ignore: cast_nullable_to_non_nullable
as List<WorkdayDay>,totals: freezed == totals ? _self.totals : totals // ignore: cast_nullable_to_non_nullable
as WorkdayTotals?,syncedAt: freezed == syncedAt ? _self.syncedAt : syncedAt // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$WorkdayTodayCopyWith<$Res>? get today {
    if (_self.today == null) {
    return null;
  }

  return $WorkdayTodayCopyWith<$Res>(_self.today!, (value) {
    return _then(_self.copyWith(today: value));
  });
}/// Create a copy of MyWorkday
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$WorkdayTotalsCopyWith<$Res>? get totals {
    if (_self.totals == null) {
    return null;
  }

  return $WorkdayTotalsCopyWith<$Res>(_self.totals!, (value) {
    return _then(_self.copyWith(totals: value));
  });
}
}


/// @nodoc
mixin _$MyTasks {

 bool get available; int get total;@JsonKey(name: 'new') int get fresh; int get inProgress; int get waiting; int get overdue; int get done;
/// Create a copy of MyTasks
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$MyTasksCopyWith<MyTasks> get copyWith => _$MyTasksCopyWithImpl<MyTasks>(this as MyTasks, _$identity);

  /// Serializes this MyTasks to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as MyTasks;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is MyTasks&&(identical(other.available, _this.available) || other.available == _this.available)&&(identical(other.total, _this.total) || other.total == _this.total)&&(identical(other.fresh, _this.fresh) || other.fresh == _this.fresh)&&(identical(other.inProgress, _this.inProgress) || other.inProgress == _this.inProgress)&&(identical(other.waiting, _this.waiting) || other.waiting == _this.waiting)&&(identical(other.overdue, _this.overdue) || other.overdue == _this.overdue)&&(identical(other.done, _this.done) || other.done == _this.done));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as MyTasks;
  return Object.hash(runtimeType,_this.available,_this.total,_this.fresh,_this.inProgress,_this.waiting,_this.overdue,_this.done);
}

@override
String toString() {
  final _this = this as MyTasks;
  return 'MyTasks(available: ${_this.available}, total: ${_this.total}, fresh: ${_this.fresh}, inProgress: ${_this.inProgress}, waiting: ${_this.waiting}, overdue: ${_this.overdue}, done: ${_this.done})';
}


}

/// @nodoc
abstract mixin class $MyTasksCopyWith<$Res>  {
  factory $MyTasksCopyWith(MyTasks value, $Res Function(MyTasks) _then) = _$MyTasksCopyWithImpl;
@useResult
$Res call({
 bool available, int total,@JsonKey(name: 'new') int fresh, int inProgress, int waiting, int overdue, int done
});




}
/// @nodoc
class _$MyTasksCopyWithImpl<$Res>
    implements $MyTasksCopyWith<$Res> {
  _$MyTasksCopyWithImpl(this._self, this._then);

  final MyTasks _self;
  final $Res Function(MyTasks) _then;

/// Create a copy of MyTasks
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? available = null,Object? total = null,Object? fresh = null,Object? inProgress = null,Object? waiting = null,Object? overdue = null,Object? done = null,}) {
  return _then(MyTasks(
available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as bool,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as int,fresh: null == fresh ? _self.fresh : fresh // ignore: cast_nullable_to_non_nullable
as int,inProgress: null == inProgress ? _self.inProgress : inProgress // ignore: cast_nullable_to_non_nullable
as int,waiting: null == waiting ? _self.waiting : waiting // ignore: cast_nullable_to_non_nullable
as int,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as int,done: null == done ? _self.done : done // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [MyTasks].
extension MyTasksPatterns on MyTasks {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _MyTasks value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _MyTasks() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _MyTasks value)  $default,){
final _that = this;
switch (_that) {
case _MyTasks():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _MyTasks value)?  $default,){
final _that = this;
switch (_that) {
case _MyTasks() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( bool available,  int total, @JsonKey(name: 'new')  int fresh,  int inProgress,  int waiting,  int overdue,  int done)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _MyTasks() when $default != null:
return $default(_that.available,_that.total,_that.fresh,_that.inProgress,_that.waiting,_that.overdue,_that.done);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( bool available,  int total, @JsonKey(name: 'new')  int fresh,  int inProgress,  int waiting,  int overdue,  int done)  $default,) {final _that = this;
switch (_that) {
case _MyTasks():
return $default(_that.available,_that.total,_that.fresh,_that.inProgress,_that.waiting,_that.overdue,_that.done);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( bool available,  int total, @JsonKey(name: 'new')  int fresh,  int inProgress,  int waiting,  int overdue,  int done)?  $default,) {final _that = this;
switch (_that) {
case _MyTasks() when $default != null:
return $default(_that.available,_that.total,_that.fresh,_that.inProgress,_that.waiting,_that.overdue,_that.done);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _MyTasks implements MyTasks {
  const _MyTasks({this.available = false, this.total = 0, @JsonKey(name: 'new') this.fresh = 0, this.inProgress = 0, this.waiting = 0, this.overdue = 0, this.done = 0});
  factory _MyTasks.fromJson(Map<String, dynamic> json) => _$MyTasksFromJson(json);

@override@JsonKey() final  bool available;
@override@JsonKey() final  int total;
@override@JsonKey(name: 'new') final  int fresh;
@override@JsonKey() final  int inProgress;
@override@JsonKey() final  int waiting;
@override@JsonKey() final  int overdue;
@override@JsonKey() final  int done;

/// Create a copy of MyTasks
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$MyTasksCopyWith<_MyTasks> get copyWith => __$MyTasksCopyWithImpl<_MyTasks>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$MyTasksToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _MyTasks&&(identical(other.available, available) || other.available == available)&&(identical(other.total, total) || other.total == total)&&(identical(other.fresh, fresh) || other.fresh == fresh)&&(identical(other.inProgress, inProgress) || other.inProgress == inProgress)&&(identical(other.waiting, waiting) || other.waiting == waiting)&&(identical(other.overdue, overdue) || other.overdue == overdue)&&(identical(other.done, done) || other.done == done));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,available,total,fresh,inProgress,waiting,overdue,done);
}

@override
String toString() {
    return 'MyTasks(available: $available, total: $total, fresh: $fresh, inProgress: $inProgress, waiting: $waiting, overdue: $overdue, done: $done)';
}


}

/// @nodoc
abstract mixin class _$MyTasksCopyWith<$Res> implements $MyTasksCopyWith<$Res> {
  factory _$MyTasksCopyWith(_MyTasks value, $Res Function(_MyTasks) _then) = __$MyTasksCopyWithImpl;
@override @useResult
$Res call({
 bool available, int total,@JsonKey(name: 'new') int fresh, int inProgress, int waiting, int overdue, int done
});




}
/// @nodoc
class __$MyTasksCopyWithImpl<$Res>
    implements _$MyTasksCopyWith<$Res> {
  __$MyTasksCopyWithImpl(this._self, this._then);

  final _MyTasks _self;
  final $Res Function(_MyTasks) _then;

/// Create a copy of MyTasks
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? available = null,Object? total = null,Object? fresh = null,Object? inProgress = null,Object? waiting = null,Object? overdue = null,Object? done = null,}) {
  return _then(_MyTasks(
available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as bool,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as int,fresh: null == fresh ? _self.fresh : fresh // ignore: cast_nullable_to_non_nullable
as int,inProgress: null == inProgress ? _self.inProgress : inProgress // ignore: cast_nullable_to_non_nullable
as int,waiting: null == waiting ? _self.waiting : waiting // ignore: cast_nullable_to_non_nullable
as int,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as int,done: null == done ? _self.done : done // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$NotificationPrefs {

 List<String> get muted; List<String> get categories;
/// Create a copy of NotificationPrefs
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$NotificationPrefsCopyWith<NotificationPrefs> get copyWith => _$NotificationPrefsCopyWithImpl<NotificationPrefs>(this as NotificationPrefs, _$identity);

  /// Serializes this NotificationPrefs to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as NotificationPrefs;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is NotificationPrefs&&const DeepCollectionEquality().equals(other.muted, _this.muted)&&const DeepCollectionEquality().equals(other.categories, _this.categories));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as NotificationPrefs;
  return Object.hash(runtimeType,const DeepCollectionEquality().hash(_this.muted),const DeepCollectionEquality().hash(_this.categories));
}

@override
String toString() {
  final _this = this as NotificationPrefs;
  return 'NotificationPrefs(muted: ${_this.muted}, categories: ${_this.categories})';
}


}

/// @nodoc
abstract mixin class $NotificationPrefsCopyWith<$Res>  {
  factory $NotificationPrefsCopyWith(NotificationPrefs value, $Res Function(NotificationPrefs) _then) = _$NotificationPrefsCopyWithImpl;
@useResult
$Res call({
 List<String> muted, List<String> categories
});




}
/// @nodoc
class _$NotificationPrefsCopyWithImpl<$Res>
    implements $NotificationPrefsCopyWith<$Res> {
  _$NotificationPrefsCopyWithImpl(this._self, this._then);

  final NotificationPrefs _self;
  final $Res Function(NotificationPrefs) _then;

/// Create a copy of NotificationPrefs
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? muted = null,Object? categories = null,}) {
  return _then(NotificationPrefs(
muted: null == muted ? _self.muted : muted // ignore: cast_nullable_to_non_nullable
as List<String>,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}

}


/// Adds pattern-matching-related methods to [NotificationPrefs].
extension NotificationPrefsPatterns on NotificationPrefs {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _NotificationPrefs value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _NotificationPrefs() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _NotificationPrefs value)  $default,){
final _that = this;
switch (_that) {
case _NotificationPrefs():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _NotificationPrefs value)?  $default,){
final _that = this;
switch (_that) {
case _NotificationPrefs() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( List<String> muted,  List<String> categories)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _NotificationPrefs() when $default != null:
return $default(_that.muted,_that.categories);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( List<String> muted,  List<String> categories)  $default,) {final _that = this;
switch (_that) {
case _NotificationPrefs():
return $default(_that.muted,_that.categories);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( List<String> muted,  List<String> categories)?  $default,) {final _that = this;
switch (_that) {
case _NotificationPrefs() when $default != null:
return $default(_that.muted,_that.categories);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _NotificationPrefs implements NotificationPrefs {
  const _NotificationPrefs({ List<String> muted = const <String>[],  List<String> categories = const <String>[]}): _muted = muted,_categories = categories;
  factory _NotificationPrefs.fromJson(Map<String, dynamic> json) => _$NotificationPrefsFromJson(json);

 final  List<String> _muted;
@override@JsonKey() List<String> get muted {
  if (_muted is EqualUnmodifiableListView) return _muted;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_muted);
}

 final  List<String> _categories;
@override@JsonKey() List<String> get categories {
  if (_categories is EqualUnmodifiableListView) return _categories;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_categories);
}


/// Create a copy of NotificationPrefs
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$NotificationPrefsCopyWith<_NotificationPrefs> get copyWith => __$NotificationPrefsCopyWithImpl<_NotificationPrefs>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$NotificationPrefsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _NotificationPrefs&&const DeepCollectionEquality().equals(other.muted, _muted)&&const DeepCollectionEquality().equals(other.categories, _categories));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,const DeepCollectionEquality().hash(_muted),const DeepCollectionEquality().hash(_categories));
}

@override
String toString() {
    return 'NotificationPrefs(muted: $muted, categories: $categories)';
}


}

/// @nodoc
abstract mixin class _$NotificationPrefsCopyWith<$Res> implements $NotificationPrefsCopyWith<$Res> {
  factory _$NotificationPrefsCopyWith(_NotificationPrefs value, $Res Function(_NotificationPrefs) _then) = __$NotificationPrefsCopyWithImpl;
@override @useResult
$Res call({
 List<String> muted, List<String> categories
});




}
/// @nodoc
class __$NotificationPrefsCopyWithImpl<$Res>
    implements _$NotificationPrefsCopyWith<$Res> {
  __$NotificationPrefsCopyWithImpl(this._self, this._then);

  final _NotificationPrefs _self;
  final $Res Function(_NotificationPrefs) _then;

/// Create a copy of NotificationPrefs
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? muted = null,Object? categories = null,}) {
  return _then(_NotificationPrefs(
muted: null == muted ? _self._muted : muted // ignore: cast_nullable_to_non_nullable
as List<String>,categories: null == categories ? _self._categories : categories // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}


}


/// @nodoc
mixin _$TelegramLink {

 String get permissionId; String get account; String? get accountUsername; String? get username;/// Full — полный доступ; иначе — только отправленные данные.
 String? get mode; String? get verifiedAt;
/// Create a copy of TelegramLink
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TelegramLinkCopyWith<TelegramLink> get copyWith => _$TelegramLinkCopyWithImpl<TelegramLink>(this as TelegramLink, _$identity);

  /// Serializes this TelegramLink to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TelegramLink;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TelegramLink&&(identical(other.permissionId, _this.permissionId) || other.permissionId == _this.permissionId)&&(identical(other.account, _this.account) || other.account == _this.account)&&(identical(other.accountUsername, _this.accountUsername) || other.accountUsername == _this.accountUsername)&&(identical(other.username, _this.username) || other.username == _this.username)&&(identical(other.mode, _this.mode) || other.mode == _this.mode)&&(identical(other.verifiedAt, _this.verifiedAt) || other.verifiedAt == _this.verifiedAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TelegramLink;
  return Object.hash(runtimeType,_this.permissionId,_this.account,_this.accountUsername,_this.username,_this.mode,_this.verifiedAt);
}

@override
String toString() {
  final _this = this as TelegramLink;
  return 'TelegramLink(permissionId: ${_this.permissionId}, account: ${_this.account}, accountUsername: ${_this.accountUsername}, username: ${_this.username}, mode: ${_this.mode}, verifiedAt: ${_this.verifiedAt})';
}


}

/// @nodoc
abstract mixin class $TelegramLinkCopyWith<$Res>  {
  factory $TelegramLinkCopyWith(TelegramLink value, $Res Function(TelegramLink) _then) = _$TelegramLinkCopyWithImpl;
@useResult
$Res call({
 String permissionId, String account, String? accountUsername, String? username, String? mode, String? verifiedAt
});




}
/// @nodoc
class _$TelegramLinkCopyWithImpl<$Res>
    implements $TelegramLinkCopyWith<$Res> {
  _$TelegramLinkCopyWithImpl(this._self, this._then);

  final TelegramLink _self;
  final $Res Function(TelegramLink) _then;

/// Create a copy of TelegramLink
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? permissionId = null,Object? account = null,Object? accountUsername = freezed,Object? username = freezed,Object? mode = freezed,Object? verifiedAt = freezed,}) {
  return _then(TelegramLink(
permissionId: null == permissionId ? _self.permissionId : permissionId // ignore: cast_nullable_to_non_nullable
as String,account: null == account ? _self.account : account // ignore: cast_nullable_to_non_nullable
as String,accountUsername: freezed == accountUsername ? _self.accountUsername : accountUsername // ignore: cast_nullable_to_non_nullable
as String?,username: freezed == username ? _self.username : username // ignore: cast_nullable_to_non_nullable
as String?,mode: freezed == mode ? _self.mode : mode // ignore: cast_nullable_to_non_nullable
as String?,verifiedAt: freezed == verifiedAt ? _self.verifiedAt : verifiedAt // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [TelegramLink].
extension TelegramLinkPatterns on TelegramLink {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TelegramLink value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TelegramLink() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TelegramLink value)  $default,){
final _that = this;
switch (_that) {
case _TelegramLink():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TelegramLink value)?  $default,){
final _that = this;
switch (_that) {
case _TelegramLink() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String permissionId,  String account,  String? accountUsername,  String? username,  String? mode,  String? verifiedAt)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TelegramLink() when $default != null:
return $default(_that.permissionId,_that.account,_that.accountUsername,_that.username,_that.mode,_that.verifiedAt);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String permissionId,  String account,  String? accountUsername,  String? username,  String? mode,  String? verifiedAt)  $default,) {final _that = this;
switch (_that) {
case _TelegramLink():
return $default(_that.permissionId,_that.account,_that.accountUsername,_that.username,_that.mode,_that.verifiedAt);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String permissionId,  String account,  String? accountUsername,  String? username,  String? mode,  String? verifiedAt)?  $default,) {final _that = this;
switch (_that) {
case _TelegramLink() when $default != null:
return $default(_that.permissionId,_that.account,_that.accountUsername,_that.username,_that.mode,_that.verifiedAt);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TelegramLink implements TelegramLink {
  const _TelegramLink({required this.permissionId, required this.account, this.accountUsername, this.username, this.mode, this.verifiedAt});
  factory _TelegramLink.fromJson(Map<String, dynamic> json) => _$TelegramLinkFromJson(json);

@override final  String permissionId;
@override final  String account;
@override final  String? accountUsername;
@override final  String? username;
/// Full — полный доступ; иначе — только отправленные данные.
@override final  String? mode;
@override final  String? verifiedAt;

/// Create a copy of TelegramLink
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TelegramLinkCopyWith<_TelegramLink> get copyWith => __$TelegramLinkCopyWithImpl<_TelegramLink>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TelegramLinkToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TelegramLink&&(identical(other.permissionId, permissionId) || other.permissionId == permissionId)&&(identical(other.account, account) || other.account == account)&&(identical(other.accountUsername, accountUsername) || other.accountUsername == accountUsername)&&(identical(other.username, username) || other.username == username)&&(identical(other.mode, mode) || other.mode == mode)&&(identical(other.verifiedAt, verifiedAt) || other.verifiedAt == verifiedAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,permissionId,account,accountUsername,username,mode,verifiedAt);
}

@override
String toString() {
    return 'TelegramLink(permissionId: $permissionId, account: $account, accountUsername: $accountUsername, username: $username, mode: $mode, verifiedAt: $verifiedAt)';
}


}

/// @nodoc
abstract mixin class _$TelegramLinkCopyWith<$Res> implements $TelegramLinkCopyWith<$Res> {
  factory _$TelegramLinkCopyWith(_TelegramLink value, $Res Function(_TelegramLink) _then) = __$TelegramLinkCopyWithImpl;
@override @useResult
$Res call({
 String permissionId, String account, String? accountUsername, String? username, String? mode, String? verifiedAt
});




}
/// @nodoc
class __$TelegramLinkCopyWithImpl<$Res>
    implements _$TelegramLinkCopyWith<$Res> {
  __$TelegramLinkCopyWithImpl(this._self, this._then);

  final _TelegramLink _self;
  final $Res Function(_TelegramLink) _then;

/// Create a copy of TelegramLink
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? permissionId = null,Object? account = null,Object? accountUsername = freezed,Object? username = freezed,Object? mode = freezed,Object? verifiedAt = freezed,}) {
  return _then(_TelegramLink(
permissionId: null == permissionId ? _self.permissionId : permissionId // ignore: cast_nullable_to_non_nullable
as String,account: null == account ? _self.account : account // ignore: cast_nullable_to_non_nullable
as String,accountUsername: freezed == accountUsername ? _self.accountUsername : accountUsername // ignore: cast_nullable_to_non_nullable
as String?,username: freezed == username ? _self.username : username // ignore: cast_nullable_to_non_nullable
as String?,mode: freezed == mode ? _self.mode : mode // ignore: cast_nullable_to_non_nullable
as String?,verifiedAt: freezed == verifiedAt ? _self.verifiedAt : verifiedAt // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$TelegramCode {

 String get code; String get expiresAt; List<String> get sendTo;
/// Create a copy of TelegramCode
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TelegramCodeCopyWith<TelegramCode> get copyWith => _$TelegramCodeCopyWithImpl<TelegramCode>(this as TelegramCode, _$identity);

  /// Serializes this TelegramCode to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TelegramCode;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TelegramCode&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.expiresAt, _this.expiresAt) || other.expiresAt == _this.expiresAt)&&const DeepCollectionEquality().equals(other.sendTo, _this.sendTo));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TelegramCode;
  return Object.hash(runtimeType,_this.code,_this.expiresAt,const DeepCollectionEquality().hash(_this.sendTo));
}

@override
String toString() {
  final _this = this as TelegramCode;
  return 'TelegramCode(code: ${_this.code}, expiresAt: ${_this.expiresAt}, sendTo: ${_this.sendTo})';
}


}

/// @nodoc
abstract mixin class $TelegramCodeCopyWith<$Res>  {
  factory $TelegramCodeCopyWith(TelegramCode value, $Res Function(TelegramCode) _then) = _$TelegramCodeCopyWithImpl;
@useResult
$Res call({
 String code, String expiresAt, List<String> sendTo
});




}
/// @nodoc
class _$TelegramCodeCopyWithImpl<$Res>
    implements $TelegramCodeCopyWith<$Res> {
  _$TelegramCodeCopyWithImpl(this._self, this._then);

  final TelegramCode _self;
  final $Res Function(TelegramCode) _then;

/// Create a copy of TelegramCode
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? code = null,Object? expiresAt = null,Object? sendTo = null,}) {
  return _then(TelegramCode(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,expiresAt: null == expiresAt ? _self.expiresAt : expiresAt // ignore: cast_nullable_to_non_nullable
as String,sendTo: null == sendTo ? _self.sendTo : sendTo // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}

}


/// Adds pattern-matching-related methods to [TelegramCode].
extension TelegramCodePatterns on TelegramCode {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TelegramCode value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TelegramCode() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TelegramCode value)  $default,){
final _that = this;
switch (_that) {
case _TelegramCode():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TelegramCode value)?  $default,){
final _that = this;
switch (_that) {
case _TelegramCode() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String code,  String expiresAt,  List<String> sendTo)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TelegramCode() when $default != null:
return $default(_that.code,_that.expiresAt,_that.sendTo);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String code,  String expiresAt,  List<String> sendTo)  $default,) {final _that = this;
switch (_that) {
case _TelegramCode():
return $default(_that.code,_that.expiresAt,_that.sendTo);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String code,  String expiresAt,  List<String> sendTo)?  $default,) {final _that = this;
switch (_that) {
case _TelegramCode() when $default != null:
return $default(_that.code,_that.expiresAt,_that.sendTo);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TelegramCode implements TelegramCode {
  const _TelegramCode({required this.code, required this.expiresAt,  List<String> sendTo = const <String>[]}): _sendTo = sendTo;
  factory _TelegramCode.fromJson(Map<String, dynamic> json) => _$TelegramCodeFromJson(json);

@override final  String code;
@override final  String expiresAt;
 final  List<String> _sendTo;
@override@JsonKey() List<String> get sendTo {
  if (_sendTo is EqualUnmodifiableListView) return _sendTo;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_sendTo);
}


/// Create a copy of TelegramCode
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TelegramCodeCopyWith<_TelegramCode> get copyWith => __$TelegramCodeCopyWithImpl<_TelegramCode>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TelegramCodeToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TelegramCode&&(identical(other.code, code) || other.code == code)&&(identical(other.expiresAt, expiresAt) || other.expiresAt == expiresAt)&&const DeepCollectionEquality().equals(other.sendTo, _sendTo));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,code,expiresAt,const DeepCollectionEquality().hash(_sendTo));
}

@override
String toString() {
    return 'TelegramCode(code: $code, expiresAt: $expiresAt, sendTo: $sendTo)';
}


}

/// @nodoc
abstract mixin class _$TelegramCodeCopyWith<$Res> implements $TelegramCodeCopyWith<$Res> {
  factory _$TelegramCodeCopyWith(_TelegramCode value, $Res Function(_TelegramCode) _then) = __$TelegramCodeCopyWithImpl;
@override @useResult
$Res call({
 String code, String expiresAt, List<String> sendTo
});




}
/// @nodoc
class __$TelegramCodeCopyWithImpl<$Res>
    implements _$TelegramCodeCopyWith<$Res> {
  __$TelegramCodeCopyWithImpl(this._self, this._then);

  final _TelegramCode _self;
  final $Res Function(_TelegramCode) _then;

/// Create a copy of TelegramCode
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? code = null,Object? expiresAt = null,Object? sendTo = null,}) {
  return _then(_TelegramCode(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,expiresAt: null == expiresAt ? _self.expiresAt : expiresAt // ignore: cast_nullable_to_non_nullable
as String,sendTo: null == sendTo ? _self._sendTo : sendTo // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}


}

// dart format on
