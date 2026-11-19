// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'dashboard_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$SalesPeriod {

 int get year; int get month;/// Последний день с данными («2026-11-12»).
 String get dataThrough; String? get today; int get workedDays; double? get elapsedDays; int get daysInMonth; bool get closed; String? get previousCutoff;
/// Create a copy of SalesPeriod
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<SalesPeriod> get copyWith => _$SalesPeriodCopyWithImpl<SalesPeriod>(this as SalesPeriod, _$identity);

  /// Serializes this SalesPeriod to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SalesPeriod;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SalesPeriod&&(identical(other.year, _this.year) || other.year == _this.year)&&(identical(other.month, _this.month) || other.month == _this.month)&&(identical(other.dataThrough, _this.dataThrough) || other.dataThrough == _this.dataThrough)&&(identical(other.today, _this.today) || other.today == _this.today)&&(identical(other.workedDays, _this.workedDays) || other.workedDays == _this.workedDays)&&(identical(other.elapsedDays, _this.elapsedDays) || other.elapsedDays == _this.elapsedDays)&&(identical(other.daysInMonth, _this.daysInMonth) || other.daysInMonth == _this.daysInMonth)&&(identical(other.closed, _this.closed) || other.closed == _this.closed)&&(identical(other.previousCutoff, _this.previousCutoff) || other.previousCutoff == _this.previousCutoff));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SalesPeriod;
  return Object.hash(runtimeType,_this.year,_this.month,_this.dataThrough,_this.today,_this.workedDays,_this.elapsedDays,_this.daysInMonth,_this.closed,_this.previousCutoff);
}

@override
String toString() {
  final _this = this as SalesPeriod;
  return 'SalesPeriod(year: ${_this.year}, month: ${_this.month}, dataThrough: ${_this.dataThrough}, today: ${_this.today}, workedDays: ${_this.workedDays}, elapsedDays: ${_this.elapsedDays}, daysInMonth: ${_this.daysInMonth}, closed: ${_this.closed}, previousCutoff: ${_this.previousCutoff})';
}


}

/// @nodoc
abstract mixin class $SalesPeriodCopyWith<$Res>  {
  factory $SalesPeriodCopyWith(SalesPeriod value, $Res Function(SalesPeriod) _then) = _$SalesPeriodCopyWithImpl;
@useResult
$Res call({
 int year, int month, String dataThrough, String? today, int workedDays, double? elapsedDays, int daysInMonth, bool closed, String? previousCutoff
});




}
/// @nodoc
class _$SalesPeriodCopyWithImpl<$Res>
    implements $SalesPeriodCopyWith<$Res> {
  _$SalesPeriodCopyWithImpl(this._self, this._then);

  final SalesPeriod _self;
  final $Res Function(SalesPeriod) _then;

/// Create a copy of SalesPeriod
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? year = null,Object? month = null,Object? dataThrough = null,Object? today = freezed,Object? workedDays = null,Object? elapsedDays = freezed,Object? daysInMonth = null,Object? closed = null,Object? previousCutoff = freezed,}) {
  return _then(SalesPeriod(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,dataThrough: null == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as String?,workedDays: null == workedDays ? _self.workedDays : workedDays // ignore: cast_nullable_to_non_nullable
as int,elapsedDays: freezed == elapsedDays ? _self.elapsedDays : elapsedDays // ignore: cast_nullable_to_non_nullable
as double?,daysInMonth: null == daysInMonth ? _self.daysInMonth : daysInMonth // ignore: cast_nullable_to_non_nullable
as int,closed: null == closed ? _self.closed : closed // ignore: cast_nullable_to_non_nullable
as bool,previousCutoff: freezed == previousCutoff ? _self.previousCutoff : previousCutoff // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [SalesPeriod].
extension SalesPeriodPatterns on SalesPeriod {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SalesPeriod value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SalesPeriod() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SalesPeriod value)  $default,){
final _that = this;
switch (_that) {
case _SalesPeriod():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SalesPeriod value)?  $default,){
final _that = this;
switch (_that) {
case _SalesPeriod() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int year,  int month,  String dataThrough,  String? today,  int workedDays,  double? elapsedDays,  int daysInMonth,  bool closed,  String? previousCutoff)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SalesPeriod() when $default != null:
return $default(_that.year,_that.month,_that.dataThrough,_that.today,_that.workedDays,_that.elapsedDays,_that.daysInMonth,_that.closed,_that.previousCutoff);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int year,  int month,  String dataThrough,  String? today,  int workedDays,  double? elapsedDays,  int daysInMonth,  bool closed,  String? previousCutoff)  $default,) {final _that = this;
switch (_that) {
case _SalesPeriod():
return $default(_that.year,_that.month,_that.dataThrough,_that.today,_that.workedDays,_that.elapsedDays,_that.daysInMonth,_that.closed,_that.previousCutoff);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int year,  int month,  String dataThrough,  String? today,  int workedDays,  double? elapsedDays,  int daysInMonth,  bool closed,  String? previousCutoff)?  $default,) {final _that = this;
switch (_that) {
case _SalesPeriod() when $default != null:
return $default(_that.year,_that.month,_that.dataThrough,_that.today,_that.workedDays,_that.elapsedDays,_that.daysInMonth,_that.closed,_that.previousCutoff);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SalesPeriod implements SalesPeriod {
  const _SalesPeriod({required this.year, required this.month, required this.dataThrough, this.today, this.workedDays = 0, this.elapsedDays, required this.daysInMonth, this.closed = false, this.previousCutoff});
  factory _SalesPeriod.fromJson(Map<String, dynamic> json) => _$SalesPeriodFromJson(json);

@override final  int year;
@override final  int month;
/// Последний день с данными («2026-11-12»).
@override final  String dataThrough;
@override final  String? today;
@override@JsonKey() final  int workedDays;
@override final  double? elapsedDays;
@override final  int daysInMonth;
@override@JsonKey() final  bool closed;
@override final  String? previousCutoff;

/// Create a copy of SalesPeriod
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SalesPeriodCopyWith<_SalesPeriod> get copyWith => __$SalesPeriodCopyWithImpl<_SalesPeriod>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SalesPeriodToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SalesPeriod&&(identical(other.year, year) || other.year == year)&&(identical(other.month, month) || other.month == month)&&(identical(other.dataThrough, dataThrough) || other.dataThrough == dataThrough)&&(identical(other.today, today) || other.today == today)&&(identical(other.workedDays, workedDays) || other.workedDays == workedDays)&&(identical(other.elapsedDays, elapsedDays) || other.elapsedDays == elapsedDays)&&(identical(other.daysInMonth, daysInMonth) || other.daysInMonth == daysInMonth)&&(identical(other.closed, closed) || other.closed == closed)&&(identical(other.previousCutoff, previousCutoff) || other.previousCutoff == previousCutoff));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,year,month,dataThrough,today,workedDays,elapsedDays,daysInMonth,closed,previousCutoff);
}

@override
String toString() {
    return 'SalesPeriod(year: $year, month: $month, dataThrough: $dataThrough, today: $today, workedDays: $workedDays, elapsedDays: $elapsedDays, daysInMonth: $daysInMonth, closed: $closed, previousCutoff: $previousCutoff)';
}


}

/// @nodoc
abstract mixin class _$SalesPeriodCopyWith<$Res> implements $SalesPeriodCopyWith<$Res> {
  factory _$SalesPeriodCopyWith(_SalesPeriod value, $Res Function(_SalesPeriod) _then) = __$SalesPeriodCopyWithImpl;
@override @useResult
$Res call({
 int year, int month, String dataThrough, String? today, int workedDays, double? elapsedDays, int daysInMonth, bool closed, String? previousCutoff
});




}
/// @nodoc
class __$SalesPeriodCopyWithImpl<$Res>
    implements _$SalesPeriodCopyWith<$Res> {
  __$SalesPeriodCopyWithImpl(this._self, this._then);

  final _SalesPeriod _self;
  final $Res Function(_SalesPeriod) _then;

/// Create a copy of SalesPeriod
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? year = null,Object? month = null,Object? dataThrough = null,Object? today = freezed,Object? workedDays = null,Object? elapsedDays = freezed,Object? daysInMonth = null,Object? closed = null,Object? previousCutoff = freezed,}) {
  return _then(_SalesPeriod(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,dataThrough: null == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as String?,workedDays: null == workedDays ? _self.workedDays : workedDays // ignore: cast_nullable_to_non_nullable
as int,elapsedDays: freezed == elapsedDays ? _self.elapsedDays : elapsedDays // ignore: cast_nullable_to_non_nullable
as double?,daysInMonth: null == daysInMonth ? _self.daysInMonth : daysInMonth // ignore: cast_nullable_to_non_nullable
as int,closed: null == closed ? _self.closed : closed // ignore: cast_nullable_to_non_nullable
as bool,previousCutoff: freezed == previousCutoff ? _self.previousCutoff : previousCutoff // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$TargetValue {

 double? get value; double get target; double? get ratio; String? get level; double? get forecast;
/// Create a copy of TargetValue
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TargetValueCopyWith<TargetValue> get copyWith => _$TargetValueCopyWithImpl<TargetValue>(this as TargetValue, _$identity);

  /// Serializes this TargetValue to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TargetValue;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TargetValue&&(identical(other.value, _this.value) || other.value == _this.value)&&(identical(other.target, _this.target) || other.target == _this.target)&&(identical(other.ratio, _this.ratio) || other.ratio == _this.ratio)&&(identical(other.level, _this.level) || other.level == _this.level)&&(identical(other.forecast, _this.forecast) || other.forecast == _this.forecast));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TargetValue;
  return Object.hash(runtimeType,_this.value,_this.target,_this.ratio,_this.level,_this.forecast);
}

@override
String toString() {
  final _this = this as TargetValue;
  return 'TargetValue(value: ${_this.value}, target: ${_this.target}, ratio: ${_this.ratio}, level: ${_this.level}, forecast: ${_this.forecast})';
}


}

/// @nodoc
abstract mixin class $TargetValueCopyWith<$Res>  {
  factory $TargetValueCopyWith(TargetValue value, $Res Function(TargetValue) _then) = _$TargetValueCopyWithImpl;
@useResult
$Res call({
 double? value, double target, double? ratio, String? level, double? forecast
});




}
/// @nodoc
class _$TargetValueCopyWithImpl<$Res>
    implements $TargetValueCopyWith<$Res> {
  _$TargetValueCopyWithImpl(this._self, this._then);

  final TargetValue _self;
  final $Res Function(TargetValue) _then;

/// Create a copy of TargetValue
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? value = freezed,Object? target = null,Object? ratio = freezed,Object? level = freezed,Object? forecast = freezed,}) {
  return _then(TargetValue(
value: freezed == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double?,target: null == target ? _self.target : target // ignore: cast_nullable_to_non_nullable
as double,ratio: freezed == ratio ? _self.ratio : ratio // ignore: cast_nullable_to_non_nullable
as double?,level: freezed == level ? _self.level : level // ignore: cast_nullable_to_non_nullable
as String?,forecast: freezed == forecast ? _self.forecast : forecast // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [TargetValue].
extension TargetValuePatterns on TargetValue {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TargetValue value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TargetValue() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TargetValue value)  $default,){
final _that = this;
switch (_that) {
case _TargetValue():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TargetValue value)?  $default,){
final _that = this;
switch (_that) {
case _TargetValue() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( double? value,  double target,  double? ratio,  String? level,  double? forecast)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TargetValue() when $default != null:
return $default(_that.value,_that.target,_that.ratio,_that.level,_that.forecast);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( double? value,  double target,  double? ratio,  String? level,  double? forecast)  $default,) {final _that = this;
switch (_that) {
case _TargetValue():
return $default(_that.value,_that.target,_that.ratio,_that.level,_that.forecast);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( double? value,  double target,  double? ratio,  String? level,  double? forecast)?  $default,) {final _that = this;
switch (_that) {
case _TargetValue() when $default != null:
return $default(_that.value,_that.target,_that.ratio,_that.level,_that.forecast);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TargetValue implements TargetValue {
  const _TargetValue({this.value, this.target = 0, this.ratio, this.level, this.forecast});
  factory _TargetValue.fromJson(Map<String, dynamic> json) => _$TargetValueFromJson(json);

@override final  double? value;
@override@JsonKey() final  double target;
@override final  double? ratio;
@override final  String? level;
@override final  double? forecast;

/// Create a copy of TargetValue
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TargetValueCopyWith<_TargetValue> get copyWith => __$TargetValueCopyWithImpl<_TargetValue>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TargetValueToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TargetValue&&(identical(other.value, value) || other.value == value)&&(identical(other.target, target) || other.target == target)&&(identical(other.ratio, ratio) || other.ratio == ratio)&&(identical(other.level, level) || other.level == level)&&(identical(other.forecast, forecast) || other.forecast == forecast));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,value,target,ratio,level,forecast);
}

@override
String toString() {
    return 'TargetValue(value: $value, target: $target, ratio: $ratio, level: $level, forecast: $forecast)';
}


}

/// @nodoc
abstract mixin class _$TargetValueCopyWith<$Res> implements $TargetValueCopyWith<$Res> {
  factory _$TargetValueCopyWith(_TargetValue value, $Res Function(_TargetValue) _then) = __$TargetValueCopyWithImpl;
@override @useResult
$Res call({
 double? value, double target, double? ratio, String? level, double? forecast
});




}
/// @nodoc
class __$TargetValueCopyWithImpl<$Res>
    implements _$TargetValueCopyWith<$Res> {
  __$TargetValueCopyWithImpl(this._self, this._then);

  final _TargetValue _self;
  final $Res Function(_TargetValue) _then;

/// Create a copy of TargetValue
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? value = freezed,Object? target = null,Object? ratio = freezed,Object? level = freezed,Object? forecast = freezed,}) {
  return _then(_TargetValue(
value: freezed == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double?,target: null == target ? _self.target : target // ignore: cast_nullable_to_non_nullable
as double,ratio: freezed == ratio ? _self.ratio : ratio // ignore: cast_nullable_to_non_nullable
as double?,level: freezed == level ? _self.level : level // ignore: cast_nullable_to_non_nullable
as String?,forecast: freezed == forecast ? _self.forecast : forecast // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$SalesPace {

 double? get expected; double? get paceKg; double? get requiredKg; String? get level;
/// Create a copy of SalesPace
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SalesPaceCopyWith<SalesPace> get copyWith => _$SalesPaceCopyWithImpl<SalesPace>(this as SalesPace, _$identity);

  /// Serializes this SalesPace to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SalesPace;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SalesPace&&(identical(other.expected, _this.expected) || other.expected == _this.expected)&&(identical(other.paceKg, _this.paceKg) || other.paceKg == _this.paceKg)&&(identical(other.requiredKg, _this.requiredKg) || other.requiredKg == _this.requiredKg)&&(identical(other.level, _this.level) || other.level == _this.level));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SalesPace;
  return Object.hash(runtimeType,_this.expected,_this.paceKg,_this.requiredKg,_this.level);
}

@override
String toString() {
  final _this = this as SalesPace;
  return 'SalesPace(expected: ${_this.expected}, paceKg: ${_this.paceKg}, requiredKg: ${_this.requiredKg}, level: ${_this.level})';
}


}

/// @nodoc
abstract mixin class $SalesPaceCopyWith<$Res>  {
  factory $SalesPaceCopyWith(SalesPace value, $Res Function(SalesPace) _then) = _$SalesPaceCopyWithImpl;
@useResult
$Res call({
 double? expected, double? paceKg, double? requiredKg, String? level
});




}
/// @nodoc
class _$SalesPaceCopyWithImpl<$Res>
    implements $SalesPaceCopyWith<$Res> {
  _$SalesPaceCopyWithImpl(this._self, this._then);

  final SalesPace _self;
  final $Res Function(SalesPace) _then;

/// Create a copy of SalesPace
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? expected = freezed,Object? paceKg = freezed,Object? requiredKg = freezed,Object? level = freezed,}) {
  return _then(SalesPace(
expected: freezed == expected ? _self.expected : expected // ignore: cast_nullable_to_non_nullable
as double?,paceKg: freezed == paceKg ? _self.paceKg : paceKg // ignore: cast_nullable_to_non_nullable
as double?,requiredKg: freezed == requiredKg ? _self.requiredKg : requiredKg // ignore: cast_nullable_to_non_nullable
as double?,level: freezed == level ? _self.level : level // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [SalesPace].
extension SalesPacePatterns on SalesPace {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SalesPace value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SalesPace() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SalesPace value)  $default,){
final _that = this;
switch (_that) {
case _SalesPace():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SalesPace value)?  $default,){
final _that = this;
switch (_that) {
case _SalesPace() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( double? expected,  double? paceKg,  double? requiredKg,  String? level)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SalesPace() when $default != null:
return $default(_that.expected,_that.paceKg,_that.requiredKg,_that.level);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( double? expected,  double? paceKg,  double? requiredKg,  String? level)  $default,) {final _that = this;
switch (_that) {
case _SalesPace():
return $default(_that.expected,_that.paceKg,_that.requiredKg,_that.level);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( double? expected,  double? paceKg,  double? requiredKg,  String? level)?  $default,) {final _that = this;
switch (_that) {
case _SalesPace() when $default != null:
return $default(_that.expected,_that.paceKg,_that.requiredKg,_that.level);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SalesPace implements SalesPace {
  const _SalesPace({this.expected, this.paceKg, this.requiredKg, this.level});
  factory _SalesPace.fromJson(Map<String, dynamic> json) => _$SalesPaceFromJson(json);

@override final  double? expected;
@override final  double? paceKg;
@override final  double? requiredKg;
@override final  String? level;

/// Create a copy of SalesPace
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SalesPaceCopyWith<_SalesPace> get copyWith => __$SalesPaceCopyWithImpl<_SalesPace>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SalesPaceToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SalesPace&&(identical(other.expected, expected) || other.expected == expected)&&(identical(other.paceKg, paceKg) || other.paceKg == paceKg)&&(identical(other.requiredKg, requiredKg) || other.requiredKg == requiredKg)&&(identical(other.level, level) || other.level == level));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,expected,paceKg,requiredKg,level);
}

@override
String toString() {
    return 'SalesPace(expected: $expected, paceKg: $paceKg, requiredKg: $requiredKg, level: $level)';
}


}

/// @nodoc
abstract mixin class _$SalesPaceCopyWith<$Res> implements $SalesPaceCopyWith<$Res> {
  factory _$SalesPaceCopyWith(_SalesPace value, $Res Function(_SalesPace) _then) = __$SalesPaceCopyWithImpl;
@override @useResult
$Res call({
 double? expected, double? paceKg, double? requiredKg, String? level
});




}
/// @nodoc
class __$SalesPaceCopyWithImpl<$Res>
    implements _$SalesPaceCopyWith<$Res> {
  __$SalesPaceCopyWithImpl(this._self, this._then);

  final _SalesPace _self;
  final $Res Function(_SalesPace) _then;

/// Create a copy of SalesPace
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? expected = freezed,Object? paceKg = freezed,Object? requiredKg = freezed,Object? level = freezed,}) {
  return _then(_SalesPace(
expected: freezed == expected ? _self.expected : expected // ignore: cast_nullable_to_non_nullable
as double?,paceKg: freezed == paceKg ? _self.paceKg : paceKg // ignore: cast_nullable_to_non_nullable
as double?,requiredKg: freezed == requiredKg ? _self.requiredKg : requiredKg // ignore: cast_nullable_to_non_nullable
as double?,level: freezed == level ? _self.level : level // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$SalesKpi {

 double get factKg; double? get planKg; double? get execution; double? get forecastKg; double? get forecastExecution; double get revenue;/// АКБ — точки, которые купили в месяце (клиенты).
 int get akb; TargetValue? get conversion; TargetValue? get revenuePerOutlet; TargetValue? get akbPerAgent; int get visitsWithoutOrder; int get visitsDone; int get activeAgents;/// Откуда план: rop | factory | linko.
 String? get planSource; String? get executionLevel; SalesPace? get pace;/// Факт в охвате плана (кг): у недель продаж факт приводится к нему, чтобы недели сходились с выполнением месяца.
 double? get planFactKg;
/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SalesKpiCopyWith<SalesKpi> get copyWith => _$SalesKpiCopyWithImpl<SalesKpi>(this as SalesKpi, _$identity);

  /// Serializes this SalesKpi to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SalesKpi;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SalesKpi&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.execution, _this.execution) || other.execution == _this.execution)&&(identical(other.forecastKg, _this.forecastKg) || other.forecastKg == _this.forecastKg)&&(identical(other.forecastExecution, _this.forecastExecution) || other.forecastExecution == _this.forecastExecution)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.akb, _this.akb) || other.akb == _this.akb)&&(identical(other.conversion, _this.conversion) || other.conversion == _this.conversion)&&(identical(other.revenuePerOutlet, _this.revenuePerOutlet) || other.revenuePerOutlet == _this.revenuePerOutlet)&&(identical(other.akbPerAgent, _this.akbPerAgent) || other.akbPerAgent == _this.akbPerAgent)&&(identical(other.visitsWithoutOrder, _this.visitsWithoutOrder) || other.visitsWithoutOrder == _this.visitsWithoutOrder)&&(identical(other.visitsDone, _this.visitsDone) || other.visitsDone == _this.visitsDone)&&(identical(other.activeAgents, _this.activeAgents) || other.activeAgents == _this.activeAgents)&&(identical(other.planSource, _this.planSource) || other.planSource == _this.planSource)&&(identical(other.executionLevel, _this.executionLevel) || other.executionLevel == _this.executionLevel)&&(identical(other.pace, _this.pace) || other.pace == _this.pace)&&(identical(other.planFactKg, _this.planFactKg) || other.planFactKg == _this.planFactKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SalesKpi;
  return Object.hash(runtimeType,_this.factKg,_this.planKg,_this.execution,_this.forecastKg,_this.forecastExecution,_this.revenue,_this.akb,_this.conversion,_this.revenuePerOutlet,_this.akbPerAgent,_this.visitsWithoutOrder,_this.visitsDone,_this.activeAgents,_this.planSource,_this.executionLevel,_this.pace,_this.planFactKg);
}

@override
String toString() {
  final _this = this as SalesKpi;
  return 'SalesKpi(factKg: ${_this.factKg}, planKg: ${_this.planKg}, execution: ${_this.execution}, forecastKg: ${_this.forecastKg}, forecastExecution: ${_this.forecastExecution}, revenue: ${_this.revenue}, akb: ${_this.akb}, conversion: ${_this.conversion}, revenuePerOutlet: ${_this.revenuePerOutlet}, akbPerAgent: ${_this.akbPerAgent}, visitsWithoutOrder: ${_this.visitsWithoutOrder}, visitsDone: ${_this.visitsDone}, activeAgents: ${_this.activeAgents}, planSource: ${_this.planSource}, executionLevel: ${_this.executionLevel}, pace: ${_this.pace}, planFactKg: ${_this.planFactKg})';
}


}

/// @nodoc
abstract mixin class $SalesKpiCopyWith<$Res>  {
  factory $SalesKpiCopyWith(SalesKpi value, $Res Function(SalesKpi) _then) = _$SalesKpiCopyWithImpl;
@useResult
$Res call({
 double factKg, double? planKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int akb, TargetValue? conversion, TargetValue? revenuePerOutlet, TargetValue? akbPerAgent, int visitsWithoutOrder, int visitsDone, int activeAgents, String? planSource, String? executionLevel, SalesPace? pace, double? planFactKg
});


$TargetValueCopyWith<$Res>? get conversion;$TargetValueCopyWith<$Res>? get revenuePerOutlet;$TargetValueCopyWith<$Res>? get akbPerAgent;$SalesPaceCopyWith<$Res>? get pace;

}
/// @nodoc
class _$SalesKpiCopyWithImpl<$Res>
    implements $SalesKpiCopyWith<$Res> {
  _$SalesKpiCopyWithImpl(this._self, this._then);

  final SalesKpi _self;
  final $Res Function(SalesKpi) _then;

/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? factKg = null,Object? planKg = freezed,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? akb = null,Object? conversion = freezed,Object? revenuePerOutlet = freezed,Object? akbPerAgent = freezed,Object? visitsWithoutOrder = null,Object? visitsDone = null,Object? activeAgents = null,Object? planSource = freezed,Object? executionLevel = freezed,Object? pace = freezed,Object? planFactKg = freezed,}) {
  return _then(SalesKpi(
factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,conversion: freezed == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as TargetValue?,revenuePerOutlet: freezed == revenuePerOutlet ? _self.revenuePerOutlet : revenuePerOutlet // ignore: cast_nullable_to_non_nullable
as TargetValue?,akbPerAgent: freezed == akbPerAgent ? _self.akbPerAgent : akbPerAgent // ignore: cast_nullable_to_non_nullable
as TargetValue?,visitsWithoutOrder: null == visitsWithoutOrder ? _self.visitsWithoutOrder : visitsWithoutOrder // ignore: cast_nullable_to_non_nullable
as int,visitsDone: null == visitsDone ? _self.visitsDone : visitsDone // ignore: cast_nullable_to_non_nullable
as int,activeAgents: null == activeAgents ? _self.activeAgents : activeAgents // ignore: cast_nullable_to_non_nullable
as int,planSource: freezed == planSource ? _self.planSource : planSource // ignore: cast_nullable_to_non_nullable
as String?,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,pace: freezed == pace ? _self.pace : pace // ignore: cast_nullable_to_non_nullable
as SalesPace?,planFactKg: freezed == planFactKg ? _self.planFactKg : planFactKg // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}
/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TargetValueCopyWith<$Res>? get conversion {
    if (_self.conversion == null) {
    return null;
  }

  return $TargetValueCopyWith<$Res>(_self.conversion!, (value) {
    return _then(_self.copyWith(conversion: value));
  });
}/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TargetValueCopyWith<$Res>? get revenuePerOutlet {
    if (_self.revenuePerOutlet == null) {
    return null;
  }

  return $TargetValueCopyWith<$Res>(_self.revenuePerOutlet!, (value) {
    return _then(_self.copyWith(revenuePerOutlet: value));
  });
}/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TargetValueCopyWith<$Res>? get akbPerAgent {
    if (_self.akbPerAgent == null) {
    return null;
  }

  return $TargetValueCopyWith<$Res>(_self.akbPerAgent!, (value) {
    return _then(_self.copyWith(akbPerAgent: value));
  });
}/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPaceCopyWith<$Res>? get pace {
    if (_self.pace == null) {
    return null;
  }

  return $SalesPaceCopyWith<$Res>(_self.pace!, (value) {
    return _then(_self.copyWith(pace: value));
  });
}
}


/// Adds pattern-matching-related methods to [SalesKpi].
extension SalesKpiPatterns on SalesKpi {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SalesKpi value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SalesKpi() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SalesKpi value)  $default,){
final _that = this;
switch (_that) {
case _SalesKpi():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SalesKpi value)?  $default,){
final _that = this;
switch (_that) {
case _SalesKpi() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( double factKg,  double? planKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  TargetValue? conversion,  TargetValue? revenuePerOutlet,  TargetValue? akbPerAgent,  int visitsWithoutOrder,  int visitsDone,  int activeAgents,  String? planSource,  String? executionLevel,  SalesPace? pace,  double? planFactKg)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SalesKpi() when $default != null:
return $default(_that.factKg,_that.planKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.conversion,_that.revenuePerOutlet,_that.akbPerAgent,_that.visitsWithoutOrder,_that.visitsDone,_that.activeAgents,_that.planSource,_that.executionLevel,_that.pace,_that.planFactKg);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( double factKg,  double? planKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  TargetValue? conversion,  TargetValue? revenuePerOutlet,  TargetValue? akbPerAgent,  int visitsWithoutOrder,  int visitsDone,  int activeAgents,  String? planSource,  String? executionLevel,  SalesPace? pace,  double? planFactKg)  $default,) {final _that = this;
switch (_that) {
case _SalesKpi():
return $default(_that.factKg,_that.planKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.conversion,_that.revenuePerOutlet,_that.akbPerAgent,_that.visitsWithoutOrder,_that.visitsDone,_that.activeAgents,_that.planSource,_that.executionLevel,_that.pace,_that.planFactKg);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( double factKg,  double? planKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  TargetValue? conversion,  TargetValue? revenuePerOutlet,  TargetValue? akbPerAgent,  int visitsWithoutOrder,  int visitsDone,  int activeAgents,  String? planSource,  String? executionLevel,  SalesPace? pace,  double? planFactKg)?  $default,) {final _that = this;
switch (_that) {
case _SalesKpi() when $default != null:
return $default(_that.factKg,_that.planKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.conversion,_that.revenuePerOutlet,_that.akbPerAgent,_that.visitsWithoutOrder,_that.visitsDone,_that.activeAgents,_that.planSource,_that.executionLevel,_that.pace,_that.planFactKg);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SalesKpi implements SalesKpi {
  const _SalesKpi({this.factKg = 0, this.planKg, this.execution, this.forecastKg, this.forecastExecution, this.revenue = 0, this.akb = 0, this.conversion, this.revenuePerOutlet, this.akbPerAgent, this.visitsWithoutOrder = 0, this.visitsDone = 0, this.activeAgents = 0, this.planSource, this.executionLevel, this.pace, this.planFactKg});
  factory _SalesKpi.fromJson(Map<String, dynamic> json) => _$SalesKpiFromJson(json);

@override@JsonKey() final  double factKg;
@override final  double? planKg;
@override final  double? execution;
@override final  double? forecastKg;
@override final  double? forecastExecution;
@override@JsonKey() final  double revenue;
/// АКБ — точки, которые купили в месяце (клиенты).
@override@JsonKey() final  int akb;
@override final  TargetValue? conversion;
@override final  TargetValue? revenuePerOutlet;
@override final  TargetValue? akbPerAgent;
@override@JsonKey() final  int visitsWithoutOrder;
@override@JsonKey() final  int visitsDone;
@override@JsonKey() final  int activeAgents;
/// Откуда план: rop | factory | linko.
@override final  String? planSource;
@override final  String? executionLevel;
@override final  SalesPace? pace;
/// Факт в охвате плана (кг): у недель продаж факт приводится к нему, чтобы недели сходились с выполнением месяца.
@override final  double? planFactKg;

/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SalesKpiCopyWith<_SalesKpi> get copyWith => __$SalesKpiCopyWithImpl<_SalesKpi>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SalesKpiToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SalesKpi&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.execution, execution) || other.execution == execution)&&(identical(other.forecastKg, forecastKg) || other.forecastKg == forecastKg)&&(identical(other.forecastExecution, forecastExecution) || other.forecastExecution == forecastExecution)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.akb, akb) || other.akb == akb)&&(identical(other.conversion, conversion) || other.conversion == conversion)&&(identical(other.revenuePerOutlet, revenuePerOutlet) || other.revenuePerOutlet == revenuePerOutlet)&&(identical(other.akbPerAgent, akbPerAgent) || other.akbPerAgent == akbPerAgent)&&(identical(other.visitsWithoutOrder, visitsWithoutOrder) || other.visitsWithoutOrder == visitsWithoutOrder)&&(identical(other.visitsDone, visitsDone) || other.visitsDone == visitsDone)&&(identical(other.activeAgents, activeAgents) || other.activeAgents == activeAgents)&&(identical(other.planSource, planSource) || other.planSource == planSource)&&(identical(other.executionLevel, executionLevel) || other.executionLevel == executionLevel)&&(identical(other.pace, pace) || other.pace == pace)&&(identical(other.planFactKg, planFactKg) || other.planFactKg == planFactKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,factKg,planKg,execution,forecastKg,forecastExecution,revenue,akb,conversion,revenuePerOutlet,akbPerAgent,visitsWithoutOrder,visitsDone,activeAgents,planSource,executionLevel,pace,planFactKg);
}

@override
String toString() {
    return 'SalesKpi(factKg: $factKg, planKg: $planKg, execution: $execution, forecastKg: $forecastKg, forecastExecution: $forecastExecution, revenue: $revenue, akb: $akb, conversion: $conversion, revenuePerOutlet: $revenuePerOutlet, akbPerAgent: $akbPerAgent, visitsWithoutOrder: $visitsWithoutOrder, visitsDone: $visitsDone, activeAgents: $activeAgents, planSource: $planSource, executionLevel: $executionLevel, pace: $pace, planFactKg: $planFactKg)';
}


}

/// @nodoc
abstract mixin class _$SalesKpiCopyWith<$Res> implements $SalesKpiCopyWith<$Res> {
  factory _$SalesKpiCopyWith(_SalesKpi value, $Res Function(_SalesKpi) _then) = __$SalesKpiCopyWithImpl;
@override @useResult
$Res call({
 double factKg, double? planKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int akb, TargetValue? conversion, TargetValue? revenuePerOutlet, TargetValue? akbPerAgent, int visitsWithoutOrder, int visitsDone, int activeAgents, String? planSource, String? executionLevel, SalesPace? pace, double? planFactKg
});


@override $TargetValueCopyWith<$Res>? get conversion;@override $TargetValueCopyWith<$Res>? get revenuePerOutlet;@override $TargetValueCopyWith<$Res>? get akbPerAgent;@override $SalesPaceCopyWith<$Res>? get pace;

}
/// @nodoc
class __$SalesKpiCopyWithImpl<$Res>
    implements _$SalesKpiCopyWith<$Res> {
  __$SalesKpiCopyWithImpl(this._self, this._then);

  final _SalesKpi _self;
  final $Res Function(_SalesKpi) _then;

/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? factKg = null,Object? planKg = freezed,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? akb = null,Object? conversion = freezed,Object? revenuePerOutlet = freezed,Object? akbPerAgent = freezed,Object? visitsWithoutOrder = null,Object? visitsDone = null,Object? activeAgents = null,Object? planSource = freezed,Object? executionLevel = freezed,Object? pace = freezed,Object? planFactKg = freezed,}) {
  return _then(_SalesKpi(
factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,conversion: freezed == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as TargetValue?,revenuePerOutlet: freezed == revenuePerOutlet ? _self.revenuePerOutlet : revenuePerOutlet // ignore: cast_nullable_to_non_nullable
as TargetValue?,akbPerAgent: freezed == akbPerAgent ? _self.akbPerAgent : akbPerAgent // ignore: cast_nullable_to_non_nullable
as TargetValue?,visitsWithoutOrder: null == visitsWithoutOrder ? _self.visitsWithoutOrder : visitsWithoutOrder // ignore: cast_nullable_to_non_nullable
as int,visitsDone: null == visitsDone ? _self.visitsDone : visitsDone // ignore: cast_nullable_to_non_nullable
as int,activeAgents: null == activeAgents ? _self.activeAgents : activeAgents // ignore: cast_nullable_to_non_nullable
as int,planSource: freezed == planSource ? _self.planSource : planSource // ignore: cast_nullable_to_non_nullable
as String?,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,pace: freezed == pace ? _self.pace : pace // ignore: cast_nullable_to_non_nullable
as SalesPace?,planFactKg: freezed == planFactKg ? _self.planFactKg : planFactKg // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TargetValueCopyWith<$Res>? get conversion {
    if (_self.conversion == null) {
    return null;
  }

  return $TargetValueCopyWith<$Res>(_self.conversion!, (value) {
    return _then(_self.copyWith(conversion: value));
  });
}/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TargetValueCopyWith<$Res>? get revenuePerOutlet {
    if (_self.revenuePerOutlet == null) {
    return null;
  }

  return $TargetValueCopyWith<$Res>(_self.revenuePerOutlet!, (value) {
    return _then(_self.copyWith(revenuePerOutlet: value));
  });
}/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TargetValueCopyWith<$Res>? get akbPerAgent {
    if (_self.akbPerAgent == null) {
    return null;
  }

  return $TargetValueCopyWith<$Res>(_self.akbPerAgent!, (value) {
    return _then(_self.copyWith(akbPerAgent: value));
  });
}/// Create a copy of SalesKpi
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPaceCopyWith<$Res>? get pace {
    if (_self.pace == null) {
    return null;
  }

  return $SalesPaceCopyWith<$Res>(_self.pace!, (value) {
    return _then(_self.copyWith(pace: value));
  });
}
}


/// @nodoc
mixin _$FlagCounts {

 int get critical; int get risk;
/// Create a copy of FlagCounts
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FlagCountsCopyWith<FlagCounts> get copyWith => _$FlagCountsCopyWithImpl<FlagCounts>(this as FlagCounts, _$identity);

  /// Serializes this FlagCounts to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FlagCounts;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FlagCounts&&(identical(other.critical, _this.critical) || other.critical == _this.critical)&&(identical(other.risk, _this.risk) || other.risk == _this.risk));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FlagCounts;
  return Object.hash(runtimeType,_this.critical,_this.risk);
}

@override
String toString() {
  final _this = this as FlagCounts;
  return 'FlagCounts(critical: ${_this.critical}, risk: ${_this.risk})';
}


}

/// @nodoc
abstract mixin class $FlagCountsCopyWith<$Res>  {
  factory $FlagCountsCopyWith(FlagCounts value, $Res Function(FlagCounts) _then) = _$FlagCountsCopyWithImpl;
@useResult
$Res call({
 int critical, int risk
});




}
/// @nodoc
class _$FlagCountsCopyWithImpl<$Res>
    implements $FlagCountsCopyWith<$Res> {
  _$FlagCountsCopyWithImpl(this._self, this._then);

  final FlagCounts _self;
  final $Res Function(FlagCounts) _then;

/// Create a copy of FlagCounts
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? critical = null,Object? risk = null,}) {
  return _then(FlagCounts(
critical: null == critical ? _self.critical : critical // ignore: cast_nullable_to_non_nullable
as int,risk: null == risk ? _self.risk : risk // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [FlagCounts].
extension FlagCountsPatterns on FlagCounts {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FlagCounts value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FlagCounts() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FlagCounts value)  $default,){
final _that = this;
switch (_that) {
case _FlagCounts():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FlagCounts value)?  $default,){
final _that = this;
switch (_that) {
case _FlagCounts() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int critical,  int risk)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FlagCounts() when $default != null:
return $default(_that.critical,_that.risk);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int critical,  int risk)  $default,) {final _that = this;
switch (_that) {
case _FlagCounts():
return $default(_that.critical,_that.risk);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int critical,  int risk)?  $default,) {final _that = this;
switch (_that) {
case _FlagCounts() when $default != null:
return $default(_that.critical,_that.risk);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FlagCounts implements FlagCounts {
  const _FlagCounts({this.critical = 0, this.risk = 0});
  factory _FlagCounts.fromJson(Map<String, dynamic> json) => _$FlagCountsFromJson(json);

@override@JsonKey() final  int critical;
@override@JsonKey() final  int risk;

/// Create a copy of FlagCounts
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FlagCountsCopyWith<_FlagCounts> get copyWith => __$FlagCountsCopyWithImpl<_FlagCounts>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FlagCountsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FlagCounts&&(identical(other.critical, critical) || other.critical == critical)&&(identical(other.risk, risk) || other.risk == risk));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,critical,risk);
}

@override
String toString() {
    return 'FlagCounts(critical: $critical, risk: $risk)';
}


}

/// @nodoc
abstract mixin class _$FlagCountsCopyWith<$Res> implements $FlagCountsCopyWith<$Res> {
  factory _$FlagCountsCopyWith(_FlagCounts value, $Res Function(_FlagCounts) _then) = __$FlagCountsCopyWithImpl;
@override @useResult
$Res call({
 int critical, int risk
});




}
/// @nodoc
class __$FlagCountsCopyWithImpl<$Res>
    implements _$FlagCountsCopyWith<$Res> {
  __$FlagCountsCopyWithImpl(this._self, this._then);

  final _FlagCounts _self;
  final $Res Function(_FlagCounts) _then;

/// Create a copy of FlagCounts
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? critical = null,Object? risk = null,}) {
  return _then(_FlagCounts(
critical: null == critical ? _self.critical : critical // ignore: cast_nullable_to_non_nullable
as int,risk: null == risk ? _self.risk : risk // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$SalesOverview {

 SalesPeriod get period; SalesKpi get kpi; int get activeAgents; FlagCounts get flags;
/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SalesOverviewCopyWith<SalesOverview> get copyWith => _$SalesOverviewCopyWithImpl<SalesOverview>(this as SalesOverview, _$identity);

  /// Serializes this SalesOverview to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SalesOverview;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SalesOverview&&(identical(other.period, _this.period) || other.period == _this.period)&&(identical(other.kpi, _this.kpi) || other.kpi == _this.kpi)&&(identical(other.activeAgents, _this.activeAgents) || other.activeAgents == _this.activeAgents)&&(identical(other.flags, _this.flags) || other.flags == _this.flags));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SalesOverview;
  return Object.hash(runtimeType,_this.period,_this.kpi,_this.activeAgents,_this.flags);
}

@override
String toString() {
  final _this = this as SalesOverview;
  return 'SalesOverview(period: ${_this.period}, kpi: ${_this.kpi}, activeAgents: ${_this.activeAgents}, flags: ${_this.flags})';
}


}

/// @nodoc
abstract mixin class $SalesOverviewCopyWith<$Res>  {
  factory $SalesOverviewCopyWith(SalesOverview value, $Res Function(SalesOverview) _then) = _$SalesOverviewCopyWithImpl;
@useResult
$Res call({
 SalesPeriod period, SalesKpi kpi, int activeAgents, FlagCounts flags
});


$SalesPeriodCopyWith<$Res> get period;$SalesKpiCopyWith<$Res> get kpi;$FlagCountsCopyWith<$Res> get flags;

}
/// @nodoc
class _$SalesOverviewCopyWithImpl<$Res>
    implements $SalesOverviewCopyWith<$Res> {
  _$SalesOverviewCopyWithImpl(this._self, this._then);

  final SalesOverview _self;
  final $Res Function(SalesOverview) _then;

/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? period = null,Object? kpi = null,Object? activeAgents = null,Object? flags = null,}) {
  return _then(SalesOverview(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,kpi: null == kpi ? _self.kpi : kpi // ignore: cast_nullable_to_non_nullable
as SalesKpi,activeAgents: null == activeAgents ? _self.activeAgents : activeAgents // ignore: cast_nullable_to_non_nullable
as int,flags: null == flags ? _self.flags : flags // ignore: cast_nullable_to_non_nullable
as FlagCounts,
  ));
}
/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesKpiCopyWith<$Res> get kpi {
  
  return $SalesKpiCopyWith<$Res>(_self.kpi, (value) {
    return _then(_self.copyWith(kpi: value));
  });
}/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FlagCountsCopyWith<$Res> get flags {
  
  return $FlagCountsCopyWith<$Res>(_self.flags, (value) {
    return _then(_self.copyWith(flags: value));
  });
}
}


/// Adds pattern-matching-related methods to [SalesOverview].
extension SalesOverviewPatterns on SalesOverview {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SalesOverview value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SalesOverview() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SalesOverview value)  $default,){
final _that = this;
switch (_that) {
case _SalesOverview():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SalesOverview value)?  $default,){
final _that = this;
switch (_that) {
case _SalesOverview() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( SalesPeriod period,  SalesKpi kpi,  int activeAgents,  FlagCounts flags)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SalesOverview() when $default != null:
return $default(_that.period,_that.kpi,_that.activeAgents,_that.flags);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( SalesPeriod period,  SalesKpi kpi,  int activeAgents,  FlagCounts flags)  $default,) {final _that = this;
switch (_that) {
case _SalesOverview():
return $default(_that.period,_that.kpi,_that.activeAgents,_that.flags);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( SalesPeriod period,  SalesKpi kpi,  int activeAgents,  FlagCounts flags)?  $default,) {final _that = this;
switch (_that) {
case _SalesOverview() when $default != null:
return $default(_that.period,_that.kpi,_that.activeAgents,_that.flags);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SalesOverview implements SalesOverview {
  const _SalesOverview({required this.period, required this.kpi, this.activeAgents = 0, this.flags = const FlagCounts()});
  factory _SalesOverview.fromJson(Map<String, dynamic> json) => _$SalesOverviewFromJson(json);

@override final  SalesPeriod period;
@override final  SalesKpi kpi;
@override@JsonKey() final  int activeAgents;
@override@JsonKey() final  FlagCounts flags;

/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SalesOverviewCopyWith<_SalesOverview> get copyWith => __$SalesOverviewCopyWithImpl<_SalesOverview>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SalesOverviewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SalesOverview&&(identical(other.period, period) || other.period == period)&&(identical(other.kpi, kpi) || other.kpi == kpi)&&(identical(other.activeAgents, activeAgents) || other.activeAgents == activeAgents)&&(identical(other.flags, flags) || other.flags == flags));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,period,kpi,activeAgents,flags);
}

@override
String toString() {
    return 'SalesOverview(period: $period, kpi: $kpi, activeAgents: $activeAgents, flags: $flags)';
}


}

/// @nodoc
abstract mixin class _$SalesOverviewCopyWith<$Res> implements $SalesOverviewCopyWith<$Res> {
  factory _$SalesOverviewCopyWith(_SalesOverview value, $Res Function(_SalesOverview) _then) = __$SalesOverviewCopyWithImpl;
@override @useResult
$Res call({
 SalesPeriod period, SalesKpi kpi, int activeAgents, FlagCounts flags
});


@override $SalesPeriodCopyWith<$Res> get period;@override $SalesKpiCopyWith<$Res> get kpi;@override $FlagCountsCopyWith<$Res> get flags;

}
/// @nodoc
class __$SalesOverviewCopyWithImpl<$Res>
    implements _$SalesOverviewCopyWith<$Res> {
  __$SalesOverviewCopyWithImpl(this._self, this._then);

  final _SalesOverview _self;
  final $Res Function(_SalesOverview) _then;

/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? period = null,Object? kpi = null,Object? activeAgents = null,Object? flags = null,}) {
  return _then(_SalesOverview(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,kpi: null == kpi ? _self.kpi : kpi // ignore: cast_nullable_to_non_nullable
as SalesKpi,activeAgents: null == activeAgents ? _self.activeAgents : activeAgents // ignore: cast_nullable_to_non_nullable
as int,flags: null == flags ? _self.flags : flags // ignore: cast_nullable_to_non_nullable
as FlagCounts,
  ));
}

/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesKpiCopyWith<$Res> get kpi {
  
  return $SalesKpiCopyWith<$Res>(_self.kpi, (value) {
    return _then(_self.copyWith(kpi: value));
  });
}/// Create a copy of SalesOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FlagCountsCopyWith<$Res> get flags {
  
  return $FlagCountsCopyWith<$Res>(_self.flags, (value) {
    return _then(_self.copyWith(flags: value));
  });
}
}


/// @nodoc
mixin _$SalesMonth {

 int get year; int get month;
/// Create a copy of SalesMonth
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SalesMonthCopyWith<SalesMonth> get copyWith => _$SalesMonthCopyWithImpl<SalesMonth>(this as SalesMonth, _$identity);

  /// Serializes this SalesMonth to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SalesMonth;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SalesMonth&&(identical(other.year, _this.year) || other.year == _this.year)&&(identical(other.month, _this.month) || other.month == _this.month));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SalesMonth;
  return Object.hash(runtimeType,_this.year,_this.month);
}

@override
String toString() {
  final _this = this as SalesMonth;
  return 'SalesMonth(year: ${_this.year}, month: ${_this.month})';
}


}

/// @nodoc
abstract mixin class $SalesMonthCopyWith<$Res>  {
  factory $SalesMonthCopyWith(SalesMonth value, $Res Function(SalesMonth) _then) = _$SalesMonthCopyWithImpl;
@useResult
$Res call({
 int year, int month
});




}
/// @nodoc
class _$SalesMonthCopyWithImpl<$Res>
    implements $SalesMonthCopyWith<$Res> {
  _$SalesMonthCopyWithImpl(this._self, this._then);

  final SalesMonth _self;
  final $Res Function(SalesMonth) _then;

/// Create a copy of SalesMonth
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? year = null,Object? month = null,}) {
  return _then(SalesMonth(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [SalesMonth].
extension SalesMonthPatterns on SalesMonth {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SalesMonth value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SalesMonth() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SalesMonth value)  $default,){
final _that = this;
switch (_that) {
case _SalesMonth():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SalesMonth value)?  $default,){
final _that = this;
switch (_that) {
case _SalesMonth() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int year,  int month)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SalesMonth() when $default != null:
return $default(_that.year,_that.month);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int year,  int month)  $default,) {final _that = this;
switch (_that) {
case _SalesMonth():
return $default(_that.year,_that.month);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int year,  int month)?  $default,) {final _that = this;
switch (_that) {
case _SalesMonth() when $default != null:
return $default(_that.year,_that.month);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SalesMonth implements SalesMonth {
  const _SalesMonth({required this.year, required this.month});
  factory _SalesMonth.fromJson(Map<String, dynamic> json) => _$SalesMonthFromJson(json);

@override final  int year;
@override final  int month;

/// Create a copy of SalesMonth
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SalesMonthCopyWith<_SalesMonth> get copyWith => __$SalesMonthCopyWithImpl<_SalesMonth>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SalesMonthToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SalesMonth&&(identical(other.year, year) || other.year == year)&&(identical(other.month, month) || other.month == month));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,year,month);
}

@override
String toString() {
    return 'SalesMonth(year: $year, month: $month)';
}


}

/// @nodoc
abstract mixin class _$SalesMonthCopyWith<$Res> implements $SalesMonthCopyWith<$Res> {
  factory _$SalesMonthCopyWith(_SalesMonth value, $Res Function(_SalesMonth) _then) = __$SalesMonthCopyWithImpl;
@override @useResult
$Res call({
 int year, int month
});




}
/// @nodoc
class __$SalesMonthCopyWithImpl<$Res>
    implements _$SalesMonthCopyWith<$Res> {
  __$SalesMonthCopyWithImpl(this._self, this._then);

  final _SalesMonth _self;
  final $Res Function(_SalesMonth) _then;

/// Create a copy of SalesMonth
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? year = null,Object? month = null,}) {
  return _then(_SalesMonth(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$SalesDaily {

 int get year; int get month; String get dataThrough; String? get today; int get daysInMonth; bool get closed;/// Кг за каждый день месяца; null — дня ещё нет или данных нет.
 List<double?> get days; List<bool> get sundays; double get total;/// Сумма строк «те же дни» по регионам: сейчас и в прошлом месяце (по ту же дату).
 double get sameNowKg; double get sameBeforeKg; String? get previousCutoff;/// План и факт месяца (кг) и прошедшие дни — для недель «Динамики» главной.
 double? get planKg; double? get planFactKg; double get factKg; int get workedDays; double? get elapsedDays;
/// Create a copy of SalesDaily
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SalesDailyCopyWith<SalesDaily> get copyWith => _$SalesDailyCopyWithImpl<SalesDaily>(this as SalesDaily, _$identity);

  /// Serializes this SalesDaily to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SalesDaily;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SalesDaily&&(identical(other.year, _this.year) || other.year == _this.year)&&(identical(other.month, _this.month) || other.month == _this.month)&&(identical(other.dataThrough, _this.dataThrough) || other.dataThrough == _this.dataThrough)&&(identical(other.today, _this.today) || other.today == _this.today)&&(identical(other.daysInMonth, _this.daysInMonth) || other.daysInMonth == _this.daysInMonth)&&(identical(other.closed, _this.closed) || other.closed == _this.closed)&&const DeepCollectionEquality().equals(other.days, _this.days)&&const DeepCollectionEquality().equals(other.sundays, _this.sundays)&&(identical(other.total, _this.total) || other.total == _this.total)&&(identical(other.sameNowKg, _this.sameNowKg) || other.sameNowKg == _this.sameNowKg)&&(identical(other.sameBeforeKg, _this.sameBeforeKg) || other.sameBeforeKg == _this.sameBeforeKg)&&(identical(other.previousCutoff, _this.previousCutoff) || other.previousCutoff == _this.previousCutoff)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.planFactKg, _this.planFactKg) || other.planFactKg == _this.planFactKg)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.workedDays, _this.workedDays) || other.workedDays == _this.workedDays)&&(identical(other.elapsedDays, _this.elapsedDays) || other.elapsedDays == _this.elapsedDays));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SalesDaily;
  return Object.hash(runtimeType,_this.year,_this.month,_this.dataThrough,_this.today,_this.daysInMonth,_this.closed,const DeepCollectionEquality().hash(_this.days),const DeepCollectionEquality().hash(_this.sundays),_this.total,_this.sameNowKg,_this.sameBeforeKg,_this.previousCutoff,_this.planKg,_this.planFactKg,_this.factKg,_this.workedDays,_this.elapsedDays);
}

@override
String toString() {
  final _this = this as SalesDaily;
  return 'SalesDaily(year: ${_this.year}, month: ${_this.month}, dataThrough: ${_this.dataThrough}, today: ${_this.today}, daysInMonth: ${_this.daysInMonth}, closed: ${_this.closed}, days: ${_this.days}, sundays: ${_this.sundays}, total: ${_this.total}, sameNowKg: ${_this.sameNowKg}, sameBeforeKg: ${_this.sameBeforeKg}, previousCutoff: ${_this.previousCutoff}, planKg: ${_this.planKg}, planFactKg: ${_this.planFactKg}, factKg: ${_this.factKg}, workedDays: ${_this.workedDays}, elapsedDays: ${_this.elapsedDays})';
}


}

/// @nodoc
abstract mixin class $SalesDailyCopyWith<$Res>  {
  factory $SalesDailyCopyWith(SalesDaily value, $Res Function(SalesDaily) _then) = _$SalesDailyCopyWithImpl;
@useResult
$Res call({
 int year, int month, String dataThrough, String? today, int daysInMonth, bool closed, List<double?> days, List<bool> sundays, double total, double sameNowKg, double sameBeforeKg, String? previousCutoff, double? planKg, double? planFactKg, double factKg, int workedDays, double? elapsedDays
});




}
/// @nodoc
class _$SalesDailyCopyWithImpl<$Res>
    implements $SalesDailyCopyWith<$Res> {
  _$SalesDailyCopyWithImpl(this._self, this._then);

  final SalesDaily _self;
  final $Res Function(SalesDaily) _then;

/// Create a copy of SalesDaily
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? year = null,Object? month = null,Object? dataThrough = null,Object? today = freezed,Object? daysInMonth = null,Object? closed = null,Object? days = null,Object? sundays = null,Object? total = null,Object? sameNowKg = null,Object? sameBeforeKg = null,Object? previousCutoff = freezed,Object? planKg = freezed,Object? planFactKg = freezed,Object? factKg = null,Object? workedDays = null,Object? elapsedDays = freezed,}) {
  return _then(SalesDaily(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,dataThrough: null == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as String?,daysInMonth: null == daysInMonth ? _self.daysInMonth : daysInMonth // ignore: cast_nullable_to_non_nullable
as int,closed: null == closed ? _self.closed : closed // ignore: cast_nullable_to_non_nullable
as bool,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as List<double?>,sundays: null == sundays ? _self.sundays : sundays // ignore: cast_nullable_to_non_nullable
as List<bool>,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,sameNowKg: null == sameNowKg ? _self.sameNowKg : sameNowKg // ignore: cast_nullable_to_non_nullable
as double,sameBeforeKg: null == sameBeforeKg ? _self.sameBeforeKg : sameBeforeKg // ignore: cast_nullable_to_non_nullable
as double,previousCutoff: freezed == previousCutoff ? _self.previousCutoff : previousCutoff // ignore: cast_nullable_to_non_nullable
as String?,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,planFactKg: freezed == planFactKg ? _self.planFactKg : planFactKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,workedDays: null == workedDays ? _self.workedDays : workedDays // ignore: cast_nullable_to_non_nullable
as int,elapsedDays: freezed == elapsedDays ? _self.elapsedDays : elapsedDays // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [SalesDaily].
extension SalesDailyPatterns on SalesDaily {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SalesDaily value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SalesDaily() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SalesDaily value)  $default,){
final _that = this;
switch (_that) {
case _SalesDaily():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SalesDaily value)?  $default,){
final _that = this;
switch (_that) {
case _SalesDaily() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int year,  int month,  String dataThrough,  String? today,  int daysInMonth,  bool closed,  List<double?> days,  List<bool> sundays,  double total,  double sameNowKg,  double sameBeforeKg,  String? previousCutoff,  double? planKg,  double? planFactKg,  double factKg,  int workedDays,  double? elapsedDays)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SalesDaily() when $default != null:
return $default(_that.year,_that.month,_that.dataThrough,_that.today,_that.daysInMonth,_that.closed,_that.days,_that.sundays,_that.total,_that.sameNowKg,_that.sameBeforeKg,_that.previousCutoff,_that.planKg,_that.planFactKg,_that.factKg,_that.workedDays,_that.elapsedDays);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int year,  int month,  String dataThrough,  String? today,  int daysInMonth,  bool closed,  List<double?> days,  List<bool> sundays,  double total,  double sameNowKg,  double sameBeforeKg,  String? previousCutoff,  double? planKg,  double? planFactKg,  double factKg,  int workedDays,  double? elapsedDays)  $default,) {final _that = this;
switch (_that) {
case _SalesDaily():
return $default(_that.year,_that.month,_that.dataThrough,_that.today,_that.daysInMonth,_that.closed,_that.days,_that.sundays,_that.total,_that.sameNowKg,_that.sameBeforeKg,_that.previousCutoff,_that.planKg,_that.planFactKg,_that.factKg,_that.workedDays,_that.elapsedDays);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int year,  int month,  String dataThrough,  String? today,  int daysInMonth,  bool closed,  List<double?> days,  List<bool> sundays,  double total,  double sameNowKg,  double sameBeforeKg,  String? previousCutoff,  double? planKg,  double? planFactKg,  double factKg,  int workedDays,  double? elapsedDays)?  $default,) {final _that = this;
switch (_that) {
case _SalesDaily() when $default != null:
return $default(_that.year,_that.month,_that.dataThrough,_that.today,_that.daysInMonth,_that.closed,_that.days,_that.sundays,_that.total,_that.sameNowKg,_that.sameBeforeKg,_that.previousCutoff,_that.planKg,_that.planFactKg,_that.factKg,_that.workedDays,_that.elapsedDays);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SalesDaily extends SalesDaily {
  const _SalesDaily({required this.year, required this.month, required this.dataThrough, this.today, required this.daysInMonth, this.closed = false, required  List<double?> days,  List<bool> sundays = const <bool>[], this.total = 0, this.sameNowKg = 0, this.sameBeforeKg = 0, this.previousCutoff, this.planKg, this.planFactKg, this.factKg = 0, this.workedDays = 0, this.elapsedDays}): _days = days,_sundays = sundays,super._();
  factory _SalesDaily.fromJson(Map<String, dynamic> json) => _$SalesDailyFromJson(json);

@override final  int year;
@override final  int month;
@override final  String dataThrough;
@override final  String? today;
@override final  int daysInMonth;
@override@JsonKey() final  bool closed;
/// Кг за каждый день месяца; null — дня ещё нет или данных нет.
 final  List<double?> _days;
/// Кг за каждый день месяца; null — дня ещё нет или данных нет.
@override List<double?> get days {
  if (_days is EqualUnmodifiableListView) return _days;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_days);
}

 final  List<bool> _sundays;
@override@JsonKey() List<bool> get sundays {
  if (_sundays is EqualUnmodifiableListView) return _sundays;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_sundays);
}

@override@JsonKey() final  double total;
/// Сумма строк «те же дни» по регионам: сейчас и в прошлом месяце (по ту же дату).
@override@JsonKey() final  double sameNowKg;
@override@JsonKey() final  double sameBeforeKg;
@override final  String? previousCutoff;
/// План и факт месяца (кг) и прошедшие дни — для недель «Динамики» главной.
@override final  double? planKg;
@override final  double? planFactKg;
@override@JsonKey() final  double factKg;
@override@JsonKey() final  int workedDays;
@override final  double? elapsedDays;

/// Create a copy of SalesDaily
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SalesDailyCopyWith<_SalesDaily> get copyWith => __$SalesDailyCopyWithImpl<_SalesDaily>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SalesDailyToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SalesDaily&&(identical(other.year, year) || other.year == year)&&(identical(other.month, month) || other.month == month)&&(identical(other.dataThrough, dataThrough) || other.dataThrough == dataThrough)&&(identical(other.today, today) || other.today == today)&&(identical(other.daysInMonth, daysInMonth) || other.daysInMonth == daysInMonth)&&(identical(other.closed, closed) || other.closed == closed)&&const DeepCollectionEquality().equals(other.days, _days)&&const DeepCollectionEquality().equals(other.sundays, _sundays)&&(identical(other.total, total) || other.total == total)&&(identical(other.sameNowKg, sameNowKg) || other.sameNowKg == sameNowKg)&&(identical(other.sameBeforeKg, sameBeforeKg) || other.sameBeforeKg == sameBeforeKg)&&(identical(other.previousCutoff, previousCutoff) || other.previousCutoff == previousCutoff)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.planFactKg, planFactKg) || other.planFactKg == planFactKg)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.workedDays, workedDays) || other.workedDays == workedDays)&&(identical(other.elapsedDays, elapsedDays) || other.elapsedDays == elapsedDays));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,year,month,dataThrough,today,daysInMonth,closed,const DeepCollectionEquality().hash(_days),const DeepCollectionEquality().hash(_sundays),total,sameNowKg,sameBeforeKg,previousCutoff,planKg,planFactKg,factKg,workedDays,elapsedDays);
}

@override
String toString() {
    return 'SalesDaily(year: $year, month: $month, dataThrough: $dataThrough, today: $today, daysInMonth: $daysInMonth, closed: $closed, days: $days, sundays: $sundays, total: $total, sameNowKg: $sameNowKg, sameBeforeKg: $sameBeforeKg, previousCutoff: $previousCutoff, planKg: $planKg, planFactKg: $planFactKg, factKg: $factKg, workedDays: $workedDays, elapsedDays: $elapsedDays)';
}


}

/// @nodoc
abstract mixin class _$SalesDailyCopyWith<$Res> implements $SalesDailyCopyWith<$Res> {
  factory _$SalesDailyCopyWith(_SalesDaily value, $Res Function(_SalesDaily) _then) = __$SalesDailyCopyWithImpl;
@override @useResult
$Res call({
 int year, int month, String dataThrough, String? today, int daysInMonth, bool closed, List<double?> days, List<bool> sundays, double total, double sameNowKg, double sameBeforeKg, String? previousCutoff, double? planKg, double? planFactKg, double factKg, int workedDays, double? elapsedDays
});




}
/// @nodoc
class __$SalesDailyCopyWithImpl<$Res>
    implements _$SalesDailyCopyWith<$Res> {
  __$SalesDailyCopyWithImpl(this._self, this._then);

  final _SalesDaily _self;
  final $Res Function(_SalesDaily) _then;

/// Create a copy of SalesDaily
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? year = null,Object? month = null,Object? dataThrough = null,Object? today = freezed,Object? daysInMonth = null,Object? closed = null,Object? days = null,Object? sundays = null,Object? total = null,Object? sameNowKg = null,Object? sameBeforeKg = null,Object? previousCutoff = freezed,Object? planKg = freezed,Object? planFactKg = freezed,Object? factKg = null,Object? workedDays = null,Object? elapsedDays = freezed,}) {
  return _then(_SalesDaily(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,dataThrough: null == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String,today: freezed == today ? _self.today : today // ignore: cast_nullable_to_non_nullable
as String?,daysInMonth: null == daysInMonth ? _self.daysInMonth : daysInMonth // ignore: cast_nullable_to_non_nullable
as int,closed: null == closed ? _self.closed : closed // ignore: cast_nullable_to_non_nullable
as bool,days: null == days ? _self._days : days // ignore: cast_nullable_to_non_nullable
as List<double?>,sundays: null == sundays ? _self._sundays : sundays // ignore: cast_nullable_to_non_nullable
as List<bool>,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,sameNowKg: null == sameNowKg ? _self.sameNowKg : sameNowKg // ignore: cast_nullable_to_non_nullable
as double,sameBeforeKg: null == sameBeforeKg ? _self.sameBeforeKg : sameBeforeKg // ignore: cast_nullable_to_non_nullable
as double,previousCutoff: freezed == previousCutoff ? _self.previousCutoff : previousCutoff // ignore: cast_nullable_to_non_nullable
as String?,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,planFactKg: freezed == planFactKg ? _self.planFactKg : planFactKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,workedDays: null == workedDays ? _self.workedDays : workedDays // ignore: cast_nullable_to_non_nullable
as int,elapsedDays: freezed == elapsedDays ? _self.elapsedDays : elapsedDays // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$FinanceMeta {

 bool get hasData; bool get demo;/// Пояснение сервера, если данных нет («Данных 1С в OneBase пока нет: …»).
 String? get note; String? get dataThrough; String? get syncedAt; String? get organizationName; String get from; String get to;
/// Create a copy of FinanceMeta
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<FinanceMeta> get copyWith => _$FinanceMetaCopyWithImpl<FinanceMeta>(this as FinanceMeta, _$identity);

  /// Serializes this FinanceMeta to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinanceMeta;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinanceMeta&&(identical(other.hasData, _this.hasData) || other.hasData == _this.hasData)&&(identical(other.demo, _this.demo) || other.demo == _this.demo)&&(identical(other.note, _this.note) || other.note == _this.note)&&(identical(other.dataThrough, _this.dataThrough) || other.dataThrough == _this.dataThrough)&&(identical(other.syncedAt, _this.syncedAt) || other.syncedAt == _this.syncedAt)&&(identical(other.organizationName, _this.organizationName) || other.organizationName == _this.organizationName)&&(identical(other.from, _this.from) || other.from == _this.from)&&(identical(other.to, _this.to) || other.to == _this.to));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinanceMeta;
  return Object.hash(runtimeType,_this.hasData,_this.demo,_this.note,_this.dataThrough,_this.syncedAt,_this.organizationName,_this.from,_this.to);
}

@override
String toString() {
  final _this = this as FinanceMeta;
  return 'FinanceMeta(hasData: ${_this.hasData}, demo: ${_this.demo}, note: ${_this.note}, dataThrough: ${_this.dataThrough}, syncedAt: ${_this.syncedAt}, organizationName: ${_this.organizationName}, from: ${_this.from}, to: ${_this.to})';
}


}

/// @nodoc
abstract mixin class $FinanceMetaCopyWith<$Res>  {
  factory $FinanceMetaCopyWith(FinanceMeta value, $Res Function(FinanceMeta) _then) = _$FinanceMetaCopyWithImpl;
@useResult
$Res call({
 bool hasData, bool demo, String? note, String? dataThrough, String? syncedAt, String? organizationName, String from, String to
});




}
/// @nodoc
class _$FinanceMetaCopyWithImpl<$Res>
    implements $FinanceMetaCopyWith<$Res> {
  _$FinanceMetaCopyWithImpl(this._self, this._then);

  final FinanceMeta _self;
  final $Res Function(FinanceMeta) _then;

/// Create a copy of FinanceMeta
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? hasData = null,Object? demo = null,Object? note = freezed,Object? dataThrough = freezed,Object? syncedAt = freezed,Object? organizationName = freezed,Object? from = null,Object? to = null,}) {
  return _then(FinanceMeta(
hasData: null == hasData ? _self.hasData : hasData // ignore: cast_nullable_to_non_nullable
as bool,demo: null == demo ? _self.demo : demo // ignore: cast_nullable_to_non_nullable
as bool,note: freezed == note ? _self.note : note // ignore: cast_nullable_to_non_nullable
as String?,dataThrough: freezed == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String?,syncedAt: freezed == syncedAt ? _self.syncedAt : syncedAt // ignore: cast_nullable_to_non_nullable
as String?,organizationName: freezed == organizationName ? _self.organizationName : organizationName // ignore: cast_nullable_to_non_nullable
as String?,from: null == from ? _self.from : from // ignore: cast_nullable_to_non_nullable
as String,to: null == to ? _self.to : to // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [FinanceMeta].
extension FinanceMetaPatterns on FinanceMeta {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinanceMeta value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinanceMeta() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinanceMeta value)  $default,){
final _that = this;
switch (_that) {
case _FinanceMeta():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinanceMeta value)?  $default,){
final _that = this;
switch (_that) {
case _FinanceMeta() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( bool hasData,  bool demo,  String? note,  String? dataThrough,  String? syncedAt,  String? organizationName,  String from,  String to)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinanceMeta() when $default != null:
return $default(_that.hasData,_that.demo,_that.note,_that.dataThrough,_that.syncedAt,_that.organizationName,_that.from,_that.to);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( bool hasData,  bool demo,  String? note,  String? dataThrough,  String? syncedAt,  String? organizationName,  String from,  String to)  $default,) {final _that = this;
switch (_that) {
case _FinanceMeta():
return $default(_that.hasData,_that.demo,_that.note,_that.dataThrough,_that.syncedAt,_that.organizationName,_that.from,_that.to);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( bool hasData,  bool demo,  String? note,  String? dataThrough,  String? syncedAt,  String? organizationName,  String from,  String to)?  $default,) {final _that = this;
switch (_that) {
case _FinanceMeta() when $default != null:
return $default(_that.hasData,_that.demo,_that.note,_that.dataThrough,_that.syncedAt,_that.organizationName,_that.from,_that.to);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinanceMeta implements FinanceMeta {
  const _FinanceMeta({this.hasData = false, this.demo = false, this.note, this.dataThrough, this.syncedAt, this.organizationName, required this.from, required this.to});
  factory _FinanceMeta.fromJson(Map<String, dynamic> json) => _$FinanceMetaFromJson(json);

@override@JsonKey() final  bool hasData;
@override@JsonKey() final  bool demo;
/// Пояснение сервера, если данных нет («Данных 1С в OneBase пока нет: …»).
@override final  String? note;
@override final  String? dataThrough;
@override final  String? syncedAt;
@override final  String? organizationName;
@override final  String from;
@override final  String to;

/// Create a copy of FinanceMeta
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinanceMetaCopyWith<_FinanceMeta> get copyWith => __$FinanceMetaCopyWithImpl<_FinanceMeta>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinanceMetaToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinanceMeta&&(identical(other.hasData, hasData) || other.hasData == hasData)&&(identical(other.demo, demo) || other.demo == demo)&&(identical(other.note, note) || other.note == note)&&(identical(other.dataThrough, dataThrough) || other.dataThrough == dataThrough)&&(identical(other.syncedAt, syncedAt) || other.syncedAt == syncedAt)&&(identical(other.organizationName, organizationName) || other.organizationName == organizationName)&&(identical(other.from, from) || other.from == from)&&(identical(other.to, to) || other.to == to));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,hasData,demo,note,dataThrough,syncedAt,organizationName,from,to);
}

@override
String toString() {
    return 'FinanceMeta(hasData: $hasData, demo: $demo, note: $note, dataThrough: $dataThrough, syncedAt: $syncedAt, organizationName: $organizationName, from: $from, to: $to)';
}


}

/// @nodoc
abstract mixin class _$FinanceMetaCopyWith<$Res> implements $FinanceMetaCopyWith<$Res> {
  factory _$FinanceMetaCopyWith(_FinanceMeta value, $Res Function(_FinanceMeta) _then) = __$FinanceMetaCopyWithImpl;
@override @useResult
$Res call({
 bool hasData, bool demo, String? note, String? dataThrough, String? syncedAt, String? organizationName, String from, String to
});




}
/// @nodoc
class __$FinanceMetaCopyWithImpl<$Res>
    implements _$FinanceMetaCopyWith<$Res> {
  __$FinanceMetaCopyWithImpl(this._self, this._then);

  final _FinanceMeta _self;
  final $Res Function(_FinanceMeta) _then;

/// Create a copy of FinanceMeta
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? hasData = null,Object? demo = null,Object? note = freezed,Object? dataThrough = freezed,Object? syncedAt = freezed,Object? organizationName = freezed,Object? from = null,Object? to = null,}) {
  return _then(_FinanceMeta(
hasData: null == hasData ? _self.hasData : hasData // ignore: cast_nullable_to_non_nullable
as bool,demo: null == demo ? _self.demo : demo // ignore: cast_nullable_to_non_nullable
as bool,note: freezed == note ? _self.note : note // ignore: cast_nullable_to_non_nullable
as String?,dataThrough: freezed == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String?,syncedAt: freezed == syncedAt ? _self.syncedAt : syncedAt // ignore: cast_nullable_to_non_nullable
as String?,organizationName: freezed == organizationName ? _self.organizationName : organizationName // ignore: cast_nullable_to_non_nullable
as String?,from: null == from ? _self.from : from // ignore: cast_nullable_to_non_nullable
as String,to: null == to ? _self.to : to // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$PnlValue {

 String get code; String get title;/// income | expense | total
 String get kind; double get value; double? get previous;
/// Create a copy of PnlValue
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$PnlValueCopyWith<PnlValue> get copyWith => _$PnlValueCopyWithImpl<PnlValue>(this as PnlValue, _$identity);

  /// Serializes this PnlValue to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as PnlValue;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is PnlValue&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.value, _this.value) || other.value == _this.value)&&(identical(other.previous, _this.previous) || other.previous == _this.previous));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as PnlValue;
  return Object.hash(runtimeType,_this.code,_this.title,_this.kind,_this.value,_this.previous);
}

@override
String toString() {
  final _this = this as PnlValue;
  return 'PnlValue(code: ${_this.code}, title: ${_this.title}, kind: ${_this.kind}, value: ${_this.value}, previous: ${_this.previous})';
}


}

/// @nodoc
abstract mixin class $PnlValueCopyWith<$Res>  {
  factory $PnlValueCopyWith(PnlValue value, $Res Function(PnlValue) _then) = _$PnlValueCopyWithImpl;
@useResult
$Res call({
 String code, String title, String kind, double value, double? previous
});




}
/// @nodoc
class _$PnlValueCopyWithImpl<$Res>
    implements $PnlValueCopyWith<$Res> {
  _$PnlValueCopyWithImpl(this._self, this._then);

  final PnlValue _self;
  final $Res Function(PnlValue) _then;

/// Create a copy of PnlValue
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? code = null,Object? title = null,Object? kind = null,Object? value = null,Object? previous = freezed,}) {
  return _then(PnlValue(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [PnlValue].
extension PnlValuePatterns on PnlValue {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _PnlValue value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _PnlValue() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _PnlValue value)  $default,){
final _that = this;
switch (_that) {
case _PnlValue():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _PnlValue value)?  $default,){
final _that = this;
switch (_that) {
case _PnlValue() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String code,  String title,  String kind,  double value,  double? previous)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _PnlValue() when $default != null:
return $default(_that.code,_that.title,_that.kind,_that.value,_that.previous);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String code,  String title,  String kind,  double value,  double? previous)  $default,) {final _that = this;
switch (_that) {
case _PnlValue():
return $default(_that.code,_that.title,_that.kind,_that.value,_that.previous);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String code,  String title,  String kind,  double value,  double? previous)?  $default,) {final _that = this;
switch (_that) {
case _PnlValue() when $default != null:
return $default(_that.code,_that.title,_that.kind,_that.value,_that.previous);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _PnlValue implements PnlValue {
  const _PnlValue({required this.code, required this.title, required this.kind, this.value = 0, this.previous});
  factory _PnlValue.fromJson(Map<String, dynamic> json) => _$PnlValueFromJson(json);

@override final  String code;
@override final  String title;
/// income | expense | total
@override final  String kind;
@override@JsonKey() final  double value;
@override final  double? previous;

/// Create a copy of PnlValue
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$PnlValueCopyWith<_PnlValue> get copyWith => __$PnlValueCopyWithImpl<_PnlValue>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$PnlValueToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _PnlValue&&(identical(other.code, code) || other.code == code)&&(identical(other.title, title) || other.title == title)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.value, value) || other.value == value)&&(identical(other.previous, previous) || other.previous == previous));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,code,title,kind,value,previous);
}

@override
String toString() {
    return 'PnlValue(code: $code, title: $title, kind: $kind, value: $value, previous: $previous)';
}


}

/// @nodoc
abstract mixin class _$PnlValueCopyWith<$Res> implements $PnlValueCopyWith<$Res> {
  factory _$PnlValueCopyWith(_PnlValue value, $Res Function(_PnlValue) _then) = __$PnlValueCopyWithImpl;
@override @useResult
$Res call({
 String code, String title, String kind, double value, double? previous
});




}
/// @nodoc
class __$PnlValueCopyWithImpl<$Res>
    implements _$PnlValueCopyWith<$Res> {
  __$PnlValueCopyWithImpl(this._self, this._then);

  final _PnlValue _self;
  final $Res Function(_PnlValue) _then;

/// Create a copy of PnlValue
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? code = null,Object? title = null,Object? kind = null,Object? value = null,Object? previous = freezed,}) {
  return _then(_PnlValue(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$MonthPoint {

/// Первое число месяца: «2026-11-01».
 String get month; double get revenue;/// Расходы месяца — себестоимость, расходы периода и финансовые (считает сервер).
 double get expenses; double get netProfit;
/// Create a copy of MonthPoint
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$MonthPointCopyWith<MonthPoint> get copyWith => _$MonthPointCopyWithImpl<MonthPoint>(this as MonthPoint, _$identity);

  /// Serializes this MonthPoint to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as MonthPoint;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is MonthPoint&&(identical(other.month, _this.month) || other.month == _this.month)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.expenses, _this.expenses) || other.expenses == _this.expenses)&&(identical(other.netProfit, _this.netProfit) || other.netProfit == _this.netProfit));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as MonthPoint;
  return Object.hash(runtimeType,_this.month,_this.revenue,_this.expenses,_this.netProfit);
}

@override
String toString() {
  final _this = this as MonthPoint;
  return 'MonthPoint(month: ${_this.month}, revenue: ${_this.revenue}, expenses: ${_this.expenses}, netProfit: ${_this.netProfit})';
}


}

/// @nodoc
abstract mixin class $MonthPointCopyWith<$Res>  {
  factory $MonthPointCopyWith(MonthPoint value, $Res Function(MonthPoint) _then) = _$MonthPointCopyWithImpl;
@useResult
$Res call({
 String month, double revenue, double expenses, double netProfit
});




}
/// @nodoc
class _$MonthPointCopyWithImpl<$Res>
    implements $MonthPointCopyWith<$Res> {
  _$MonthPointCopyWithImpl(this._self, this._then);

  final MonthPoint _self;
  final $Res Function(MonthPoint) _then;

/// Create a copy of MonthPoint
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? month = null,Object? revenue = null,Object? expenses = null,Object? netProfit = null,}) {
  return _then(MonthPoint(
month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as String,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,expenses: null == expenses ? _self.expenses : expenses // ignore: cast_nullable_to_non_nullable
as double,netProfit: null == netProfit ? _self.netProfit : netProfit // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [MonthPoint].
extension MonthPointPatterns on MonthPoint {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _MonthPoint value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _MonthPoint() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _MonthPoint value)  $default,){
final _that = this;
switch (_that) {
case _MonthPoint():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _MonthPoint value)?  $default,){
final _that = this;
switch (_that) {
case _MonthPoint() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String month,  double revenue,  double expenses,  double netProfit)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _MonthPoint() when $default != null:
return $default(_that.month,_that.revenue,_that.expenses,_that.netProfit);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String month,  double revenue,  double expenses,  double netProfit)  $default,) {final _that = this;
switch (_that) {
case _MonthPoint():
return $default(_that.month,_that.revenue,_that.expenses,_that.netProfit);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String month,  double revenue,  double expenses,  double netProfit)?  $default,) {final _that = this;
switch (_that) {
case _MonthPoint() when $default != null:
return $default(_that.month,_that.revenue,_that.expenses,_that.netProfit);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _MonthPoint implements MonthPoint {
  const _MonthPoint({required this.month, this.revenue = 0, this.expenses = 0, this.netProfit = 0});
  factory _MonthPoint.fromJson(Map<String, dynamic> json) => _$MonthPointFromJson(json);

/// Первое число месяца: «2026-11-01».
@override final  String month;
@override@JsonKey() final  double revenue;
/// Расходы месяца — себестоимость, расходы периода и финансовые (считает сервер).
@override@JsonKey() final  double expenses;
@override@JsonKey() final  double netProfit;

/// Create a copy of MonthPoint
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$MonthPointCopyWith<_MonthPoint> get copyWith => __$MonthPointCopyWithImpl<_MonthPoint>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$MonthPointToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _MonthPoint&&(identical(other.month, month) || other.month == month)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.expenses, expenses) || other.expenses == expenses)&&(identical(other.netProfit, netProfit) || other.netProfit == netProfit));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,month,revenue,expenses,netProfit);
}

@override
String toString() {
    return 'MonthPoint(month: $month, revenue: $revenue, expenses: $expenses, netProfit: $netProfit)';
}


}

/// @nodoc
abstract mixin class _$MonthPointCopyWith<$Res> implements $MonthPointCopyWith<$Res> {
  factory _$MonthPointCopyWith(_MonthPoint value, $Res Function(_MonthPoint) _then) = __$MonthPointCopyWithImpl;
@override @useResult
$Res call({
 String month, double revenue, double expenses, double netProfit
});




}
/// @nodoc
class __$MonthPointCopyWithImpl<$Res>
    implements _$MonthPointCopyWith<$Res> {
  __$MonthPointCopyWithImpl(this._self, this._then);

  final _MonthPoint _self;
  final $Res Function(_MonthPoint) _then;

/// Create a copy of MonthPoint
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? month = null,Object? revenue = null,Object? expenses = null,Object? netProfit = null,}) {
  return _then(_MonthPoint(
month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as String,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,expenses: null == expenses ? _self.expenses : expenses // ignore: cast_nullable_to_non_nullable
as double,netProfit: null == netProfit ? _self.netProfit : netProfit // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$FinanceOverview {

 FinanceMeta get meta; List<PnlValue> get pnl; List<MonthPoint> get months; double get cashTotal; double get receivables; double get payables;
/// Create a copy of FinanceOverview
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinanceOverviewCopyWith<FinanceOverview> get copyWith => _$FinanceOverviewCopyWithImpl<FinanceOverview>(this as FinanceOverview, _$identity);

  /// Serializes this FinanceOverview to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinanceOverview;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinanceOverview&&(identical(other.meta, _this.meta) || other.meta == _this.meta)&&const DeepCollectionEquality().equals(other.pnl, _this.pnl)&&const DeepCollectionEquality().equals(other.months, _this.months)&&(identical(other.cashTotal, _this.cashTotal) || other.cashTotal == _this.cashTotal)&&(identical(other.receivables, _this.receivables) || other.receivables == _this.receivables)&&(identical(other.payables, _this.payables) || other.payables == _this.payables));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinanceOverview;
  return Object.hash(runtimeType,_this.meta,const DeepCollectionEquality().hash(_this.pnl),const DeepCollectionEquality().hash(_this.months),_this.cashTotal,_this.receivables,_this.payables);
}

@override
String toString() {
  final _this = this as FinanceOverview;
  return 'FinanceOverview(meta: ${_this.meta}, pnl: ${_this.pnl}, months: ${_this.months}, cashTotal: ${_this.cashTotal}, receivables: ${_this.receivables}, payables: ${_this.payables})';
}


}

/// @nodoc
abstract mixin class $FinanceOverviewCopyWith<$Res>  {
  factory $FinanceOverviewCopyWith(FinanceOverview value, $Res Function(FinanceOverview) _then) = _$FinanceOverviewCopyWithImpl;
@useResult
$Res call({
 FinanceMeta meta, List<PnlValue> pnl, List<MonthPoint> months, double cashTotal, double receivables, double payables
});


$FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class _$FinanceOverviewCopyWithImpl<$Res>
    implements $FinanceOverviewCopyWith<$Res> {
  _$FinanceOverviewCopyWithImpl(this._self, this._then);

  final FinanceOverview _self;
  final $Res Function(FinanceOverview) _then;

/// Create a copy of FinanceOverview
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? meta = null,Object? pnl = null,Object? months = null,Object? cashTotal = null,Object? receivables = null,Object? payables = null,}) {
  return _then(FinanceOverview(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,pnl: null == pnl ? _self.pnl : pnl // ignore: cast_nullable_to_non_nullable
as List<PnlValue>,months: null == months ? _self.months : months // ignore: cast_nullable_to_non_nullable
as List<MonthPoint>,cashTotal: null == cashTotal ? _self.cashTotal : cashTotal // ignore: cast_nullable_to_non_nullable
as double,receivables: null == receivables ? _self.receivables : receivables // ignore: cast_nullable_to_non_nullable
as double,payables: null == payables ? _self.payables : payables // ignore: cast_nullable_to_non_nullable
as double,
  ));
}
/// Create a copy of FinanceOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}
}


/// Adds pattern-matching-related methods to [FinanceOverview].
extension FinanceOverviewPatterns on FinanceOverview {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinanceOverview value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinanceOverview() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinanceOverview value)  $default,){
final _that = this;
switch (_that) {
case _FinanceOverview():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinanceOverview value)?  $default,){
final _that = this;
switch (_that) {
case _FinanceOverview() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<PnlValue> pnl,  List<MonthPoint> months,  double cashTotal,  double receivables,  double payables)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinanceOverview() when $default != null:
return $default(_that.meta,_that.pnl,_that.months,_that.cashTotal,_that.receivables,_that.payables);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<PnlValue> pnl,  List<MonthPoint> months,  double cashTotal,  double receivables,  double payables)  $default,) {final _that = this;
switch (_that) {
case _FinanceOverview():
return $default(_that.meta,_that.pnl,_that.months,_that.cashTotal,_that.receivables,_that.payables);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FinanceMeta meta,  List<PnlValue> pnl,  List<MonthPoint> months,  double cashTotal,  double receivables,  double payables)?  $default,) {final _that = this;
switch (_that) {
case _FinanceOverview() when $default != null:
return $default(_that.meta,_that.pnl,_that.months,_that.cashTotal,_that.receivables,_that.payables);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinanceOverview extends FinanceOverview {
  const _FinanceOverview({required this.meta,  List<PnlValue> pnl = const <PnlValue>[],  List<MonthPoint> months = const <MonthPoint>[], this.cashTotal = 0, this.receivables = 0, this.payables = 0}): _pnl = pnl,_months = months,super._();
  factory _FinanceOverview.fromJson(Map<String, dynamic> json) => _$FinanceOverviewFromJson(json);

@override final  FinanceMeta meta;
 final  List<PnlValue> _pnl;
@override@JsonKey() List<PnlValue> get pnl {
  if (_pnl is EqualUnmodifiableListView) return _pnl;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_pnl);
}

 final  List<MonthPoint> _months;
@override@JsonKey() List<MonthPoint> get months {
  if (_months is EqualUnmodifiableListView) return _months;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_months);
}

@override@JsonKey() final  double cashTotal;
@override@JsonKey() final  double receivables;
@override@JsonKey() final  double payables;

/// Create a copy of FinanceOverview
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinanceOverviewCopyWith<_FinanceOverview> get copyWith => __$FinanceOverviewCopyWithImpl<_FinanceOverview>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinanceOverviewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinanceOverview&&(identical(other.meta, meta) || other.meta == meta)&&const DeepCollectionEquality().equals(other.pnl, _pnl)&&const DeepCollectionEquality().equals(other.months, _months)&&(identical(other.cashTotal, cashTotal) || other.cashTotal == cashTotal)&&(identical(other.receivables, receivables) || other.receivables == receivables)&&(identical(other.payables, payables) || other.payables == payables));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,meta,const DeepCollectionEquality().hash(_pnl),const DeepCollectionEquality().hash(_months),cashTotal,receivables,payables);
}

@override
String toString() {
    return 'FinanceOverview(meta: $meta, pnl: $pnl, months: $months, cashTotal: $cashTotal, receivables: $receivables, payables: $payables)';
}


}

/// @nodoc
abstract mixin class _$FinanceOverviewCopyWith<$Res> implements $FinanceOverviewCopyWith<$Res> {
  factory _$FinanceOverviewCopyWith(_FinanceOverview value, $Res Function(_FinanceOverview) _then) = __$FinanceOverviewCopyWithImpl;
@override @useResult
$Res call({
 FinanceMeta meta, List<PnlValue> pnl, List<MonthPoint> months, double cashTotal, double receivables, double payables
});


@override $FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class __$FinanceOverviewCopyWithImpl<$Res>
    implements _$FinanceOverviewCopyWith<$Res> {
  __$FinanceOverviewCopyWithImpl(this._self, this._then);

  final _FinanceOverview _self;
  final $Res Function(_FinanceOverview) _then;

/// Create a copy of FinanceOverview
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? meta = null,Object? pnl = null,Object? months = null,Object? cashTotal = null,Object? receivables = null,Object? payables = null,}) {
  return _then(_FinanceOverview(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,pnl: null == pnl ? _self._pnl : pnl // ignore: cast_nullable_to_non_nullable
as List<PnlValue>,months: null == months ? _self._months : months // ignore: cast_nullable_to_non_nullable
as List<MonthPoint>,cashTotal: null == cashTotal ? _self.cashTotal : cashTotal // ignore: cast_nullable_to_non_nullable
as double,receivables: null == receivables ? _self.receivables : receivables // ignore: cast_nullable_to_non_nullable
as double,payables: null == payables ? _self.payables : payables // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

/// Create a copy of FinanceOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}
}


/// @nodoc
mixin _$FinanceOrganization {

 String get id; String get name;
/// Create a copy of FinanceOrganization
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinanceOrganizationCopyWith<FinanceOrganization> get copyWith => _$FinanceOrganizationCopyWithImpl<FinanceOrganization>(this as FinanceOrganization, _$identity);

  /// Serializes this FinanceOrganization to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinanceOrganization;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinanceOrganization&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinanceOrganization;
  return Object.hash(runtimeType,_this.id,_this.name);
}

@override
String toString() {
  final _this = this as FinanceOrganization;
  return 'FinanceOrganization(id: ${_this.id}, name: ${_this.name})';
}


}

/// @nodoc
abstract mixin class $FinanceOrganizationCopyWith<$Res>  {
  factory $FinanceOrganizationCopyWith(FinanceOrganization value, $Res Function(FinanceOrganization) _then) = _$FinanceOrganizationCopyWithImpl;
@useResult
$Res call({
 String id, String name
});




}
/// @nodoc
class _$FinanceOrganizationCopyWithImpl<$Res>
    implements $FinanceOrganizationCopyWith<$Res> {
  _$FinanceOrganizationCopyWithImpl(this._self, this._then);

  final FinanceOrganization _self;
  final $Res Function(FinanceOrganization) _then;

/// Create a copy of FinanceOrganization
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,}) {
  return _then(FinanceOrganization(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [FinanceOrganization].
extension FinanceOrganizationPatterns on FinanceOrganization {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinanceOrganization value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinanceOrganization() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinanceOrganization value)  $default,){
final _that = this;
switch (_that) {
case _FinanceOrganization():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinanceOrganization value)?  $default,){
final _that = this;
switch (_that) {
case _FinanceOrganization() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String name)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinanceOrganization() when $default != null:
return $default(_that.id,_that.name);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String name)  $default,) {final _that = this;
switch (_that) {
case _FinanceOrganization():
return $default(_that.id,_that.name);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String name)?  $default,) {final _that = this;
switch (_that) {
case _FinanceOrganization() when $default != null:
return $default(_that.id,_that.name);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinanceOrganization implements FinanceOrganization {
  const _FinanceOrganization({required this.id, required this.name});
  factory _FinanceOrganization.fromJson(Map<String, dynamic> json) => _$FinanceOrganizationFromJson(json);

@override final  String id;
@override final  String name;

/// Create a copy of FinanceOrganization
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinanceOrganizationCopyWith<_FinanceOrganization> get copyWith => __$FinanceOrganizationCopyWithImpl<_FinanceOrganization>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinanceOrganizationToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinanceOrganization&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name);
}

@override
String toString() {
    return 'FinanceOrganization(id: $id, name: $name)';
}


}

/// @nodoc
abstract mixin class _$FinanceOrganizationCopyWith<$Res> implements $FinanceOrganizationCopyWith<$Res> {
  factory _$FinanceOrganizationCopyWith(_FinanceOrganization value, $Res Function(_FinanceOrganization) _then) = __$FinanceOrganizationCopyWithImpl;
@override @useResult
$Res call({
 String id, String name
});




}
/// @nodoc
class __$FinanceOrganizationCopyWithImpl<$Res>
    implements _$FinanceOrganizationCopyWith<$Res> {
  __$FinanceOrganizationCopyWithImpl(this._self, this._then);

  final _FinanceOrganization _self;
  final $Res Function(_FinanceOrganization) _then;

/// Create a copy of FinanceOrganization
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,}) {
  return _then(_FinanceOrganization(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$HrDivision {

@JsonKey(fromJson: _string) String get id; String get name; int get active; int get scheduled; int get present; int get late; int get absent;
/// Create a copy of HrDivision
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrDivisionCopyWith<HrDivision> get copyWith => _$HrDivisionCopyWithImpl<HrDivision>(this as HrDivision, _$identity);

  /// Serializes this HrDivision to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrDivision;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrDivision&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.active, _this.active) || other.active == _this.active)&&(identical(other.scheduled, _this.scheduled) || other.scheduled == _this.scheduled)&&(identical(other.present, _this.present) || other.present == _this.present)&&(identical(other.late, _this.late) || other.late == _this.late)&&(identical(other.absent, _this.absent) || other.absent == _this.absent));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrDivision;
  return Object.hash(runtimeType,_this.id,_this.name,_this.active,_this.scheduled,_this.present,_this.late,_this.absent);
}

@override
String toString() {
  final _this = this as HrDivision;
  return 'HrDivision(id: ${_this.id}, name: ${_this.name}, active: ${_this.active}, scheduled: ${_this.scheduled}, present: ${_this.present}, late: ${_this.late}, absent: ${_this.absent})';
}


}

/// @nodoc
abstract mixin class $HrDivisionCopyWith<$Res>  {
  factory $HrDivisionCopyWith(HrDivision value, $Res Function(HrDivision) _then) = _$HrDivisionCopyWithImpl;
@useResult
$Res call({
@JsonKey(fromJson: _string) String id, String name, int active, int scheduled, int present, int late, int absent
});




}
/// @nodoc
class _$HrDivisionCopyWithImpl<$Res>
    implements $HrDivisionCopyWith<$Res> {
  _$HrDivisionCopyWithImpl(this._self, this._then);

  final HrDivision _self;
  final $Res Function(HrDivision) _then;

/// Create a copy of HrDivision
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,}) {
  return _then(HrDivision(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,active: null == active ? _self.active : active // ignore: cast_nullable_to_non_nullable
as int,scheduled: null == scheduled ? _self.scheduled : scheduled // ignore: cast_nullable_to_non_nullable
as int,present: null == present ? _self.present : present // ignore: cast_nullable_to_non_nullable
as int,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as int,absent: null == absent ? _self.absent : absent // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [HrDivision].
extension HrDivisionPatterns on HrDivision {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrDivision value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrDivision() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrDivision value)  $default,){
final _that = this;
switch (_that) {
case _HrDivision():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrDivision value)?  $default,){
final _that = this;
switch (_that) {
case _HrDivision() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function(@JsonKey(fromJson: _string)  String id,  String name,  int active,  int scheduled,  int present,  int late,  int absent)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrDivision() when $default != null:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function(@JsonKey(fromJson: _string)  String id,  String name,  int active,  int scheduled,  int present,  int late,  int absent)  $default,) {final _that = this;
switch (_that) {
case _HrDivision():
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function(@JsonKey(fromJson: _string)  String id,  String name,  int active,  int scheduled,  int present,  int late,  int absent)?  $default,) {final _that = this;
switch (_that) {
case _HrDivision() when $default != null:
return $default(_that.id,_that.name,_that.active,_that.scheduled,_that.present,_that.late,_that.absent);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrDivision implements HrDivision {
  const _HrDivision({@JsonKey(fromJson: _string) required this.id, required this.name, this.active = 0, this.scheduled = 0, this.present = 0, this.late = 0, this.absent = 0});
  factory _HrDivision.fromJson(Map<String, dynamic> json) => _$HrDivisionFromJson(json);

@override@JsonKey(fromJson: _string) final  String id;
@override final  String name;
@override@JsonKey() final  int active;
@override@JsonKey() final  int scheduled;
@override@JsonKey() final  int present;
@override@JsonKey() final  int late;
@override@JsonKey() final  int absent;

/// Create a copy of HrDivision
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrDivisionCopyWith<_HrDivision> get copyWith => __$HrDivisionCopyWithImpl<_HrDivision>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrDivisionToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrDivision&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.active, active) || other.active == active)&&(identical(other.scheduled, scheduled) || other.scheduled == scheduled)&&(identical(other.present, present) || other.present == present)&&(identical(other.late, late) || other.late == late)&&(identical(other.absent, absent) || other.absent == absent));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name,active,scheduled,present,late,absent);
}

@override
String toString() {
    return 'HrDivision(id: $id, name: $name, active: $active, scheduled: $scheduled, present: $present, late: $late, absent: $absent)';
}


}

/// @nodoc
abstract mixin class _$HrDivisionCopyWith<$Res> implements $HrDivisionCopyWith<$Res> {
  factory _$HrDivisionCopyWith(_HrDivision value, $Res Function(_HrDivision) _then) = __$HrDivisionCopyWithImpl;
@override @useResult
$Res call({
@JsonKey(fromJson: _string) String id, String name, int active, int scheduled, int present, int late, int absent
});




}
/// @nodoc
class __$HrDivisionCopyWithImpl<$Res>
    implements _$HrDivisionCopyWith<$Res> {
  __$HrDivisionCopyWithImpl(this._self, this._then);

  final _HrDivision _self;
  final $Res Function(_HrDivision) _then;

/// Create a copy of HrDivision
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,}) {
  return _then(_HrDivision(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
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
mixin _$HrCompare {

 String? get date; double? get presentPct;
/// Create a copy of HrCompare
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrCompareCopyWith<HrCompare> get copyWith => _$HrCompareCopyWithImpl<HrCompare>(this as HrCompare, _$identity);

  /// Serializes this HrCompare to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrCompare;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrCompare&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.presentPct, _this.presentPct) || other.presentPct == _this.presentPct));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrCompare;
  return Object.hash(runtimeType,_this.date,_this.presentPct);
}

@override
String toString() {
  final _this = this as HrCompare;
  return 'HrCompare(date: ${_this.date}, presentPct: ${_this.presentPct})';
}


}

/// @nodoc
abstract mixin class $HrCompareCopyWith<$Res>  {
  factory $HrCompareCopyWith(HrCompare value, $Res Function(HrCompare) _then) = _$HrCompareCopyWithImpl;
@useResult
$Res call({
 String? date, double? presentPct
});




}
/// @nodoc
class _$HrCompareCopyWithImpl<$Res>
    implements $HrCompareCopyWith<$Res> {
  _$HrCompareCopyWithImpl(this._self, this._then);

  final HrCompare _self;
  final $Res Function(HrCompare) _then;

/// Create a copy of HrCompare
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? date = freezed,Object? presentPct = freezed,}) {
  return _then(HrCompare(
date: freezed == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String?,presentPct: freezed == presentPct ? _self.presentPct : presentPct // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [HrCompare].
extension HrComparePatterns on HrCompare {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrCompare value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrCompare() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrCompare value)  $default,){
final _that = this;
switch (_that) {
case _HrCompare():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrCompare value)?  $default,){
final _that = this;
switch (_that) {
case _HrCompare() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String? date,  double? presentPct)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrCompare() when $default != null:
return $default(_that.date,_that.presentPct);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String? date,  double? presentPct)  $default,) {final _that = this;
switch (_that) {
case _HrCompare():
return $default(_that.date,_that.presentPct);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String? date,  double? presentPct)?  $default,) {final _that = this;
switch (_that) {
case _HrCompare() when $default != null:
return $default(_that.date,_that.presentPct);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrCompare implements HrCompare {
  const _HrCompare({this.date, this.presentPct});
  factory _HrCompare.fromJson(Map<String, dynamic> json) => _$HrCompareFromJson(json);

@override final  String? date;
@override final  double? presentPct;

/// Create a copy of HrCompare
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrCompareCopyWith<_HrCompare> get copyWith => __$HrCompareCopyWithImpl<_HrCompare>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrCompareToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrCompare&&(identical(other.date, date) || other.date == date)&&(identical(other.presentPct, presentPct) || other.presentPct == presentPct));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,date,presentPct);
}

@override
String toString() {
    return 'HrCompare(date: $date, presentPct: $presentPct)';
}


}

/// @nodoc
abstract mixin class _$HrCompareCopyWith<$Res> implements $HrCompareCopyWith<$Res> {
  factory _$HrCompareCopyWith(_HrCompare value, $Res Function(_HrCompare) _then) = __$HrCompareCopyWithImpl;
@override @useResult
$Res call({
 String? date, double? presentPct
});




}
/// @nodoc
class __$HrCompareCopyWithImpl<$Res>
    implements _$HrCompareCopyWith<$Res> {
  __$HrCompareCopyWithImpl(this._self, this._then);

  final _HrCompare _self;
  final $Res Function(_HrCompare) _then;

/// Create a copy of HrCompare
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? date = freezed,Object? presentPct = freezed,}) {
  return _then(_HrCompare(
date: freezed == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String?,presentPct: freezed == presentPct ? _self.presentPct : presentPct // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$HrSync {

 String? get lastSuccessAt; String? get lastStatus;
/// Create a copy of HrSync
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrSyncCopyWith<HrSync> get copyWith => _$HrSyncCopyWithImpl<HrSync>(this as HrSync, _$identity);

  /// Serializes this HrSync to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrSync;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrSync&&(identical(other.lastSuccessAt, _this.lastSuccessAt) || other.lastSuccessAt == _this.lastSuccessAt)&&(identical(other.lastStatus, _this.lastStatus) || other.lastStatus == _this.lastStatus));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrSync;
  return Object.hash(runtimeType,_this.lastSuccessAt,_this.lastStatus);
}

@override
String toString() {
  final _this = this as HrSync;
  return 'HrSync(lastSuccessAt: ${_this.lastSuccessAt}, lastStatus: ${_this.lastStatus})';
}


}

/// @nodoc
abstract mixin class $HrSyncCopyWith<$Res>  {
  factory $HrSyncCopyWith(HrSync value, $Res Function(HrSync) _then) = _$HrSyncCopyWithImpl;
@useResult
$Res call({
 String? lastSuccessAt, String? lastStatus
});




}
/// @nodoc
class _$HrSyncCopyWithImpl<$Res>
    implements $HrSyncCopyWith<$Res> {
  _$HrSyncCopyWithImpl(this._self, this._then);

  final HrSync _self;
  final $Res Function(HrSync) _then;

/// Create a copy of HrSync
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? lastSuccessAt = freezed,Object? lastStatus = freezed,}) {
  return _then(HrSync(
lastSuccessAt: freezed == lastSuccessAt ? _self.lastSuccessAt : lastSuccessAt // ignore: cast_nullable_to_non_nullable
as String?,lastStatus: freezed == lastStatus ? _self.lastStatus : lastStatus // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [HrSync].
extension HrSyncPatterns on HrSync {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrSync value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrSync() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrSync value)  $default,){
final _that = this;
switch (_that) {
case _HrSync():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrSync value)?  $default,){
final _that = this;
switch (_that) {
case _HrSync() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String? lastSuccessAt,  String? lastStatus)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrSync() when $default != null:
return $default(_that.lastSuccessAt,_that.lastStatus);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String? lastSuccessAt,  String? lastStatus)  $default,) {final _that = this;
switch (_that) {
case _HrSync():
return $default(_that.lastSuccessAt,_that.lastStatus);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String? lastSuccessAt,  String? lastStatus)?  $default,) {final _that = this;
switch (_that) {
case _HrSync() when $default != null:
return $default(_that.lastSuccessAt,_that.lastStatus);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrSync implements HrSync {
  const _HrSync({this.lastSuccessAt, this.lastStatus});
  factory _HrSync.fromJson(Map<String, dynamic> json) => _$HrSyncFromJson(json);

@override final  String? lastSuccessAt;
@override final  String? lastStatus;

/// Create a copy of HrSync
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrSyncCopyWith<_HrSync> get copyWith => __$HrSyncCopyWithImpl<_HrSync>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrSyncToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrSync&&(identical(other.lastSuccessAt, lastSuccessAt) || other.lastSuccessAt == lastSuccessAt)&&(identical(other.lastStatus, lastStatus) || other.lastStatus == lastStatus));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,lastSuccessAt,lastStatus);
}

@override
String toString() {
    return 'HrSync(lastSuccessAt: $lastSuccessAt, lastStatus: $lastStatus)';
}


}

/// @nodoc
abstract mixin class _$HrSyncCopyWith<$Res> implements $HrSyncCopyWith<$Res> {
  factory _$HrSyncCopyWith(_HrSync value, $Res Function(_HrSync) _then) = __$HrSyncCopyWithImpl;
@override @useResult
$Res call({
 String? lastSuccessAt, String? lastStatus
});




}
/// @nodoc
class __$HrSyncCopyWithImpl<$Res>
    implements _$HrSyncCopyWith<$Res> {
  __$HrSyncCopyWithImpl(this._self, this._then);

  final _HrSync _self;
  final $Res Function(_HrSync) _then;

/// Create a copy of HrSync
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? lastSuccessAt = freezed,Object? lastStatus = freezed,}) {
  return _then(_HrSync(
lastSuccessAt: freezed == lastSuccessAt ? _self.lastSuccessAt : lastSuccessAt // ignore: cast_nullable_to_non_nullable
as String?,lastStatus: freezed == lastStatus ? _self.lastStatus : lastStatus // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$HrOverview {

 String get date; int get active; int get scheduled; int get present; int get late; int get absent; int get onLeave; HrCompare? get compare; List<HrDivision> get divisions; HrSync? get sync;
/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HrOverviewCopyWith<HrOverview> get copyWith => _$HrOverviewCopyWithImpl<HrOverview>(this as HrOverview, _$identity);

  /// Serializes this HrOverview to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HrOverview;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HrOverview&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.active, _this.active) || other.active == _this.active)&&(identical(other.scheduled, _this.scheduled) || other.scheduled == _this.scheduled)&&(identical(other.present, _this.present) || other.present == _this.present)&&(identical(other.late, _this.late) || other.late == _this.late)&&(identical(other.absent, _this.absent) || other.absent == _this.absent)&&(identical(other.onLeave, _this.onLeave) || other.onLeave == _this.onLeave)&&(identical(other.compare, _this.compare) || other.compare == _this.compare)&&const DeepCollectionEquality().equals(other.divisions, _this.divisions)&&(identical(other.sync, _this.sync) || other.sync == _this.sync));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HrOverview;
  return Object.hash(runtimeType,_this.date,_this.active,_this.scheduled,_this.present,_this.late,_this.absent,_this.onLeave,_this.compare,const DeepCollectionEquality().hash(_this.divisions),_this.sync);
}

@override
String toString() {
  final _this = this as HrOverview;
  return 'HrOverview(date: ${_this.date}, active: ${_this.active}, scheduled: ${_this.scheduled}, present: ${_this.present}, late: ${_this.late}, absent: ${_this.absent}, onLeave: ${_this.onLeave}, compare: ${_this.compare}, divisions: ${_this.divisions}, sync: ${_this.sync})';
}


}

/// @nodoc
abstract mixin class $HrOverviewCopyWith<$Res>  {
  factory $HrOverviewCopyWith(HrOverview value, $Res Function(HrOverview) _then) = _$HrOverviewCopyWithImpl;
@useResult
$Res call({
 String date, int active, int scheduled, int present, int late, int absent, int onLeave, HrCompare? compare, List<HrDivision> divisions, HrSync? sync
});


$HrCompareCopyWith<$Res>? get compare;$HrSyncCopyWith<$Res>? get sync;

}
/// @nodoc
class _$HrOverviewCopyWithImpl<$Res>
    implements $HrOverviewCopyWith<$Res> {
  _$HrOverviewCopyWithImpl(this._self, this._then);

  final HrOverview _self;
  final $Res Function(HrOverview) _then;

/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? date = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,Object? onLeave = null,Object? compare = freezed,Object? divisions = null,Object? sync = freezed,}) {
  return _then(HrOverview(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,active: null == active ? _self.active : active // ignore: cast_nullable_to_non_nullable
as int,scheduled: null == scheduled ? _self.scheduled : scheduled // ignore: cast_nullable_to_non_nullable
as int,present: null == present ? _self.present : present // ignore: cast_nullable_to_non_nullable
as int,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as int,absent: null == absent ? _self.absent : absent // ignore: cast_nullable_to_non_nullable
as int,onLeave: null == onLeave ? _self.onLeave : onLeave // ignore: cast_nullable_to_non_nullable
as int,compare: freezed == compare ? _self.compare : compare // ignore: cast_nullable_to_non_nullable
as HrCompare?,divisions: null == divisions ? _self.divisions : divisions // ignore: cast_nullable_to_non_nullable
as List<HrDivision>,sync: freezed == sync ? _self.sync : sync // ignore: cast_nullable_to_non_nullable
as HrSync?,
  ));
}
/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrCompareCopyWith<$Res>? get compare {
    if (_self.compare == null) {
    return null;
  }

  return $HrCompareCopyWith<$Res>(_self.compare!, (value) {
    return _then(_self.copyWith(compare: value));
  });
}/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrSyncCopyWith<$Res>? get sync {
    if (_self.sync == null) {
    return null;
  }

  return $HrSyncCopyWith<$Res>(_self.sync!, (value) {
    return _then(_self.copyWith(sync: value));
  });
}
}


/// Adds pattern-matching-related methods to [HrOverview].
extension HrOverviewPatterns on HrOverview {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HrOverview value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HrOverview() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HrOverview value)  $default,){
final _that = this;
switch (_that) {
case _HrOverview():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HrOverview value)?  $default,){
final _that = this;
switch (_that) {
case _HrOverview() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String date,  int active,  int scheduled,  int present,  int late,  int absent,  int onLeave,  HrCompare? compare,  List<HrDivision> divisions,  HrSync? sync)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HrOverview() when $default != null:
return $default(_that.date,_that.active,_that.scheduled,_that.present,_that.late,_that.absent,_that.onLeave,_that.compare,_that.divisions,_that.sync);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String date,  int active,  int scheduled,  int present,  int late,  int absent,  int onLeave,  HrCompare? compare,  List<HrDivision> divisions,  HrSync? sync)  $default,) {final _that = this;
switch (_that) {
case _HrOverview():
return $default(_that.date,_that.active,_that.scheduled,_that.present,_that.late,_that.absent,_that.onLeave,_that.compare,_that.divisions,_that.sync);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String date,  int active,  int scheduled,  int present,  int late,  int absent,  int onLeave,  HrCompare? compare,  List<HrDivision> divisions,  HrSync? sync)?  $default,) {final _that = this;
switch (_that) {
case _HrOverview() when $default != null:
return $default(_that.date,_that.active,_that.scheduled,_that.present,_that.late,_that.absent,_that.onLeave,_that.compare,_that.divisions,_that.sync);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HrOverview extends HrOverview {
  const _HrOverview({required this.date, this.active = 0, this.scheduled = 0, this.present = 0, this.late = 0, this.absent = 0, this.onLeave = 0, this.compare,  List<HrDivision> divisions = const <HrDivision>[], this.sync}): _divisions = divisions,super._();
  factory _HrOverview.fromJson(Map<String, dynamic> json) => _$HrOverviewFromJson(json);

@override final  String date;
@override@JsonKey() final  int active;
@override@JsonKey() final  int scheduled;
@override@JsonKey() final  int present;
@override@JsonKey() final  int late;
@override@JsonKey() final  int absent;
@override@JsonKey() final  int onLeave;
@override final  HrCompare? compare;
 final  List<HrDivision> _divisions;
@override@JsonKey() List<HrDivision> get divisions {
  if (_divisions is EqualUnmodifiableListView) return _divisions;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_divisions);
}

@override final  HrSync? sync;

/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HrOverviewCopyWith<_HrOverview> get copyWith => __$HrOverviewCopyWithImpl<_HrOverview>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HrOverviewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HrOverview&&(identical(other.date, date) || other.date == date)&&(identical(other.active, active) || other.active == active)&&(identical(other.scheduled, scheduled) || other.scheduled == scheduled)&&(identical(other.present, present) || other.present == present)&&(identical(other.late, late) || other.late == late)&&(identical(other.absent, absent) || other.absent == absent)&&(identical(other.onLeave, onLeave) || other.onLeave == onLeave)&&(identical(other.compare, compare) || other.compare == compare)&&const DeepCollectionEquality().equals(other.divisions, _divisions)&&(identical(other.sync, sync) || other.sync == sync));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,date,active,scheduled,present,late,absent,onLeave,compare,const DeepCollectionEquality().hash(_divisions),sync);
}

@override
String toString() {
    return 'HrOverview(date: $date, active: $active, scheduled: $scheduled, present: $present, late: $late, absent: $absent, onLeave: $onLeave, compare: $compare, divisions: $divisions, sync: $sync)';
}


}

/// @nodoc
abstract mixin class _$HrOverviewCopyWith<$Res> implements $HrOverviewCopyWith<$Res> {
  factory _$HrOverviewCopyWith(_HrOverview value, $Res Function(_HrOverview) _then) = __$HrOverviewCopyWithImpl;
@override @useResult
$Res call({
 String date, int active, int scheduled, int present, int late, int absent, int onLeave, HrCompare? compare, List<HrDivision> divisions, HrSync? sync
});


@override $HrCompareCopyWith<$Res>? get compare;@override $HrSyncCopyWith<$Res>? get sync;

}
/// @nodoc
class __$HrOverviewCopyWithImpl<$Res>
    implements _$HrOverviewCopyWith<$Res> {
  __$HrOverviewCopyWithImpl(this._self, this._then);

  final _HrOverview _self;
  final $Res Function(_HrOverview) _then;

/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? date = null,Object? active = null,Object? scheduled = null,Object? present = null,Object? late = null,Object? absent = null,Object? onLeave = null,Object? compare = freezed,Object? divisions = null,Object? sync = freezed,}) {
  return _then(_HrOverview(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,active: null == active ? _self.active : active // ignore: cast_nullable_to_non_nullable
as int,scheduled: null == scheduled ? _self.scheduled : scheduled // ignore: cast_nullable_to_non_nullable
as int,present: null == present ? _self.present : present // ignore: cast_nullable_to_non_nullable
as int,late: null == late ? _self.late : late // ignore: cast_nullable_to_non_nullable
as int,absent: null == absent ? _self.absent : absent // ignore: cast_nullable_to_non_nullable
as int,onLeave: null == onLeave ? _self.onLeave : onLeave // ignore: cast_nullable_to_non_nullable
as int,compare: freezed == compare ? _self.compare : compare // ignore: cast_nullable_to_non_nullable
as HrCompare?,divisions: null == divisions ? _self._divisions : divisions // ignore: cast_nullable_to_non_nullable
as List<HrDivision>,sync: freezed == sync ? _self.sync : sync // ignore: cast_nullable_to_non_nullable
as HrSync?,
  ));
}

/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrCompareCopyWith<$Res>? get compare {
    if (_self.compare == null) {
    return null;
  }

  return $HrCompareCopyWith<$Res>(_self.compare!, (value) {
    return _then(_self.copyWith(compare: value));
  });
}/// Create a copy of HrOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$HrSyncCopyWith<$Res>? get sync {
    if (_self.sync == null) {
    return null;
  }

  return $HrSyncCopyWith<$Res>(_self.sync!, (value) {
    return _then(_self.copyWith(sync: value));
  });
}
}


/// @nodoc
mixin _$WorkDepartment {

 String get code; String get name; int get open; int get overdue; int get review; int get blocked;
/// Create a copy of WorkDepartment
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WorkDepartmentCopyWith<WorkDepartment> get copyWith => _$WorkDepartmentCopyWithImpl<WorkDepartment>(this as WorkDepartment, _$identity);

  /// Serializes this WorkDepartment to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WorkDepartment;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WorkDepartment&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.open, _this.open) || other.open == _this.open)&&(identical(other.overdue, _this.overdue) || other.overdue == _this.overdue)&&(identical(other.review, _this.review) || other.review == _this.review)&&(identical(other.blocked, _this.blocked) || other.blocked == _this.blocked));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WorkDepartment;
  return Object.hash(runtimeType,_this.code,_this.name,_this.open,_this.overdue,_this.review,_this.blocked);
}

@override
String toString() {
  final _this = this as WorkDepartment;
  return 'WorkDepartment(code: ${_this.code}, name: ${_this.name}, open: ${_this.open}, overdue: ${_this.overdue}, review: ${_this.review}, blocked: ${_this.blocked})';
}


}

/// @nodoc
abstract mixin class $WorkDepartmentCopyWith<$Res>  {
  factory $WorkDepartmentCopyWith(WorkDepartment value, $Res Function(WorkDepartment) _then) = _$WorkDepartmentCopyWithImpl;
@useResult
$Res call({
 String code, String name, int open, int overdue, int review, int blocked
});




}
/// @nodoc
class _$WorkDepartmentCopyWithImpl<$Res>
    implements $WorkDepartmentCopyWith<$Res> {
  _$WorkDepartmentCopyWithImpl(this._self, this._then);

  final WorkDepartment _self;
  final $Res Function(WorkDepartment) _then;

/// Create a copy of WorkDepartment
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? code = null,Object? name = null,Object? open = null,Object? overdue = null,Object? review = null,Object? blocked = null,}) {
  return _then(WorkDepartment(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,open: null == open ? _self.open : open // ignore: cast_nullable_to_non_nullable
as int,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as int,review: null == review ? _self.review : review // ignore: cast_nullable_to_non_nullable
as int,blocked: null == blocked ? _self.blocked : blocked // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [WorkDepartment].
extension WorkDepartmentPatterns on WorkDepartment {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WorkDepartment value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WorkDepartment() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WorkDepartment value)  $default,){
final _that = this;
switch (_that) {
case _WorkDepartment():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WorkDepartment value)?  $default,){
final _that = this;
switch (_that) {
case _WorkDepartment() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String code,  String name,  int open,  int overdue,  int review,  int blocked)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WorkDepartment() when $default != null:
return $default(_that.code,_that.name,_that.open,_that.overdue,_that.review,_that.blocked);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String code,  String name,  int open,  int overdue,  int review,  int blocked)  $default,) {final _that = this;
switch (_that) {
case _WorkDepartment():
return $default(_that.code,_that.name,_that.open,_that.overdue,_that.review,_that.blocked);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String code,  String name,  int open,  int overdue,  int review,  int blocked)?  $default,) {final _that = this;
switch (_that) {
case _WorkDepartment() when $default != null:
return $default(_that.code,_that.name,_that.open,_that.overdue,_that.review,_that.blocked);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WorkDepartment implements WorkDepartment {
  const _WorkDepartment({required this.code, required this.name, this.open = 0, this.overdue = 0, this.review = 0, this.blocked = 0});
  factory _WorkDepartment.fromJson(Map<String, dynamic> json) => _$WorkDepartmentFromJson(json);

@override final  String code;
@override final  String name;
@override@JsonKey() final  int open;
@override@JsonKey() final  int overdue;
@override@JsonKey() final  int review;
@override@JsonKey() final  int blocked;

/// Create a copy of WorkDepartment
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WorkDepartmentCopyWith<_WorkDepartment> get copyWith => __$WorkDepartmentCopyWithImpl<_WorkDepartment>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WorkDepartmentToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WorkDepartment&&(identical(other.code, code) || other.code == code)&&(identical(other.name, name) || other.name == name)&&(identical(other.open, open) || other.open == open)&&(identical(other.overdue, overdue) || other.overdue == overdue)&&(identical(other.review, review) || other.review == review)&&(identical(other.blocked, blocked) || other.blocked == blocked));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,code,name,open,overdue,review,blocked);
}

@override
String toString() {
    return 'WorkDepartment(code: $code, name: $name, open: $open, overdue: $overdue, review: $review, blocked: $blocked)';
}


}

/// @nodoc
abstract mixin class _$WorkDepartmentCopyWith<$Res> implements $WorkDepartmentCopyWith<$Res> {
  factory _$WorkDepartmentCopyWith(_WorkDepartment value, $Res Function(_WorkDepartment) _then) = __$WorkDepartmentCopyWithImpl;
@override @useResult
$Res call({
 String code, String name, int open, int overdue, int review, int blocked
});




}
/// @nodoc
class __$WorkDepartmentCopyWithImpl<$Res>
    implements _$WorkDepartmentCopyWith<$Res> {
  __$WorkDepartmentCopyWithImpl(this._self, this._then);

  final _WorkDepartment _self;
  final $Res Function(_WorkDepartment) _then;

/// Create a copy of WorkDepartment
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? code = null,Object? name = null,Object? open = null,Object? overdue = null,Object? review = null,Object? blocked = null,}) {
  return _then(_WorkDepartment(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,open: null == open ? _self.open : open // ignore: cast_nullable_to_non_nullable
as int,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as int,review: null == review ? _self.review : review // ignore: cast_nullable_to_non_nullable
as int,blocked: null == blocked ? _self.blocked : blocked // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$WorkTaskBrief {

 String get id; String get title;/// New | InProgress | Review | Done | Cancelled
 String get status;/// Low | Medium | High | Urgent
 String get priority; String? get dueDate; bool get overdue; String? get departmentName; String? get assigneeName;
/// Create a copy of WorkTaskBrief
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WorkTaskBriefCopyWith<WorkTaskBrief> get copyWith => _$WorkTaskBriefCopyWithImpl<WorkTaskBrief>(this as WorkTaskBrief, _$identity);

  /// Serializes this WorkTaskBrief to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WorkTaskBrief;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WorkTaskBrief&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.priority, _this.priority) || other.priority == _this.priority)&&(identical(other.dueDate, _this.dueDate) || other.dueDate == _this.dueDate)&&(identical(other.overdue, _this.overdue) || other.overdue == _this.overdue)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&(identical(other.assigneeName, _this.assigneeName) || other.assigneeName == _this.assigneeName));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WorkTaskBrief;
  return Object.hash(runtimeType,_this.id,_this.title,_this.status,_this.priority,_this.dueDate,_this.overdue,_this.departmentName,_this.assigneeName);
}

@override
String toString() {
  final _this = this as WorkTaskBrief;
  return 'WorkTaskBrief(id: ${_this.id}, title: ${_this.title}, status: ${_this.status}, priority: ${_this.priority}, dueDate: ${_this.dueDate}, overdue: ${_this.overdue}, departmentName: ${_this.departmentName}, assigneeName: ${_this.assigneeName})';
}


}

/// @nodoc
abstract mixin class $WorkTaskBriefCopyWith<$Res>  {
  factory $WorkTaskBriefCopyWith(WorkTaskBrief value, $Res Function(WorkTaskBrief) _then) = _$WorkTaskBriefCopyWithImpl;
@useResult
$Res call({
 String id, String title, String status, String priority, String? dueDate, bool overdue, String? departmentName, String? assigneeName
});




}
/// @nodoc
class _$WorkTaskBriefCopyWithImpl<$Res>
    implements $WorkTaskBriefCopyWith<$Res> {
  _$WorkTaskBriefCopyWithImpl(this._self, this._then);

  final WorkTaskBrief _self;
  final $Res Function(WorkTaskBrief) _then;

/// Create a copy of WorkTaskBrief
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? title = null,Object? status = null,Object? priority = null,Object? dueDate = freezed,Object? overdue = null,Object? departmentName = freezed,Object? assigneeName = freezed,}) {
  return _then(WorkTaskBrief(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,dueDate: freezed == dueDate ? _self.dueDate : dueDate // ignore: cast_nullable_to_non_nullable
as String?,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as bool,departmentName: freezed == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String?,assigneeName: freezed == assigneeName ? _self.assigneeName : assigneeName // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [WorkTaskBrief].
extension WorkTaskBriefPatterns on WorkTaskBrief {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WorkTaskBrief value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WorkTaskBrief() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WorkTaskBrief value)  $default,){
final _that = this;
switch (_that) {
case _WorkTaskBrief():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WorkTaskBrief value)?  $default,){
final _that = this;
switch (_that) {
case _WorkTaskBrief() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String title,  String status,  String priority,  String? dueDate,  bool overdue,  String? departmentName,  String? assigneeName)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WorkTaskBrief() when $default != null:
return $default(_that.id,_that.title,_that.status,_that.priority,_that.dueDate,_that.overdue,_that.departmentName,_that.assigneeName);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String title,  String status,  String priority,  String? dueDate,  bool overdue,  String? departmentName,  String? assigneeName)  $default,) {final _that = this;
switch (_that) {
case _WorkTaskBrief():
return $default(_that.id,_that.title,_that.status,_that.priority,_that.dueDate,_that.overdue,_that.departmentName,_that.assigneeName);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String title,  String status,  String priority,  String? dueDate,  bool overdue,  String? departmentName,  String? assigneeName)?  $default,) {final _that = this;
switch (_that) {
case _WorkTaskBrief() when $default != null:
return $default(_that.id,_that.title,_that.status,_that.priority,_that.dueDate,_that.overdue,_that.departmentName,_that.assigneeName);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WorkTaskBrief implements WorkTaskBrief {
  const _WorkTaskBrief({required this.id, required this.title, required this.status, this.priority = 'Medium', this.dueDate, this.overdue = false, this.departmentName, this.assigneeName});
  factory _WorkTaskBrief.fromJson(Map<String, dynamic> json) => _$WorkTaskBriefFromJson(json);

@override final  String id;
@override final  String title;
/// New | InProgress | Review | Done | Cancelled
@override final  String status;
/// Low | Medium | High | Urgent
@override@JsonKey() final  String priority;
@override final  String? dueDate;
@override@JsonKey() final  bool overdue;
@override final  String? departmentName;
@override final  String? assigneeName;

/// Create a copy of WorkTaskBrief
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WorkTaskBriefCopyWith<_WorkTaskBrief> get copyWith => __$WorkTaskBriefCopyWithImpl<_WorkTaskBrief>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WorkTaskBriefToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WorkTaskBrief&&(identical(other.id, id) || other.id == id)&&(identical(other.title, title) || other.title == title)&&(identical(other.status, status) || other.status == status)&&(identical(other.priority, priority) || other.priority == priority)&&(identical(other.dueDate, dueDate) || other.dueDate == dueDate)&&(identical(other.overdue, overdue) || other.overdue == overdue)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&(identical(other.assigneeName, assigneeName) || other.assigneeName == assigneeName));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,title,status,priority,dueDate,overdue,departmentName,assigneeName);
}

@override
String toString() {
    return 'WorkTaskBrief(id: $id, title: $title, status: $status, priority: $priority, dueDate: $dueDate, overdue: $overdue, departmentName: $departmentName, assigneeName: $assigneeName)';
}


}

/// @nodoc
abstract mixin class _$WorkTaskBriefCopyWith<$Res> implements $WorkTaskBriefCopyWith<$Res> {
  factory _$WorkTaskBriefCopyWith(_WorkTaskBrief value, $Res Function(_WorkTaskBrief) _then) = __$WorkTaskBriefCopyWithImpl;
@override @useResult
$Res call({
 String id, String title, String status, String priority, String? dueDate, bool overdue, String? departmentName, String? assigneeName
});




}
/// @nodoc
class __$WorkTaskBriefCopyWithImpl<$Res>
    implements _$WorkTaskBriefCopyWith<$Res> {
  __$WorkTaskBriefCopyWithImpl(this._self, this._then);

  final _WorkTaskBrief _self;
  final $Res Function(_WorkTaskBrief) _then;

/// Create a copy of WorkTaskBrief
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? title = null,Object? status = null,Object? priority = null,Object? dueDate = freezed,Object? overdue = null,Object? departmentName = freezed,Object? assigneeName = freezed,}) {
  return _then(_WorkTaskBrief(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,dueDate: freezed == dueDate ? _self.dueDate : dueDate // ignore: cast_nullable_to_non_nullable
as String?,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as bool,departmentName: freezed == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String?,assigneeName: freezed == assigneeName ? _self.assigneeName : assigneeName // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$WorkOverview {

 int get open; int get inProgress; int get review; int get overdue; int get blocked; int get dueThisWeek; int get doneThisMonth; int get myOpen; int get myOverdue; int get awaitingMyReview; List<WorkDepartment> get departments; List<WorkTaskBrief> get myUpcoming;
/// Create a copy of WorkOverview
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WorkOverviewCopyWith<WorkOverview> get copyWith => _$WorkOverviewCopyWithImpl<WorkOverview>(this as WorkOverview, _$identity);

  /// Serializes this WorkOverview to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WorkOverview;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WorkOverview&&(identical(other.open, _this.open) || other.open == _this.open)&&(identical(other.inProgress, _this.inProgress) || other.inProgress == _this.inProgress)&&(identical(other.review, _this.review) || other.review == _this.review)&&(identical(other.overdue, _this.overdue) || other.overdue == _this.overdue)&&(identical(other.blocked, _this.blocked) || other.blocked == _this.blocked)&&(identical(other.dueThisWeek, _this.dueThisWeek) || other.dueThisWeek == _this.dueThisWeek)&&(identical(other.doneThisMonth, _this.doneThisMonth) || other.doneThisMonth == _this.doneThisMonth)&&(identical(other.myOpen, _this.myOpen) || other.myOpen == _this.myOpen)&&(identical(other.myOverdue, _this.myOverdue) || other.myOverdue == _this.myOverdue)&&(identical(other.awaitingMyReview, _this.awaitingMyReview) || other.awaitingMyReview == _this.awaitingMyReview)&&const DeepCollectionEquality().equals(other.departments, _this.departments)&&const DeepCollectionEquality().equals(other.myUpcoming, _this.myUpcoming));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WorkOverview;
  return Object.hash(runtimeType,_this.open,_this.inProgress,_this.review,_this.overdue,_this.blocked,_this.dueThisWeek,_this.doneThisMonth,_this.myOpen,_this.myOverdue,_this.awaitingMyReview,const DeepCollectionEquality().hash(_this.departments),const DeepCollectionEquality().hash(_this.myUpcoming));
}

@override
String toString() {
  final _this = this as WorkOverview;
  return 'WorkOverview(open: ${_this.open}, inProgress: ${_this.inProgress}, review: ${_this.review}, overdue: ${_this.overdue}, blocked: ${_this.blocked}, dueThisWeek: ${_this.dueThisWeek}, doneThisMonth: ${_this.doneThisMonth}, myOpen: ${_this.myOpen}, myOverdue: ${_this.myOverdue}, awaitingMyReview: ${_this.awaitingMyReview}, departments: ${_this.departments}, myUpcoming: ${_this.myUpcoming})';
}


}

/// @nodoc
abstract mixin class $WorkOverviewCopyWith<$Res>  {
  factory $WorkOverviewCopyWith(WorkOverview value, $Res Function(WorkOverview) _then) = _$WorkOverviewCopyWithImpl;
@useResult
$Res call({
 int open, int inProgress, int review, int overdue, int blocked, int dueThisWeek, int doneThisMonth, int myOpen, int myOverdue, int awaitingMyReview, List<WorkDepartment> departments, List<WorkTaskBrief> myUpcoming
});




}
/// @nodoc
class _$WorkOverviewCopyWithImpl<$Res>
    implements $WorkOverviewCopyWith<$Res> {
  _$WorkOverviewCopyWithImpl(this._self, this._then);

  final WorkOverview _self;
  final $Res Function(WorkOverview) _then;

/// Create a copy of WorkOverview
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? open = null,Object? inProgress = null,Object? review = null,Object? overdue = null,Object? blocked = null,Object? dueThisWeek = null,Object? doneThisMonth = null,Object? myOpen = null,Object? myOverdue = null,Object? awaitingMyReview = null,Object? departments = null,Object? myUpcoming = null,}) {
  return _then(WorkOverview(
open: null == open ? _self.open : open // ignore: cast_nullable_to_non_nullable
as int,inProgress: null == inProgress ? _self.inProgress : inProgress // ignore: cast_nullable_to_non_nullable
as int,review: null == review ? _self.review : review // ignore: cast_nullable_to_non_nullable
as int,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as int,blocked: null == blocked ? _self.blocked : blocked // ignore: cast_nullable_to_non_nullable
as int,dueThisWeek: null == dueThisWeek ? _self.dueThisWeek : dueThisWeek // ignore: cast_nullable_to_non_nullable
as int,doneThisMonth: null == doneThisMonth ? _self.doneThisMonth : doneThisMonth // ignore: cast_nullable_to_non_nullable
as int,myOpen: null == myOpen ? _self.myOpen : myOpen // ignore: cast_nullable_to_non_nullable
as int,myOverdue: null == myOverdue ? _self.myOverdue : myOverdue // ignore: cast_nullable_to_non_nullable
as int,awaitingMyReview: null == awaitingMyReview ? _self.awaitingMyReview : awaitingMyReview // ignore: cast_nullable_to_non_nullable
as int,departments: null == departments ? _self.departments : departments // ignore: cast_nullable_to_non_nullable
as List<WorkDepartment>,myUpcoming: null == myUpcoming ? _self.myUpcoming : myUpcoming // ignore: cast_nullable_to_non_nullable
as List<WorkTaskBrief>,
  ));
}

}


/// Adds pattern-matching-related methods to [WorkOverview].
extension WorkOverviewPatterns on WorkOverview {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WorkOverview value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WorkOverview() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WorkOverview value)  $default,){
final _that = this;
switch (_that) {
case _WorkOverview():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WorkOverview value)?  $default,){
final _that = this;
switch (_that) {
case _WorkOverview() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int open,  int inProgress,  int review,  int overdue,  int blocked,  int dueThisWeek,  int doneThisMonth,  int myOpen,  int myOverdue,  int awaitingMyReview,  List<WorkDepartment> departments,  List<WorkTaskBrief> myUpcoming)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WorkOverview() when $default != null:
return $default(_that.open,_that.inProgress,_that.review,_that.overdue,_that.blocked,_that.dueThisWeek,_that.doneThisMonth,_that.myOpen,_that.myOverdue,_that.awaitingMyReview,_that.departments,_that.myUpcoming);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int open,  int inProgress,  int review,  int overdue,  int blocked,  int dueThisWeek,  int doneThisMonth,  int myOpen,  int myOverdue,  int awaitingMyReview,  List<WorkDepartment> departments,  List<WorkTaskBrief> myUpcoming)  $default,) {final _that = this;
switch (_that) {
case _WorkOverview():
return $default(_that.open,_that.inProgress,_that.review,_that.overdue,_that.blocked,_that.dueThisWeek,_that.doneThisMonth,_that.myOpen,_that.myOverdue,_that.awaitingMyReview,_that.departments,_that.myUpcoming);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int open,  int inProgress,  int review,  int overdue,  int blocked,  int dueThisWeek,  int doneThisMonth,  int myOpen,  int myOverdue,  int awaitingMyReview,  List<WorkDepartment> departments,  List<WorkTaskBrief> myUpcoming)?  $default,) {final _that = this;
switch (_that) {
case _WorkOverview() when $default != null:
return $default(_that.open,_that.inProgress,_that.review,_that.overdue,_that.blocked,_that.dueThisWeek,_that.doneThisMonth,_that.myOpen,_that.myOverdue,_that.awaitingMyReview,_that.departments,_that.myUpcoming);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WorkOverview implements WorkOverview {
  const _WorkOverview({this.open = 0, this.inProgress = 0, this.review = 0, this.overdue = 0, this.blocked = 0, this.dueThisWeek = 0, this.doneThisMonth = 0, this.myOpen = 0, this.myOverdue = 0, this.awaitingMyReview = 0,  List<WorkDepartment> departments = const <WorkDepartment>[],  List<WorkTaskBrief> myUpcoming = const <WorkTaskBrief>[]}): _departments = departments,_myUpcoming = myUpcoming;
  factory _WorkOverview.fromJson(Map<String, dynamic> json) => _$WorkOverviewFromJson(json);

@override@JsonKey() final  int open;
@override@JsonKey() final  int inProgress;
@override@JsonKey() final  int review;
@override@JsonKey() final  int overdue;
@override@JsonKey() final  int blocked;
@override@JsonKey() final  int dueThisWeek;
@override@JsonKey() final  int doneThisMonth;
@override@JsonKey() final  int myOpen;
@override@JsonKey() final  int myOverdue;
@override@JsonKey() final  int awaitingMyReview;
 final  List<WorkDepartment> _departments;
@override@JsonKey() List<WorkDepartment> get departments {
  if (_departments is EqualUnmodifiableListView) return _departments;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_departments);
}

 final  List<WorkTaskBrief> _myUpcoming;
@override@JsonKey() List<WorkTaskBrief> get myUpcoming {
  if (_myUpcoming is EqualUnmodifiableListView) return _myUpcoming;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_myUpcoming);
}


/// Create a copy of WorkOverview
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WorkOverviewCopyWith<_WorkOverview> get copyWith => __$WorkOverviewCopyWithImpl<_WorkOverview>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WorkOverviewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WorkOverview&&(identical(other.open, open) || other.open == open)&&(identical(other.inProgress, inProgress) || other.inProgress == inProgress)&&(identical(other.review, review) || other.review == review)&&(identical(other.overdue, overdue) || other.overdue == overdue)&&(identical(other.blocked, blocked) || other.blocked == blocked)&&(identical(other.dueThisWeek, dueThisWeek) || other.dueThisWeek == dueThisWeek)&&(identical(other.doneThisMonth, doneThisMonth) || other.doneThisMonth == doneThisMonth)&&(identical(other.myOpen, myOpen) || other.myOpen == myOpen)&&(identical(other.myOverdue, myOverdue) || other.myOverdue == myOverdue)&&(identical(other.awaitingMyReview, awaitingMyReview) || other.awaitingMyReview == awaitingMyReview)&&const DeepCollectionEquality().equals(other.departments, _departments)&&const DeepCollectionEquality().equals(other.myUpcoming, _myUpcoming));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,open,inProgress,review,overdue,blocked,dueThisWeek,doneThisMonth,myOpen,myOverdue,awaitingMyReview,const DeepCollectionEquality().hash(_departments),const DeepCollectionEquality().hash(_myUpcoming));
}

@override
String toString() {
    return 'WorkOverview(open: $open, inProgress: $inProgress, review: $review, overdue: $overdue, blocked: $blocked, dueThisWeek: $dueThisWeek, doneThisMonth: $doneThisMonth, myOpen: $myOpen, myOverdue: $myOverdue, awaitingMyReview: $awaitingMyReview, departments: $departments, myUpcoming: $myUpcoming)';
}


}

/// @nodoc
abstract mixin class _$WorkOverviewCopyWith<$Res> implements $WorkOverviewCopyWith<$Res> {
  factory _$WorkOverviewCopyWith(_WorkOverview value, $Res Function(_WorkOverview) _then) = __$WorkOverviewCopyWithImpl;
@override @useResult
$Res call({
 int open, int inProgress, int review, int overdue, int blocked, int dueThisWeek, int doneThisMonth, int myOpen, int myOverdue, int awaitingMyReview, List<WorkDepartment> departments, List<WorkTaskBrief> myUpcoming
});




}
/// @nodoc
class __$WorkOverviewCopyWithImpl<$Res>
    implements _$WorkOverviewCopyWith<$Res> {
  __$WorkOverviewCopyWithImpl(this._self, this._then);

  final _WorkOverview _self;
  final $Res Function(_WorkOverview) _then;

/// Create a copy of WorkOverview
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? open = null,Object? inProgress = null,Object? review = null,Object? overdue = null,Object? blocked = null,Object? dueThisWeek = null,Object? doneThisMonth = null,Object? myOpen = null,Object? myOverdue = null,Object? awaitingMyReview = null,Object? departments = null,Object? myUpcoming = null,}) {
  return _then(_WorkOverview(
open: null == open ? _self.open : open // ignore: cast_nullable_to_non_nullable
as int,inProgress: null == inProgress ? _self.inProgress : inProgress // ignore: cast_nullable_to_non_nullable
as int,review: null == review ? _self.review : review // ignore: cast_nullable_to_non_nullable
as int,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as int,blocked: null == blocked ? _self.blocked : blocked // ignore: cast_nullable_to_non_nullable
as int,dueThisWeek: null == dueThisWeek ? _self.dueThisWeek : dueThisWeek // ignore: cast_nullable_to_non_nullable
as int,doneThisMonth: null == doneThisMonth ? _self.doneThisMonth : doneThisMonth // ignore: cast_nullable_to_non_nullable
as int,myOpen: null == myOpen ? _self.myOpen : myOpen // ignore: cast_nullable_to_non_nullable
as int,myOverdue: null == myOverdue ? _self.myOverdue : myOverdue // ignore: cast_nullable_to_non_nullable
as int,awaitingMyReview: null == awaitingMyReview ? _self.awaitingMyReview : awaitingMyReview // ignore: cast_nullable_to_non_nullable
as int,departments: null == departments ? _self._departments : departments // ignore: cast_nullable_to_non_nullable
as List<WorkDepartment>,myUpcoming: null == myUpcoming ? _self._myUpcoming : myUpcoming // ignore: cast_nullable_to_non_nullable
as List<WorkTaskBrief>,
  ));
}


}


/// @nodoc
mixin _$NotificationItem {

 String get id;/// tasks | hr | ai | system …
 String? get category; String? get kind; String get title; String? get body; String get createdAt; bool get read;/// info | warning | critical
 String? get severity;/// Вид события («task_assigned» …) и ссылка — путь веба.
 String? get type; String? get href;
/// Create a copy of NotificationItem
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$NotificationItemCopyWith<NotificationItem> get copyWith => _$NotificationItemCopyWithImpl<NotificationItem>(this as NotificationItem, _$identity);

  /// Serializes this NotificationItem to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as NotificationItem;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is NotificationItem&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.category, _this.category) || other.category == _this.category)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.body, _this.body) || other.body == _this.body)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt)&&(identical(other.read, _this.read) || other.read == _this.read)&&(identical(other.severity, _this.severity) || other.severity == _this.severity)&&(identical(other.type, _this.type) || other.type == _this.type)&&(identical(other.href, _this.href) || other.href == _this.href));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as NotificationItem;
  return Object.hash(runtimeType,_this.id,_this.category,_this.kind,_this.title,_this.body,_this.createdAt,_this.read,_this.severity,_this.type,_this.href);
}

@override
String toString() {
  final _this = this as NotificationItem;
  return 'NotificationItem(id: ${_this.id}, category: ${_this.category}, kind: ${_this.kind}, title: ${_this.title}, body: ${_this.body}, createdAt: ${_this.createdAt}, read: ${_this.read}, severity: ${_this.severity}, type: ${_this.type}, href: ${_this.href})';
}


}

/// @nodoc
abstract mixin class $NotificationItemCopyWith<$Res>  {
  factory $NotificationItemCopyWith(NotificationItem value, $Res Function(NotificationItem) _then) = _$NotificationItemCopyWithImpl;
@useResult
$Res call({
 String id, String? category, String? kind, String title, String? body, String createdAt, bool read, String? severity, String? type, String? href
});




}
/// @nodoc
class _$NotificationItemCopyWithImpl<$Res>
    implements $NotificationItemCopyWith<$Res> {
  _$NotificationItemCopyWithImpl(this._self, this._then);

  final NotificationItem _self;
  final $Res Function(NotificationItem) _then;

/// Create a copy of NotificationItem
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? category = freezed,Object? kind = freezed,Object? title = null,Object? body = freezed,Object? createdAt = null,Object? read = null,Object? severity = freezed,Object? type = freezed,Object? href = freezed,}) {
  return _then(NotificationItem(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,category: freezed == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String?,kind: freezed == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String?,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,body: freezed == body ? _self.body : body // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,read: null == read ? _self.read : read // ignore: cast_nullable_to_non_nullable
as bool,severity: freezed == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String?,type: freezed == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String?,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [NotificationItem].
extension NotificationItemPatterns on NotificationItem {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _NotificationItem value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _NotificationItem() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _NotificationItem value)  $default,){
final _that = this;
switch (_that) {
case _NotificationItem():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _NotificationItem value)?  $default,){
final _that = this;
switch (_that) {
case _NotificationItem() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String? category,  String? kind,  String title,  String? body,  String createdAt,  bool read,  String? severity,  String? type,  String? href)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _NotificationItem() when $default != null:
return $default(_that.id,_that.category,_that.kind,_that.title,_that.body,_that.createdAt,_that.read,_that.severity,_that.type,_that.href);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String? category,  String? kind,  String title,  String? body,  String createdAt,  bool read,  String? severity,  String? type,  String? href)  $default,) {final _that = this;
switch (_that) {
case _NotificationItem():
return $default(_that.id,_that.category,_that.kind,_that.title,_that.body,_that.createdAt,_that.read,_that.severity,_that.type,_that.href);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String? category,  String? kind,  String title,  String? body,  String createdAt,  bool read,  String? severity,  String? type,  String? href)?  $default,) {final _that = this;
switch (_that) {
case _NotificationItem() when $default != null:
return $default(_that.id,_that.category,_that.kind,_that.title,_that.body,_that.createdAt,_that.read,_that.severity,_that.type,_that.href);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _NotificationItem implements NotificationItem {
  const _NotificationItem({required this.id, this.category, this.kind, required this.title, this.body, required this.createdAt, this.read = false, this.severity, this.type, this.href});
  factory _NotificationItem.fromJson(Map<String, dynamic> json) => _$NotificationItemFromJson(json);

@override final  String id;
/// tasks | hr | ai | system …
@override final  String? category;
@override final  String? kind;
@override final  String title;
@override final  String? body;
@override final  String createdAt;
@override@JsonKey() final  bool read;
/// info | warning | critical
@override final  String? severity;
/// Вид события («task_assigned» …) и ссылка — путь веба.
@override final  String? type;
@override final  String? href;

/// Create a copy of NotificationItem
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$NotificationItemCopyWith<_NotificationItem> get copyWith => __$NotificationItemCopyWithImpl<_NotificationItem>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$NotificationItemToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _NotificationItem&&(identical(other.id, id) || other.id == id)&&(identical(other.category, category) || other.category == category)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.title, title) || other.title == title)&&(identical(other.body, body) || other.body == body)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt)&&(identical(other.read, read) || other.read == read)&&(identical(other.severity, severity) || other.severity == severity)&&(identical(other.type, type) || other.type == type)&&(identical(other.href, href) || other.href == href));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,category,kind,title,body,createdAt,read,severity,type,href);
}

@override
String toString() {
    return 'NotificationItem(id: $id, category: $category, kind: $kind, title: $title, body: $body, createdAt: $createdAt, read: $read, severity: $severity, type: $type, href: $href)';
}


}

/// @nodoc
abstract mixin class _$NotificationItemCopyWith<$Res> implements $NotificationItemCopyWith<$Res> {
  factory _$NotificationItemCopyWith(_NotificationItem value, $Res Function(_NotificationItem) _then) = __$NotificationItemCopyWithImpl;
@override @useResult
$Res call({
 String id, String? category, String? kind, String title, String? body, String createdAt, bool read, String? severity, String? type, String? href
});




}
/// @nodoc
class __$NotificationItemCopyWithImpl<$Res>
    implements _$NotificationItemCopyWith<$Res> {
  __$NotificationItemCopyWithImpl(this._self, this._then);

  final _NotificationItem _self;
  final $Res Function(_NotificationItem) _then;

/// Create a copy of NotificationItem
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? category = freezed,Object? kind = freezed,Object? title = null,Object? body = freezed,Object? createdAt = null,Object? read = null,Object? severity = freezed,Object? type = freezed,Object? href = freezed,}) {
  return _then(_NotificationItem(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,category: freezed == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String?,kind: freezed == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String?,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,body: freezed == body ? _self.body : body // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,read: null == read ? _self.read : read // ignore: cast_nullable_to_non_nullable
as bool,severity: freezed == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String?,type: freezed == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String?,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$NotificationPage {

 List<NotificationItem> get items; int get unread;
/// Create a copy of NotificationPage
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$NotificationPageCopyWith<NotificationPage> get copyWith => _$NotificationPageCopyWithImpl<NotificationPage>(this as NotificationPage, _$identity);

  /// Serializes this NotificationPage to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as NotificationPage;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is NotificationPage&&const DeepCollectionEquality().equals(other.items, _this.items)&&(identical(other.unread, _this.unread) || other.unread == _this.unread));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as NotificationPage;
  return Object.hash(runtimeType,const DeepCollectionEquality().hash(_this.items),_this.unread);
}

@override
String toString() {
  final _this = this as NotificationPage;
  return 'NotificationPage(items: ${_this.items}, unread: ${_this.unread})';
}


}

/// @nodoc
abstract mixin class $NotificationPageCopyWith<$Res>  {
  factory $NotificationPageCopyWith(NotificationPage value, $Res Function(NotificationPage) _then) = _$NotificationPageCopyWithImpl;
@useResult
$Res call({
 List<NotificationItem> items, int unread
});




}
/// @nodoc
class _$NotificationPageCopyWithImpl<$Res>
    implements $NotificationPageCopyWith<$Res> {
  _$NotificationPageCopyWithImpl(this._self, this._then);

  final NotificationPage _self;
  final $Res Function(NotificationPage) _then;

/// Create a copy of NotificationPage
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? items = null,Object? unread = null,}) {
  return _then(NotificationPage(
items: null == items ? _self.items : items // ignore: cast_nullable_to_non_nullable
as List<NotificationItem>,unread: null == unread ? _self.unread : unread // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [NotificationPage].
extension NotificationPagePatterns on NotificationPage {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _NotificationPage value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _NotificationPage() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _NotificationPage value)  $default,){
final _that = this;
switch (_that) {
case _NotificationPage():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _NotificationPage value)?  $default,){
final _that = this;
switch (_that) {
case _NotificationPage() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( List<NotificationItem> items,  int unread)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _NotificationPage() when $default != null:
return $default(_that.items,_that.unread);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( List<NotificationItem> items,  int unread)  $default,) {final _that = this;
switch (_that) {
case _NotificationPage():
return $default(_that.items,_that.unread);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( List<NotificationItem> items,  int unread)?  $default,) {final _that = this;
switch (_that) {
case _NotificationPage() when $default != null:
return $default(_that.items,_that.unread);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _NotificationPage implements NotificationPage {
  const _NotificationPage({ List<NotificationItem> items = const <NotificationItem>[], this.unread = 0}): _items = items;
  factory _NotificationPage.fromJson(Map<String, dynamic> json) => _$NotificationPageFromJson(json);

 final  List<NotificationItem> _items;
@override@JsonKey() List<NotificationItem> get items {
  if (_items is EqualUnmodifiableListView) return _items;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_items);
}

@override@JsonKey() final  int unread;

/// Create a copy of NotificationPage
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$NotificationPageCopyWith<_NotificationPage> get copyWith => __$NotificationPageCopyWithImpl<_NotificationPage>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$NotificationPageToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _NotificationPage&&const DeepCollectionEquality().equals(other.items, _items)&&(identical(other.unread, unread) || other.unread == unread));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,const DeepCollectionEquality().hash(_items),unread);
}

@override
String toString() {
    return 'NotificationPage(items: $items, unread: $unread)';
}


}

/// @nodoc
abstract mixin class _$NotificationPageCopyWith<$Res> implements $NotificationPageCopyWith<$Res> {
  factory _$NotificationPageCopyWith(_NotificationPage value, $Res Function(_NotificationPage) _then) = __$NotificationPageCopyWithImpl;
@override @useResult
$Res call({
 List<NotificationItem> items, int unread
});




}
/// @nodoc
class __$NotificationPageCopyWithImpl<$Res>
    implements _$NotificationPageCopyWith<$Res> {
  __$NotificationPageCopyWithImpl(this._self, this._then);

  final _NotificationPage _self;
  final $Res Function(_NotificationPage) _then;

/// Create a copy of NotificationPage
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? items = null,Object? unread = null,}) {
  return _then(_NotificationPage(
items: null == items ? _self._items : items // ignore: cast_nullable_to_non_nullable
as List<NotificationItem>,unread: null == unread ? _self.unread : unread // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$AlertsSummary {

 int get problems; int get critical; int get recommendations; int get opportunities;
/// Create a copy of AlertsSummary
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AlertsSummaryCopyWith<AlertsSummary> get copyWith => _$AlertsSummaryCopyWithImpl<AlertsSummary>(this as AlertsSummary, _$identity);

  /// Serializes this AlertsSummary to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AlertsSummary;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AlertsSummary&&(identical(other.problems, _this.problems) || other.problems == _this.problems)&&(identical(other.critical, _this.critical) || other.critical == _this.critical)&&(identical(other.recommendations, _this.recommendations) || other.recommendations == _this.recommendations)&&(identical(other.opportunities, _this.opportunities) || other.opportunities == _this.opportunities));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AlertsSummary;
  return Object.hash(runtimeType,_this.problems,_this.critical,_this.recommendations,_this.opportunities);
}

@override
String toString() {
  final _this = this as AlertsSummary;
  return 'AlertsSummary(problems: ${_this.problems}, critical: ${_this.critical}, recommendations: ${_this.recommendations}, opportunities: ${_this.opportunities})';
}


}

/// @nodoc
abstract mixin class $AlertsSummaryCopyWith<$Res>  {
  factory $AlertsSummaryCopyWith(AlertsSummary value, $Res Function(AlertsSummary) _then) = _$AlertsSummaryCopyWithImpl;
@useResult
$Res call({
 int problems, int critical, int recommendations, int opportunities
});




}
/// @nodoc
class _$AlertsSummaryCopyWithImpl<$Res>
    implements $AlertsSummaryCopyWith<$Res> {
  _$AlertsSummaryCopyWithImpl(this._self, this._then);

  final AlertsSummary _self;
  final $Res Function(AlertsSummary) _then;

/// Create a copy of AlertsSummary
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? problems = null,Object? critical = null,Object? recommendations = null,Object? opportunities = null,}) {
  return _then(AlertsSummary(
problems: null == problems ? _self.problems : problems // ignore: cast_nullable_to_non_nullable
as int,critical: null == critical ? _self.critical : critical // ignore: cast_nullable_to_non_nullable
as int,recommendations: null == recommendations ? _self.recommendations : recommendations // ignore: cast_nullable_to_non_nullable
as int,opportunities: null == opportunities ? _self.opportunities : opportunities // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [AlertsSummary].
extension AlertsSummaryPatterns on AlertsSummary {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AlertsSummary value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AlertsSummary() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AlertsSummary value)  $default,){
final _that = this;
switch (_that) {
case _AlertsSummary():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AlertsSummary value)?  $default,){
final _that = this;
switch (_that) {
case _AlertsSummary() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int problems,  int critical,  int recommendations,  int opportunities)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AlertsSummary() when $default != null:
return $default(_that.problems,_that.critical,_that.recommendations,_that.opportunities);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int problems,  int critical,  int recommendations,  int opportunities)  $default,) {final _that = this;
switch (_that) {
case _AlertsSummary():
return $default(_that.problems,_that.critical,_that.recommendations,_that.opportunities);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int problems,  int critical,  int recommendations,  int opportunities)?  $default,) {final _that = this;
switch (_that) {
case _AlertsSummary() when $default != null:
return $default(_that.problems,_that.critical,_that.recommendations,_that.opportunities);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AlertsSummary implements AlertsSummary {
  const _AlertsSummary({this.problems = 0, this.critical = 0, this.recommendations = 0, this.opportunities = 0});
  factory _AlertsSummary.fromJson(Map<String, dynamic> json) => _$AlertsSummaryFromJson(json);

@override@JsonKey() final  int problems;
@override@JsonKey() final  int critical;
@override@JsonKey() final  int recommendations;
@override@JsonKey() final  int opportunities;

/// Create a copy of AlertsSummary
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AlertsSummaryCopyWith<_AlertsSummary> get copyWith => __$AlertsSummaryCopyWithImpl<_AlertsSummary>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AlertsSummaryToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AlertsSummary&&(identical(other.problems, problems) || other.problems == problems)&&(identical(other.critical, critical) || other.critical == critical)&&(identical(other.recommendations, recommendations) || other.recommendations == recommendations)&&(identical(other.opportunities, opportunities) || other.opportunities == opportunities));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,problems,critical,recommendations,opportunities);
}

@override
String toString() {
    return 'AlertsSummary(problems: $problems, critical: $critical, recommendations: $recommendations, opportunities: $opportunities)';
}


}

/// @nodoc
abstract mixin class _$AlertsSummaryCopyWith<$Res> implements $AlertsSummaryCopyWith<$Res> {
  factory _$AlertsSummaryCopyWith(_AlertsSummary value, $Res Function(_AlertsSummary) _then) = __$AlertsSummaryCopyWithImpl;
@override @useResult
$Res call({
 int problems, int critical, int recommendations, int opportunities
});




}
/// @nodoc
class __$AlertsSummaryCopyWithImpl<$Res>
    implements _$AlertsSummaryCopyWith<$Res> {
  __$AlertsSummaryCopyWithImpl(this._self, this._then);

  final _AlertsSummary _self;
  final $Res Function(_AlertsSummary) _then;

/// Create a copy of AlertsSummary
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? problems = null,Object? critical = null,Object? recommendations = null,Object? opportunities = null,}) {
  return _then(_AlertsSummary(
problems: null == problems ? _self.problems : problems // ignore: cast_nullable_to_non_nullable
as int,critical: null == critical ? _self.critical : critical // ignore: cast_nullable_to_non_nullable
as int,recommendations: null == recommendations ? _self.recommendations : recommendations // ignore: cast_nullable_to_non_nullable
as int,opportunities: null == opportunities ? _self.opportunities : opportunities // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$AlertsRun {

 String? get at;
/// Create a copy of AlertsRun
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AlertsRunCopyWith<AlertsRun> get copyWith => _$AlertsRunCopyWithImpl<AlertsRun>(this as AlertsRun, _$identity);

  /// Serializes this AlertsRun to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AlertsRun;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AlertsRun&&(identical(other.at, _this.at) || other.at == _this.at));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AlertsRun;
  return Object.hash(runtimeType,_this.at);
}

@override
String toString() {
  final _this = this as AlertsRun;
  return 'AlertsRun(at: ${_this.at})';
}


}

/// @nodoc
abstract mixin class $AlertsRunCopyWith<$Res>  {
  factory $AlertsRunCopyWith(AlertsRun value, $Res Function(AlertsRun) _then) = _$AlertsRunCopyWithImpl;
@useResult
$Res call({
 String? at
});




}
/// @nodoc
class _$AlertsRunCopyWithImpl<$Res>
    implements $AlertsRunCopyWith<$Res> {
  _$AlertsRunCopyWithImpl(this._self, this._then);

  final AlertsRun _self;
  final $Res Function(AlertsRun) _then;

/// Create a copy of AlertsRun
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? at = freezed,}) {
  return _then(AlertsRun(
at: freezed == at ? _self.at : at // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [AlertsRun].
extension AlertsRunPatterns on AlertsRun {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AlertsRun value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AlertsRun() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AlertsRun value)  $default,){
final _that = this;
switch (_that) {
case _AlertsRun():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AlertsRun value)?  $default,){
final _that = this;
switch (_that) {
case _AlertsRun() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String? at)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AlertsRun() when $default != null:
return $default(_that.at);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String? at)  $default,) {final _that = this;
switch (_that) {
case _AlertsRun():
return $default(_that.at);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String? at)?  $default,) {final _that = this;
switch (_that) {
case _AlertsRun() when $default != null:
return $default(_that.at);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AlertsRun implements AlertsRun {
  const _AlertsRun({this.at});
  factory _AlertsRun.fromJson(Map<String, dynamic> json) => _$AlertsRunFromJson(json);

@override final  String? at;

/// Create a copy of AlertsRun
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AlertsRunCopyWith<_AlertsRun> get copyWith => __$AlertsRunCopyWithImpl<_AlertsRun>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AlertsRunToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AlertsRun&&(identical(other.at, at) || other.at == at));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,at);
}

@override
String toString() {
    return 'AlertsRun(at: $at)';
}


}

/// @nodoc
abstract mixin class _$AlertsRunCopyWith<$Res> implements $AlertsRunCopyWith<$Res> {
  factory _$AlertsRunCopyWith(_AlertsRun value, $Res Function(_AlertsRun) _then) = __$AlertsRunCopyWithImpl;
@override @useResult
$Res call({
 String? at
});




}
/// @nodoc
class __$AlertsRunCopyWithImpl<$Res>
    implements _$AlertsRunCopyWith<$Res> {
  __$AlertsRunCopyWithImpl(this._self, this._then);

  final _AlertsRun _self;
  final $Res Function(_AlertsRun) _then;

/// Create a copy of AlertsRun
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? at = freezed,}) {
  return _then(_AlertsRun(
at: freezed == at ? _self.at : at // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$AiAlert {

 String get id;/// sales | finance | hr | production | supply | marketing
 String get category;/// Critical | Warning | Opportunity
 String get severity; String get title; String? get message; String? get recommendation; String? get detectedAt;/// Правило, которое нашло проблему («sales.region.drop»).
 String? get rule;/// Куда ведёт находка — путь веба («/sales/regions/…»), приложение переводит его в свой экран.
 String? get href;
/// Create a copy of AiAlert
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AiAlertCopyWith<AiAlert> get copyWith => _$AiAlertCopyWithImpl<AiAlert>(this as AiAlert, _$identity);

  /// Serializes this AiAlert to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AiAlert;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AiAlert&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.category, _this.category) || other.category == _this.category)&&(identical(other.severity, _this.severity) || other.severity == _this.severity)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.message, _this.message) || other.message == _this.message)&&(identical(other.recommendation, _this.recommendation) || other.recommendation == _this.recommendation)&&(identical(other.detectedAt, _this.detectedAt) || other.detectedAt == _this.detectedAt)&&(identical(other.rule, _this.rule) || other.rule == _this.rule)&&(identical(other.href, _this.href) || other.href == _this.href));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AiAlert;
  return Object.hash(runtimeType,_this.id,_this.category,_this.severity,_this.title,_this.message,_this.recommendation,_this.detectedAt,_this.rule,_this.href);
}

@override
String toString() {
  final _this = this as AiAlert;
  return 'AiAlert(id: ${_this.id}, category: ${_this.category}, severity: ${_this.severity}, title: ${_this.title}, message: ${_this.message}, recommendation: ${_this.recommendation}, detectedAt: ${_this.detectedAt}, rule: ${_this.rule}, href: ${_this.href})';
}


}

/// @nodoc
abstract mixin class $AiAlertCopyWith<$Res>  {
  factory $AiAlertCopyWith(AiAlert value, $Res Function(AiAlert) _then) = _$AiAlertCopyWithImpl;
@useResult
$Res call({
 String id, String category, String severity, String title, String? message, String? recommendation, String? detectedAt, String? rule, String? href
});




}
/// @nodoc
class _$AiAlertCopyWithImpl<$Res>
    implements $AiAlertCopyWith<$Res> {
  _$AiAlertCopyWithImpl(this._self, this._then);

  final AiAlert _self;
  final $Res Function(AiAlert) _then;

/// Create a copy of AiAlert
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? category = null,Object? severity = null,Object? title = null,Object? message = freezed,Object? recommendation = freezed,Object? detectedAt = freezed,Object? rule = freezed,Object? href = freezed,}) {
  return _then(AiAlert(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,category: null == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,message: freezed == message ? _self.message : message // ignore: cast_nullable_to_non_nullable
as String?,recommendation: freezed == recommendation ? _self.recommendation : recommendation // ignore: cast_nullable_to_non_nullable
as String?,detectedAt: freezed == detectedAt ? _self.detectedAt : detectedAt // ignore: cast_nullable_to_non_nullable
as String?,rule: freezed == rule ? _self.rule : rule // ignore: cast_nullable_to_non_nullable
as String?,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [AiAlert].
extension AiAlertPatterns on AiAlert {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AiAlert value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AiAlert() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AiAlert value)  $default,){
final _that = this;
switch (_that) {
case _AiAlert():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AiAlert value)?  $default,){
final _that = this;
switch (_that) {
case _AiAlert() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String category,  String severity,  String title,  String? message,  String? recommendation,  String? detectedAt,  String? rule,  String? href)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AiAlert() when $default != null:
return $default(_that.id,_that.category,_that.severity,_that.title,_that.message,_that.recommendation,_that.detectedAt,_that.rule,_that.href);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String category,  String severity,  String title,  String? message,  String? recommendation,  String? detectedAt,  String? rule,  String? href)  $default,) {final _that = this;
switch (_that) {
case _AiAlert():
return $default(_that.id,_that.category,_that.severity,_that.title,_that.message,_that.recommendation,_that.detectedAt,_that.rule,_that.href);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String category,  String severity,  String title,  String? message,  String? recommendation,  String? detectedAt,  String? rule,  String? href)?  $default,) {final _that = this;
switch (_that) {
case _AiAlert() when $default != null:
return $default(_that.id,_that.category,_that.severity,_that.title,_that.message,_that.recommendation,_that.detectedAt,_that.rule,_that.href);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AiAlert implements AiAlert {
  const _AiAlert({required this.id, required this.category, required this.severity, required this.title, this.message, this.recommendation, this.detectedAt, this.rule, this.href});
  factory _AiAlert.fromJson(Map<String, dynamic> json) => _$AiAlertFromJson(json);

@override final  String id;
/// sales | finance | hr | production | supply | marketing
@override final  String category;
/// Critical | Warning | Opportunity
@override final  String severity;
@override final  String title;
@override final  String? message;
@override final  String? recommendation;
@override final  String? detectedAt;
/// Правило, которое нашло проблему («sales.region.drop»).
@override final  String? rule;
/// Куда ведёт находка — путь веба («/sales/regions/…»), приложение переводит его в свой экран.
@override final  String? href;

/// Create a copy of AiAlert
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AiAlertCopyWith<_AiAlert> get copyWith => __$AiAlertCopyWithImpl<_AiAlert>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AiAlertToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AiAlert&&(identical(other.id, id) || other.id == id)&&(identical(other.category, category) || other.category == category)&&(identical(other.severity, severity) || other.severity == severity)&&(identical(other.title, title) || other.title == title)&&(identical(other.message, message) || other.message == message)&&(identical(other.recommendation, recommendation) || other.recommendation == recommendation)&&(identical(other.detectedAt, detectedAt) || other.detectedAt == detectedAt)&&(identical(other.rule, rule) || other.rule == rule)&&(identical(other.href, href) || other.href == href));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,category,severity,title,message,recommendation,detectedAt,rule,href);
}

@override
String toString() {
    return 'AiAlert(id: $id, category: $category, severity: $severity, title: $title, message: $message, recommendation: $recommendation, detectedAt: $detectedAt, rule: $rule, href: $href)';
}


}

/// @nodoc
abstract mixin class _$AiAlertCopyWith<$Res> implements $AiAlertCopyWith<$Res> {
  factory _$AiAlertCopyWith(_AiAlert value, $Res Function(_AiAlert) _then) = __$AiAlertCopyWithImpl;
@override @useResult
$Res call({
 String id, String category, String severity, String title, String? message, String? recommendation, String? detectedAt, String? rule, String? href
});




}
/// @nodoc
class __$AiAlertCopyWithImpl<$Res>
    implements _$AiAlertCopyWith<$Res> {
  __$AiAlertCopyWithImpl(this._self, this._then);

  final _AiAlert _self;
  final $Res Function(_AiAlert) _then;

/// Create a copy of AiAlert
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? category = null,Object? severity = null,Object? title = null,Object? message = freezed,Object? recommendation = freezed,Object? detectedAt = freezed,Object? rule = freezed,Object? href = freezed,}) {
  return _then(_AiAlert(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,category: null == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,message: freezed == message ? _self.message : message // ignore: cast_nullable_to_non_nullable
as String?,recommendation: freezed == recommendation ? _self.recommendation : recommendation // ignore: cast_nullable_to_non_nullable
as String?,detectedAt: freezed == detectedAt ? _self.detectedAt : detectedAt // ignore: cast_nullable_to_non_nullable
as String?,rule: freezed == rule ? _self.rule : rule // ignore: cast_nullable_to_non_nullable
as String?,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$AiAlerts {

 AlertsSummary get summary; AlertsRun? get lastRun; List<AiAlert> get alerts;
/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AiAlertsCopyWith<AiAlerts> get copyWith => _$AiAlertsCopyWithImpl<AiAlerts>(this as AiAlerts, _$identity);

  /// Serializes this AiAlerts to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AiAlerts;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AiAlerts&&(identical(other.summary, _this.summary) || other.summary == _this.summary)&&(identical(other.lastRun, _this.lastRun) || other.lastRun == _this.lastRun)&&const DeepCollectionEquality().equals(other.alerts, _this.alerts));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AiAlerts;
  return Object.hash(runtimeType,_this.summary,_this.lastRun,const DeepCollectionEquality().hash(_this.alerts));
}

@override
String toString() {
  final _this = this as AiAlerts;
  return 'AiAlerts(summary: ${_this.summary}, lastRun: ${_this.lastRun}, alerts: ${_this.alerts})';
}


}

/// @nodoc
abstract mixin class $AiAlertsCopyWith<$Res>  {
  factory $AiAlertsCopyWith(AiAlerts value, $Res Function(AiAlerts) _then) = _$AiAlertsCopyWithImpl;
@useResult
$Res call({
 AlertsSummary summary, AlertsRun? lastRun, List<AiAlert> alerts
});


$AlertsSummaryCopyWith<$Res> get summary;$AlertsRunCopyWith<$Res>? get lastRun;

}
/// @nodoc
class _$AiAlertsCopyWithImpl<$Res>
    implements $AiAlertsCopyWith<$Res> {
  _$AiAlertsCopyWithImpl(this._self, this._then);

  final AiAlerts _self;
  final $Res Function(AiAlerts) _then;

/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? summary = null,Object? lastRun = freezed,Object? alerts = null,}) {
  return _then(AiAlerts(
summary: null == summary ? _self.summary : summary // ignore: cast_nullable_to_non_nullable
as AlertsSummary,lastRun: freezed == lastRun ? _self.lastRun : lastRun // ignore: cast_nullable_to_non_nullable
as AlertsRun?,alerts: null == alerts ? _self.alerts : alerts // ignore: cast_nullable_to_non_nullable
as List<AiAlert>,
  ));
}
/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AlertsSummaryCopyWith<$Res> get summary {
  
  return $AlertsSummaryCopyWith<$Res>(_self.summary, (value) {
    return _then(_self.copyWith(summary: value));
  });
}/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AlertsRunCopyWith<$Res>? get lastRun {
    if (_self.lastRun == null) {
    return null;
  }

  return $AlertsRunCopyWith<$Res>(_self.lastRun!, (value) {
    return _then(_self.copyWith(lastRun: value));
  });
}
}


/// Adds pattern-matching-related methods to [AiAlerts].
extension AiAlertsPatterns on AiAlerts {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AiAlerts value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AiAlerts() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AiAlerts value)  $default,){
final _that = this;
switch (_that) {
case _AiAlerts():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AiAlerts value)?  $default,){
final _that = this;
switch (_that) {
case _AiAlerts() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( AlertsSummary summary,  AlertsRun? lastRun,  List<AiAlert> alerts)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AiAlerts() when $default != null:
return $default(_that.summary,_that.lastRun,_that.alerts);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( AlertsSummary summary,  AlertsRun? lastRun,  List<AiAlert> alerts)  $default,) {final _that = this;
switch (_that) {
case _AiAlerts():
return $default(_that.summary,_that.lastRun,_that.alerts);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( AlertsSummary summary,  AlertsRun? lastRun,  List<AiAlert> alerts)?  $default,) {final _that = this;
switch (_that) {
case _AiAlerts() when $default != null:
return $default(_that.summary,_that.lastRun,_that.alerts);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AiAlerts implements AiAlerts {
  const _AiAlerts({this.summary = const AlertsSummary(), this.lastRun,  List<AiAlert> alerts = const <AiAlert>[]}): _alerts = alerts;
  factory _AiAlerts.fromJson(Map<String, dynamic> json) => _$AiAlertsFromJson(json);

@override@JsonKey() final  AlertsSummary summary;
@override final  AlertsRun? lastRun;
 final  List<AiAlert> _alerts;
@override@JsonKey() List<AiAlert> get alerts {
  if (_alerts is EqualUnmodifiableListView) return _alerts;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_alerts);
}


/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AiAlertsCopyWith<_AiAlerts> get copyWith => __$AiAlertsCopyWithImpl<_AiAlerts>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AiAlertsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AiAlerts&&(identical(other.summary, summary) || other.summary == summary)&&(identical(other.lastRun, lastRun) || other.lastRun == lastRun)&&const DeepCollectionEquality().equals(other.alerts, _alerts));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,summary,lastRun,const DeepCollectionEquality().hash(_alerts));
}

@override
String toString() {
    return 'AiAlerts(summary: $summary, lastRun: $lastRun, alerts: $alerts)';
}


}

/// @nodoc
abstract mixin class _$AiAlertsCopyWith<$Res> implements $AiAlertsCopyWith<$Res> {
  factory _$AiAlertsCopyWith(_AiAlerts value, $Res Function(_AiAlerts) _then) = __$AiAlertsCopyWithImpl;
@override @useResult
$Res call({
 AlertsSummary summary, AlertsRun? lastRun, List<AiAlert> alerts
});


@override $AlertsSummaryCopyWith<$Res> get summary;@override $AlertsRunCopyWith<$Res>? get lastRun;

}
/// @nodoc
class __$AiAlertsCopyWithImpl<$Res>
    implements _$AiAlertsCopyWith<$Res> {
  __$AiAlertsCopyWithImpl(this._self, this._then);

  final _AiAlerts _self;
  final $Res Function(_AiAlerts) _then;

/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? summary = null,Object? lastRun = freezed,Object? alerts = null,}) {
  return _then(_AiAlerts(
summary: null == summary ? _self.summary : summary // ignore: cast_nullable_to_non_nullable
as AlertsSummary,lastRun: freezed == lastRun ? _self.lastRun : lastRun // ignore: cast_nullable_to_non_nullable
as AlertsRun?,alerts: null == alerts ? _self._alerts : alerts // ignore: cast_nullable_to_non_nullable
as List<AiAlert>,
  ));
}

/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AlertsSummaryCopyWith<$Res> get summary {
  
  return $AlertsSummaryCopyWith<$Res>(_self.summary, (value) {
    return _then(_self.copyWith(summary: value));
  });
}/// Create a copy of AiAlerts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AlertsRunCopyWith<$Res>? get lastRun {
    if (_self.lastRun == null) {
    return null;
  }

  return $AlertsRunCopyWith<$Res>(_self.lastRun!, (value) {
    return _then(_self.copyWith(lastRun: value));
  });
}
}

// dart format on
