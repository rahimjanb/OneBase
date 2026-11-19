// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'analytik_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$AnalytikPeriod {

 String get kind; String get from; String get to; String? get previousFrom; String? get previousTo; String get label; String get previousLabel; int get days;
/// Create a copy of AnalytikPeriod
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AnalytikPeriodCopyWith<AnalytikPeriod> get copyWith => _$AnalytikPeriodCopyWithImpl<AnalytikPeriod>(this as AnalytikPeriod, _$identity);

  /// Serializes this AnalytikPeriod to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AnalytikPeriod;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AnalytikPeriod&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.from, _this.from) || other.from == _this.from)&&(identical(other.to, _this.to) || other.to == _this.to)&&(identical(other.previousFrom, _this.previousFrom) || other.previousFrom == _this.previousFrom)&&(identical(other.previousTo, _this.previousTo) || other.previousTo == _this.previousTo)&&(identical(other.label, _this.label) || other.label == _this.label)&&(identical(other.previousLabel, _this.previousLabel) || other.previousLabel == _this.previousLabel)&&(identical(other.days, _this.days) || other.days == _this.days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AnalytikPeriod;
  return Object.hash(runtimeType,_this.kind,_this.from,_this.to,_this.previousFrom,_this.previousTo,_this.label,_this.previousLabel,_this.days);
}

@override
String toString() {
  final _this = this as AnalytikPeriod;
  return 'AnalytikPeriod(kind: ${_this.kind}, from: ${_this.from}, to: ${_this.to}, previousFrom: ${_this.previousFrom}, previousTo: ${_this.previousTo}, label: ${_this.label}, previousLabel: ${_this.previousLabel}, days: ${_this.days})';
}


}

/// @nodoc
abstract mixin class $AnalytikPeriodCopyWith<$Res>  {
  factory $AnalytikPeriodCopyWith(AnalytikPeriod value, $Res Function(AnalytikPeriod) _then) = _$AnalytikPeriodCopyWithImpl;
@useResult
$Res call({
 String kind, String from, String to, String? previousFrom, String? previousTo, String label, String previousLabel, int days
});




}
/// @nodoc
class _$AnalytikPeriodCopyWithImpl<$Res>
    implements $AnalytikPeriodCopyWith<$Res> {
  _$AnalytikPeriodCopyWithImpl(this._self, this._then);

  final AnalytikPeriod _self;
  final $Res Function(AnalytikPeriod) _then;

/// Create a copy of AnalytikPeriod
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? kind = null,Object? from = null,Object? to = null,Object? previousFrom = freezed,Object? previousTo = freezed,Object? label = null,Object? previousLabel = null,Object? days = null,}) {
  return _then(AnalytikPeriod(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,from: null == from ? _self.from : from // ignore: cast_nullable_to_non_nullable
as String,to: null == to ? _self.to : to // ignore: cast_nullable_to_non_nullable
as String,previousFrom: freezed == previousFrom ? _self.previousFrom : previousFrom // ignore: cast_nullable_to_non_nullable
as String?,previousTo: freezed == previousTo ? _self.previousTo : previousTo // ignore: cast_nullable_to_non_nullable
as String?,label: null == label ? _self.label : label // ignore: cast_nullable_to_non_nullable
as String,previousLabel: null == previousLabel ? _self.previousLabel : previousLabel // ignore: cast_nullable_to_non_nullable
as String,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [AnalytikPeriod].
extension AnalytikPeriodPatterns on AnalytikPeriod {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AnalytikPeriod value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AnalytikPeriod() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AnalytikPeriod value)  $default,){
final _that = this;
switch (_that) {
case _AnalytikPeriod():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AnalytikPeriod value)?  $default,){
final _that = this;
switch (_that) {
case _AnalytikPeriod() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String kind,  String from,  String to,  String? previousFrom,  String? previousTo,  String label,  String previousLabel,  int days)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AnalytikPeriod() when $default != null:
return $default(_that.kind,_that.from,_that.to,_that.previousFrom,_that.previousTo,_that.label,_that.previousLabel,_that.days);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String kind,  String from,  String to,  String? previousFrom,  String? previousTo,  String label,  String previousLabel,  int days)  $default,) {final _that = this;
switch (_that) {
case _AnalytikPeriod():
return $default(_that.kind,_that.from,_that.to,_that.previousFrom,_that.previousTo,_that.label,_that.previousLabel,_that.days);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String kind,  String from,  String to,  String? previousFrom,  String? previousTo,  String label,  String previousLabel,  int days)?  $default,) {final _that = this;
switch (_that) {
case _AnalytikPeriod() when $default != null:
return $default(_that.kind,_that.from,_that.to,_that.previousFrom,_that.previousTo,_that.label,_that.previousLabel,_that.days);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AnalytikPeriod implements AnalytikPeriod {
  const _AnalytikPeriod({required this.kind, required this.from, required this.to, this.previousFrom, this.previousTo, this.label = '', this.previousLabel = '', this.days = 0});
  factory _AnalytikPeriod.fromJson(Map<String, dynamic> json) => _$AnalytikPeriodFromJson(json);

@override final  String kind;
@override final  String from;
@override final  String to;
@override final  String? previousFrom;
@override final  String? previousTo;
@override@JsonKey() final  String label;
@override@JsonKey() final  String previousLabel;
@override@JsonKey() final  int days;

/// Create a copy of AnalytikPeriod
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AnalytikPeriodCopyWith<_AnalytikPeriod> get copyWith => __$AnalytikPeriodCopyWithImpl<_AnalytikPeriod>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AnalytikPeriodToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AnalytikPeriod&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.from, from) || other.from == from)&&(identical(other.to, to) || other.to == to)&&(identical(other.previousFrom, previousFrom) || other.previousFrom == previousFrom)&&(identical(other.previousTo, previousTo) || other.previousTo == previousTo)&&(identical(other.label, label) || other.label == label)&&(identical(other.previousLabel, previousLabel) || other.previousLabel == previousLabel)&&(identical(other.days, days) || other.days == days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,kind,from,to,previousFrom,previousTo,label,previousLabel,days);
}

@override
String toString() {
    return 'AnalytikPeriod(kind: $kind, from: $from, to: $to, previousFrom: $previousFrom, previousTo: $previousTo, label: $label, previousLabel: $previousLabel, days: $days)';
}


}

/// @nodoc
abstract mixin class _$AnalytikPeriodCopyWith<$Res> implements $AnalytikPeriodCopyWith<$Res> {
  factory _$AnalytikPeriodCopyWith(_AnalytikPeriod value, $Res Function(_AnalytikPeriod) _then) = __$AnalytikPeriodCopyWithImpl;
@override @useResult
$Res call({
 String kind, String from, String to, String? previousFrom, String? previousTo, String label, String previousLabel, int days
});




}
/// @nodoc
class __$AnalytikPeriodCopyWithImpl<$Res>
    implements _$AnalytikPeriodCopyWith<$Res> {
  __$AnalytikPeriodCopyWithImpl(this._self, this._then);

  final _AnalytikPeriod _self;
  final $Res Function(_AnalytikPeriod) _then;

/// Create a copy of AnalytikPeriod
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? kind = null,Object? from = null,Object? to = null,Object? previousFrom = freezed,Object? previousTo = freezed,Object? label = null,Object? previousLabel = null,Object? days = null,}) {
  return _then(_AnalytikPeriod(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,from: null == from ? _self.from : from // ignore: cast_nullable_to_non_nullable
as String,to: null == to ? _self.to : to // ignore: cast_nullable_to_non_nullable
as String,previousFrom: freezed == previousFrom ? _self.previousFrom : previousFrom // ignore: cast_nullable_to_non_nullable
as String?,previousTo: freezed == previousTo ? _self.previousTo : previousTo // ignore: cast_nullable_to_non_nullable
as String?,label: null == label ? _self.label : label // ignore: cast_nullable_to_non_nullable
as String,previousLabel: null == previousLabel ? _self.previousLabel : previousLabel // ignore: cast_nullable_to_non_nullable
as String,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$AnalytikKpi {

 String get code; String get title; double? get value; String get unit; double? get previous;/// Изменение в процентах (уже ×100), считает сервер.
 double? get changePercent;/// fact | calculated | forecast
 String get kind; String? get href; String? get note;
/// Create a copy of AnalytikKpi
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AnalytikKpiCopyWith<AnalytikKpi> get copyWith => _$AnalytikKpiCopyWithImpl<AnalytikKpi>(this as AnalytikKpi, _$identity);

  /// Serializes this AnalytikKpi to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AnalytikKpi;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AnalytikKpi&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.value, _this.value) || other.value == _this.value)&&(identical(other.unit, _this.unit) || other.unit == _this.unit)&&(identical(other.previous, _this.previous) || other.previous == _this.previous)&&(identical(other.changePercent, _this.changePercent) || other.changePercent == _this.changePercent)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.href, _this.href) || other.href == _this.href)&&(identical(other.note, _this.note) || other.note == _this.note));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AnalytikKpi;
  return Object.hash(runtimeType,_this.code,_this.title,_this.value,_this.unit,_this.previous,_this.changePercent,_this.kind,_this.href,_this.note);
}

@override
String toString() {
  final _this = this as AnalytikKpi;
  return 'AnalytikKpi(code: ${_this.code}, title: ${_this.title}, value: ${_this.value}, unit: ${_this.unit}, previous: ${_this.previous}, changePercent: ${_this.changePercent}, kind: ${_this.kind}, href: ${_this.href}, note: ${_this.note})';
}


}

/// @nodoc
abstract mixin class $AnalytikKpiCopyWith<$Res>  {
  factory $AnalytikKpiCopyWith(AnalytikKpi value, $Res Function(AnalytikKpi) _then) = _$AnalytikKpiCopyWithImpl;
@useResult
$Res call({
 String code, String title, double? value, String unit, double? previous, double? changePercent, String kind, String? href, String? note
});




}
/// @nodoc
class _$AnalytikKpiCopyWithImpl<$Res>
    implements $AnalytikKpiCopyWith<$Res> {
  _$AnalytikKpiCopyWithImpl(this._self, this._then);

  final AnalytikKpi _self;
  final $Res Function(AnalytikKpi) _then;

/// Create a copy of AnalytikKpi
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? code = null,Object? title = null,Object? value = freezed,Object? unit = null,Object? previous = freezed,Object? changePercent = freezed,Object? kind = null,Object? href = freezed,Object? note = freezed,}) {
  return _then(AnalytikKpi(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,value: freezed == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double?,unit: null == unit ? _self.unit : unit // ignore: cast_nullable_to_non_nullable
as String,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,changePercent: freezed == changePercent ? _self.changePercent : changePercent // ignore: cast_nullable_to_non_nullable
as double?,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,note: freezed == note ? _self.note : note // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [AnalytikKpi].
extension AnalytikKpiPatterns on AnalytikKpi {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AnalytikKpi value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AnalytikKpi() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AnalytikKpi value)  $default,){
final _that = this;
switch (_that) {
case _AnalytikKpi():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AnalytikKpi value)?  $default,){
final _that = this;
switch (_that) {
case _AnalytikKpi() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String code,  String title,  double? value,  String unit,  double? previous,  double? changePercent,  String kind,  String? href,  String? note)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AnalytikKpi() when $default != null:
return $default(_that.code,_that.title,_that.value,_that.unit,_that.previous,_that.changePercent,_that.kind,_that.href,_that.note);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String code,  String title,  double? value,  String unit,  double? previous,  double? changePercent,  String kind,  String? href,  String? note)  $default,) {final _that = this;
switch (_that) {
case _AnalytikKpi():
return $default(_that.code,_that.title,_that.value,_that.unit,_that.previous,_that.changePercent,_that.kind,_that.href,_that.note);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String code,  String title,  double? value,  String unit,  double? previous,  double? changePercent,  String kind,  String? href,  String? note)?  $default,) {final _that = this;
switch (_that) {
case _AnalytikKpi() when $default != null:
return $default(_that.code,_that.title,_that.value,_that.unit,_that.previous,_that.changePercent,_that.kind,_that.href,_that.note);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AnalytikKpi implements AnalytikKpi {
  const _AnalytikKpi({required this.code, required this.title, this.value, this.unit = '', this.previous, this.changePercent, this.kind = 'fact', this.href, this.note});
  factory _AnalytikKpi.fromJson(Map<String, dynamic> json) => _$AnalytikKpiFromJson(json);

@override final  String code;
@override final  String title;
@override final  double? value;
@override@JsonKey() final  String unit;
@override final  double? previous;
/// Изменение в процентах (уже ×100), считает сервер.
@override final  double? changePercent;
/// fact | calculated | forecast
@override@JsonKey() final  String kind;
@override final  String? href;
@override final  String? note;

/// Create a copy of AnalytikKpi
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AnalytikKpiCopyWith<_AnalytikKpi> get copyWith => __$AnalytikKpiCopyWithImpl<_AnalytikKpi>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AnalytikKpiToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AnalytikKpi&&(identical(other.code, code) || other.code == code)&&(identical(other.title, title) || other.title == title)&&(identical(other.value, value) || other.value == value)&&(identical(other.unit, unit) || other.unit == unit)&&(identical(other.previous, previous) || other.previous == previous)&&(identical(other.changePercent, changePercent) || other.changePercent == changePercent)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.href, href) || other.href == href)&&(identical(other.note, note) || other.note == note));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,code,title,value,unit,previous,changePercent,kind,href,note);
}

@override
String toString() {
    return 'AnalytikKpi(code: $code, title: $title, value: $value, unit: $unit, previous: $previous, changePercent: $changePercent, kind: $kind, href: $href, note: $note)';
}


}

/// @nodoc
abstract mixin class _$AnalytikKpiCopyWith<$Res> implements $AnalytikKpiCopyWith<$Res> {
  factory _$AnalytikKpiCopyWith(_AnalytikKpi value, $Res Function(_AnalytikKpi) _then) = __$AnalytikKpiCopyWithImpl;
@override @useResult
$Res call({
 String code, String title, double? value, String unit, double? previous, double? changePercent, String kind, String? href, String? note
});




}
/// @nodoc
class __$AnalytikKpiCopyWithImpl<$Res>
    implements _$AnalytikKpiCopyWith<$Res> {
  __$AnalytikKpiCopyWithImpl(this._self, this._then);

  final _AnalytikKpi _self;
  final $Res Function(_AnalytikKpi) _then;

/// Create a copy of AnalytikKpi
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? code = null,Object? title = null,Object? value = freezed,Object? unit = null,Object? previous = freezed,Object? changePercent = freezed,Object? kind = null,Object? href = freezed,Object? note = freezed,}) {
  return _then(_AnalytikKpi(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,value: freezed == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double?,unit: null == unit ? _self.unit : unit // ignore: cast_nullable_to_non_nullable
as String,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,changePercent: freezed == changePercent ? _self.changePercent : changePercent // ignore: cast_nullable_to_non_nullable
as double?,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,note: freezed == note ? _self.note : note // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$AnalytikBlock {

 AnalytikPeriod get period; List<AnalytikKpi> get kpis; bool get complete; String? get dataThrough; String? get scope; List<String> get notes; List<String> get sources;
/// Create a copy of AnalytikBlock
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AnalytikBlockCopyWith<AnalytikBlock> get copyWith => _$AnalytikBlockCopyWithImpl<AnalytikBlock>(this as AnalytikBlock, _$identity);

  /// Serializes this AnalytikBlock to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AnalytikBlock;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AnalytikBlock&&(identical(other.period, _this.period) || other.period == _this.period)&&const DeepCollectionEquality().equals(other.kpis, _this.kpis)&&(identical(other.complete, _this.complete) || other.complete == _this.complete)&&(identical(other.dataThrough, _this.dataThrough) || other.dataThrough == _this.dataThrough)&&(identical(other.scope, _this.scope) || other.scope == _this.scope)&&const DeepCollectionEquality().equals(other.notes, _this.notes)&&const DeepCollectionEquality().equals(other.sources, _this.sources));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AnalytikBlock;
  return Object.hash(runtimeType,_this.period,const DeepCollectionEquality().hash(_this.kpis),_this.complete,_this.dataThrough,_this.scope,const DeepCollectionEquality().hash(_this.notes),const DeepCollectionEquality().hash(_this.sources));
}

@override
String toString() {
  final _this = this as AnalytikBlock;
  return 'AnalytikBlock(period: ${_this.period}, kpis: ${_this.kpis}, complete: ${_this.complete}, dataThrough: ${_this.dataThrough}, scope: ${_this.scope}, notes: ${_this.notes}, sources: ${_this.sources})';
}


}

/// @nodoc
abstract mixin class $AnalytikBlockCopyWith<$Res>  {
  factory $AnalytikBlockCopyWith(AnalytikBlock value, $Res Function(AnalytikBlock) _then) = _$AnalytikBlockCopyWithImpl;
@useResult
$Res call({
 AnalytikPeriod period, List<AnalytikKpi> kpis, bool complete, String? dataThrough, String? scope, List<String> notes, List<String> sources
});


$AnalytikPeriodCopyWith<$Res> get period;

}
/// @nodoc
class _$AnalytikBlockCopyWithImpl<$Res>
    implements $AnalytikBlockCopyWith<$Res> {
  _$AnalytikBlockCopyWithImpl(this._self, this._then);

  final AnalytikBlock _self;
  final $Res Function(AnalytikBlock) _then;

/// Create a copy of AnalytikBlock
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? period = null,Object? kpis = null,Object? complete = null,Object? dataThrough = freezed,Object? scope = freezed,Object? notes = null,Object? sources = null,}) {
  return _then(AnalytikBlock(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as AnalytikPeriod,kpis: null == kpis ? _self.kpis : kpis // ignore: cast_nullable_to_non_nullable
as List<AnalytikKpi>,complete: null == complete ? _self.complete : complete // ignore: cast_nullable_to_non_nullable
as bool,dataThrough: freezed == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String?,scope: freezed == scope ? _self.scope : scope // ignore: cast_nullable_to_non_nullable
as String?,notes: null == notes ? _self.notes : notes // ignore: cast_nullable_to_non_nullable
as List<String>,sources: null == sources ? _self.sources : sources // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}
/// Create a copy of AnalytikBlock
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AnalytikPeriodCopyWith<$Res> get period {
  
  return $AnalytikPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}
}


/// Adds pattern-matching-related methods to [AnalytikBlock].
extension AnalytikBlockPatterns on AnalytikBlock {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AnalytikBlock value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AnalytikBlock() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AnalytikBlock value)  $default,){
final _that = this;
switch (_that) {
case _AnalytikBlock():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AnalytikBlock value)?  $default,){
final _that = this;
switch (_that) {
case _AnalytikBlock() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( AnalytikPeriod period,  List<AnalytikKpi> kpis,  bool complete,  String? dataThrough,  String? scope,  List<String> notes,  List<String> sources)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AnalytikBlock() when $default != null:
return $default(_that.period,_that.kpis,_that.complete,_that.dataThrough,_that.scope,_that.notes,_that.sources);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( AnalytikPeriod period,  List<AnalytikKpi> kpis,  bool complete,  String? dataThrough,  String? scope,  List<String> notes,  List<String> sources)  $default,) {final _that = this;
switch (_that) {
case _AnalytikBlock():
return $default(_that.period,_that.kpis,_that.complete,_that.dataThrough,_that.scope,_that.notes,_that.sources);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( AnalytikPeriod period,  List<AnalytikKpi> kpis,  bool complete,  String? dataThrough,  String? scope,  List<String> notes,  List<String> sources)?  $default,) {final _that = this;
switch (_that) {
case _AnalytikBlock() when $default != null:
return $default(_that.period,_that.kpis,_that.complete,_that.dataThrough,_that.scope,_that.notes,_that.sources);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AnalytikBlock implements AnalytikBlock {
  const _AnalytikBlock({required this.period,  List<AnalytikKpi> kpis = const <AnalytikKpi>[], this.complete = true, this.dataThrough, this.scope,  List<String> notes = const <String>[],  List<String> sources = const <String>[]}): _kpis = kpis,_notes = notes,_sources = sources;
  factory _AnalytikBlock.fromJson(Map<String, dynamic> json) => _$AnalytikBlockFromJson(json);

@override final  AnalytikPeriod period;
 final  List<AnalytikKpi> _kpis;
@override@JsonKey() List<AnalytikKpi> get kpis {
  if (_kpis is EqualUnmodifiableListView) return _kpis;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_kpis);
}

@override@JsonKey() final  bool complete;
@override final  String? dataThrough;
@override final  String? scope;
 final  List<String> _notes;
@override@JsonKey() List<String> get notes {
  if (_notes is EqualUnmodifiableListView) return _notes;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_notes);
}

 final  List<String> _sources;
@override@JsonKey() List<String> get sources {
  if (_sources is EqualUnmodifiableListView) return _sources;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_sources);
}


/// Create a copy of AnalytikBlock
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AnalytikBlockCopyWith<_AnalytikBlock> get copyWith => __$AnalytikBlockCopyWithImpl<_AnalytikBlock>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AnalytikBlockToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AnalytikBlock&&(identical(other.period, period) || other.period == period)&&const DeepCollectionEquality().equals(other.kpis, _kpis)&&(identical(other.complete, complete) || other.complete == complete)&&(identical(other.dataThrough, dataThrough) || other.dataThrough == dataThrough)&&(identical(other.scope, scope) || other.scope == scope)&&const DeepCollectionEquality().equals(other.notes, _notes)&&const DeepCollectionEquality().equals(other.sources, _sources));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,period,const DeepCollectionEquality().hash(_kpis),complete,dataThrough,scope,const DeepCollectionEquality().hash(_notes),const DeepCollectionEquality().hash(_sources));
}

@override
String toString() {
    return 'AnalytikBlock(period: $period, kpis: $kpis, complete: $complete, dataThrough: $dataThrough, scope: $scope, notes: $notes, sources: $sources)';
}


}

/// @nodoc
abstract mixin class _$AnalytikBlockCopyWith<$Res> implements $AnalytikBlockCopyWith<$Res> {
  factory _$AnalytikBlockCopyWith(_AnalytikBlock value, $Res Function(_AnalytikBlock) _then) = __$AnalytikBlockCopyWithImpl;
@override @useResult
$Res call({
 AnalytikPeriod period, List<AnalytikKpi> kpis, bool complete, String? dataThrough, String? scope, List<String> notes, List<String> sources
});


@override $AnalytikPeriodCopyWith<$Res> get period;

}
/// @nodoc
class __$AnalytikBlockCopyWithImpl<$Res>
    implements _$AnalytikBlockCopyWith<$Res> {
  __$AnalytikBlockCopyWithImpl(this._self, this._then);

  final _AnalytikBlock _self;
  final $Res Function(_AnalytikBlock) _then;

/// Create a copy of AnalytikBlock
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? period = null,Object? kpis = null,Object? complete = null,Object? dataThrough = freezed,Object? scope = freezed,Object? notes = null,Object? sources = null,}) {
  return _then(_AnalytikBlock(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as AnalytikPeriod,kpis: null == kpis ? _self._kpis : kpis // ignore: cast_nullable_to_non_nullable
as List<AnalytikKpi>,complete: null == complete ? _self.complete : complete // ignore: cast_nullable_to_non_nullable
as bool,dataThrough: freezed == dataThrough ? _self.dataThrough : dataThrough // ignore: cast_nullable_to_non_nullable
as String?,scope: freezed == scope ? _self.scope : scope // ignore: cast_nullable_to_non_nullable
as String?,notes: null == notes ? _self._notes : notes // ignore: cast_nullable_to_non_nullable
as List<String>,sources: null == sources ? _self._sources : sources // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}

/// Create a copy of AnalytikBlock
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AnalytikPeriodCopyWith<$Res> get period {
  
  return $AnalytikPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}
}


/// @nodoc
mixin _$AnalytikNav {

 int get critical; int get warning; int get info; int get total;
/// Create a copy of AnalytikNav
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AnalytikNavCopyWith<AnalytikNav> get copyWith => _$AnalytikNavCopyWithImpl<AnalytikNav>(this as AnalytikNav, _$identity);

  /// Serializes this AnalytikNav to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AnalytikNav;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AnalytikNav&&(identical(other.critical, _this.critical) || other.critical == _this.critical)&&(identical(other.warning, _this.warning) || other.warning == _this.warning)&&(identical(other.info, _this.info) || other.info == _this.info)&&(identical(other.total, _this.total) || other.total == _this.total));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AnalytikNav;
  return Object.hash(runtimeType,_this.critical,_this.warning,_this.info,_this.total);
}

@override
String toString() {
  final _this = this as AnalytikNav;
  return 'AnalytikNav(critical: ${_this.critical}, warning: ${_this.warning}, info: ${_this.info}, total: ${_this.total})';
}


}

/// @nodoc
abstract mixin class $AnalytikNavCopyWith<$Res>  {
  factory $AnalytikNavCopyWith(AnalytikNav value, $Res Function(AnalytikNav) _then) = _$AnalytikNavCopyWithImpl;
@useResult
$Res call({
 int critical, int warning, int info, int total
});




}
/// @nodoc
class _$AnalytikNavCopyWithImpl<$Res>
    implements $AnalytikNavCopyWith<$Res> {
  _$AnalytikNavCopyWithImpl(this._self, this._then);

  final AnalytikNav _self;
  final $Res Function(AnalytikNav) _then;

/// Create a copy of AnalytikNav
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? critical = null,Object? warning = null,Object? info = null,Object? total = null,}) {
  return _then(AnalytikNav(
critical: null == critical ? _self.critical : critical // ignore: cast_nullable_to_non_nullable
as int,warning: null == warning ? _self.warning : warning // ignore: cast_nullable_to_non_nullable
as int,info: null == info ? _self.info : info // ignore: cast_nullable_to_non_nullable
as int,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [AnalytikNav].
extension AnalytikNavPatterns on AnalytikNav {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AnalytikNav value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AnalytikNav() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AnalytikNav value)  $default,){
final _that = this;
switch (_that) {
case _AnalytikNav():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AnalytikNav value)?  $default,){
final _that = this;
switch (_that) {
case _AnalytikNav() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int critical,  int warning,  int info,  int total)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AnalytikNav() when $default != null:
return $default(_that.critical,_that.warning,_that.info,_that.total);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int critical,  int warning,  int info,  int total)  $default,) {final _that = this;
switch (_that) {
case _AnalytikNav():
return $default(_that.critical,_that.warning,_that.info,_that.total);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int critical,  int warning,  int info,  int total)?  $default,) {final _that = this;
switch (_that) {
case _AnalytikNav() when $default != null:
return $default(_that.critical,_that.warning,_that.info,_that.total);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AnalytikNav implements AnalytikNav {
  const _AnalytikNav({this.critical = 0, this.warning = 0, this.info = 0, this.total = 0});
  factory _AnalytikNav.fromJson(Map<String, dynamic> json) => _$AnalytikNavFromJson(json);

@override@JsonKey() final  int critical;
@override@JsonKey() final  int warning;
@override@JsonKey() final  int info;
@override@JsonKey() final  int total;

/// Create a copy of AnalytikNav
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AnalytikNavCopyWith<_AnalytikNav> get copyWith => __$AnalytikNavCopyWithImpl<_AnalytikNav>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AnalytikNavToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AnalytikNav&&(identical(other.critical, critical) || other.critical == critical)&&(identical(other.warning, warning) || other.warning == warning)&&(identical(other.info, info) || other.info == info)&&(identical(other.total, total) || other.total == total));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,critical,warning,info,total);
}

@override
String toString() {
    return 'AnalytikNav(critical: $critical, warning: $warning, info: $info, total: $total)';
}


}

/// @nodoc
abstract mixin class _$AnalytikNavCopyWith<$Res> implements $AnalytikNavCopyWith<$Res> {
  factory _$AnalytikNavCopyWith(_AnalytikNav value, $Res Function(_AnalytikNav) _then) = __$AnalytikNavCopyWithImpl;
@override @useResult
$Res call({
 int critical, int warning, int info, int total
});




}
/// @nodoc
class __$AnalytikNavCopyWithImpl<$Res>
    implements _$AnalytikNavCopyWith<$Res> {
  __$AnalytikNavCopyWithImpl(this._self, this._then);

  final _AnalytikNav _self;
  final $Res Function(_AnalytikNav) _then;

/// Create a copy of AnalytikNav
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? critical = null,Object? warning = null,Object? info = null,Object? total = null,}) {
  return _then(_AnalytikNav(
critical: null == critical ? _self.critical : critical // ignore: cast_nullable_to_non_nullable
as int,warning: null == warning ? _self.warning : warning // ignore: cast_nullable_to_non_nullable
as int,info: null == info ? _self.info : info // ignore: cast_nullable_to_non_nullable
as int,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$TechnologistBrief {

 double get todayKg; double get goodKg; double get planKg; double? get completionPercent; double get defectKg; double? get defectPercent; int get activeOrders; int get openShifts; double? get normDeviationPercent; List<TechDay> get days;
/// Create a copy of TechnologistBrief
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TechnologistBriefCopyWith<TechnologistBrief> get copyWith => _$TechnologistBriefCopyWithImpl<TechnologistBrief>(this as TechnologistBrief, _$identity);

  /// Serializes this TechnologistBrief to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TechnologistBrief;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TechnologistBrief&&(identical(other.todayKg, _this.todayKg) || other.todayKg == _this.todayKg)&&(identical(other.goodKg, _this.goodKg) || other.goodKg == _this.goodKg)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.completionPercent, _this.completionPercent) || other.completionPercent == _this.completionPercent)&&(identical(other.defectKg, _this.defectKg) || other.defectKg == _this.defectKg)&&(identical(other.defectPercent, _this.defectPercent) || other.defectPercent == _this.defectPercent)&&(identical(other.activeOrders, _this.activeOrders) || other.activeOrders == _this.activeOrders)&&(identical(other.openShifts, _this.openShifts) || other.openShifts == _this.openShifts)&&(identical(other.normDeviationPercent, _this.normDeviationPercent) || other.normDeviationPercent == _this.normDeviationPercent)&&const DeepCollectionEquality().equals(other.days, _this.days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TechnologistBrief;
  return Object.hash(runtimeType,_this.todayKg,_this.goodKg,_this.planKg,_this.completionPercent,_this.defectKg,_this.defectPercent,_this.activeOrders,_this.openShifts,_this.normDeviationPercent,const DeepCollectionEquality().hash(_this.days));
}

@override
String toString() {
  final _this = this as TechnologistBrief;
  return 'TechnologistBrief(todayKg: ${_this.todayKg}, goodKg: ${_this.goodKg}, planKg: ${_this.planKg}, completionPercent: ${_this.completionPercent}, defectKg: ${_this.defectKg}, defectPercent: ${_this.defectPercent}, activeOrders: ${_this.activeOrders}, openShifts: ${_this.openShifts}, normDeviationPercent: ${_this.normDeviationPercent}, days: ${_this.days})';
}


}

/// @nodoc
abstract mixin class $TechnologistBriefCopyWith<$Res>  {
  factory $TechnologistBriefCopyWith(TechnologistBrief value, $Res Function(TechnologistBrief) _then) = _$TechnologistBriefCopyWithImpl;
@useResult
$Res call({
 double todayKg, double goodKg, double planKg, double? completionPercent, double defectKg, double? defectPercent, int activeOrders, int openShifts, double? normDeviationPercent, List<TechDay> days
});




}
/// @nodoc
class _$TechnologistBriefCopyWithImpl<$Res>
    implements $TechnologistBriefCopyWith<$Res> {
  _$TechnologistBriefCopyWithImpl(this._self, this._then);

  final TechnologistBrief _self;
  final $Res Function(TechnologistBrief) _then;

/// Create a copy of TechnologistBrief
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? todayKg = null,Object? goodKg = null,Object? planKg = null,Object? completionPercent = freezed,Object? defectKg = null,Object? defectPercent = freezed,Object? activeOrders = null,Object? openShifts = null,Object? normDeviationPercent = freezed,Object? days = null,}) {
  return _then(TechnologistBrief(
todayKg: null == todayKg ? _self.todayKg : todayKg // ignore: cast_nullable_to_non_nullable
as double,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,planKg: null == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double,completionPercent: freezed == completionPercent ? _self.completionPercent : completionPercent // ignore: cast_nullable_to_non_nullable
as double?,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,defectPercent: freezed == defectPercent ? _self.defectPercent : defectPercent // ignore: cast_nullable_to_non_nullable
as double?,activeOrders: null == activeOrders ? _self.activeOrders : activeOrders // ignore: cast_nullable_to_non_nullable
as int,openShifts: null == openShifts ? _self.openShifts : openShifts // ignore: cast_nullable_to_non_nullable
as int,normDeviationPercent: freezed == normDeviationPercent ? _self.normDeviationPercent : normDeviationPercent // ignore: cast_nullable_to_non_nullable
as double?,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as List<TechDay>,
  ));
}

}


/// Adds pattern-matching-related methods to [TechnologistBrief].
extension TechnologistBriefPatterns on TechnologistBrief {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TechnologistBrief value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TechnologistBrief() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TechnologistBrief value)  $default,){
final _that = this;
switch (_that) {
case _TechnologistBrief():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TechnologistBrief value)?  $default,){
final _that = this;
switch (_that) {
case _TechnologistBrief() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( double todayKg,  double goodKg,  double planKg,  double? completionPercent,  double defectKg,  double? defectPercent,  int activeOrders,  int openShifts,  double? normDeviationPercent,  List<TechDay> days)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TechnologistBrief() when $default != null:
return $default(_that.todayKg,_that.goodKg,_that.planKg,_that.completionPercent,_that.defectKg,_that.defectPercent,_that.activeOrders,_that.openShifts,_that.normDeviationPercent,_that.days);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( double todayKg,  double goodKg,  double planKg,  double? completionPercent,  double defectKg,  double? defectPercent,  int activeOrders,  int openShifts,  double? normDeviationPercent,  List<TechDay> days)  $default,) {final _that = this;
switch (_that) {
case _TechnologistBrief():
return $default(_that.todayKg,_that.goodKg,_that.planKg,_that.completionPercent,_that.defectKg,_that.defectPercent,_that.activeOrders,_that.openShifts,_that.normDeviationPercent,_that.days);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( double todayKg,  double goodKg,  double planKg,  double? completionPercent,  double defectKg,  double? defectPercent,  int activeOrders,  int openShifts,  double? normDeviationPercent,  List<TechDay> days)?  $default,) {final _that = this;
switch (_that) {
case _TechnologistBrief() when $default != null:
return $default(_that.todayKg,_that.goodKg,_that.planKg,_that.completionPercent,_that.defectKg,_that.defectPercent,_that.activeOrders,_that.openShifts,_that.normDeviationPercent,_that.days);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TechnologistBrief implements TechnologistBrief {
  const _TechnologistBrief({this.todayKg = 0, this.goodKg = 0, this.planKg = 0, this.completionPercent, this.defectKg = 0, this.defectPercent, this.activeOrders = 0, this.openShifts = 0, this.normDeviationPercent,  List<TechDay> days = const <TechDay>[]}): _days = days;
  factory _TechnologistBrief.fromJson(Map<String, dynamic> json) => _$TechnologistBriefFromJson(json);

@override@JsonKey() final  double todayKg;
@override@JsonKey() final  double goodKg;
@override@JsonKey() final  double planKg;
@override final  double? completionPercent;
@override@JsonKey() final  double defectKg;
@override final  double? defectPercent;
@override@JsonKey() final  int activeOrders;
@override@JsonKey() final  int openShifts;
@override final  double? normDeviationPercent;
 final  List<TechDay> _days;
@override@JsonKey() List<TechDay> get days {
  if (_days is EqualUnmodifiableListView) return _days;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_days);
}


/// Create a copy of TechnologistBrief
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TechnologistBriefCopyWith<_TechnologistBrief> get copyWith => __$TechnologistBriefCopyWithImpl<_TechnologistBrief>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TechnologistBriefToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TechnologistBrief&&(identical(other.todayKg, todayKg) || other.todayKg == todayKg)&&(identical(other.goodKg, goodKg) || other.goodKg == goodKg)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.completionPercent, completionPercent) || other.completionPercent == completionPercent)&&(identical(other.defectKg, defectKg) || other.defectKg == defectKg)&&(identical(other.defectPercent, defectPercent) || other.defectPercent == defectPercent)&&(identical(other.activeOrders, activeOrders) || other.activeOrders == activeOrders)&&(identical(other.openShifts, openShifts) || other.openShifts == openShifts)&&(identical(other.normDeviationPercent, normDeviationPercent) || other.normDeviationPercent == normDeviationPercent)&&const DeepCollectionEquality().equals(other.days, _days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,todayKg,goodKg,planKg,completionPercent,defectKg,defectPercent,activeOrders,openShifts,normDeviationPercent,const DeepCollectionEquality().hash(_days));
}

@override
String toString() {
    return 'TechnologistBrief(todayKg: $todayKg, goodKg: $goodKg, planKg: $planKg, completionPercent: $completionPercent, defectKg: $defectKg, defectPercent: $defectPercent, activeOrders: $activeOrders, openShifts: $openShifts, normDeviationPercent: $normDeviationPercent, days: $days)';
}


}

/// @nodoc
abstract mixin class _$TechnologistBriefCopyWith<$Res> implements $TechnologistBriefCopyWith<$Res> {
  factory _$TechnologistBriefCopyWith(_TechnologistBrief value, $Res Function(_TechnologistBrief) _then) = __$TechnologistBriefCopyWithImpl;
@override @useResult
$Res call({
 double todayKg, double goodKg, double planKg, double? completionPercent, double defectKg, double? defectPercent, int activeOrders, int openShifts, double? normDeviationPercent, List<TechDay> days
});




}
/// @nodoc
class __$TechnologistBriefCopyWithImpl<$Res>
    implements _$TechnologistBriefCopyWith<$Res> {
  __$TechnologistBriefCopyWithImpl(this._self, this._then);

  final _TechnologistBrief _self;
  final $Res Function(_TechnologistBrief) _then;

/// Create a copy of TechnologistBrief
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? todayKg = null,Object? goodKg = null,Object? planKg = null,Object? completionPercent = freezed,Object? defectKg = null,Object? defectPercent = freezed,Object? activeOrders = null,Object? openShifts = null,Object? normDeviationPercent = freezed,Object? days = null,}) {
  return _then(_TechnologistBrief(
todayKg: null == todayKg ? _self.todayKg : todayKg // ignore: cast_nullable_to_non_nullable
as double,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,planKg: null == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double,completionPercent: freezed == completionPercent ? _self.completionPercent : completionPercent // ignore: cast_nullable_to_non_nullable
as double?,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,defectPercent: freezed == defectPercent ? _self.defectPercent : defectPercent // ignore: cast_nullable_to_non_nullable
as double?,activeOrders: null == activeOrders ? _self.activeOrders : activeOrders // ignore: cast_nullable_to_non_nullable
as int,openShifts: null == openShifts ? _self.openShifts : openShifts // ignore: cast_nullable_to_non_nullable
as int,normDeviationPercent: freezed == normDeviationPercent ? _self.normDeviationPercent : normDeviationPercent // ignore: cast_nullable_to_non_nullable
as double?,days: null == days ? _self._days : days // ignore: cast_nullable_to_non_nullable
as List<TechDay>,
  ));
}


}


/// @nodoc
mixin _$TechDay {

 String get date; double get goodKg; double get defectKg;
/// Create a copy of TechDay
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TechDayCopyWith<TechDay> get copyWith => _$TechDayCopyWithImpl<TechDay>(this as TechDay, _$identity);

  /// Serializes this TechDay to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TechDay;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TechDay&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.goodKg, _this.goodKg) || other.goodKg == _this.goodKg)&&(identical(other.defectKg, _this.defectKg) || other.defectKg == _this.defectKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TechDay;
  return Object.hash(runtimeType,_this.date,_this.goodKg,_this.defectKg);
}

@override
String toString() {
  final _this = this as TechDay;
  return 'TechDay(date: ${_this.date}, goodKg: ${_this.goodKg}, defectKg: ${_this.defectKg})';
}


}

/// @nodoc
abstract mixin class $TechDayCopyWith<$Res>  {
  factory $TechDayCopyWith(TechDay value, $Res Function(TechDay) _then) = _$TechDayCopyWithImpl;
@useResult
$Res call({
 String date, double goodKg, double defectKg
});




}
/// @nodoc
class _$TechDayCopyWithImpl<$Res>
    implements $TechDayCopyWith<$Res> {
  _$TechDayCopyWithImpl(this._self, this._then);

  final TechDay _self;
  final $Res Function(TechDay) _then;

/// Create a copy of TechDay
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? date = null,Object? goodKg = null,Object? defectKg = null,}) {
  return _then(TechDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [TechDay].
extension TechDayPatterns on TechDay {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TechDay value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TechDay() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TechDay value)  $default,){
final _that = this;
switch (_that) {
case _TechDay():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TechDay value)?  $default,){
final _that = this;
switch (_that) {
case _TechDay() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String date,  double goodKg,  double defectKg)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TechDay() when $default != null:
return $default(_that.date,_that.goodKg,_that.defectKg);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String date,  double goodKg,  double defectKg)  $default,) {final _that = this;
switch (_that) {
case _TechDay():
return $default(_that.date,_that.goodKg,_that.defectKg);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String date,  double goodKg,  double defectKg)?  $default,) {final _that = this;
switch (_that) {
case _TechDay() when $default != null:
return $default(_that.date,_that.goodKg,_that.defectKg);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TechDay implements TechDay {
  const _TechDay({required this.date, this.goodKg = 0, this.defectKg = 0});
  factory _TechDay.fromJson(Map<String, dynamic> json) => _$TechDayFromJson(json);

@override final  String date;
@override@JsonKey() final  double goodKg;
@override@JsonKey() final  double defectKg;

/// Create a copy of TechDay
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TechDayCopyWith<_TechDay> get copyWith => __$TechDayCopyWithImpl<_TechDay>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TechDayToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TechDay&&(identical(other.date, date) || other.date == date)&&(identical(other.goodKg, goodKg) || other.goodKg == goodKg)&&(identical(other.defectKg, defectKg) || other.defectKg == defectKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,date,goodKg,defectKg);
}

@override
String toString() {
    return 'TechDay(date: $date, goodKg: $goodKg, defectKg: $defectKg)';
}


}

/// @nodoc
abstract mixin class _$TechDayCopyWith<$Res> implements $TechDayCopyWith<$Res> {
  factory _$TechDayCopyWith(_TechDay value, $Res Function(_TechDay) _then) = __$TechDayCopyWithImpl;
@override @useResult
$Res call({
 String date, double goodKg, double defectKg
});




}
/// @nodoc
class __$TechDayCopyWithImpl<$Res>
    implements _$TechDayCopyWith<$Res> {
  __$TechDayCopyWithImpl(this._self, this._then);

  final _TechDay _self;
  final $Res Function(_TechDay) _then;

/// Create a copy of TechDay
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? date = null,Object? goodKg = null,Object? defectKg = null,}) {
  return _then(_TechDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}

// dart format on
