// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'production_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$NamedValue {

 String get name; double get value; double? get extra; String? get unit;
/// Create a copy of NamedValue
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$NamedValueCopyWith<NamedValue> get copyWith => _$NamedValueCopyWithImpl<NamedValue>(this as NamedValue, _$identity);

  /// Serializes this NamedValue to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as NamedValue;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is NamedValue&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.value, _this.value) || other.value == _this.value)&&(identical(other.extra, _this.extra) || other.extra == _this.extra)&&(identical(other.unit, _this.unit) || other.unit == _this.unit));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as NamedValue;
  return Object.hash(runtimeType,_this.name,_this.value,_this.extra,_this.unit);
}

@override
String toString() {
  final _this = this as NamedValue;
  return 'NamedValue(name: ${_this.name}, value: ${_this.value}, extra: ${_this.extra}, unit: ${_this.unit})';
}


}

/// @nodoc
abstract mixin class $NamedValueCopyWith<$Res>  {
  factory $NamedValueCopyWith(NamedValue value, $Res Function(NamedValue) _then) = _$NamedValueCopyWithImpl;
@useResult
$Res call({
 String name, double value, double? extra, String? unit
});




}
/// @nodoc
class _$NamedValueCopyWithImpl<$Res>
    implements $NamedValueCopyWith<$Res> {
  _$NamedValueCopyWithImpl(this._self, this._then);

  final NamedValue _self;
  final $Res Function(NamedValue) _then;

/// Create a copy of NamedValue
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? name = null,Object? value = null,Object? extra = freezed,Object? unit = freezed,}) {
  return _then(NamedValue(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,extra: freezed == extra ? _self.extra : extra // ignore: cast_nullable_to_non_nullable
as double?,unit: freezed == unit ? _self.unit : unit // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [NamedValue].
extension NamedValuePatterns on NamedValue {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _NamedValue value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _NamedValue() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _NamedValue value)  $default,){
final _that = this;
switch (_that) {
case _NamedValue():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _NamedValue value)?  $default,){
final _that = this;
switch (_that) {
case _NamedValue() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String name,  double value,  double? extra,  String? unit)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _NamedValue() when $default != null:
return $default(_that.name,_that.value,_that.extra,_that.unit);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String name,  double value,  double? extra,  String? unit)  $default,) {final _that = this;
switch (_that) {
case _NamedValue():
return $default(_that.name,_that.value,_that.extra,_that.unit);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String name,  double value,  double? extra,  String? unit)?  $default,) {final _that = this;
switch (_that) {
case _NamedValue() when $default != null:
return $default(_that.name,_that.value,_that.extra,_that.unit);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _NamedValue implements NamedValue {
  const _NamedValue({required this.name, this.value = 0, this.extra, this.unit});
  factory _NamedValue.fromJson(Map<String, dynamic> json) => _$NamedValueFromJson(json);

@override final  String name;
@override@JsonKey() final  double value;
@override final  double? extra;
@override final  String? unit;

/// Create a copy of NamedValue
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$NamedValueCopyWith<_NamedValue> get copyWith => __$NamedValueCopyWithImpl<_NamedValue>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$NamedValueToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _NamedValue&&(identical(other.name, name) || other.name == name)&&(identical(other.value, value) || other.value == value)&&(identical(other.extra, extra) || other.extra == extra)&&(identical(other.unit, unit) || other.unit == unit));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,name,value,extra,unit);
}

@override
String toString() {
    return 'NamedValue(name: $name, value: $value, extra: $extra, unit: $unit)';
}


}

/// @nodoc
abstract mixin class _$NamedValueCopyWith<$Res> implements $NamedValueCopyWith<$Res> {
  factory _$NamedValueCopyWith(_NamedValue value, $Res Function(_NamedValue) _then) = __$NamedValueCopyWithImpl;
@override @useResult
$Res call({
 String name, double value, double? extra, String? unit
});




}
/// @nodoc
class __$NamedValueCopyWithImpl<$Res>
    implements _$NamedValueCopyWith<$Res> {
  __$NamedValueCopyWithImpl(this._self, this._then);

  final _NamedValue _self;
  final $Res Function(_NamedValue) _then;

/// Create a copy of NamedValue
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? name = null,Object? value = null,Object? extra = freezed,Object? unit = freezed,}) {
  return _then(_NamedValue(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,extra: freezed == extra ? _self.extra : extra // ignore: cast_nullable_to_non_nullable
as double?,unit: freezed == unit ? _self.unit : unit // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$ProdIssue {

 String get kind; String get title; String get detail; String? get link;
/// Create a copy of ProdIssue
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProdIssueCopyWith<ProdIssue> get copyWith => _$ProdIssueCopyWithImpl<ProdIssue>(this as ProdIssue, _$identity);

  /// Serializes this ProdIssue to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProdIssue;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProdIssue&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.detail, _this.detail) || other.detail == _this.detail)&&(identical(other.link, _this.link) || other.link == _this.link));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProdIssue;
  return Object.hash(runtimeType,_this.kind,_this.title,_this.detail,_this.link);
}

@override
String toString() {
  final _this = this as ProdIssue;
  return 'ProdIssue(kind: ${_this.kind}, title: ${_this.title}, detail: ${_this.detail}, link: ${_this.link})';
}


}

/// @nodoc
abstract mixin class $ProdIssueCopyWith<$Res>  {
  factory $ProdIssueCopyWith(ProdIssue value, $Res Function(ProdIssue) _then) = _$ProdIssueCopyWithImpl;
@useResult
$Res call({
 String kind, String title, String detail, String? link
});




}
/// @nodoc
class _$ProdIssueCopyWithImpl<$Res>
    implements $ProdIssueCopyWith<$Res> {
  _$ProdIssueCopyWithImpl(this._self, this._then);

  final ProdIssue _self;
  final $Res Function(ProdIssue) _then;

/// Create a copy of ProdIssue
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? kind = null,Object? title = null,Object? detail = null,Object? link = freezed,}) {
  return _then(ProdIssue(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,detail: null == detail ? _self.detail : detail // ignore: cast_nullable_to_non_nullable
as String,link: freezed == link ? _self.link : link // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [ProdIssue].
extension ProdIssuePatterns on ProdIssue {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProdIssue value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProdIssue() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProdIssue value)  $default,){
final _that = this;
switch (_that) {
case _ProdIssue():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProdIssue value)?  $default,){
final _that = this;
switch (_that) {
case _ProdIssue() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String kind,  String title,  String detail,  String? link)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ProdIssue() when $default != null:
return $default(_that.kind,_that.title,_that.detail,_that.link);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String kind,  String title,  String detail,  String? link)  $default,) {final _that = this;
switch (_that) {
case _ProdIssue():
return $default(_that.kind,_that.title,_that.detail,_that.link);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String kind,  String title,  String detail,  String? link)?  $default,) {final _that = this;
switch (_that) {
case _ProdIssue() when $default != null:
return $default(_that.kind,_that.title,_that.detail,_that.link);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProdIssue implements ProdIssue {
  const _ProdIssue({required this.kind, required this.title, this.detail = '', this.link});
  factory _ProdIssue.fromJson(Map<String, dynamic> json) => _$ProdIssueFromJson(json);

@override final  String kind;
@override final  String title;
@override@JsonKey() final  String detail;
@override final  String? link;

/// Create a copy of ProdIssue
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProdIssueCopyWith<_ProdIssue> get copyWith => __$ProdIssueCopyWithImpl<_ProdIssue>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProdIssueToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProdIssue&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.title, title) || other.title == title)&&(identical(other.detail, detail) || other.detail == detail)&&(identical(other.link, link) || other.link == link));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,kind,title,detail,link);
}

@override
String toString() {
    return 'ProdIssue(kind: $kind, title: $title, detail: $detail, link: $link)';
}


}

/// @nodoc
abstract mixin class _$ProdIssueCopyWith<$Res> implements $ProdIssueCopyWith<$Res> {
  factory _$ProdIssueCopyWith(_ProdIssue value, $Res Function(_ProdIssue) _then) = __$ProdIssueCopyWithImpl;
@override @useResult
$Res call({
 String kind, String title, String detail, String? link
});




}
/// @nodoc
class __$ProdIssueCopyWithImpl<$Res>
    implements _$ProdIssueCopyWith<$Res> {
  __$ProdIssueCopyWithImpl(this._self, this._then);

  final _ProdIssue _self;
  final $Res Function(_ProdIssue) _then;

/// Create a copy of ProdIssue
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? kind = null,Object? title = null,Object? detail = null,Object? link = freezed,}) {
  return _then(_ProdIssue(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,detail: null == detail ? _self.detail : detail // ignore: cast_nullable_to_non_nullable
as String,link: freezed == link ? _self.link : link // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$ProdDay {

 String get date; double get goodKg; double get defectKg;
/// Create a copy of ProdDay
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProdDayCopyWith<ProdDay> get copyWith => _$ProdDayCopyWithImpl<ProdDay>(this as ProdDay, _$identity);

  /// Serializes this ProdDay to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProdDay;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProdDay&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.goodKg, _this.goodKg) || other.goodKg == _this.goodKg)&&(identical(other.defectKg, _this.defectKg) || other.defectKg == _this.defectKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProdDay;
  return Object.hash(runtimeType,_this.date,_this.goodKg,_this.defectKg);
}

@override
String toString() {
  final _this = this as ProdDay;
  return 'ProdDay(date: ${_this.date}, goodKg: ${_this.goodKg}, defectKg: ${_this.defectKg})';
}


}

/// @nodoc
abstract mixin class $ProdDayCopyWith<$Res>  {
  factory $ProdDayCopyWith(ProdDay value, $Res Function(ProdDay) _then) = _$ProdDayCopyWithImpl;
@useResult
$Res call({
 String date, double goodKg, double defectKg
});




}
/// @nodoc
class _$ProdDayCopyWithImpl<$Res>
    implements $ProdDayCopyWith<$Res> {
  _$ProdDayCopyWithImpl(this._self, this._then);

  final ProdDay _self;
  final $Res Function(ProdDay) _then;

/// Create a copy of ProdDay
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? date = null,Object? goodKg = null,Object? defectKg = null,}) {
  return _then(ProdDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [ProdDay].
extension ProdDayPatterns on ProdDay {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProdDay value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProdDay() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProdDay value)  $default,){
final _that = this;
switch (_that) {
case _ProdDay():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProdDay value)?  $default,){
final _that = this;
switch (_that) {
case _ProdDay() when $default != null:
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
case _ProdDay() when $default != null:
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
case _ProdDay():
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
case _ProdDay() when $default != null:
return $default(_that.date,_that.goodKg,_that.defectKg);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProdDay implements ProdDay {
  const _ProdDay({required this.date, this.goodKg = 0, this.defectKg = 0});
  factory _ProdDay.fromJson(Map<String, dynamic> json) => _$ProdDayFromJson(json);

@override final  String date;
@override@JsonKey() final  double goodKg;
@override@JsonKey() final  double defectKg;

/// Create a copy of ProdDay
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProdDayCopyWith<_ProdDay> get copyWith => __$ProdDayCopyWithImpl<_ProdDay>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProdDayToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProdDay&&(identical(other.date, date) || other.date == date)&&(identical(other.goodKg, goodKg) || other.goodKg == goodKg)&&(identical(other.defectKg, defectKg) || other.defectKg == defectKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,date,goodKg,defectKg);
}

@override
String toString() {
    return 'ProdDay(date: $date, goodKg: $goodKg, defectKg: $defectKg)';
}


}

/// @nodoc
abstract mixin class _$ProdDayCopyWith<$Res> implements $ProdDayCopyWith<$Res> {
  factory _$ProdDayCopyWith(_ProdDay value, $Res Function(_ProdDay) _then) = __$ProdDayCopyWithImpl;
@override @useResult
$Res call({
 String date, double goodKg, double defectKg
});




}
/// @nodoc
class __$ProdDayCopyWithImpl<$Res>
    implements _$ProdDayCopyWith<$Res> {
  __$ProdDayCopyWithImpl(this._self, this._then);

  final _ProdDay _self;
  final $Res Function(_ProdDay) _then;

/// Create a copy of ProdDay
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? date = null,Object? goodKg = null,Object? defectKg = null,}) {
  return _then(_ProdDay(
date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$ProductionDashboard {

 double get todayKg; double get goodKg; double get planKg; double? get completionPercent; double get defectKg; double? get defectPercent; double? get defectCost; int get activeOrders; int get openShifts; int get closedShifts; double get normKg; double get factKg; double? get normDeviationPercent; List<ProdDay> get days; List<NamedValue> get byProduct; List<NamedValue> get defectsByType; List<ProdIssue> get issues;
/// Create a copy of ProductionDashboard
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProductionDashboardCopyWith<ProductionDashboard> get copyWith => _$ProductionDashboardCopyWithImpl<ProductionDashboard>(this as ProductionDashboard, _$identity);

  /// Serializes this ProductionDashboard to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProductionDashboard;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProductionDashboard&&(identical(other.todayKg, _this.todayKg) || other.todayKg == _this.todayKg)&&(identical(other.goodKg, _this.goodKg) || other.goodKg == _this.goodKg)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.completionPercent, _this.completionPercent) || other.completionPercent == _this.completionPercent)&&(identical(other.defectKg, _this.defectKg) || other.defectKg == _this.defectKg)&&(identical(other.defectPercent, _this.defectPercent) || other.defectPercent == _this.defectPercent)&&(identical(other.defectCost, _this.defectCost) || other.defectCost == _this.defectCost)&&(identical(other.activeOrders, _this.activeOrders) || other.activeOrders == _this.activeOrders)&&(identical(other.openShifts, _this.openShifts) || other.openShifts == _this.openShifts)&&(identical(other.closedShifts, _this.closedShifts) || other.closedShifts == _this.closedShifts)&&(identical(other.normKg, _this.normKg) || other.normKg == _this.normKg)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.normDeviationPercent, _this.normDeviationPercent) || other.normDeviationPercent == _this.normDeviationPercent)&&const DeepCollectionEquality().equals(other.days, _this.days)&&const DeepCollectionEquality().equals(other.byProduct, _this.byProduct)&&const DeepCollectionEquality().equals(other.defectsByType, _this.defectsByType)&&const DeepCollectionEquality().equals(other.issues, _this.issues));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProductionDashboard;
  return Object.hash(runtimeType,_this.todayKg,_this.goodKg,_this.planKg,_this.completionPercent,_this.defectKg,_this.defectPercent,_this.defectCost,_this.activeOrders,_this.openShifts,_this.closedShifts,_this.normKg,_this.factKg,_this.normDeviationPercent,const DeepCollectionEquality().hash(_this.days),const DeepCollectionEquality().hash(_this.byProduct),const DeepCollectionEquality().hash(_this.defectsByType),const DeepCollectionEquality().hash(_this.issues));
}

@override
String toString() {
  final _this = this as ProductionDashboard;
  return 'ProductionDashboard(todayKg: ${_this.todayKg}, goodKg: ${_this.goodKg}, planKg: ${_this.planKg}, completionPercent: ${_this.completionPercent}, defectKg: ${_this.defectKg}, defectPercent: ${_this.defectPercent}, defectCost: ${_this.defectCost}, activeOrders: ${_this.activeOrders}, openShifts: ${_this.openShifts}, closedShifts: ${_this.closedShifts}, normKg: ${_this.normKg}, factKg: ${_this.factKg}, normDeviationPercent: ${_this.normDeviationPercent}, days: ${_this.days}, byProduct: ${_this.byProduct}, defectsByType: ${_this.defectsByType}, issues: ${_this.issues})';
}


}

/// @nodoc
abstract mixin class $ProductionDashboardCopyWith<$Res>  {
  factory $ProductionDashboardCopyWith(ProductionDashboard value, $Res Function(ProductionDashboard) _then) = _$ProductionDashboardCopyWithImpl;
@useResult
$Res call({
 double todayKg, double goodKg, double planKg, double? completionPercent, double defectKg, double? defectPercent, double? defectCost, int activeOrders, int openShifts, int closedShifts, double normKg, double factKg, double? normDeviationPercent, List<ProdDay> days, List<NamedValue> byProduct, List<NamedValue> defectsByType, List<ProdIssue> issues
});




}
/// @nodoc
class _$ProductionDashboardCopyWithImpl<$Res>
    implements $ProductionDashboardCopyWith<$Res> {
  _$ProductionDashboardCopyWithImpl(this._self, this._then);

  final ProductionDashboard _self;
  final $Res Function(ProductionDashboard) _then;

/// Create a copy of ProductionDashboard
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? todayKg = null,Object? goodKg = null,Object? planKg = null,Object? completionPercent = freezed,Object? defectKg = null,Object? defectPercent = freezed,Object? defectCost = freezed,Object? activeOrders = null,Object? openShifts = null,Object? closedShifts = null,Object? normKg = null,Object? factKg = null,Object? normDeviationPercent = freezed,Object? days = null,Object? byProduct = null,Object? defectsByType = null,Object? issues = null,}) {
  return _then(ProductionDashboard(
todayKg: null == todayKg ? _self.todayKg : todayKg // ignore: cast_nullable_to_non_nullable
as double,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,planKg: null == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double,completionPercent: freezed == completionPercent ? _self.completionPercent : completionPercent // ignore: cast_nullable_to_non_nullable
as double?,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,defectPercent: freezed == defectPercent ? _self.defectPercent : defectPercent // ignore: cast_nullable_to_non_nullable
as double?,defectCost: freezed == defectCost ? _self.defectCost : defectCost // ignore: cast_nullable_to_non_nullable
as double?,activeOrders: null == activeOrders ? _self.activeOrders : activeOrders // ignore: cast_nullable_to_non_nullable
as int,openShifts: null == openShifts ? _self.openShifts : openShifts // ignore: cast_nullable_to_non_nullable
as int,closedShifts: null == closedShifts ? _self.closedShifts : closedShifts // ignore: cast_nullable_to_non_nullable
as int,normKg: null == normKg ? _self.normKg : normKg // ignore: cast_nullable_to_non_nullable
as double,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,normDeviationPercent: freezed == normDeviationPercent ? _self.normDeviationPercent : normDeviationPercent // ignore: cast_nullable_to_non_nullable
as double?,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as List<ProdDay>,byProduct: null == byProduct ? _self.byProduct : byProduct // ignore: cast_nullable_to_non_nullable
as List<NamedValue>,defectsByType: null == defectsByType ? _self.defectsByType : defectsByType // ignore: cast_nullable_to_non_nullable
as List<NamedValue>,issues: null == issues ? _self.issues : issues // ignore: cast_nullable_to_non_nullable
as List<ProdIssue>,
  ));
}

}


/// Adds pattern-matching-related methods to [ProductionDashboard].
extension ProductionDashboardPatterns on ProductionDashboard {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProductionDashboard value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProductionDashboard() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProductionDashboard value)  $default,){
final _that = this;
switch (_that) {
case _ProductionDashboard():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProductionDashboard value)?  $default,){
final _that = this;
switch (_that) {
case _ProductionDashboard() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( double todayKg,  double goodKg,  double planKg,  double? completionPercent,  double defectKg,  double? defectPercent,  double? defectCost,  int activeOrders,  int openShifts,  int closedShifts,  double normKg,  double factKg,  double? normDeviationPercent,  List<ProdDay> days,  List<NamedValue> byProduct,  List<NamedValue> defectsByType,  List<ProdIssue> issues)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ProductionDashboard() when $default != null:
return $default(_that.todayKg,_that.goodKg,_that.planKg,_that.completionPercent,_that.defectKg,_that.defectPercent,_that.defectCost,_that.activeOrders,_that.openShifts,_that.closedShifts,_that.normKg,_that.factKg,_that.normDeviationPercent,_that.days,_that.byProduct,_that.defectsByType,_that.issues);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( double todayKg,  double goodKg,  double planKg,  double? completionPercent,  double defectKg,  double? defectPercent,  double? defectCost,  int activeOrders,  int openShifts,  int closedShifts,  double normKg,  double factKg,  double? normDeviationPercent,  List<ProdDay> days,  List<NamedValue> byProduct,  List<NamedValue> defectsByType,  List<ProdIssue> issues)  $default,) {final _that = this;
switch (_that) {
case _ProductionDashboard():
return $default(_that.todayKg,_that.goodKg,_that.planKg,_that.completionPercent,_that.defectKg,_that.defectPercent,_that.defectCost,_that.activeOrders,_that.openShifts,_that.closedShifts,_that.normKg,_that.factKg,_that.normDeviationPercent,_that.days,_that.byProduct,_that.defectsByType,_that.issues);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( double todayKg,  double goodKg,  double planKg,  double? completionPercent,  double defectKg,  double? defectPercent,  double? defectCost,  int activeOrders,  int openShifts,  int closedShifts,  double normKg,  double factKg,  double? normDeviationPercent,  List<ProdDay> days,  List<NamedValue> byProduct,  List<NamedValue> defectsByType,  List<ProdIssue> issues)?  $default,) {final _that = this;
switch (_that) {
case _ProductionDashboard() when $default != null:
return $default(_that.todayKg,_that.goodKg,_that.planKg,_that.completionPercent,_that.defectKg,_that.defectPercent,_that.defectCost,_that.activeOrders,_that.openShifts,_that.closedShifts,_that.normKg,_that.factKg,_that.normDeviationPercent,_that.days,_that.byProduct,_that.defectsByType,_that.issues);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProductionDashboard implements ProductionDashboard {
  const _ProductionDashboard({this.todayKg = 0, this.goodKg = 0, this.planKg = 0, this.completionPercent, this.defectKg = 0, this.defectPercent, this.defectCost, this.activeOrders = 0, this.openShifts = 0, this.closedShifts = 0, this.normKg = 0, this.factKg = 0, this.normDeviationPercent,  List<ProdDay> days = const <ProdDay>[],  List<NamedValue> byProduct = const <NamedValue>[],  List<NamedValue> defectsByType = const <NamedValue>[],  List<ProdIssue> issues = const <ProdIssue>[]}): _days = days,_byProduct = byProduct,_defectsByType = defectsByType,_issues = issues;
  factory _ProductionDashboard.fromJson(Map<String, dynamic> json) => _$ProductionDashboardFromJson(json);

@override@JsonKey() final  double todayKg;
@override@JsonKey() final  double goodKg;
@override@JsonKey() final  double planKg;
@override final  double? completionPercent;
@override@JsonKey() final  double defectKg;
@override final  double? defectPercent;
@override final  double? defectCost;
@override@JsonKey() final  int activeOrders;
@override@JsonKey() final  int openShifts;
@override@JsonKey() final  int closedShifts;
@override@JsonKey() final  double normKg;
@override@JsonKey() final  double factKg;
@override final  double? normDeviationPercent;
 final  List<ProdDay> _days;
@override@JsonKey() List<ProdDay> get days {
  if (_days is EqualUnmodifiableListView) return _days;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_days);
}

 final  List<NamedValue> _byProduct;
@override@JsonKey() List<NamedValue> get byProduct {
  if (_byProduct is EqualUnmodifiableListView) return _byProduct;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_byProduct);
}

 final  List<NamedValue> _defectsByType;
@override@JsonKey() List<NamedValue> get defectsByType {
  if (_defectsByType is EqualUnmodifiableListView) return _defectsByType;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_defectsByType);
}

 final  List<ProdIssue> _issues;
@override@JsonKey() List<ProdIssue> get issues {
  if (_issues is EqualUnmodifiableListView) return _issues;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_issues);
}


/// Create a copy of ProductionDashboard
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProductionDashboardCopyWith<_ProductionDashboard> get copyWith => __$ProductionDashboardCopyWithImpl<_ProductionDashboard>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProductionDashboardToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProductionDashboard&&(identical(other.todayKg, todayKg) || other.todayKg == todayKg)&&(identical(other.goodKg, goodKg) || other.goodKg == goodKg)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.completionPercent, completionPercent) || other.completionPercent == completionPercent)&&(identical(other.defectKg, defectKg) || other.defectKg == defectKg)&&(identical(other.defectPercent, defectPercent) || other.defectPercent == defectPercent)&&(identical(other.defectCost, defectCost) || other.defectCost == defectCost)&&(identical(other.activeOrders, activeOrders) || other.activeOrders == activeOrders)&&(identical(other.openShifts, openShifts) || other.openShifts == openShifts)&&(identical(other.closedShifts, closedShifts) || other.closedShifts == closedShifts)&&(identical(other.normKg, normKg) || other.normKg == normKg)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.normDeviationPercent, normDeviationPercent) || other.normDeviationPercent == normDeviationPercent)&&const DeepCollectionEquality().equals(other.days, _days)&&const DeepCollectionEquality().equals(other.byProduct, _byProduct)&&const DeepCollectionEquality().equals(other.defectsByType, _defectsByType)&&const DeepCollectionEquality().equals(other.issues, _issues));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,todayKg,goodKg,planKg,completionPercent,defectKg,defectPercent,defectCost,activeOrders,openShifts,closedShifts,normKg,factKg,normDeviationPercent,const DeepCollectionEquality().hash(_days),const DeepCollectionEquality().hash(_byProduct),const DeepCollectionEquality().hash(_defectsByType),const DeepCollectionEquality().hash(_issues));
}

@override
String toString() {
    return 'ProductionDashboard(todayKg: $todayKg, goodKg: $goodKg, planKg: $planKg, completionPercent: $completionPercent, defectKg: $defectKg, defectPercent: $defectPercent, defectCost: $defectCost, activeOrders: $activeOrders, openShifts: $openShifts, closedShifts: $closedShifts, normKg: $normKg, factKg: $factKg, normDeviationPercent: $normDeviationPercent, days: $days, byProduct: $byProduct, defectsByType: $defectsByType, issues: $issues)';
}


}

/// @nodoc
abstract mixin class _$ProductionDashboardCopyWith<$Res> implements $ProductionDashboardCopyWith<$Res> {
  factory _$ProductionDashboardCopyWith(_ProductionDashboard value, $Res Function(_ProductionDashboard) _then) = __$ProductionDashboardCopyWithImpl;
@override @useResult
$Res call({
 double todayKg, double goodKg, double planKg, double? completionPercent, double defectKg, double? defectPercent, double? defectCost, int activeOrders, int openShifts, int closedShifts, double normKg, double factKg, double? normDeviationPercent, List<ProdDay> days, List<NamedValue> byProduct, List<NamedValue> defectsByType, List<ProdIssue> issues
});




}
/// @nodoc
class __$ProductionDashboardCopyWithImpl<$Res>
    implements _$ProductionDashboardCopyWith<$Res> {
  __$ProductionDashboardCopyWithImpl(this._self, this._then);

  final _ProductionDashboard _self;
  final $Res Function(_ProductionDashboard) _then;

/// Create a copy of ProductionDashboard
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? todayKg = null,Object? goodKg = null,Object? planKg = null,Object? completionPercent = freezed,Object? defectKg = null,Object? defectPercent = freezed,Object? defectCost = freezed,Object? activeOrders = null,Object? openShifts = null,Object? closedShifts = null,Object? normKg = null,Object? factKg = null,Object? normDeviationPercent = freezed,Object? days = null,Object? byProduct = null,Object? defectsByType = null,Object? issues = null,}) {
  return _then(_ProductionDashboard(
todayKg: null == todayKg ? _self.todayKg : todayKg // ignore: cast_nullable_to_non_nullable
as double,goodKg: null == goodKg ? _self.goodKg : goodKg // ignore: cast_nullable_to_non_nullable
as double,planKg: null == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double,completionPercent: freezed == completionPercent ? _self.completionPercent : completionPercent // ignore: cast_nullable_to_non_nullable
as double?,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,defectPercent: freezed == defectPercent ? _self.defectPercent : defectPercent // ignore: cast_nullable_to_non_nullable
as double?,defectCost: freezed == defectCost ? _self.defectCost : defectCost // ignore: cast_nullable_to_non_nullable
as double?,activeOrders: null == activeOrders ? _self.activeOrders : activeOrders // ignore: cast_nullable_to_non_nullable
as int,openShifts: null == openShifts ? _self.openShifts : openShifts // ignore: cast_nullable_to_non_nullable
as int,closedShifts: null == closedShifts ? _self.closedShifts : closedShifts // ignore: cast_nullable_to_non_nullable
as int,normKg: null == normKg ? _self.normKg : normKg // ignore: cast_nullable_to_non_nullable
as double,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,normDeviationPercent: freezed == normDeviationPercent ? _self.normDeviationPercent : normDeviationPercent // ignore: cast_nullable_to_non_nullable
as double?,days: null == days ? _self._days : days // ignore: cast_nullable_to_non_nullable
as List<ProdDay>,byProduct: null == byProduct ? _self._byProduct : byProduct // ignore: cast_nullable_to_non_nullable
as List<NamedValue>,defectsByType: null == defectsByType ? _self._defectsByType : defectsByType // ignore: cast_nullable_to_non_nullable
as List<NamedValue>,issues: null == issues ? _self._issues : issues // ignore: cast_nullable_to_non_nullable
as List<ProdIssue>,
  ));
}


}


/// @nodoc
mixin _$CountValue {

 int get count; double get value;
/// Create a copy of CountValue
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CountValueCopyWith<CountValue> get copyWith => _$CountValueCopyWithImpl<CountValue>(this as CountValue, _$identity);

  /// Serializes this CountValue to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CountValue;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CountValue&&(identical(other.count, _this.count) || other.count == _this.count)&&(identical(other.value, _this.value) || other.value == _this.value));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CountValue;
  return Object.hash(runtimeType,_this.count,_this.value);
}

@override
String toString() {
  final _this = this as CountValue;
  return 'CountValue(count: ${_this.count}, value: ${_this.value})';
}


}

/// @nodoc
abstract mixin class $CountValueCopyWith<$Res>  {
  factory $CountValueCopyWith(CountValue value, $Res Function(CountValue) _then) = _$CountValueCopyWithImpl;
@useResult
$Res call({
 int count, double value
});




}
/// @nodoc
class _$CountValueCopyWithImpl<$Res>
    implements $CountValueCopyWith<$Res> {
  _$CountValueCopyWithImpl(this._self, this._then);

  final CountValue _self;
  final $Res Function(CountValue) _then;

/// Create a copy of CountValue
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? count = null,Object? value = null,}) {
  return _then(CountValue(
count: null == count ? _self.count : count // ignore: cast_nullable_to_non_nullable
as int,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [CountValue].
extension CountValuePatterns on CountValue {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CountValue value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CountValue() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CountValue value)  $default,){
final _that = this;
switch (_that) {
case _CountValue():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CountValue value)?  $default,){
final _that = this;
switch (_that) {
case _CountValue() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int count,  double value)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CountValue() when $default != null:
return $default(_that.count,_that.value);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int count,  double value)  $default,) {final _that = this;
switch (_that) {
case _CountValue():
return $default(_that.count,_that.value);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int count,  double value)?  $default,) {final _that = this;
switch (_that) {
case _CountValue() when $default != null:
return $default(_that.count,_that.value);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CountValue implements CountValue {
  const _CountValue({this.count = 0, this.value = 0});
  factory _CountValue.fromJson(Map<String, dynamic> json) => _$CountValueFromJson(json);

@override@JsonKey() final  int count;
@override@JsonKey() final  double value;

/// Create a copy of CountValue
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CountValueCopyWith<_CountValue> get copyWith => __$CountValueCopyWithImpl<_CountValue>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CountValueToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CountValue&&(identical(other.count, count) || other.count == count)&&(identical(other.value, value) || other.value == value));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,count,value);
}

@override
String toString() {
    return 'CountValue(count: $count, value: $value)';
}


}

/// @nodoc
abstract mixin class _$CountValueCopyWith<$Res> implements $CountValueCopyWith<$Res> {
  factory _$CountValueCopyWith(_CountValue value, $Res Function(_CountValue) _then) = __$CountValueCopyWithImpl;
@override @useResult
$Res call({
 int count, double value
});




}
/// @nodoc
class __$CountValueCopyWithImpl<$Res>
    implements _$CountValueCopyWith<$Res> {
  __$CountValueCopyWithImpl(this._self, this._then);

  final _CountValue _self;
  final $Res Function(_CountValue) _then;

/// Create a copy of CountValue
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? count = null,Object? value = null,}) {
  return _then(_CountValue(
count: null == count ? _self.count : count // ignore: cast_nullable_to_non_nullable
as int,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$LowDays {

 String get name; String get unitCode; double get onHand; double get perDay; double get days;
/// Create a copy of LowDays
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$LowDaysCopyWith<LowDays> get copyWith => _$LowDaysCopyWithImpl<LowDays>(this as LowDays, _$identity);

  /// Serializes this LowDays to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as LowDays;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is LowDays&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.unitCode, _this.unitCode) || other.unitCode == _this.unitCode)&&(identical(other.onHand, _this.onHand) || other.onHand == _this.onHand)&&(identical(other.perDay, _this.perDay) || other.perDay == _this.perDay)&&(identical(other.days, _this.days) || other.days == _this.days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as LowDays;
  return Object.hash(runtimeType,_this.name,_this.unitCode,_this.onHand,_this.perDay,_this.days);
}

@override
String toString() {
  final _this = this as LowDays;
  return 'LowDays(name: ${_this.name}, unitCode: ${_this.unitCode}, onHand: ${_this.onHand}, perDay: ${_this.perDay}, days: ${_this.days})';
}


}

/// @nodoc
abstract mixin class $LowDaysCopyWith<$Res>  {
  factory $LowDaysCopyWith(LowDays value, $Res Function(LowDays) _then) = _$LowDaysCopyWithImpl;
@useResult
$Res call({
 String name, String unitCode, double onHand, double perDay, double days
});




}
/// @nodoc
class _$LowDaysCopyWithImpl<$Res>
    implements $LowDaysCopyWith<$Res> {
  _$LowDaysCopyWithImpl(this._self, this._then);

  final LowDays _self;
  final $Res Function(LowDays) _then;

/// Create a copy of LowDays
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? name = null,Object? unitCode = null,Object? onHand = null,Object? perDay = null,Object? days = null,}) {
  return _then(LowDays(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,onHand: null == onHand ? _self.onHand : onHand // ignore: cast_nullable_to_non_nullable
as double,perDay: null == perDay ? _self.perDay : perDay // ignore: cast_nullable_to_non_nullable
as double,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [LowDays].
extension LowDaysPatterns on LowDays {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _LowDays value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _LowDays() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _LowDays value)  $default,){
final _that = this;
switch (_that) {
case _LowDays():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _LowDays value)?  $default,){
final _that = this;
switch (_that) {
case _LowDays() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String name,  String unitCode,  double onHand,  double perDay,  double days)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _LowDays() when $default != null:
return $default(_that.name,_that.unitCode,_that.onHand,_that.perDay,_that.days);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String name,  String unitCode,  double onHand,  double perDay,  double days)  $default,) {final _that = this;
switch (_that) {
case _LowDays():
return $default(_that.name,_that.unitCode,_that.onHand,_that.perDay,_that.days);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String name,  String unitCode,  double onHand,  double perDay,  double days)?  $default,) {final _that = this;
switch (_that) {
case _LowDays() when $default != null:
return $default(_that.name,_that.unitCode,_that.onHand,_that.perDay,_that.days);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _LowDays implements LowDays {
  const _LowDays({required this.name, this.unitCode = '', this.onHand = 0, this.perDay = 0, this.days = 0});
  factory _LowDays.fromJson(Map<String, dynamic> json) => _$LowDaysFromJson(json);

@override final  String name;
@override@JsonKey() final  String unitCode;
@override@JsonKey() final  double onHand;
@override@JsonKey() final  double perDay;
@override@JsonKey() final  double days;

/// Create a copy of LowDays
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$LowDaysCopyWith<_LowDays> get copyWith => __$LowDaysCopyWithImpl<_LowDays>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$LowDaysToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _LowDays&&(identical(other.name, name) || other.name == name)&&(identical(other.unitCode, unitCode) || other.unitCode == unitCode)&&(identical(other.onHand, onHand) || other.onHand == onHand)&&(identical(other.perDay, perDay) || other.perDay == perDay)&&(identical(other.days, days) || other.days == days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,name,unitCode,onHand,perDay,days);
}

@override
String toString() {
    return 'LowDays(name: $name, unitCode: $unitCode, onHand: $onHand, perDay: $perDay, days: $days)';
}


}

/// @nodoc
abstract mixin class _$LowDaysCopyWith<$Res> implements $LowDaysCopyWith<$Res> {
  factory _$LowDaysCopyWith(_LowDays value, $Res Function(_LowDays) _then) = __$LowDaysCopyWithImpl;
@override @useResult
$Res call({
 String name, String unitCode, double onHand, double perDay, double days
});




}
/// @nodoc
class __$LowDaysCopyWithImpl<$Res>
    implements _$LowDaysCopyWith<$Res> {
  __$LowDaysCopyWithImpl(this._self, this._then);

  final _LowDays _self;
  final $Res Function(_LowDays) _then;

/// Create a copy of LowDays
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? name = null,Object? unitCode = null,Object? onHand = null,Object? perDay = null,Object? days = null,}) {
  return _then(_LowDays(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,onHand: null == onHand ? _self.onHand : onHand // ignore: cast_nullable_to_non_nullable
as double,perDay: null == perDay ? _self.perDay : perDay // ignore: cast_nullable_to_non_nullable
as double,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$WarehouseValue {

 String get name; int get items; double get value;
/// Create a copy of WarehouseValue
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WarehouseValueCopyWith<WarehouseValue> get copyWith => _$WarehouseValueCopyWithImpl<WarehouseValue>(this as WarehouseValue, _$identity);

  /// Serializes this WarehouseValue to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WarehouseValue;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WarehouseValue&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.items, _this.items) || other.items == _this.items)&&(identical(other.value, _this.value) || other.value == _this.value));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WarehouseValue;
  return Object.hash(runtimeType,_this.name,_this.items,_this.value);
}

@override
String toString() {
  final _this = this as WarehouseValue;
  return 'WarehouseValue(name: ${_this.name}, items: ${_this.items}, value: ${_this.value})';
}


}

/// @nodoc
abstract mixin class $WarehouseValueCopyWith<$Res>  {
  factory $WarehouseValueCopyWith(WarehouseValue value, $Res Function(WarehouseValue) _then) = _$WarehouseValueCopyWithImpl;
@useResult
$Res call({
 String name, int items, double value
});




}
/// @nodoc
class _$WarehouseValueCopyWithImpl<$Res>
    implements $WarehouseValueCopyWith<$Res> {
  _$WarehouseValueCopyWithImpl(this._self, this._then);

  final WarehouseValue _self;
  final $Res Function(WarehouseValue) _then;

/// Create a copy of WarehouseValue
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? name = null,Object? items = null,Object? value = null,}) {
  return _then(WarehouseValue(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,items: null == items ? _self.items : items // ignore: cast_nullable_to_non_nullable
as int,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [WarehouseValue].
extension WarehouseValuePatterns on WarehouseValue {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WarehouseValue value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WarehouseValue() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WarehouseValue value)  $default,){
final _that = this;
switch (_that) {
case _WarehouseValue():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WarehouseValue value)?  $default,){
final _that = this;
switch (_that) {
case _WarehouseValue() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String name,  int items,  double value)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WarehouseValue() when $default != null:
return $default(_that.name,_that.items,_that.value);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String name,  int items,  double value)  $default,) {final _that = this;
switch (_that) {
case _WarehouseValue():
return $default(_that.name,_that.items,_that.value);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String name,  int items,  double value)?  $default,) {final _that = this;
switch (_that) {
case _WarehouseValue() when $default != null:
return $default(_that.name,_that.items,_that.value);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WarehouseValue implements WarehouseValue {
  const _WarehouseValue({required this.name, this.items = 0, this.value = 0});
  factory _WarehouseValue.fromJson(Map<String, dynamic> json) => _$WarehouseValueFromJson(json);

@override final  String name;
@override@JsonKey() final  int items;
@override@JsonKey() final  double value;

/// Create a copy of WarehouseValue
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WarehouseValueCopyWith<_WarehouseValue> get copyWith => __$WarehouseValueCopyWithImpl<_WarehouseValue>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WarehouseValueToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WarehouseValue&&(identical(other.name, name) || other.name == name)&&(identical(other.items, items) || other.items == items)&&(identical(other.value, value) || other.value == value));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,name,items,value);
}

@override
String toString() {
    return 'WarehouseValue(name: $name, items: $items, value: $value)';
}


}

/// @nodoc
abstract mixin class _$WarehouseValueCopyWith<$Res> implements $WarehouseValueCopyWith<$Res> {
  factory _$WarehouseValueCopyWith(_WarehouseValue value, $Res Function(_WarehouseValue) _then) = __$WarehouseValueCopyWithImpl;
@override @useResult
$Res call({
 String name, int items, double value
});




}
/// @nodoc
class __$WarehouseValueCopyWithImpl<$Res>
    implements _$WarehouseValueCopyWith<$Res> {
  __$WarehouseValueCopyWithImpl(this._self, this._then);

  final _WarehouseValue _self;
  final $Res Function(_WarehouseValue) _then;

/// Create a copy of WarehouseValue
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? name = null,Object? items = null,Object? value = null,}) {
  return _then(_WarehouseValue(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,items: null == items ? _self.items : items // ignore: cast_nullable_to_non_nullable
as int,value: null == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$ProductionOverview {

 List<WarehouseValue> get stock; double get stockValue; CountValue get expiring7; CountValue get expiring30; CountValue get expired; CountValue get onControl; int get belowMin; int get openOrders; int get overdueOrders; List<LowDays> get lowDays;
/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProductionOverviewCopyWith<ProductionOverview> get copyWith => _$ProductionOverviewCopyWithImpl<ProductionOverview>(this as ProductionOverview, _$identity);

  /// Serializes this ProductionOverview to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProductionOverview;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProductionOverview&&const DeepCollectionEquality().equals(other.stock, _this.stock)&&(identical(other.stockValue, _this.stockValue) || other.stockValue == _this.stockValue)&&(identical(other.expiring7, _this.expiring7) || other.expiring7 == _this.expiring7)&&(identical(other.expiring30, _this.expiring30) || other.expiring30 == _this.expiring30)&&(identical(other.expired, _this.expired) || other.expired == _this.expired)&&(identical(other.onControl, _this.onControl) || other.onControl == _this.onControl)&&(identical(other.belowMin, _this.belowMin) || other.belowMin == _this.belowMin)&&(identical(other.openOrders, _this.openOrders) || other.openOrders == _this.openOrders)&&(identical(other.overdueOrders, _this.overdueOrders) || other.overdueOrders == _this.overdueOrders)&&const DeepCollectionEquality().equals(other.lowDays, _this.lowDays));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProductionOverview;
  return Object.hash(runtimeType,const DeepCollectionEquality().hash(_this.stock),_this.stockValue,_this.expiring7,_this.expiring30,_this.expired,_this.onControl,_this.belowMin,_this.openOrders,_this.overdueOrders,const DeepCollectionEquality().hash(_this.lowDays));
}

@override
String toString() {
  final _this = this as ProductionOverview;
  return 'ProductionOverview(stock: ${_this.stock}, stockValue: ${_this.stockValue}, expiring7: ${_this.expiring7}, expiring30: ${_this.expiring30}, expired: ${_this.expired}, onControl: ${_this.onControl}, belowMin: ${_this.belowMin}, openOrders: ${_this.openOrders}, overdueOrders: ${_this.overdueOrders}, lowDays: ${_this.lowDays})';
}


}

/// @nodoc
abstract mixin class $ProductionOverviewCopyWith<$Res>  {
  factory $ProductionOverviewCopyWith(ProductionOverview value, $Res Function(ProductionOverview) _then) = _$ProductionOverviewCopyWithImpl;
@useResult
$Res call({
 List<WarehouseValue> stock, double stockValue, CountValue expiring7, CountValue expiring30, CountValue expired, CountValue onControl, int belowMin, int openOrders, int overdueOrders, List<LowDays> lowDays
});


$CountValueCopyWith<$Res> get expiring7;$CountValueCopyWith<$Res> get expiring30;$CountValueCopyWith<$Res> get expired;$CountValueCopyWith<$Res> get onControl;

}
/// @nodoc
class _$ProductionOverviewCopyWithImpl<$Res>
    implements $ProductionOverviewCopyWith<$Res> {
  _$ProductionOverviewCopyWithImpl(this._self, this._then);

  final ProductionOverview _self;
  final $Res Function(ProductionOverview) _then;

/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? stock = null,Object? stockValue = null,Object? expiring7 = null,Object? expiring30 = null,Object? expired = null,Object? onControl = null,Object? belowMin = null,Object? openOrders = null,Object? overdueOrders = null,Object? lowDays = null,}) {
  return _then(ProductionOverview(
stock: null == stock ? _self.stock : stock // ignore: cast_nullable_to_non_nullable
as List<WarehouseValue>,stockValue: null == stockValue ? _self.stockValue : stockValue // ignore: cast_nullable_to_non_nullable
as double,expiring7: null == expiring7 ? _self.expiring7 : expiring7 // ignore: cast_nullable_to_non_nullable
as CountValue,expiring30: null == expiring30 ? _self.expiring30 : expiring30 // ignore: cast_nullable_to_non_nullable
as CountValue,expired: null == expired ? _self.expired : expired // ignore: cast_nullable_to_non_nullable
as CountValue,onControl: null == onControl ? _self.onControl : onControl // ignore: cast_nullable_to_non_nullable
as CountValue,belowMin: null == belowMin ? _self.belowMin : belowMin // ignore: cast_nullable_to_non_nullable
as int,openOrders: null == openOrders ? _self.openOrders : openOrders // ignore: cast_nullable_to_non_nullable
as int,overdueOrders: null == overdueOrders ? _self.overdueOrders : overdueOrders // ignore: cast_nullable_to_non_nullable
as int,lowDays: null == lowDays ? _self.lowDays : lowDays // ignore: cast_nullable_to_non_nullable
as List<LowDays>,
  ));
}
/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get expiring7 {
  
  return $CountValueCopyWith<$Res>(_self.expiring7, (value) {
    return _then(_self.copyWith(expiring7: value));
  });
}/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get expiring30 {
  
  return $CountValueCopyWith<$Res>(_self.expiring30, (value) {
    return _then(_self.copyWith(expiring30: value));
  });
}/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get expired {
  
  return $CountValueCopyWith<$Res>(_self.expired, (value) {
    return _then(_self.copyWith(expired: value));
  });
}/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get onControl {
  
  return $CountValueCopyWith<$Res>(_self.onControl, (value) {
    return _then(_self.copyWith(onControl: value));
  });
}
}


/// Adds pattern-matching-related methods to [ProductionOverview].
extension ProductionOverviewPatterns on ProductionOverview {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProductionOverview value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProductionOverview() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProductionOverview value)  $default,){
final _that = this;
switch (_that) {
case _ProductionOverview():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProductionOverview value)?  $default,){
final _that = this;
switch (_that) {
case _ProductionOverview() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( List<WarehouseValue> stock,  double stockValue,  CountValue expiring7,  CountValue expiring30,  CountValue expired,  CountValue onControl,  int belowMin,  int openOrders,  int overdueOrders,  List<LowDays> lowDays)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ProductionOverview() when $default != null:
return $default(_that.stock,_that.stockValue,_that.expiring7,_that.expiring30,_that.expired,_that.onControl,_that.belowMin,_that.openOrders,_that.overdueOrders,_that.lowDays);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( List<WarehouseValue> stock,  double stockValue,  CountValue expiring7,  CountValue expiring30,  CountValue expired,  CountValue onControl,  int belowMin,  int openOrders,  int overdueOrders,  List<LowDays> lowDays)  $default,) {final _that = this;
switch (_that) {
case _ProductionOverview():
return $default(_that.stock,_that.stockValue,_that.expiring7,_that.expiring30,_that.expired,_that.onControl,_that.belowMin,_that.openOrders,_that.overdueOrders,_that.lowDays);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( List<WarehouseValue> stock,  double stockValue,  CountValue expiring7,  CountValue expiring30,  CountValue expired,  CountValue onControl,  int belowMin,  int openOrders,  int overdueOrders,  List<LowDays> lowDays)?  $default,) {final _that = this;
switch (_that) {
case _ProductionOverview() when $default != null:
return $default(_that.stock,_that.stockValue,_that.expiring7,_that.expiring30,_that.expired,_that.onControl,_that.belowMin,_that.openOrders,_that.overdueOrders,_that.lowDays);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProductionOverview implements ProductionOverview {
  const _ProductionOverview({ List<WarehouseValue> stock = const <WarehouseValue>[], this.stockValue = 0, this.expiring7 = const CountValue(), this.expiring30 = const CountValue(), this.expired = const CountValue(), this.onControl = const CountValue(), this.belowMin = 0, this.openOrders = 0, this.overdueOrders = 0,  List<LowDays> lowDays = const <LowDays>[]}): _stock = stock,_lowDays = lowDays;
  factory _ProductionOverview.fromJson(Map<String, dynamic> json) => _$ProductionOverviewFromJson(json);

 final  List<WarehouseValue> _stock;
@override@JsonKey() List<WarehouseValue> get stock {
  if (_stock is EqualUnmodifiableListView) return _stock;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_stock);
}

@override@JsonKey() final  double stockValue;
@override@JsonKey() final  CountValue expiring7;
@override@JsonKey() final  CountValue expiring30;
@override@JsonKey() final  CountValue expired;
@override@JsonKey() final  CountValue onControl;
@override@JsonKey() final  int belowMin;
@override@JsonKey() final  int openOrders;
@override@JsonKey() final  int overdueOrders;
 final  List<LowDays> _lowDays;
@override@JsonKey() List<LowDays> get lowDays {
  if (_lowDays is EqualUnmodifiableListView) return _lowDays;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_lowDays);
}


/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProductionOverviewCopyWith<_ProductionOverview> get copyWith => __$ProductionOverviewCopyWithImpl<_ProductionOverview>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProductionOverviewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProductionOverview&&const DeepCollectionEquality().equals(other.stock, _stock)&&(identical(other.stockValue, stockValue) || other.stockValue == stockValue)&&(identical(other.expiring7, expiring7) || other.expiring7 == expiring7)&&(identical(other.expiring30, expiring30) || other.expiring30 == expiring30)&&(identical(other.expired, expired) || other.expired == expired)&&(identical(other.onControl, onControl) || other.onControl == onControl)&&(identical(other.belowMin, belowMin) || other.belowMin == belowMin)&&(identical(other.openOrders, openOrders) || other.openOrders == openOrders)&&(identical(other.overdueOrders, overdueOrders) || other.overdueOrders == overdueOrders)&&const DeepCollectionEquality().equals(other.lowDays, _lowDays));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,const DeepCollectionEquality().hash(_stock),stockValue,expiring7,expiring30,expired,onControl,belowMin,openOrders,overdueOrders,const DeepCollectionEquality().hash(_lowDays));
}

@override
String toString() {
    return 'ProductionOverview(stock: $stock, stockValue: $stockValue, expiring7: $expiring7, expiring30: $expiring30, expired: $expired, onControl: $onControl, belowMin: $belowMin, openOrders: $openOrders, overdueOrders: $overdueOrders, lowDays: $lowDays)';
}


}

/// @nodoc
abstract mixin class _$ProductionOverviewCopyWith<$Res> implements $ProductionOverviewCopyWith<$Res> {
  factory _$ProductionOverviewCopyWith(_ProductionOverview value, $Res Function(_ProductionOverview) _then) = __$ProductionOverviewCopyWithImpl;
@override @useResult
$Res call({
 List<WarehouseValue> stock, double stockValue, CountValue expiring7, CountValue expiring30, CountValue expired, CountValue onControl, int belowMin, int openOrders, int overdueOrders, List<LowDays> lowDays
});


@override $CountValueCopyWith<$Res> get expiring7;@override $CountValueCopyWith<$Res> get expiring30;@override $CountValueCopyWith<$Res> get expired;@override $CountValueCopyWith<$Res> get onControl;

}
/// @nodoc
class __$ProductionOverviewCopyWithImpl<$Res>
    implements _$ProductionOverviewCopyWith<$Res> {
  __$ProductionOverviewCopyWithImpl(this._self, this._then);

  final _ProductionOverview _self;
  final $Res Function(_ProductionOverview) _then;

/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? stock = null,Object? stockValue = null,Object? expiring7 = null,Object? expiring30 = null,Object? expired = null,Object? onControl = null,Object? belowMin = null,Object? openOrders = null,Object? overdueOrders = null,Object? lowDays = null,}) {
  return _then(_ProductionOverview(
stock: null == stock ? _self._stock : stock // ignore: cast_nullable_to_non_nullable
as List<WarehouseValue>,stockValue: null == stockValue ? _self.stockValue : stockValue // ignore: cast_nullable_to_non_nullable
as double,expiring7: null == expiring7 ? _self.expiring7 : expiring7 // ignore: cast_nullable_to_non_nullable
as CountValue,expiring30: null == expiring30 ? _self.expiring30 : expiring30 // ignore: cast_nullable_to_non_nullable
as CountValue,expired: null == expired ? _self.expired : expired // ignore: cast_nullable_to_non_nullable
as CountValue,onControl: null == onControl ? _self.onControl : onControl // ignore: cast_nullable_to_non_nullable
as CountValue,belowMin: null == belowMin ? _self.belowMin : belowMin // ignore: cast_nullable_to_non_nullable
as int,openOrders: null == openOrders ? _self.openOrders : openOrders // ignore: cast_nullable_to_non_nullable
as int,overdueOrders: null == overdueOrders ? _self.overdueOrders : overdueOrders // ignore: cast_nullable_to_non_nullable
as int,lowDays: null == lowDays ? _self._lowDays : lowDays // ignore: cast_nullable_to_non_nullable
as List<LowDays>,
  ));
}

/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get expiring7 {
  
  return $CountValueCopyWith<$Res>(_self.expiring7, (value) {
    return _then(_self.copyWith(expiring7: value));
  });
}/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get expiring30 {
  
  return $CountValueCopyWith<$Res>(_self.expiring30, (value) {
    return _then(_self.copyWith(expiring30: value));
  });
}/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get expired {
  
  return $CountValueCopyWith<$Res>(_self.expired, (value) {
    return _then(_self.copyWith(expired: value));
  });
}/// Create a copy of ProductionOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$CountValueCopyWith<$Res> get onControl {
  
  return $CountValueCopyWith<$Res>(_self.onControl, (value) {
    return _then(_self.copyWith(onControl: value));
  });
}
}


/// @nodoc
mixin _$ProductionOrderRow {

 String get id; String get number; String get recipeName; String get outputUnit; double get plannedQty; double? get outputQty; String get plannedDate; String get status; String get departmentName; String get warehouseName; String? get responsibleName; bool get overconsumed;
/// Create a copy of ProductionOrderRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProductionOrderRowCopyWith<ProductionOrderRow> get copyWith => _$ProductionOrderRowCopyWithImpl<ProductionOrderRow>(this as ProductionOrderRow, _$identity);

  /// Serializes this ProductionOrderRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProductionOrderRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProductionOrderRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.number, _this.number) || other.number == _this.number)&&(identical(other.recipeName, _this.recipeName) || other.recipeName == _this.recipeName)&&(identical(other.outputUnit, _this.outputUnit) || other.outputUnit == _this.outputUnit)&&(identical(other.plannedQty, _this.plannedQty) || other.plannedQty == _this.plannedQty)&&(identical(other.outputQty, _this.outputQty) || other.outputQty == _this.outputQty)&&(identical(other.plannedDate, _this.plannedDate) || other.plannedDate == _this.plannedDate)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&(identical(other.warehouseName, _this.warehouseName) || other.warehouseName == _this.warehouseName)&&(identical(other.responsibleName, _this.responsibleName) || other.responsibleName == _this.responsibleName)&&(identical(other.overconsumed, _this.overconsumed) || other.overconsumed == _this.overconsumed));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProductionOrderRow;
  return Object.hash(runtimeType,_this.id,_this.number,_this.recipeName,_this.outputUnit,_this.plannedQty,_this.outputQty,_this.plannedDate,_this.status,_this.departmentName,_this.warehouseName,_this.responsibleName,_this.overconsumed);
}

@override
String toString() {
  final _this = this as ProductionOrderRow;
  return 'ProductionOrderRow(id: ${_this.id}, number: ${_this.number}, recipeName: ${_this.recipeName}, outputUnit: ${_this.outputUnit}, plannedQty: ${_this.plannedQty}, outputQty: ${_this.outputQty}, plannedDate: ${_this.plannedDate}, status: ${_this.status}, departmentName: ${_this.departmentName}, warehouseName: ${_this.warehouseName}, responsibleName: ${_this.responsibleName}, overconsumed: ${_this.overconsumed})';
}


}

/// @nodoc
abstract mixin class $ProductionOrderRowCopyWith<$Res>  {
  factory $ProductionOrderRowCopyWith(ProductionOrderRow value, $Res Function(ProductionOrderRow) _then) = _$ProductionOrderRowCopyWithImpl;
@useResult
$Res call({
 String id, String number, String recipeName, String outputUnit, double plannedQty, double? outputQty, String plannedDate, String status, String departmentName, String warehouseName, String? responsibleName, bool overconsumed
});




}
/// @nodoc
class _$ProductionOrderRowCopyWithImpl<$Res>
    implements $ProductionOrderRowCopyWith<$Res> {
  _$ProductionOrderRowCopyWithImpl(this._self, this._then);

  final ProductionOrderRow _self;
  final $Res Function(ProductionOrderRow) _then;

/// Create a copy of ProductionOrderRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? number = null,Object? recipeName = null,Object? outputUnit = null,Object? plannedQty = null,Object? outputQty = freezed,Object? plannedDate = null,Object? status = null,Object? departmentName = null,Object? warehouseName = null,Object? responsibleName = freezed,Object? overconsumed = null,}) {
  return _then(ProductionOrderRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,recipeName: null == recipeName ? _self.recipeName : recipeName // ignore: cast_nullable_to_non_nullable
as String,outputUnit: null == outputUnit ? _self.outputUnit : outputUnit // ignore: cast_nullable_to_non_nullable
as String,plannedQty: null == plannedQty ? _self.plannedQty : plannedQty // ignore: cast_nullable_to_non_nullable
as double,outputQty: freezed == outputQty ? _self.outputQty : outputQty // ignore: cast_nullable_to_non_nullable
as double?,plannedDate: null == plannedDate ? _self.plannedDate : plannedDate // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,warehouseName: null == warehouseName ? _self.warehouseName : warehouseName // ignore: cast_nullable_to_non_nullable
as String,responsibleName: freezed == responsibleName ? _self.responsibleName : responsibleName // ignore: cast_nullable_to_non_nullable
as String?,overconsumed: null == overconsumed ? _self.overconsumed : overconsumed // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [ProductionOrderRow].
extension ProductionOrderRowPatterns on ProductionOrderRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProductionOrderRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProductionOrderRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProductionOrderRow value)  $default,){
final _that = this;
switch (_that) {
case _ProductionOrderRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProductionOrderRow value)?  $default,){
final _that = this;
switch (_that) {
case _ProductionOrderRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String number,  String recipeName,  String outputUnit,  double plannedQty,  double? outputQty,  String plannedDate,  String status,  String departmentName,  String warehouseName,  String? responsibleName,  bool overconsumed)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ProductionOrderRow() when $default != null:
return $default(_that.id,_that.number,_that.recipeName,_that.outputUnit,_that.plannedQty,_that.outputQty,_that.plannedDate,_that.status,_that.departmentName,_that.warehouseName,_that.responsibleName,_that.overconsumed);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String number,  String recipeName,  String outputUnit,  double plannedQty,  double? outputQty,  String plannedDate,  String status,  String departmentName,  String warehouseName,  String? responsibleName,  bool overconsumed)  $default,) {final _that = this;
switch (_that) {
case _ProductionOrderRow():
return $default(_that.id,_that.number,_that.recipeName,_that.outputUnit,_that.plannedQty,_that.outputQty,_that.plannedDate,_that.status,_that.departmentName,_that.warehouseName,_that.responsibleName,_that.overconsumed);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String number,  String recipeName,  String outputUnit,  double plannedQty,  double? outputQty,  String plannedDate,  String status,  String departmentName,  String warehouseName,  String? responsibleName,  bool overconsumed)?  $default,) {final _that = this;
switch (_that) {
case _ProductionOrderRow() when $default != null:
return $default(_that.id,_that.number,_that.recipeName,_that.outputUnit,_that.plannedQty,_that.outputQty,_that.plannedDate,_that.status,_that.departmentName,_that.warehouseName,_that.responsibleName,_that.overconsumed);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProductionOrderRow implements ProductionOrderRow {
  const _ProductionOrderRow({required this.id, required this.number, this.recipeName = '', this.outputUnit = '', this.plannedQty = 0, this.outputQty, required this.plannedDate, required this.status, this.departmentName = '', this.warehouseName = '', this.responsibleName, this.overconsumed = false});
  factory _ProductionOrderRow.fromJson(Map<String, dynamic> json) => _$ProductionOrderRowFromJson(json);

@override final  String id;
@override final  String number;
@override@JsonKey() final  String recipeName;
@override@JsonKey() final  String outputUnit;
@override@JsonKey() final  double plannedQty;
@override final  double? outputQty;
@override final  String plannedDate;
@override final  String status;
@override@JsonKey() final  String departmentName;
@override@JsonKey() final  String warehouseName;
@override final  String? responsibleName;
@override@JsonKey() final  bool overconsumed;

/// Create a copy of ProductionOrderRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProductionOrderRowCopyWith<_ProductionOrderRow> get copyWith => __$ProductionOrderRowCopyWithImpl<_ProductionOrderRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProductionOrderRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProductionOrderRow&&(identical(other.id, id) || other.id == id)&&(identical(other.number, number) || other.number == number)&&(identical(other.recipeName, recipeName) || other.recipeName == recipeName)&&(identical(other.outputUnit, outputUnit) || other.outputUnit == outputUnit)&&(identical(other.plannedQty, plannedQty) || other.plannedQty == plannedQty)&&(identical(other.outputQty, outputQty) || other.outputQty == outputQty)&&(identical(other.plannedDate, plannedDate) || other.plannedDate == plannedDate)&&(identical(other.status, status) || other.status == status)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&(identical(other.warehouseName, warehouseName) || other.warehouseName == warehouseName)&&(identical(other.responsibleName, responsibleName) || other.responsibleName == responsibleName)&&(identical(other.overconsumed, overconsumed) || other.overconsumed == overconsumed));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,number,recipeName,outputUnit,plannedQty,outputQty,plannedDate,status,departmentName,warehouseName,responsibleName,overconsumed);
}

@override
String toString() {
    return 'ProductionOrderRow(id: $id, number: $number, recipeName: $recipeName, outputUnit: $outputUnit, plannedQty: $plannedQty, outputQty: $outputQty, plannedDate: $plannedDate, status: $status, departmentName: $departmentName, warehouseName: $warehouseName, responsibleName: $responsibleName, overconsumed: $overconsumed)';
}


}

/// @nodoc
abstract mixin class _$ProductionOrderRowCopyWith<$Res> implements $ProductionOrderRowCopyWith<$Res> {
  factory _$ProductionOrderRowCopyWith(_ProductionOrderRow value, $Res Function(_ProductionOrderRow) _then) = __$ProductionOrderRowCopyWithImpl;
@override @useResult
$Res call({
 String id, String number, String recipeName, String outputUnit, double plannedQty, double? outputQty, String plannedDate, String status, String departmentName, String warehouseName, String? responsibleName, bool overconsumed
});




}
/// @nodoc
class __$ProductionOrderRowCopyWithImpl<$Res>
    implements _$ProductionOrderRowCopyWith<$Res> {
  __$ProductionOrderRowCopyWithImpl(this._self, this._then);

  final _ProductionOrderRow _self;
  final $Res Function(_ProductionOrderRow) _then;

/// Create a copy of ProductionOrderRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? number = null,Object? recipeName = null,Object? outputUnit = null,Object? plannedQty = null,Object? outputQty = freezed,Object? plannedDate = null,Object? status = null,Object? departmentName = null,Object? warehouseName = null,Object? responsibleName = freezed,Object? overconsumed = null,}) {
  return _then(_ProductionOrderRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,recipeName: null == recipeName ? _self.recipeName : recipeName // ignore: cast_nullable_to_non_nullable
as String,outputUnit: null == outputUnit ? _self.outputUnit : outputUnit // ignore: cast_nullable_to_non_nullable
as String,plannedQty: null == plannedQty ? _self.plannedQty : plannedQty // ignore: cast_nullable_to_non_nullable
as double,outputQty: freezed == outputQty ? _self.outputQty : outputQty // ignore: cast_nullable_to_non_nullable
as double?,plannedDate: null == plannedDate ? _self.plannedDate : plannedDate // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,warehouseName: null == warehouseName ? _self.warehouseName : warehouseName // ignore: cast_nullable_to_non_nullable
as String,responsibleName: freezed == responsibleName ? _self.responsibleName : responsibleName // ignore: cast_nullable_to_non_nullable
as String?,overconsumed: null == overconsumed ? _self.overconsumed : overconsumed // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$OrderLine {

 String get name; String get sku; String get unitCode; double get required; double get issued; double get returned; double get left; double get available; double? get deviationPercent; bool get overconsumed;
/// Create a copy of OrderLine
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$OrderLineCopyWith<OrderLine> get copyWith => _$OrderLineCopyWithImpl<OrderLine>(this as OrderLine, _$identity);

  /// Serializes this OrderLine to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as OrderLine;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is OrderLine&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.sku, _this.sku) || other.sku == _this.sku)&&(identical(other.unitCode, _this.unitCode) || other.unitCode == _this.unitCode)&&(identical(other.required, _this.required) || other.required == _this.required)&&(identical(other.issued, _this.issued) || other.issued == _this.issued)&&(identical(other.returned, _this.returned) || other.returned == _this.returned)&&(identical(other.left, _this.left) || other.left == _this.left)&&(identical(other.available, _this.available) || other.available == _this.available)&&(identical(other.deviationPercent, _this.deviationPercent) || other.deviationPercent == _this.deviationPercent)&&(identical(other.overconsumed, _this.overconsumed) || other.overconsumed == _this.overconsumed));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as OrderLine;
  return Object.hash(runtimeType,_this.name,_this.sku,_this.unitCode,_this.required,_this.issued,_this.returned,_this.left,_this.available,_this.deviationPercent,_this.overconsumed);
}

@override
String toString() {
  final _this = this as OrderLine;
  return 'OrderLine(name: ${_this.name}, sku: ${_this.sku}, unitCode: ${_this.unitCode}, required: ${_this.required}, issued: ${_this.issued}, returned: ${_this.returned}, left: ${_this.left}, available: ${_this.available}, deviationPercent: ${_this.deviationPercent}, overconsumed: ${_this.overconsumed})';
}


}

/// @nodoc
abstract mixin class $OrderLineCopyWith<$Res>  {
  factory $OrderLineCopyWith(OrderLine value, $Res Function(OrderLine) _then) = _$OrderLineCopyWithImpl;
@useResult
$Res call({
 String name, String sku, String unitCode, double required, double issued, double returned, double left, double available, double? deviationPercent, bool overconsumed
});




}
/// @nodoc
class _$OrderLineCopyWithImpl<$Res>
    implements $OrderLineCopyWith<$Res> {
  _$OrderLineCopyWithImpl(this._self, this._then);

  final OrderLine _self;
  final $Res Function(OrderLine) _then;

/// Create a copy of OrderLine
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? name = null,Object? sku = null,Object? unitCode = null,Object? required = null,Object? issued = null,Object? returned = null,Object? left = null,Object? available = null,Object? deviationPercent = freezed,Object? overconsumed = null,}) {
  return _then(OrderLine(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,required: null == required ? _self.required : required // ignore: cast_nullable_to_non_nullable
as double,issued: null == issued ? _self.issued : issued // ignore: cast_nullable_to_non_nullable
as double,returned: null == returned ? _self.returned : returned // ignore: cast_nullable_to_non_nullable
as double,left: null == left ? _self.left : left // ignore: cast_nullable_to_non_nullable
as double,available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as double,deviationPercent: freezed == deviationPercent ? _self.deviationPercent : deviationPercent // ignore: cast_nullable_to_non_nullable
as double?,overconsumed: null == overconsumed ? _self.overconsumed : overconsumed // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [OrderLine].
extension OrderLinePatterns on OrderLine {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _OrderLine value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _OrderLine() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _OrderLine value)  $default,){
final _that = this;
switch (_that) {
case _OrderLine():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _OrderLine value)?  $default,){
final _that = this;
switch (_that) {
case _OrderLine() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String name,  String sku,  String unitCode,  double required,  double issued,  double returned,  double left,  double available,  double? deviationPercent,  bool overconsumed)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _OrderLine() when $default != null:
return $default(_that.name,_that.sku,_that.unitCode,_that.required,_that.issued,_that.returned,_that.left,_that.available,_that.deviationPercent,_that.overconsumed);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String name,  String sku,  String unitCode,  double required,  double issued,  double returned,  double left,  double available,  double? deviationPercent,  bool overconsumed)  $default,) {final _that = this;
switch (_that) {
case _OrderLine():
return $default(_that.name,_that.sku,_that.unitCode,_that.required,_that.issued,_that.returned,_that.left,_that.available,_that.deviationPercent,_that.overconsumed);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String name,  String sku,  String unitCode,  double required,  double issued,  double returned,  double left,  double available,  double? deviationPercent,  bool overconsumed)?  $default,) {final _that = this;
switch (_that) {
case _OrderLine() when $default != null:
return $default(_that.name,_that.sku,_that.unitCode,_that.required,_that.issued,_that.returned,_that.left,_that.available,_that.deviationPercent,_that.overconsumed);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _OrderLine implements OrderLine {
  const _OrderLine({required this.name, this.sku = '', this.unitCode = '', this.required = 0, this.issued = 0, this.returned = 0, this.left = 0, this.available = 0, this.deviationPercent, this.overconsumed = false});
  factory _OrderLine.fromJson(Map<String, dynamic> json) => _$OrderLineFromJson(json);

@override final  String name;
@override@JsonKey() final  String sku;
@override@JsonKey() final  String unitCode;
@override@JsonKey() final  double required;
@override@JsonKey() final  double issued;
@override@JsonKey() final  double returned;
@override@JsonKey() final  double left;
@override@JsonKey() final  double available;
@override final  double? deviationPercent;
@override@JsonKey() final  bool overconsumed;

/// Create a copy of OrderLine
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$OrderLineCopyWith<_OrderLine> get copyWith => __$OrderLineCopyWithImpl<_OrderLine>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$OrderLineToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _OrderLine&&(identical(other.name, name) || other.name == name)&&(identical(other.sku, sku) || other.sku == sku)&&(identical(other.unitCode, unitCode) || other.unitCode == unitCode)&&(identical(other.required, required) || other.required == required)&&(identical(other.issued, issued) || other.issued == issued)&&(identical(other.returned, returned) || other.returned == returned)&&(identical(other.left, left) || other.left == left)&&(identical(other.available, available) || other.available == available)&&(identical(other.deviationPercent, deviationPercent) || other.deviationPercent == deviationPercent)&&(identical(other.overconsumed, overconsumed) || other.overconsumed == overconsumed));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,name,sku,unitCode,required,issued,returned,left,available,deviationPercent,overconsumed);
}

@override
String toString() {
    return 'OrderLine(name: $name, sku: $sku, unitCode: $unitCode, required: $required, issued: $issued, returned: $returned, left: $left, available: $available, deviationPercent: $deviationPercent, overconsumed: $overconsumed)';
}


}

/// @nodoc
abstract mixin class _$OrderLineCopyWith<$Res> implements $OrderLineCopyWith<$Res> {
  factory _$OrderLineCopyWith(_OrderLine value, $Res Function(_OrderLine) _then) = __$OrderLineCopyWithImpl;
@override @useResult
$Res call({
 String name, String sku, String unitCode, double required, double issued, double returned, double left, double available, double? deviationPercent, bool overconsumed
});




}
/// @nodoc
class __$OrderLineCopyWithImpl<$Res>
    implements _$OrderLineCopyWith<$Res> {
  __$OrderLineCopyWithImpl(this._self, this._then);

  final _OrderLine _self;
  final $Res Function(_OrderLine) _then;

/// Create a copy of OrderLine
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? name = null,Object? sku = null,Object? unitCode = null,Object? required = null,Object? issued = null,Object? returned = null,Object? left = null,Object? available = null,Object? deviationPercent = freezed,Object? overconsumed = null,}) {
  return _then(_OrderLine(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,required: null == required ? _self.required : required // ignore: cast_nullable_to_non_nullable
as double,issued: null == issued ? _self.issued : issued // ignore: cast_nullable_to_non_nullable
as double,returned: null == returned ? _self.returned : returned // ignore: cast_nullable_to_non_nullable
as double,left: null == left ? _self.left : left // ignore: cast_nullable_to_non_nullable
as double,available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as double,deviationPercent: freezed == deviationPercent ? _self.deviationPercent : deviationPercent // ignore: cast_nullable_to_non_nullable
as double?,overconsumed: null == overconsumed ? _self.overconsumed : overconsumed // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$OrderCan {

 bool get release; bool get issue; bool get complete; bool get cancel; bool get receive;
/// Create a copy of OrderCan
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$OrderCanCopyWith<OrderCan> get copyWith => _$OrderCanCopyWithImpl<OrderCan>(this as OrderCan, _$identity);

  /// Serializes this OrderCan to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as OrderCan;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is OrderCan&&(identical(other.release, _this.release) || other.release == _this.release)&&(identical(other.issue, _this.issue) || other.issue == _this.issue)&&(identical(other.complete, _this.complete) || other.complete == _this.complete)&&(identical(other.cancel, _this.cancel) || other.cancel == _this.cancel)&&(identical(other.receive, _this.receive) || other.receive == _this.receive));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as OrderCan;
  return Object.hash(runtimeType,_this.release,_this.issue,_this.complete,_this.cancel,_this.receive);
}

@override
String toString() {
  final _this = this as OrderCan;
  return 'OrderCan(release: ${_this.release}, issue: ${_this.issue}, complete: ${_this.complete}, cancel: ${_this.cancel}, receive: ${_this.receive})';
}


}

/// @nodoc
abstract mixin class $OrderCanCopyWith<$Res>  {
  factory $OrderCanCopyWith(OrderCan value, $Res Function(OrderCan) _then) = _$OrderCanCopyWithImpl;
@useResult
$Res call({
 bool release, bool issue, bool complete, bool cancel, bool receive
});




}
/// @nodoc
class _$OrderCanCopyWithImpl<$Res>
    implements $OrderCanCopyWith<$Res> {
  _$OrderCanCopyWithImpl(this._self, this._then);

  final OrderCan _self;
  final $Res Function(OrderCan) _then;

/// Create a copy of OrderCan
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? release = null,Object? issue = null,Object? complete = null,Object? cancel = null,Object? receive = null,}) {
  return _then(OrderCan(
release: null == release ? _self.release : release // ignore: cast_nullable_to_non_nullable
as bool,issue: null == issue ? _self.issue : issue // ignore: cast_nullable_to_non_nullable
as bool,complete: null == complete ? _self.complete : complete // ignore: cast_nullable_to_non_nullable
as bool,cancel: null == cancel ? _self.cancel : cancel // ignore: cast_nullable_to_non_nullable
as bool,receive: null == receive ? _self.receive : receive // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [OrderCan].
extension OrderCanPatterns on OrderCan {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _OrderCan value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _OrderCan() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _OrderCan value)  $default,){
final _that = this;
switch (_that) {
case _OrderCan():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _OrderCan value)?  $default,){
final _that = this;
switch (_that) {
case _OrderCan() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( bool release,  bool issue,  bool complete,  bool cancel,  bool receive)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _OrderCan() when $default != null:
return $default(_that.release,_that.issue,_that.complete,_that.cancel,_that.receive);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( bool release,  bool issue,  bool complete,  bool cancel,  bool receive)  $default,) {final _that = this;
switch (_that) {
case _OrderCan():
return $default(_that.release,_that.issue,_that.complete,_that.cancel,_that.receive);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( bool release,  bool issue,  bool complete,  bool cancel,  bool receive)?  $default,) {final _that = this;
switch (_that) {
case _OrderCan() when $default != null:
return $default(_that.release,_that.issue,_that.complete,_that.cancel,_that.receive);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _OrderCan implements OrderCan {
  const _OrderCan({this.release = false, this.issue = false, this.complete = false, this.cancel = false, this.receive = false});
  factory _OrderCan.fromJson(Map<String, dynamic> json) => _$OrderCanFromJson(json);

@override@JsonKey() final  bool release;
@override@JsonKey() final  bool issue;
@override@JsonKey() final  bool complete;
@override@JsonKey() final  bool cancel;
@override@JsonKey() final  bool receive;

/// Create a copy of OrderCan
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$OrderCanCopyWith<_OrderCan> get copyWith => __$OrderCanCopyWithImpl<_OrderCan>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$OrderCanToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _OrderCan&&(identical(other.release, release) || other.release == release)&&(identical(other.issue, issue) || other.issue == issue)&&(identical(other.complete, complete) || other.complete == complete)&&(identical(other.cancel, cancel) || other.cancel == cancel)&&(identical(other.receive, receive) || other.receive == receive));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,release,issue,complete,cancel,receive);
}

@override
String toString() {
    return 'OrderCan(release: $release, issue: $issue, complete: $complete, cancel: $cancel, receive: $receive)';
}


}

/// @nodoc
abstract mixin class _$OrderCanCopyWith<$Res> implements $OrderCanCopyWith<$Res> {
  factory _$OrderCanCopyWith(_OrderCan value, $Res Function(_OrderCan) _then) = __$OrderCanCopyWithImpl;
@override @useResult
$Res call({
 bool release, bool issue, bool complete, bool cancel, bool receive
});




}
/// @nodoc
class __$OrderCanCopyWithImpl<$Res>
    implements _$OrderCanCopyWith<$Res> {
  __$OrderCanCopyWithImpl(this._self, this._then);

  final _OrderCan _self;
  final $Res Function(_OrderCan) _then;

/// Create a copy of OrderCan
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? release = null,Object? issue = null,Object? complete = null,Object? cancel = null,Object? receive = null,}) {
  return _then(_OrderCan(
release: null == release ? _self.release : release // ignore: cast_nullable_to_non_nullable
as bool,issue: null == issue ? _self.issue : issue // ignore: cast_nullable_to_non_nullable
as bool,complete: null == complete ? _self.complete : complete // ignore: cast_nullable_to_non_nullable
as bool,cancel: null == cancel ? _self.cancel : cancel // ignore: cast_nullable_to_non_nullable
as bool,receive: null == receive ? _self.receive : receive // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$HistoryEvent {

 String get id; String get text; String? get actorName; String get createdAt; String? get comment;
/// Create a copy of HistoryEvent
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$HistoryEventCopyWith<HistoryEvent> get copyWith => _$HistoryEventCopyWithImpl<HistoryEvent>(this as HistoryEvent, _$identity);

  /// Serializes this HistoryEvent to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as HistoryEvent;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is HistoryEvent&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.text, _this.text) || other.text == _this.text)&&(identical(other.actorName, _this.actorName) || other.actorName == _this.actorName)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt)&&(identical(other.comment, _this.comment) || other.comment == _this.comment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as HistoryEvent;
  return Object.hash(runtimeType,_this.id,_this.text,_this.actorName,_this.createdAt,_this.comment);
}

@override
String toString() {
  final _this = this as HistoryEvent;
  return 'HistoryEvent(id: ${_this.id}, text: ${_this.text}, actorName: ${_this.actorName}, createdAt: ${_this.createdAt}, comment: ${_this.comment})';
}


}

/// @nodoc
abstract mixin class $HistoryEventCopyWith<$Res>  {
  factory $HistoryEventCopyWith(HistoryEvent value, $Res Function(HistoryEvent) _then) = _$HistoryEventCopyWithImpl;
@useResult
$Res call({
 String id, String text, String? actorName, String createdAt, String? comment
});




}
/// @nodoc
class _$HistoryEventCopyWithImpl<$Res>
    implements $HistoryEventCopyWith<$Res> {
  _$HistoryEventCopyWithImpl(this._self, this._then);

  final HistoryEvent _self;
  final $Res Function(HistoryEvent) _then;

/// Create a copy of HistoryEvent
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? text = null,Object? actorName = freezed,Object? createdAt = null,Object? comment = freezed,}) {
  return _then(HistoryEvent(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,text: null == text ? _self.text : text // ignore: cast_nullable_to_non_nullable
as String,actorName: freezed == actorName ? _self.actorName : actorName // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [HistoryEvent].
extension HistoryEventPatterns on HistoryEvent {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _HistoryEvent value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _HistoryEvent() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _HistoryEvent value)  $default,){
final _that = this;
switch (_that) {
case _HistoryEvent():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _HistoryEvent value)?  $default,){
final _that = this;
switch (_that) {
case _HistoryEvent() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String text,  String? actorName,  String createdAt,  String? comment)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _HistoryEvent() when $default != null:
return $default(_that.id,_that.text,_that.actorName,_that.createdAt,_that.comment);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String text,  String? actorName,  String createdAt,  String? comment)  $default,) {final _that = this;
switch (_that) {
case _HistoryEvent():
return $default(_that.id,_that.text,_that.actorName,_that.createdAt,_that.comment);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String text,  String? actorName,  String createdAt,  String? comment)?  $default,) {final _that = this;
switch (_that) {
case _HistoryEvent() when $default != null:
return $default(_that.id,_that.text,_that.actorName,_that.createdAt,_that.comment);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _HistoryEvent implements HistoryEvent {
  const _HistoryEvent({required this.id, this.text = '', this.actorName, required this.createdAt, this.comment});
  factory _HistoryEvent.fromJson(Map<String, dynamic> json) => _$HistoryEventFromJson(json);

@override final  String id;
@override@JsonKey() final  String text;
@override final  String? actorName;
@override final  String createdAt;
@override final  String? comment;

/// Create a copy of HistoryEvent
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$HistoryEventCopyWith<_HistoryEvent> get copyWith => __$HistoryEventCopyWithImpl<_HistoryEvent>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$HistoryEventToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _HistoryEvent&&(identical(other.id, id) || other.id == id)&&(identical(other.text, text) || other.text == text)&&(identical(other.actorName, actorName) || other.actorName == actorName)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt)&&(identical(other.comment, comment) || other.comment == comment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,text,actorName,createdAt,comment);
}

@override
String toString() {
    return 'HistoryEvent(id: $id, text: $text, actorName: $actorName, createdAt: $createdAt, comment: $comment)';
}


}

/// @nodoc
abstract mixin class _$HistoryEventCopyWith<$Res> implements $HistoryEventCopyWith<$Res> {
  factory _$HistoryEventCopyWith(_HistoryEvent value, $Res Function(_HistoryEvent) _then) = __$HistoryEventCopyWithImpl;
@override @useResult
$Res call({
 String id, String text, String? actorName, String createdAt, String? comment
});




}
/// @nodoc
class __$HistoryEventCopyWithImpl<$Res>
    implements _$HistoryEventCopyWith<$Res> {
  __$HistoryEventCopyWithImpl(this._self, this._then);

  final _HistoryEvent _self;
  final $Res Function(_HistoryEvent) _then;

/// Create a copy of HistoryEvent
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? text = null,Object? actorName = freezed,Object? createdAt = null,Object? comment = freezed,}) {
  return _then(_HistoryEvent(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,text: null == text ? _self.text : text // ignore: cast_nullable_to_non_nullable
as String,actorName: freezed == actorName ? _self.actorName : actorName // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$ProductionOrderDetail {

 String get id; String get number; String get recipeName; String get outputUnit; String get departmentName; String get warehouseName; double get plannedQty; String get plannedDate; String get status; double? get outputQty; String? get responsibleName; String? get comment;/// Версия — отправляется при закрытии (конфликт → 409, экран перечитывает).
 int get version; double get thresholdPercent; List<OrderLine> get lines; List<HistoryEvent> get history; OrderCan get can; int get shiftsOpen; double? get shiftsGoodOutput;
/// Create a copy of ProductionOrderDetail
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ProductionOrderDetailCopyWith<ProductionOrderDetail> get copyWith => _$ProductionOrderDetailCopyWithImpl<ProductionOrderDetail>(this as ProductionOrderDetail, _$identity);

  /// Serializes this ProductionOrderDetail to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ProductionOrderDetail;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ProductionOrderDetail&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.number, _this.number) || other.number == _this.number)&&(identical(other.recipeName, _this.recipeName) || other.recipeName == _this.recipeName)&&(identical(other.outputUnit, _this.outputUnit) || other.outputUnit == _this.outputUnit)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&(identical(other.warehouseName, _this.warehouseName) || other.warehouseName == _this.warehouseName)&&(identical(other.plannedQty, _this.plannedQty) || other.plannedQty == _this.plannedQty)&&(identical(other.plannedDate, _this.plannedDate) || other.plannedDate == _this.plannedDate)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.outputQty, _this.outputQty) || other.outputQty == _this.outputQty)&&(identical(other.responsibleName, _this.responsibleName) || other.responsibleName == _this.responsibleName)&&(identical(other.comment, _this.comment) || other.comment == _this.comment)&&(identical(other.version, _this.version) || other.version == _this.version)&&(identical(other.thresholdPercent, _this.thresholdPercent) || other.thresholdPercent == _this.thresholdPercent)&&const DeepCollectionEquality().equals(other.lines, _this.lines)&&const DeepCollectionEquality().equals(other.history, _this.history)&&(identical(other.can, _this.can) || other.can == _this.can)&&(identical(other.shiftsOpen, _this.shiftsOpen) || other.shiftsOpen == _this.shiftsOpen)&&(identical(other.shiftsGoodOutput, _this.shiftsGoodOutput) || other.shiftsGoodOutput == _this.shiftsGoodOutput));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ProductionOrderDetail;
  return Object.hashAll([runtimeType,_this.id,_this.number,_this.recipeName,_this.outputUnit,_this.departmentName,_this.warehouseName,_this.plannedQty,_this.plannedDate,_this.status,_this.outputQty,_this.responsibleName,_this.comment,_this.version,_this.thresholdPercent,const DeepCollectionEquality().hash(_this.lines),const DeepCollectionEquality().hash(_this.history),_this.can,_this.shiftsOpen,_this.shiftsGoodOutput]);
}

@override
String toString() {
  final _this = this as ProductionOrderDetail;
  return 'ProductionOrderDetail(id: ${_this.id}, number: ${_this.number}, recipeName: ${_this.recipeName}, outputUnit: ${_this.outputUnit}, departmentName: ${_this.departmentName}, warehouseName: ${_this.warehouseName}, plannedQty: ${_this.plannedQty}, plannedDate: ${_this.plannedDate}, status: ${_this.status}, outputQty: ${_this.outputQty}, responsibleName: ${_this.responsibleName}, comment: ${_this.comment}, version: ${_this.version}, thresholdPercent: ${_this.thresholdPercent}, lines: ${_this.lines}, history: ${_this.history}, can: ${_this.can}, shiftsOpen: ${_this.shiftsOpen}, shiftsGoodOutput: ${_this.shiftsGoodOutput})';
}


}

/// @nodoc
abstract mixin class $ProductionOrderDetailCopyWith<$Res>  {
  factory $ProductionOrderDetailCopyWith(ProductionOrderDetail value, $Res Function(ProductionOrderDetail) _then) = _$ProductionOrderDetailCopyWithImpl;
@useResult
$Res call({
 String id, String number, String recipeName, String outputUnit, String departmentName, String warehouseName, double plannedQty, String plannedDate, String status, double? outputQty, String? responsibleName, String? comment, int version, double thresholdPercent, List<OrderLine> lines, List<HistoryEvent> history, OrderCan can, int shiftsOpen, double? shiftsGoodOutput
});


$OrderCanCopyWith<$Res> get can;

}
/// @nodoc
class _$ProductionOrderDetailCopyWithImpl<$Res>
    implements $ProductionOrderDetailCopyWith<$Res> {
  _$ProductionOrderDetailCopyWithImpl(this._self, this._then);

  final ProductionOrderDetail _self;
  final $Res Function(ProductionOrderDetail) _then;

/// Create a copy of ProductionOrderDetail
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? number = null,Object? recipeName = null,Object? outputUnit = null,Object? departmentName = null,Object? warehouseName = null,Object? plannedQty = null,Object? plannedDate = null,Object? status = null,Object? outputQty = freezed,Object? responsibleName = freezed,Object? comment = freezed,Object? version = null,Object? thresholdPercent = null,Object? lines = null,Object? history = null,Object? can = null,Object? shiftsOpen = null,Object? shiftsGoodOutput = freezed,}) {
  return _then(ProductionOrderDetail(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,recipeName: null == recipeName ? _self.recipeName : recipeName // ignore: cast_nullable_to_non_nullable
as String,outputUnit: null == outputUnit ? _self.outputUnit : outputUnit // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,warehouseName: null == warehouseName ? _self.warehouseName : warehouseName // ignore: cast_nullable_to_non_nullable
as String,plannedQty: null == plannedQty ? _self.plannedQty : plannedQty // ignore: cast_nullable_to_non_nullable
as double,plannedDate: null == plannedDate ? _self.plannedDate : plannedDate // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,outputQty: freezed == outputQty ? _self.outputQty : outputQty // ignore: cast_nullable_to_non_nullable
as double?,responsibleName: freezed == responsibleName ? _self.responsibleName : responsibleName // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,version: null == version ? _self.version : version // ignore: cast_nullable_to_non_nullable
as int,thresholdPercent: null == thresholdPercent ? _self.thresholdPercent : thresholdPercent // ignore: cast_nullable_to_non_nullable
as double,lines: null == lines ? _self.lines : lines // ignore: cast_nullable_to_non_nullable
as List<OrderLine>,history: null == history ? _self.history : history // ignore: cast_nullable_to_non_nullable
as List<HistoryEvent>,can: null == can ? _self.can : can // ignore: cast_nullable_to_non_nullable
as OrderCan,shiftsOpen: null == shiftsOpen ? _self.shiftsOpen : shiftsOpen // ignore: cast_nullable_to_non_nullable
as int,shiftsGoodOutput: freezed == shiftsGoodOutput ? _self.shiftsGoodOutput : shiftsGoodOutput // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}
/// Create a copy of ProductionOrderDetail
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$OrderCanCopyWith<$Res> get can {
  
  return $OrderCanCopyWith<$Res>(_self.can, (value) {
    return _then(_self.copyWith(can: value));
  });
}
}


/// Adds pattern-matching-related methods to [ProductionOrderDetail].
extension ProductionOrderDetailPatterns on ProductionOrderDetail {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ProductionOrderDetail value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ProductionOrderDetail() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ProductionOrderDetail value)  $default,){
final _that = this;
switch (_that) {
case _ProductionOrderDetail():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ProductionOrderDetail value)?  $default,){
final _that = this;
switch (_that) {
case _ProductionOrderDetail() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String number,  String recipeName,  String outputUnit,  String departmentName,  String warehouseName,  double plannedQty,  String plannedDate,  String status,  double? outputQty,  String? responsibleName,  String? comment,  int version,  double thresholdPercent,  List<OrderLine> lines,  List<HistoryEvent> history,  OrderCan can,  int shiftsOpen,  double? shiftsGoodOutput)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ProductionOrderDetail() when $default != null:
return $default(_that.id,_that.number,_that.recipeName,_that.outputUnit,_that.departmentName,_that.warehouseName,_that.plannedQty,_that.plannedDate,_that.status,_that.outputQty,_that.responsibleName,_that.comment,_that.version,_that.thresholdPercent,_that.lines,_that.history,_that.can,_that.shiftsOpen,_that.shiftsGoodOutput);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String number,  String recipeName,  String outputUnit,  String departmentName,  String warehouseName,  double plannedQty,  String plannedDate,  String status,  double? outputQty,  String? responsibleName,  String? comment,  int version,  double thresholdPercent,  List<OrderLine> lines,  List<HistoryEvent> history,  OrderCan can,  int shiftsOpen,  double? shiftsGoodOutput)  $default,) {final _that = this;
switch (_that) {
case _ProductionOrderDetail():
return $default(_that.id,_that.number,_that.recipeName,_that.outputUnit,_that.departmentName,_that.warehouseName,_that.plannedQty,_that.plannedDate,_that.status,_that.outputQty,_that.responsibleName,_that.comment,_that.version,_that.thresholdPercent,_that.lines,_that.history,_that.can,_that.shiftsOpen,_that.shiftsGoodOutput);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String number,  String recipeName,  String outputUnit,  String departmentName,  String warehouseName,  double plannedQty,  String plannedDate,  String status,  double? outputQty,  String? responsibleName,  String? comment,  int version,  double thresholdPercent,  List<OrderLine> lines,  List<HistoryEvent> history,  OrderCan can,  int shiftsOpen,  double? shiftsGoodOutput)?  $default,) {final _that = this;
switch (_that) {
case _ProductionOrderDetail() when $default != null:
return $default(_that.id,_that.number,_that.recipeName,_that.outputUnit,_that.departmentName,_that.warehouseName,_that.plannedQty,_that.plannedDate,_that.status,_that.outputQty,_that.responsibleName,_that.comment,_that.version,_that.thresholdPercent,_that.lines,_that.history,_that.can,_that.shiftsOpen,_that.shiftsGoodOutput);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ProductionOrderDetail implements ProductionOrderDetail {
  const _ProductionOrderDetail({required this.id, required this.number, this.recipeName = '', this.outputUnit = '', this.departmentName = '', this.warehouseName = '', this.plannedQty = 0, required this.plannedDate, required this.status, this.outputQty, this.responsibleName, this.comment, this.version = 0, this.thresholdPercent = 5,  List<OrderLine> lines = const <OrderLine>[],  List<HistoryEvent> history = const <HistoryEvent>[], this.can = const OrderCan(), this.shiftsOpen = 0, this.shiftsGoodOutput}): _lines = lines,_history = history;
  factory _ProductionOrderDetail.fromJson(Map<String, dynamic> json) => _$ProductionOrderDetailFromJson(json);

@override final  String id;
@override final  String number;
@override@JsonKey() final  String recipeName;
@override@JsonKey() final  String outputUnit;
@override@JsonKey() final  String departmentName;
@override@JsonKey() final  String warehouseName;
@override@JsonKey() final  double plannedQty;
@override final  String plannedDate;
@override final  String status;
@override final  double? outputQty;
@override final  String? responsibleName;
@override final  String? comment;
/// Версия — отправляется при закрытии (конфликт → 409, экран перечитывает).
@override@JsonKey() final  int version;
@override@JsonKey() final  double thresholdPercent;
 final  List<OrderLine> _lines;
@override@JsonKey() List<OrderLine> get lines {
  if (_lines is EqualUnmodifiableListView) return _lines;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_lines);
}

 final  List<HistoryEvent> _history;
@override@JsonKey() List<HistoryEvent> get history {
  if (_history is EqualUnmodifiableListView) return _history;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_history);
}

@override@JsonKey() final  OrderCan can;
@override@JsonKey() final  int shiftsOpen;
@override final  double? shiftsGoodOutput;

/// Create a copy of ProductionOrderDetail
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ProductionOrderDetailCopyWith<_ProductionOrderDetail> get copyWith => __$ProductionOrderDetailCopyWithImpl<_ProductionOrderDetail>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ProductionOrderDetailToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ProductionOrderDetail&&(identical(other.id, id) || other.id == id)&&(identical(other.number, number) || other.number == number)&&(identical(other.recipeName, recipeName) || other.recipeName == recipeName)&&(identical(other.outputUnit, outputUnit) || other.outputUnit == outputUnit)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&(identical(other.warehouseName, warehouseName) || other.warehouseName == warehouseName)&&(identical(other.plannedQty, plannedQty) || other.plannedQty == plannedQty)&&(identical(other.plannedDate, plannedDate) || other.plannedDate == plannedDate)&&(identical(other.status, status) || other.status == status)&&(identical(other.outputQty, outputQty) || other.outputQty == outputQty)&&(identical(other.responsibleName, responsibleName) || other.responsibleName == responsibleName)&&(identical(other.comment, comment) || other.comment == comment)&&(identical(other.version, version) || other.version == version)&&(identical(other.thresholdPercent, thresholdPercent) || other.thresholdPercent == thresholdPercent)&&const DeepCollectionEquality().equals(other.lines, _lines)&&const DeepCollectionEquality().equals(other.history, _history)&&(identical(other.can, can) || other.can == can)&&(identical(other.shiftsOpen, shiftsOpen) || other.shiftsOpen == shiftsOpen)&&(identical(other.shiftsGoodOutput, shiftsGoodOutput) || other.shiftsGoodOutput == shiftsGoodOutput));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hashAll([runtimeType,id,number,recipeName,outputUnit,departmentName,warehouseName,plannedQty,plannedDate,status,outputQty,responsibleName,comment,version,thresholdPercent,const DeepCollectionEquality().hash(_lines),const DeepCollectionEquality().hash(_history),can,shiftsOpen,shiftsGoodOutput]);
}

@override
String toString() {
    return 'ProductionOrderDetail(id: $id, number: $number, recipeName: $recipeName, outputUnit: $outputUnit, departmentName: $departmentName, warehouseName: $warehouseName, plannedQty: $plannedQty, plannedDate: $plannedDate, status: $status, outputQty: $outputQty, responsibleName: $responsibleName, comment: $comment, version: $version, thresholdPercent: $thresholdPercent, lines: $lines, history: $history, can: $can, shiftsOpen: $shiftsOpen, shiftsGoodOutput: $shiftsGoodOutput)';
}


}

/// @nodoc
abstract mixin class _$ProductionOrderDetailCopyWith<$Res> implements $ProductionOrderDetailCopyWith<$Res> {
  factory _$ProductionOrderDetailCopyWith(_ProductionOrderDetail value, $Res Function(_ProductionOrderDetail) _then) = __$ProductionOrderDetailCopyWithImpl;
@override @useResult
$Res call({
 String id, String number, String recipeName, String outputUnit, String departmentName, String warehouseName, double plannedQty, String plannedDate, String status, double? outputQty, String? responsibleName, String? comment, int version, double thresholdPercent, List<OrderLine> lines, List<HistoryEvent> history, OrderCan can, int shiftsOpen, double? shiftsGoodOutput
});


@override $OrderCanCopyWith<$Res> get can;

}
/// @nodoc
class __$ProductionOrderDetailCopyWithImpl<$Res>
    implements _$ProductionOrderDetailCopyWith<$Res> {
  __$ProductionOrderDetailCopyWithImpl(this._self, this._then);

  final _ProductionOrderDetail _self;
  final $Res Function(_ProductionOrderDetail) _then;

/// Create a copy of ProductionOrderDetail
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? number = null,Object? recipeName = null,Object? outputUnit = null,Object? departmentName = null,Object? warehouseName = null,Object? plannedQty = null,Object? plannedDate = null,Object? status = null,Object? outputQty = freezed,Object? responsibleName = freezed,Object? comment = freezed,Object? version = null,Object? thresholdPercent = null,Object? lines = null,Object? history = null,Object? can = null,Object? shiftsOpen = null,Object? shiftsGoodOutput = freezed,}) {
  return _then(_ProductionOrderDetail(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,recipeName: null == recipeName ? _self.recipeName : recipeName // ignore: cast_nullable_to_non_nullable
as String,outputUnit: null == outputUnit ? _self.outputUnit : outputUnit // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,warehouseName: null == warehouseName ? _self.warehouseName : warehouseName // ignore: cast_nullable_to_non_nullable
as String,plannedQty: null == plannedQty ? _self.plannedQty : plannedQty // ignore: cast_nullable_to_non_nullable
as double,plannedDate: null == plannedDate ? _self.plannedDate : plannedDate // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,outputQty: freezed == outputQty ? _self.outputQty : outputQty // ignore: cast_nullable_to_non_nullable
as double?,responsibleName: freezed == responsibleName ? _self.responsibleName : responsibleName // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,version: null == version ? _self.version : version // ignore: cast_nullable_to_non_nullable
as int,thresholdPercent: null == thresholdPercent ? _self.thresholdPercent : thresholdPercent // ignore: cast_nullable_to_non_nullable
as double,lines: null == lines ? _self._lines : lines // ignore: cast_nullable_to_non_nullable
as List<OrderLine>,history: null == history ? _self._history : history // ignore: cast_nullable_to_non_nullable
as List<HistoryEvent>,can: null == can ? _self.can : can // ignore: cast_nullable_to_non_nullable
as OrderCan,shiftsOpen: null == shiftsOpen ? _self.shiftsOpen : shiftsOpen // ignore: cast_nullable_to_non_nullable
as int,shiftsGoodOutput: freezed == shiftsGoodOutput ? _self.shiftsGoodOutput : shiftsGoodOutput // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

/// Create a copy of ProductionOrderDetail
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$OrderCanCopyWith<$Res> get can {
  
  return $OrderCanCopyWith<$Res>(_self.can, (value) {
    return _then(_self.copyWith(can: value));
  });
}
}


/// @nodoc
mixin _$ShiftRow {

 String get id; String get number; String get orderNumber; String get recipeName; String get outputUnit; String get shiftDate; int get shiftNo; String get operatorName;/// Draft, Closed, Approved, Superseded, Cancelled
 String get status; double? get goodOutput; double get defectKg;
/// Create a copy of ShiftRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ShiftRowCopyWith<ShiftRow> get copyWith => _$ShiftRowCopyWithImpl<ShiftRow>(this as ShiftRow, _$identity);

  /// Serializes this ShiftRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ShiftRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ShiftRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.number, _this.number) || other.number == _this.number)&&(identical(other.orderNumber, _this.orderNumber) || other.orderNumber == _this.orderNumber)&&(identical(other.recipeName, _this.recipeName) || other.recipeName == _this.recipeName)&&(identical(other.outputUnit, _this.outputUnit) || other.outputUnit == _this.outputUnit)&&(identical(other.shiftDate, _this.shiftDate) || other.shiftDate == _this.shiftDate)&&(identical(other.shiftNo, _this.shiftNo) || other.shiftNo == _this.shiftNo)&&(identical(other.operatorName, _this.operatorName) || other.operatorName == _this.operatorName)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.goodOutput, _this.goodOutput) || other.goodOutput == _this.goodOutput)&&(identical(other.defectKg, _this.defectKg) || other.defectKg == _this.defectKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ShiftRow;
  return Object.hash(runtimeType,_this.id,_this.number,_this.orderNumber,_this.recipeName,_this.outputUnit,_this.shiftDate,_this.shiftNo,_this.operatorName,_this.status,_this.goodOutput,_this.defectKg);
}

@override
String toString() {
  final _this = this as ShiftRow;
  return 'ShiftRow(id: ${_this.id}, number: ${_this.number}, orderNumber: ${_this.orderNumber}, recipeName: ${_this.recipeName}, outputUnit: ${_this.outputUnit}, shiftDate: ${_this.shiftDate}, shiftNo: ${_this.shiftNo}, operatorName: ${_this.operatorName}, status: ${_this.status}, goodOutput: ${_this.goodOutput}, defectKg: ${_this.defectKg})';
}


}

/// @nodoc
abstract mixin class $ShiftRowCopyWith<$Res>  {
  factory $ShiftRowCopyWith(ShiftRow value, $Res Function(ShiftRow) _then) = _$ShiftRowCopyWithImpl;
@useResult
$Res call({
 String id, String number, String orderNumber, String recipeName, String outputUnit, String shiftDate, int shiftNo, String operatorName, String status, double? goodOutput, double defectKg
});




}
/// @nodoc
class _$ShiftRowCopyWithImpl<$Res>
    implements $ShiftRowCopyWith<$Res> {
  _$ShiftRowCopyWithImpl(this._self, this._then);

  final ShiftRow _self;
  final $Res Function(ShiftRow) _then;

/// Create a copy of ShiftRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? number = null,Object? orderNumber = null,Object? recipeName = null,Object? outputUnit = null,Object? shiftDate = null,Object? shiftNo = null,Object? operatorName = null,Object? status = null,Object? goodOutput = freezed,Object? defectKg = null,}) {
  return _then(ShiftRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,orderNumber: null == orderNumber ? _self.orderNumber : orderNumber // ignore: cast_nullable_to_non_nullable
as String,recipeName: null == recipeName ? _self.recipeName : recipeName // ignore: cast_nullable_to_non_nullable
as String,outputUnit: null == outputUnit ? _self.outputUnit : outputUnit // ignore: cast_nullable_to_non_nullable
as String,shiftDate: null == shiftDate ? _self.shiftDate : shiftDate // ignore: cast_nullable_to_non_nullable
as String,shiftNo: null == shiftNo ? _self.shiftNo : shiftNo // ignore: cast_nullable_to_non_nullable
as int,operatorName: null == operatorName ? _self.operatorName : operatorName // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,goodOutput: freezed == goodOutput ? _self.goodOutput : goodOutput // ignore: cast_nullable_to_non_nullable
as double?,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [ShiftRow].
extension ShiftRowPatterns on ShiftRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ShiftRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ShiftRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ShiftRow value)  $default,){
final _that = this;
switch (_that) {
case _ShiftRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ShiftRow value)?  $default,){
final _that = this;
switch (_that) {
case _ShiftRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String number,  String orderNumber,  String recipeName,  String outputUnit,  String shiftDate,  int shiftNo,  String operatorName,  String status,  double? goodOutput,  double defectKg)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ShiftRow() when $default != null:
return $default(_that.id,_that.number,_that.orderNumber,_that.recipeName,_that.outputUnit,_that.shiftDate,_that.shiftNo,_that.operatorName,_that.status,_that.goodOutput,_that.defectKg);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String number,  String orderNumber,  String recipeName,  String outputUnit,  String shiftDate,  int shiftNo,  String operatorName,  String status,  double? goodOutput,  double defectKg)  $default,) {final _that = this;
switch (_that) {
case _ShiftRow():
return $default(_that.id,_that.number,_that.orderNumber,_that.recipeName,_that.outputUnit,_that.shiftDate,_that.shiftNo,_that.operatorName,_that.status,_that.goodOutput,_that.defectKg);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String number,  String orderNumber,  String recipeName,  String outputUnit,  String shiftDate,  int shiftNo,  String operatorName,  String status,  double? goodOutput,  double defectKg)?  $default,) {final _that = this;
switch (_that) {
case _ShiftRow() when $default != null:
return $default(_that.id,_that.number,_that.orderNumber,_that.recipeName,_that.outputUnit,_that.shiftDate,_that.shiftNo,_that.operatorName,_that.status,_that.goodOutput,_that.defectKg);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ShiftRow implements ShiftRow {
  const _ShiftRow({required this.id, required this.number, this.orderNumber = '', this.recipeName = '', this.outputUnit = '', required this.shiftDate, this.shiftNo = 1, this.operatorName = '', required this.status, this.goodOutput, this.defectKg = 0});
  factory _ShiftRow.fromJson(Map<String, dynamic> json) => _$ShiftRowFromJson(json);

@override final  String id;
@override final  String number;
@override@JsonKey() final  String orderNumber;
@override@JsonKey() final  String recipeName;
@override@JsonKey() final  String outputUnit;
@override final  String shiftDate;
@override@JsonKey() final  int shiftNo;
@override@JsonKey() final  String operatorName;
/// Draft, Closed, Approved, Superseded, Cancelled
@override final  String status;
@override final  double? goodOutput;
@override@JsonKey() final  double defectKg;

/// Create a copy of ShiftRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ShiftRowCopyWith<_ShiftRow> get copyWith => __$ShiftRowCopyWithImpl<_ShiftRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ShiftRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ShiftRow&&(identical(other.id, id) || other.id == id)&&(identical(other.number, number) || other.number == number)&&(identical(other.orderNumber, orderNumber) || other.orderNumber == orderNumber)&&(identical(other.recipeName, recipeName) || other.recipeName == recipeName)&&(identical(other.outputUnit, outputUnit) || other.outputUnit == outputUnit)&&(identical(other.shiftDate, shiftDate) || other.shiftDate == shiftDate)&&(identical(other.shiftNo, shiftNo) || other.shiftNo == shiftNo)&&(identical(other.operatorName, operatorName) || other.operatorName == operatorName)&&(identical(other.status, status) || other.status == status)&&(identical(other.goodOutput, goodOutput) || other.goodOutput == goodOutput)&&(identical(other.defectKg, defectKg) || other.defectKg == defectKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,number,orderNumber,recipeName,outputUnit,shiftDate,shiftNo,operatorName,status,goodOutput,defectKg);
}

@override
String toString() {
    return 'ShiftRow(id: $id, number: $number, orderNumber: $orderNumber, recipeName: $recipeName, outputUnit: $outputUnit, shiftDate: $shiftDate, shiftNo: $shiftNo, operatorName: $operatorName, status: $status, goodOutput: $goodOutput, defectKg: $defectKg)';
}


}

/// @nodoc
abstract mixin class _$ShiftRowCopyWith<$Res> implements $ShiftRowCopyWith<$Res> {
  factory _$ShiftRowCopyWith(_ShiftRow value, $Res Function(_ShiftRow) _then) = __$ShiftRowCopyWithImpl;
@override @useResult
$Res call({
 String id, String number, String orderNumber, String recipeName, String outputUnit, String shiftDate, int shiftNo, String operatorName, String status, double? goodOutput, double defectKg
});




}
/// @nodoc
class __$ShiftRowCopyWithImpl<$Res>
    implements _$ShiftRowCopyWith<$Res> {
  __$ShiftRowCopyWithImpl(this._self, this._then);

  final _ShiftRow _self;
  final $Res Function(_ShiftRow) _then;

/// Create a copy of ShiftRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? number = null,Object? orderNumber = null,Object? recipeName = null,Object? outputUnit = null,Object? shiftDate = null,Object? shiftNo = null,Object? operatorName = null,Object? status = null,Object? goodOutput = freezed,Object? defectKg = null,}) {
  return _then(_ShiftRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,orderNumber: null == orderNumber ? _self.orderNumber : orderNumber // ignore: cast_nullable_to_non_nullable
as String,recipeName: null == recipeName ? _self.recipeName : recipeName // ignore: cast_nullable_to_non_nullable
as String,outputUnit: null == outputUnit ? _self.outputUnit : outputUnit // ignore: cast_nullable_to_non_nullable
as String,shiftDate: null == shiftDate ? _self.shiftDate : shiftDate // ignore: cast_nullable_to_non_nullable
as String,shiftNo: null == shiftNo ? _self.shiftNo : shiftNo // ignore: cast_nullable_to_non_nullable
as int,operatorName: null == operatorName ? _self.operatorName : operatorName // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,goodOutput: freezed == goodOutput ? _self.goodOutput : goodOutput // ignore: cast_nullable_to_non_nullable
as double?,defectKg: null == defectKg ? _self.defectKg : defectKg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$StockRow {

 String get itemId; String get sku; String get name; String get categoryName; String get unitCode; double get onHand; double get reserved; double get available; double get minQty; bool get low; double get expired; String? get nearestExpiry; String? get lastMovementAt; bool get tracksBatches;
/// Create a copy of StockRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$StockRowCopyWith<StockRow> get copyWith => _$StockRowCopyWithImpl<StockRow>(this as StockRow, _$identity);

  /// Serializes this StockRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as StockRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is StockRow&&(identical(other.itemId, _this.itemId) || other.itemId == _this.itemId)&&(identical(other.sku, _this.sku) || other.sku == _this.sku)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.categoryName, _this.categoryName) || other.categoryName == _this.categoryName)&&(identical(other.unitCode, _this.unitCode) || other.unitCode == _this.unitCode)&&(identical(other.onHand, _this.onHand) || other.onHand == _this.onHand)&&(identical(other.reserved, _this.reserved) || other.reserved == _this.reserved)&&(identical(other.available, _this.available) || other.available == _this.available)&&(identical(other.minQty, _this.minQty) || other.minQty == _this.minQty)&&(identical(other.low, _this.low) || other.low == _this.low)&&(identical(other.expired, _this.expired) || other.expired == _this.expired)&&(identical(other.nearestExpiry, _this.nearestExpiry) || other.nearestExpiry == _this.nearestExpiry)&&(identical(other.lastMovementAt, _this.lastMovementAt) || other.lastMovementAt == _this.lastMovementAt)&&(identical(other.tracksBatches, _this.tracksBatches) || other.tracksBatches == _this.tracksBatches));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as StockRow;
  return Object.hash(runtimeType,_this.itemId,_this.sku,_this.name,_this.categoryName,_this.unitCode,_this.onHand,_this.reserved,_this.available,_this.minQty,_this.low,_this.expired,_this.nearestExpiry,_this.lastMovementAt,_this.tracksBatches);
}

@override
String toString() {
  final _this = this as StockRow;
  return 'StockRow(itemId: ${_this.itemId}, sku: ${_this.sku}, name: ${_this.name}, categoryName: ${_this.categoryName}, unitCode: ${_this.unitCode}, onHand: ${_this.onHand}, reserved: ${_this.reserved}, available: ${_this.available}, minQty: ${_this.minQty}, low: ${_this.low}, expired: ${_this.expired}, nearestExpiry: ${_this.nearestExpiry}, lastMovementAt: ${_this.lastMovementAt}, tracksBatches: ${_this.tracksBatches})';
}


}

/// @nodoc
abstract mixin class $StockRowCopyWith<$Res>  {
  factory $StockRowCopyWith(StockRow value, $Res Function(StockRow) _then) = _$StockRowCopyWithImpl;
@useResult
$Res call({
 String itemId, String sku, String name, String categoryName, String unitCode, double onHand, double reserved, double available, double minQty, bool low, double expired, String? nearestExpiry, String? lastMovementAt, bool tracksBatches
});




}
/// @nodoc
class _$StockRowCopyWithImpl<$Res>
    implements $StockRowCopyWith<$Res> {
  _$StockRowCopyWithImpl(this._self, this._then);

  final StockRow _self;
  final $Res Function(StockRow) _then;

/// Create a copy of StockRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? itemId = null,Object? sku = null,Object? name = null,Object? categoryName = null,Object? unitCode = null,Object? onHand = null,Object? reserved = null,Object? available = null,Object? minQty = null,Object? low = null,Object? expired = null,Object? nearestExpiry = freezed,Object? lastMovementAt = freezed,Object? tracksBatches = null,}) {
  return _then(StockRow(
itemId: null == itemId ? _self.itemId : itemId // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,categoryName: null == categoryName ? _self.categoryName : categoryName // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,onHand: null == onHand ? _self.onHand : onHand // ignore: cast_nullable_to_non_nullable
as double,reserved: null == reserved ? _self.reserved : reserved // ignore: cast_nullable_to_non_nullable
as double,available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as double,minQty: null == minQty ? _self.minQty : minQty // ignore: cast_nullable_to_non_nullable
as double,low: null == low ? _self.low : low // ignore: cast_nullable_to_non_nullable
as bool,expired: null == expired ? _self.expired : expired // ignore: cast_nullable_to_non_nullable
as double,nearestExpiry: freezed == nearestExpiry ? _self.nearestExpiry : nearestExpiry // ignore: cast_nullable_to_non_nullable
as String?,lastMovementAt: freezed == lastMovementAt ? _self.lastMovementAt : lastMovementAt // ignore: cast_nullable_to_non_nullable
as String?,tracksBatches: null == tracksBatches ? _self.tracksBatches : tracksBatches // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [StockRow].
extension StockRowPatterns on StockRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _StockRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _StockRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _StockRow value)  $default,){
final _that = this;
switch (_that) {
case _StockRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _StockRow value)?  $default,){
final _that = this;
switch (_that) {
case _StockRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String itemId,  String sku,  String name,  String categoryName,  String unitCode,  double onHand,  double reserved,  double available,  double minQty,  bool low,  double expired,  String? nearestExpiry,  String? lastMovementAt,  bool tracksBatches)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _StockRow() when $default != null:
return $default(_that.itemId,_that.sku,_that.name,_that.categoryName,_that.unitCode,_that.onHand,_that.reserved,_that.available,_that.minQty,_that.low,_that.expired,_that.nearestExpiry,_that.lastMovementAt,_that.tracksBatches);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String itemId,  String sku,  String name,  String categoryName,  String unitCode,  double onHand,  double reserved,  double available,  double minQty,  bool low,  double expired,  String? nearestExpiry,  String? lastMovementAt,  bool tracksBatches)  $default,) {final _that = this;
switch (_that) {
case _StockRow():
return $default(_that.itemId,_that.sku,_that.name,_that.categoryName,_that.unitCode,_that.onHand,_that.reserved,_that.available,_that.minQty,_that.low,_that.expired,_that.nearestExpiry,_that.lastMovementAt,_that.tracksBatches);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String itemId,  String sku,  String name,  String categoryName,  String unitCode,  double onHand,  double reserved,  double available,  double minQty,  bool low,  double expired,  String? nearestExpiry,  String? lastMovementAt,  bool tracksBatches)?  $default,) {final _that = this;
switch (_that) {
case _StockRow() when $default != null:
return $default(_that.itemId,_that.sku,_that.name,_that.categoryName,_that.unitCode,_that.onHand,_that.reserved,_that.available,_that.minQty,_that.low,_that.expired,_that.nearestExpiry,_that.lastMovementAt,_that.tracksBatches);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _StockRow implements StockRow {
  const _StockRow({required this.itemId, required this.sku, required this.name, this.categoryName = '', this.unitCode = '', this.onHand = 0, this.reserved = 0, this.available = 0, this.minQty = 0, this.low = false, this.expired = 0, this.nearestExpiry, this.lastMovementAt, this.tracksBatches = false});
  factory _StockRow.fromJson(Map<String, dynamic> json) => _$StockRowFromJson(json);

@override final  String itemId;
@override final  String sku;
@override final  String name;
@override@JsonKey() final  String categoryName;
@override@JsonKey() final  String unitCode;
@override@JsonKey() final  double onHand;
@override@JsonKey() final  double reserved;
@override@JsonKey() final  double available;
@override@JsonKey() final  double minQty;
@override@JsonKey() final  bool low;
@override@JsonKey() final  double expired;
@override final  String? nearestExpiry;
@override final  String? lastMovementAt;
@override@JsonKey() final  bool tracksBatches;

/// Create a copy of StockRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$StockRowCopyWith<_StockRow> get copyWith => __$StockRowCopyWithImpl<_StockRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$StockRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _StockRow&&(identical(other.itemId, itemId) || other.itemId == itemId)&&(identical(other.sku, sku) || other.sku == sku)&&(identical(other.name, name) || other.name == name)&&(identical(other.categoryName, categoryName) || other.categoryName == categoryName)&&(identical(other.unitCode, unitCode) || other.unitCode == unitCode)&&(identical(other.onHand, onHand) || other.onHand == onHand)&&(identical(other.reserved, reserved) || other.reserved == reserved)&&(identical(other.available, available) || other.available == available)&&(identical(other.minQty, minQty) || other.minQty == minQty)&&(identical(other.low, low) || other.low == low)&&(identical(other.expired, expired) || other.expired == expired)&&(identical(other.nearestExpiry, nearestExpiry) || other.nearestExpiry == nearestExpiry)&&(identical(other.lastMovementAt, lastMovementAt) || other.lastMovementAt == lastMovementAt)&&(identical(other.tracksBatches, tracksBatches) || other.tracksBatches == tracksBatches));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,itemId,sku,name,categoryName,unitCode,onHand,reserved,available,minQty,low,expired,nearestExpiry,lastMovementAt,tracksBatches);
}

@override
String toString() {
    return 'StockRow(itemId: $itemId, sku: $sku, name: $name, categoryName: $categoryName, unitCode: $unitCode, onHand: $onHand, reserved: $reserved, available: $available, minQty: $minQty, low: $low, expired: $expired, nearestExpiry: $nearestExpiry, lastMovementAt: $lastMovementAt, tracksBatches: $tracksBatches)';
}


}

/// @nodoc
abstract mixin class _$StockRowCopyWith<$Res> implements $StockRowCopyWith<$Res> {
  factory _$StockRowCopyWith(_StockRow value, $Res Function(_StockRow) _then) = __$StockRowCopyWithImpl;
@override @useResult
$Res call({
 String itemId, String sku, String name, String categoryName, String unitCode, double onHand, double reserved, double available, double minQty, bool low, double expired, String? nearestExpiry, String? lastMovementAt, bool tracksBatches
});




}
/// @nodoc
class __$StockRowCopyWithImpl<$Res>
    implements _$StockRowCopyWith<$Res> {
  __$StockRowCopyWithImpl(this._self, this._then);

  final _StockRow _self;
  final $Res Function(_StockRow) _then;

/// Create a copy of StockRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? itemId = null,Object? sku = null,Object? name = null,Object? categoryName = null,Object? unitCode = null,Object? onHand = null,Object? reserved = null,Object? available = null,Object? minQty = null,Object? low = null,Object? expired = null,Object? nearestExpiry = freezed,Object? lastMovementAt = freezed,Object? tracksBatches = null,}) {
  return _then(_StockRow(
itemId: null == itemId ? _self.itemId : itemId // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,categoryName: null == categoryName ? _self.categoryName : categoryName // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,onHand: null == onHand ? _self.onHand : onHand // ignore: cast_nullable_to_non_nullable
as double,reserved: null == reserved ? _self.reserved : reserved // ignore: cast_nullable_to_non_nullable
as double,available: null == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as double,minQty: null == minQty ? _self.minQty : minQty // ignore: cast_nullable_to_non_nullable
as double,low: null == low ? _self.low : low // ignore: cast_nullable_to_non_nullable
as bool,expired: null == expired ? _self.expired : expired // ignore: cast_nullable_to_non_nullable
as double,nearestExpiry: freezed == nearestExpiry ? _self.nearestExpiry : nearestExpiry // ignore: cast_nullable_to_non_nullable
as String?,lastMovementAt: freezed == lastMovementAt ? _self.lastMovementAt : lastMovementAt // ignore: cast_nullable_to_non_nullable
as String?,tracksBatches: null == tracksBatches ? _self.tracksBatches : tracksBatches // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$StockSummary {

 int get items; int get low; int get empty; int get withReserve; int get toIssueRequests; int get expired; int get expiring;
/// Create a copy of StockSummary
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$StockSummaryCopyWith<StockSummary> get copyWith => _$StockSummaryCopyWithImpl<StockSummary>(this as StockSummary, _$identity);

  /// Serializes this StockSummary to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as StockSummary;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is StockSummary&&(identical(other.items, _this.items) || other.items == _this.items)&&(identical(other.low, _this.low) || other.low == _this.low)&&(identical(other.empty, _this.empty) || other.empty == _this.empty)&&(identical(other.withReserve, _this.withReserve) || other.withReserve == _this.withReserve)&&(identical(other.toIssueRequests, _this.toIssueRequests) || other.toIssueRequests == _this.toIssueRequests)&&(identical(other.expired, _this.expired) || other.expired == _this.expired)&&(identical(other.expiring, _this.expiring) || other.expiring == _this.expiring));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as StockSummary;
  return Object.hash(runtimeType,_this.items,_this.low,_this.empty,_this.withReserve,_this.toIssueRequests,_this.expired,_this.expiring);
}

@override
String toString() {
  final _this = this as StockSummary;
  return 'StockSummary(items: ${_this.items}, low: ${_this.low}, empty: ${_this.empty}, withReserve: ${_this.withReserve}, toIssueRequests: ${_this.toIssueRequests}, expired: ${_this.expired}, expiring: ${_this.expiring})';
}


}

/// @nodoc
abstract mixin class $StockSummaryCopyWith<$Res>  {
  factory $StockSummaryCopyWith(StockSummary value, $Res Function(StockSummary) _then) = _$StockSummaryCopyWithImpl;
@useResult
$Res call({
 int items, int low, int empty, int withReserve, int toIssueRequests, int expired, int expiring
});




}
/// @nodoc
class _$StockSummaryCopyWithImpl<$Res>
    implements $StockSummaryCopyWith<$Res> {
  _$StockSummaryCopyWithImpl(this._self, this._then);

  final StockSummary _self;
  final $Res Function(StockSummary) _then;

/// Create a copy of StockSummary
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? items = null,Object? low = null,Object? empty = null,Object? withReserve = null,Object? toIssueRequests = null,Object? expired = null,Object? expiring = null,}) {
  return _then(StockSummary(
items: null == items ? _self.items : items // ignore: cast_nullable_to_non_nullable
as int,low: null == low ? _self.low : low // ignore: cast_nullable_to_non_nullable
as int,empty: null == empty ? _self.empty : empty // ignore: cast_nullable_to_non_nullable
as int,withReserve: null == withReserve ? _self.withReserve : withReserve // ignore: cast_nullable_to_non_nullable
as int,toIssueRequests: null == toIssueRequests ? _self.toIssueRequests : toIssueRequests // ignore: cast_nullable_to_non_nullable
as int,expired: null == expired ? _self.expired : expired // ignore: cast_nullable_to_non_nullable
as int,expiring: null == expiring ? _self.expiring : expiring // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [StockSummary].
extension StockSummaryPatterns on StockSummary {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _StockSummary value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _StockSummary() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _StockSummary value)  $default,){
final _that = this;
switch (_that) {
case _StockSummary():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _StockSummary value)?  $default,){
final _that = this;
switch (_that) {
case _StockSummary() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int items,  int low,  int empty,  int withReserve,  int toIssueRequests,  int expired,  int expiring)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _StockSummary() when $default != null:
return $default(_that.items,_that.low,_that.empty,_that.withReserve,_that.toIssueRequests,_that.expired,_that.expiring);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int items,  int low,  int empty,  int withReserve,  int toIssueRequests,  int expired,  int expiring)  $default,) {final _that = this;
switch (_that) {
case _StockSummary():
return $default(_that.items,_that.low,_that.empty,_that.withReserve,_that.toIssueRequests,_that.expired,_that.expiring);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int items,  int low,  int empty,  int withReserve,  int toIssueRequests,  int expired,  int expiring)?  $default,) {final _that = this;
switch (_that) {
case _StockSummary() when $default != null:
return $default(_that.items,_that.low,_that.empty,_that.withReserve,_that.toIssueRequests,_that.expired,_that.expiring);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _StockSummary implements StockSummary {
  const _StockSummary({this.items = 0, this.low = 0, this.empty = 0, this.withReserve = 0, this.toIssueRequests = 0, this.expired = 0, this.expiring = 0});
  factory _StockSummary.fromJson(Map<String, dynamic> json) => _$StockSummaryFromJson(json);

@override@JsonKey() final  int items;
@override@JsonKey() final  int low;
@override@JsonKey() final  int empty;
@override@JsonKey() final  int withReserve;
@override@JsonKey() final  int toIssueRequests;
@override@JsonKey() final  int expired;
@override@JsonKey() final  int expiring;

/// Create a copy of StockSummary
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$StockSummaryCopyWith<_StockSummary> get copyWith => __$StockSummaryCopyWithImpl<_StockSummary>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$StockSummaryToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _StockSummary&&(identical(other.items, items) || other.items == items)&&(identical(other.low, low) || other.low == low)&&(identical(other.empty, empty) || other.empty == empty)&&(identical(other.withReserve, withReserve) || other.withReserve == withReserve)&&(identical(other.toIssueRequests, toIssueRequests) || other.toIssueRequests == toIssueRequests)&&(identical(other.expired, expired) || other.expired == expired)&&(identical(other.expiring, expiring) || other.expiring == expiring));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,items,low,empty,withReserve,toIssueRequests,expired,expiring);
}

@override
String toString() {
    return 'StockSummary(items: $items, low: $low, empty: $empty, withReserve: $withReserve, toIssueRequests: $toIssueRequests, expired: $expired, expiring: $expiring)';
}


}

/// @nodoc
abstract mixin class _$StockSummaryCopyWith<$Res> implements $StockSummaryCopyWith<$Res> {
  factory _$StockSummaryCopyWith(_StockSummary value, $Res Function(_StockSummary) _then) = __$StockSummaryCopyWithImpl;
@override @useResult
$Res call({
 int items, int low, int empty, int withReserve, int toIssueRequests, int expired, int expiring
});




}
/// @nodoc
class __$StockSummaryCopyWithImpl<$Res>
    implements _$StockSummaryCopyWith<$Res> {
  __$StockSummaryCopyWithImpl(this._self, this._then);

  final _StockSummary _self;
  final $Res Function(_StockSummary) _then;

/// Create a copy of StockSummary
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? items = null,Object? low = null,Object? empty = null,Object? withReserve = null,Object? toIssueRequests = null,Object? expired = null,Object? expiring = null,}) {
  return _then(_StockSummary(
items: null == items ? _self.items : items // ignore: cast_nullable_to_non_nullable
as int,low: null == low ? _self.low : low // ignore: cast_nullable_to_non_nullable
as int,empty: null == empty ? _self.empty : empty // ignore: cast_nullable_to_non_nullable
as int,withReserve: null == withReserve ? _self.withReserve : withReserve // ignore: cast_nullable_to_non_nullable
as int,toIssueRequests: null == toIssueRequests ? _self.toIssueRequests : toIssueRequests // ignore: cast_nullable_to_non_nullable
as int,expired: null == expired ? _self.expired : expired // ignore: cast_nullable_to_non_nullable
as int,expiring: null == expiring ? _self.expiring : expiring // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$MovementRow {

 String get id; String get createdAt; String get documentNumber; String get itemName; String get sku; String get unitCode;/// Receipt, Issue, WriteOff, Adjustment, TransferOut, TransferIn, Reversal, ProductionIssue, ProductionReturn, ProductionOutput…
 String get kind; double get quantity; double get balanceAfter; String? get actorName; String? get departmentName; String? get comment; String? get warehouseName; String? get reversedBy;
/// Create a copy of MovementRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$MovementRowCopyWith<MovementRow> get copyWith => _$MovementRowCopyWithImpl<MovementRow>(this as MovementRow, _$identity);

  /// Serializes this MovementRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as MovementRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is MovementRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt)&&(identical(other.documentNumber, _this.documentNumber) || other.documentNumber == _this.documentNumber)&&(identical(other.itemName, _this.itemName) || other.itemName == _this.itemName)&&(identical(other.sku, _this.sku) || other.sku == _this.sku)&&(identical(other.unitCode, _this.unitCode) || other.unitCode == _this.unitCode)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.quantity, _this.quantity) || other.quantity == _this.quantity)&&(identical(other.balanceAfter, _this.balanceAfter) || other.balanceAfter == _this.balanceAfter)&&(identical(other.actorName, _this.actorName) || other.actorName == _this.actorName)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&(identical(other.comment, _this.comment) || other.comment == _this.comment)&&(identical(other.warehouseName, _this.warehouseName) || other.warehouseName == _this.warehouseName)&&(identical(other.reversedBy, _this.reversedBy) || other.reversedBy == _this.reversedBy));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as MovementRow;
  return Object.hash(runtimeType,_this.id,_this.createdAt,_this.documentNumber,_this.itemName,_this.sku,_this.unitCode,_this.kind,_this.quantity,_this.balanceAfter,_this.actorName,_this.departmentName,_this.comment,_this.warehouseName,_this.reversedBy);
}

@override
String toString() {
  final _this = this as MovementRow;
  return 'MovementRow(id: ${_this.id}, createdAt: ${_this.createdAt}, documentNumber: ${_this.documentNumber}, itemName: ${_this.itemName}, sku: ${_this.sku}, unitCode: ${_this.unitCode}, kind: ${_this.kind}, quantity: ${_this.quantity}, balanceAfter: ${_this.balanceAfter}, actorName: ${_this.actorName}, departmentName: ${_this.departmentName}, comment: ${_this.comment}, warehouseName: ${_this.warehouseName}, reversedBy: ${_this.reversedBy})';
}


}

/// @nodoc
abstract mixin class $MovementRowCopyWith<$Res>  {
  factory $MovementRowCopyWith(MovementRow value, $Res Function(MovementRow) _then) = _$MovementRowCopyWithImpl;
@useResult
$Res call({
 String id, String createdAt, String documentNumber, String itemName, String sku, String unitCode, String kind, double quantity, double balanceAfter, String? actorName, String? departmentName, String? comment, String? warehouseName, String? reversedBy
});




}
/// @nodoc
class _$MovementRowCopyWithImpl<$Res>
    implements $MovementRowCopyWith<$Res> {
  _$MovementRowCopyWithImpl(this._self, this._then);

  final MovementRow _self;
  final $Res Function(MovementRow) _then;

/// Create a copy of MovementRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? createdAt = null,Object? documentNumber = null,Object? itemName = null,Object? sku = null,Object? unitCode = null,Object? kind = null,Object? quantity = null,Object? balanceAfter = null,Object? actorName = freezed,Object? departmentName = freezed,Object? comment = freezed,Object? warehouseName = freezed,Object? reversedBy = freezed,}) {
  return _then(MovementRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,documentNumber: null == documentNumber ? _self.documentNumber : documentNumber // ignore: cast_nullable_to_non_nullable
as String,itemName: null == itemName ? _self.itemName : itemName // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,quantity: null == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double,balanceAfter: null == balanceAfter ? _self.balanceAfter : balanceAfter // ignore: cast_nullable_to_non_nullable
as double,actorName: freezed == actorName ? _self.actorName : actorName // ignore: cast_nullable_to_non_nullable
as String?,departmentName: freezed == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,warehouseName: freezed == warehouseName ? _self.warehouseName : warehouseName // ignore: cast_nullable_to_non_nullable
as String?,reversedBy: freezed == reversedBy ? _self.reversedBy : reversedBy // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [MovementRow].
extension MovementRowPatterns on MovementRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _MovementRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _MovementRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _MovementRow value)  $default,){
final _that = this;
switch (_that) {
case _MovementRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _MovementRow value)?  $default,){
final _that = this;
switch (_that) {
case _MovementRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String createdAt,  String documentNumber,  String itemName,  String sku,  String unitCode,  String kind,  double quantity,  double balanceAfter,  String? actorName,  String? departmentName,  String? comment,  String? warehouseName,  String? reversedBy)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _MovementRow() when $default != null:
return $default(_that.id,_that.createdAt,_that.documentNumber,_that.itemName,_that.sku,_that.unitCode,_that.kind,_that.quantity,_that.balanceAfter,_that.actorName,_that.departmentName,_that.comment,_that.warehouseName,_that.reversedBy);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String createdAt,  String documentNumber,  String itemName,  String sku,  String unitCode,  String kind,  double quantity,  double balanceAfter,  String? actorName,  String? departmentName,  String? comment,  String? warehouseName,  String? reversedBy)  $default,) {final _that = this;
switch (_that) {
case _MovementRow():
return $default(_that.id,_that.createdAt,_that.documentNumber,_that.itemName,_that.sku,_that.unitCode,_that.kind,_that.quantity,_that.balanceAfter,_that.actorName,_that.departmentName,_that.comment,_that.warehouseName,_that.reversedBy);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String createdAt,  String documentNumber,  String itemName,  String sku,  String unitCode,  String kind,  double quantity,  double balanceAfter,  String? actorName,  String? departmentName,  String? comment,  String? warehouseName,  String? reversedBy)?  $default,) {final _that = this;
switch (_that) {
case _MovementRow() when $default != null:
return $default(_that.id,_that.createdAt,_that.documentNumber,_that.itemName,_that.sku,_that.unitCode,_that.kind,_that.quantity,_that.balanceAfter,_that.actorName,_that.departmentName,_that.comment,_that.warehouseName,_that.reversedBy);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _MovementRow implements MovementRow {
  const _MovementRow({required this.id, required this.createdAt, this.documentNumber = '', this.itemName = '', this.sku = '', this.unitCode = '', required this.kind, this.quantity = 0, this.balanceAfter = 0, this.actorName, this.departmentName, this.comment, this.warehouseName, this.reversedBy});
  factory _MovementRow.fromJson(Map<String, dynamic> json) => _$MovementRowFromJson(json);

@override final  String id;
@override final  String createdAt;
@override@JsonKey() final  String documentNumber;
@override@JsonKey() final  String itemName;
@override@JsonKey() final  String sku;
@override@JsonKey() final  String unitCode;
/// Receipt, Issue, WriteOff, Adjustment, TransferOut, TransferIn, Reversal, ProductionIssue, ProductionReturn, ProductionOutput…
@override final  String kind;
@override@JsonKey() final  double quantity;
@override@JsonKey() final  double balanceAfter;
@override final  String? actorName;
@override final  String? departmentName;
@override final  String? comment;
@override final  String? warehouseName;
@override final  String? reversedBy;

/// Create a copy of MovementRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$MovementRowCopyWith<_MovementRow> get copyWith => __$MovementRowCopyWithImpl<_MovementRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$MovementRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _MovementRow&&(identical(other.id, id) || other.id == id)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt)&&(identical(other.documentNumber, documentNumber) || other.documentNumber == documentNumber)&&(identical(other.itemName, itemName) || other.itemName == itemName)&&(identical(other.sku, sku) || other.sku == sku)&&(identical(other.unitCode, unitCode) || other.unitCode == unitCode)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.quantity, quantity) || other.quantity == quantity)&&(identical(other.balanceAfter, balanceAfter) || other.balanceAfter == balanceAfter)&&(identical(other.actorName, actorName) || other.actorName == actorName)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&(identical(other.comment, comment) || other.comment == comment)&&(identical(other.warehouseName, warehouseName) || other.warehouseName == warehouseName)&&(identical(other.reversedBy, reversedBy) || other.reversedBy == reversedBy));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,createdAt,documentNumber,itemName,sku,unitCode,kind,quantity,balanceAfter,actorName,departmentName,comment,warehouseName,reversedBy);
}

@override
String toString() {
    return 'MovementRow(id: $id, createdAt: $createdAt, documentNumber: $documentNumber, itemName: $itemName, sku: $sku, unitCode: $unitCode, kind: $kind, quantity: $quantity, balanceAfter: $balanceAfter, actorName: $actorName, departmentName: $departmentName, comment: $comment, warehouseName: $warehouseName, reversedBy: $reversedBy)';
}


}

/// @nodoc
abstract mixin class _$MovementRowCopyWith<$Res> implements $MovementRowCopyWith<$Res> {
  factory _$MovementRowCopyWith(_MovementRow value, $Res Function(_MovementRow) _then) = __$MovementRowCopyWithImpl;
@override @useResult
$Res call({
 String id, String createdAt, String documentNumber, String itemName, String sku, String unitCode, String kind, double quantity, double balanceAfter, String? actorName, String? departmentName, String? comment, String? warehouseName, String? reversedBy
});




}
/// @nodoc
class __$MovementRowCopyWithImpl<$Res>
    implements _$MovementRowCopyWith<$Res> {
  __$MovementRowCopyWithImpl(this._self, this._then);

  final _MovementRow _self;
  final $Res Function(_MovementRow) _then;

/// Create a copy of MovementRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? createdAt = null,Object? documentNumber = null,Object? itemName = null,Object? sku = null,Object? unitCode = null,Object? kind = null,Object? quantity = null,Object? balanceAfter = null,Object? actorName = freezed,Object? departmentName = freezed,Object? comment = freezed,Object? warehouseName = freezed,Object? reversedBy = freezed,}) {
  return _then(_MovementRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,documentNumber: null == documentNumber ? _self.documentNumber : documentNumber // ignore: cast_nullable_to_non_nullable
as String,itemName: null == itemName ? _self.itemName : itemName // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,quantity: null == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double,balanceAfter: null == balanceAfter ? _self.balanceAfter : balanceAfter // ignore: cast_nullable_to_non_nullable
as double,actorName: freezed == actorName ? _self.actorName : actorName // ignore: cast_nullable_to_non_nullable
as String?,departmentName: freezed == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,warehouseName: freezed == warehouseName ? _self.warehouseName : warehouseName // ignore: cast_nullable_to_non_nullable
as String?,reversedBy: freezed == reversedBy ? _self.reversedBy : reversedBy // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$Reservation {

 String get requestNumber; String get requestTitle; String get departmentName; double get quantity;
/// Create a copy of Reservation
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ReservationCopyWith<Reservation> get copyWith => _$ReservationCopyWithImpl<Reservation>(this as Reservation, _$identity);

  /// Serializes this Reservation to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as Reservation;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is Reservation&&(identical(other.requestNumber, _this.requestNumber) || other.requestNumber == _this.requestNumber)&&(identical(other.requestTitle, _this.requestTitle) || other.requestTitle == _this.requestTitle)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&(identical(other.quantity, _this.quantity) || other.quantity == _this.quantity));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as Reservation;
  return Object.hash(runtimeType,_this.requestNumber,_this.requestTitle,_this.departmentName,_this.quantity);
}

@override
String toString() {
  final _this = this as Reservation;
  return 'Reservation(requestNumber: ${_this.requestNumber}, requestTitle: ${_this.requestTitle}, departmentName: ${_this.departmentName}, quantity: ${_this.quantity})';
}


}

/// @nodoc
abstract mixin class $ReservationCopyWith<$Res>  {
  factory $ReservationCopyWith(Reservation value, $Res Function(Reservation) _then) = _$ReservationCopyWithImpl;
@useResult
$Res call({
 String requestNumber, String requestTitle, String departmentName, double quantity
});




}
/// @nodoc
class _$ReservationCopyWithImpl<$Res>
    implements $ReservationCopyWith<$Res> {
  _$ReservationCopyWithImpl(this._self, this._then);

  final Reservation _self;
  final $Res Function(Reservation) _then;

/// Create a copy of Reservation
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? requestNumber = null,Object? requestTitle = null,Object? departmentName = null,Object? quantity = null,}) {
  return _then(Reservation(
requestNumber: null == requestNumber ? _self.requestNumber : requestNumber // ignore: cast_nullable_to_non_nullable
as String,requestTitle: null == requestTitle ? _self.requestTitle : requestTitle // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,quantity: null == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [Reservation].
extension ReservationPatterns on Reservation {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _Reservation value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _Reservation() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _Reservation value)  $default,){
final _that = this;
switch (_that) {
case _Reservation():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _Reservation value)?  $default,){
final _that = this;
switch (_that) {
case _Reservation() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String requestNumber,  String requestTitle,  String departmentName,  double quantity)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _Reservation() when $default != null:
return $default(_that.requestNumber,_that.requestTitle,_that.departmentName,_that.quantity);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String requestNumber,  String requestTitle,  String departmentName,  double quantity)  $default,) {final _that = this;
switch (_that) {
case _Reservation():
return $default(_that.requestNumber,_that.requestTitle,_that.departmentName,_that.quantity);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String requestNumber,  String requestTitle,  String departmentName,  double quantity)?  $default,) {final _that = this;
switch (_that) {
case _Reservation() when $default != null:
return $default(_that.requestNumber,_that.requestTitle,_that.departmentName,_that.quantity);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _Reservation implements Reservation {
  const _Reservation({required this.requestNumber, this.requestTitle = '', this.departmentName = '', this.quantity = 0});
  factory _Reservation.fromJson(Map<String, dynamic> json) => _$ReservationFromJson(json);

@override final  String requestNumber;
@override@JsonKey() final  String requestTitle;
@override@JsonKey() final  String departmentName;
@override@JsonKey() final  double quantity;

/// Create a copy of Reservation
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ReservationCopyWith<_Reservation> get copyWith => __$ReservationCopyWithImpl<_Reservation>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ReservationToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _Reservation&&(identical(other.requestNumber, requestNumber) || other.requestNumber == requestNumber)&&(identical(other.requestTitle, requestTitle) || other.requestTitle == requestTitle)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&(identical(other.quantity, quantity) || other.quantity == quantity));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,requestNumber,requestTitle,departmentName,quantity);
}

@override
String toString() {
    return 'Reservation(requestNumber: $requestNumber, requestTitle: $requestTitle, departmentName: $departmentName, quantity: $quantity)';
}


}

/// @nodoc
abstract mixin class _$ReservationCopyWith<$Res> implements $ReservationCopyWith<$Res> {
  factory _$ReservationCopyWith(_Reservation value, $Res Function(_Reservation) _then) = __$ReservationCopyWithImpl;
@override @useResult
$Res call({
 String requestNumber, String requestTitle, String departmentName, double quantity
});




}
/// @nodoc
class __$ReservationCopyWithImpl<$Res>
    implements _$ReservationCopyWith<$Res> {
  __$ReservationCopyWithImpl(this._self, this._then);

  final _Reservation _self;
  final $Res Function(_Reservation) _then;

/// Create a copy of Reservation
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? requestNumber = null,Object? requestTitle = null,Object? departmentName = null,Object? quantity = null,}) {
  return _then(_Reservation(
requestNumber: null == requestNumber ? _self.requestNumber : requestNumber // ignore: cast_nullable_to_non_nullable
as String,requestTitle: null == requestTitle ? _self.requestTitle : requestTitle // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,quantity: null == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$ItemCard {

 StockRow get item; List<MovementRow> get movements; List<Reservation> get reservations;
/// Create a copy of ItemCard
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ItemCardCopyWith<ItemCard> get copyWith => _$ItemCardCopyWithImpl<ItemCard>(this as ItemCard, _$identity);

  /// Serializes this ItemCard to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ItemCard;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ItemCard&&(identical(other.item, _this.item) || other.item == _this.item)&&const DeepCollectionEquality().equals(other.movements, _this.movements)&&const DeepCollectionEquality().equals(other.reservations, _this.reservations));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ItemCard;
  return Object.hash(runtimeType,_this.item,const DeepCollectionEquality().hash(_this.movements),const DeepCollectionEquality().hash(_this.reservations));
}

@override
String toString() {
  final _this = this as ItemCard;
  return 'ItemCard(item: ${_this.item}, movements: ${_this.movements}, reservations: ${_this.reservations})';
}


}

/// @nodoc
abstract mixin class $ItemCardCopyWith<$Res>  {
  factory $ItemCardCopyWith(ItemCard value, $Res Function(ItemCard) _then) = _$ItemCardCopyWithImpl;
@useResult
$Res call({
 StockRow item, List<MovementRow> movements, List<Reservation> reservations
});


$StockRowCopyWith<$Res> get item;

}
/// @nodoc
class _$ItemCardCopyWithImpl<$Res>
    implements $ItemCardCopyWith<$Res> {
  _$ItemCardCopyWithImpl(this._self, this._then);

  final ItemCard _self;
  final $Res Function(ItemCard) _then;

/// Create a copy of ItemCard
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? item = null,Object? movements = null,Object? reservations = null,}) {
  return _then(ItemCard(
item: null == item ? _self.item : item // ignore: cast_nullable_to_non_nullable
as StockRow,movements: null == movements ? _self.movements : movements // ignore: cast_nullable_to_non_nullable
as List<MovementRow>,reservations: null == reservations ? _self.reservations : reservations // ignore: cast_nullable_to_non_nullable
as List<Reservation>,
  ));
}
/// Create a copy of ItemCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$StockRowCopyWith<$Res> get item {
  
  return $StockRowCopyWith<$Res>(_self.item, (value) {
    return _then(_self.copyWith(item: value));
  });
}
}


/// Adds pattern-matching-related methods to [ItemCard].
extension ItemCardPatterns on ItemCard {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ItemCard value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ItemCard() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ItemCard value)  $default,){
final _that = this;
switch (_that) {
case _ItemCard():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ItemCard value)?  $default,){
final _that = this;
switch (_that) {
case _ItemCard() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( StockRow item,  List<MovementRow> movements,  List<Reservation> reservations)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ItemCard() when $default != null:
return $default(_that.item,_that.movements,_that.reservations);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( StockRow item,  List<MovementRow> movements,  List<Reservation> reservations)  $default,) {final _that = this;
switch (_that) {
case _ItemCard():
return $default(_that.item,_that.movements,_that.reservations);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( StockRow item,  List<MovementRow> movements,  List<Reservation> reservations)?  $default,) {final _that = this;
switch (_that) {
case _ItemCard() when $default != null:
return $default(_that.item,_that.movements,_that.reservations);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ItemCard implements ItemCard {
  const _ItemCard({required this.item,  List<MovementRow> movements = const <MovementRow>[],  List<Reservation> reservations = const <Reservation>[]}): _movements = movements,_reservations = reservations;
  factory _ItemCard.fromJson(Map<String, dynamic> json) => _$ItemCardFromJson(json);

@override final  StockRow item;
 final  List<MovementRow> _movements;
@override@JsonKey() List<MovementRow> get movements {
  if (_movements is EqualUnmodifiableListView) return _movements;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_movements);
}

 final  List<Reservation> _reservations;
@override@JsonKey() List<Reservation> get reservations {
  if (_reservations is EqualUnmodifiableListView) return _reservations;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_reservations);
}


/// Create a copy of ItemCard
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ItemCardCopyWith<_ItemCard> get copyWith => __$ItemCardCopyWithImpl<_ItemCard>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ItemCardToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ItemCard&&(identical(other.item, item) || other.item == item)&&const DeepCollectionEquality().equals(other.movements, _movements)&&const DeepCollectionEquality().equals(other.reservations, _reservations));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,item,const DeepCollectionEquality().hash(_movements),const DeepCollectionEquality().hash(_reservations));
}

@override
String toString() {
    return 'ItemCard(item: $item, movements: $movements, reservations: $reservations)';
}


}

/// @nodoc
abstract mixin class _$ItemCardCopyWith<$Res> implements $ItemCardCopyWith<$Res> {
  factory _$ItemCardCopyWith(_ItemCard value, $Res Function(_ItemCard) _then) = __$ItemCardCopyWithImpl;
@override @useResult
$Res call({
 StockRow item, List<MovementRow> movements, List<Reservation> reservations
});


@override $StockRowCopyWith<$Res> get item;

}
/// @nodoc
class __$ItemCardCopyWithImpl<$Res>
    implements _$ItemCardCopyWith<$Res> {
  __$ItemCardCopyWithImpl(this._self, this._then);

  final _ItemCard _self;
  final $Res Function(_ItemCard) _then;

/// Create a copy of ItemCard
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? item = null,Object? movements = null,Object? reservations = null,}) {
  return _then(_ItemCard(
item: null == item ? _self.item : item // ignore: cast_nullable_to_non_nullable
as StockRow,movements: null == movements ? _self._movements : movements // ignore: cast_nullable_to_non_nullable
as List<MovementRow>,reservations: null == reservations ? _self._reservations : reservations // ignore: cast_nullable_to_non_nullable
as List<Reservation>,
  ));
}

/// Create a copy of ItemCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$StockRowCopyWith<$Res> get item {
  
  return $StockRowCopyWith<$Res>(_self.item, (value) {
    return _then(_self.copyWith(item: value));
  });
}
}


/// @nodoc
mixin _$Warehouse {

 String get id; String get name; bool get isActive; bool get isDefault;/// Пользователь может проводить операции на этом складе.
 bool get posts;
/// Create a copy of Warehouse
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WarehouseCopyWith<Warehouse> get copyWith => _$WarehouseCopyWithImpl<Warehouse>(this as Warehouse, _$identity);

  /// Serializes this Warehouse to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as Warehouse;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is Warehouse&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.isActive, _this.isActive) || other.isActive == _this.isActive)&&(identical(other.isDefault, _this.isDefault) || other.isDefault == _this.isDefault)&&(identical(other.posts, _this.posts) || other.posts == _this.posts));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as Warehouse;
  return Object.hash(runtimeType,_this.id,_this.name,_this.isActive,_this.isDefault,_this.posts);
}

@override
String toString() {
  final _this = this as Warehouse;
  return 'Warehouse(id: ${_this.id}, name: ${_this.name}, isActive: ${_this.isActive}, isDefault: ${_this.isDefault}, posts: ${_this.posts})';
}


}

/// @nodoc
abstract mixin class $WarehouseCopyWith<$Res>  {
  factory $WarehouseCopyWith(Warehouse value, $Res Function(Warehouse) _then) = _$WarehouseCopyWithImpl;
@useResult
$Res call({
 String id, String name, bool isActive, bool isDefault, bool posts
});




}
/// @nodoc
class _$WarehouseCopyWithImpl<$Res>
    implements $WarehouseCopyWith<$Res> {
  _$WarehouseCopyWithImpl(this._self, this._then);

  final Warehouse _self;
  final $Res Function(Warehouse) _then;

/// Create a copy of Warehouse
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,Object? isActive = null,Object? isDefault = null,Object? posts = null,}) {
  return _then(Warehouse(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,isActive: null == isActive ? _self.isActive : isActive // ignore: cast_nullable_to_non_nullable
as bool,isDefault: null == isDefault ? _self.isDefault : isDefault // ignore: cast_nullable_to_non_nullable
as bool,posts: null == posts ? _self.posts : posts // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [Warehouse].
extension WarehousePatterns on Warehouse {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _Warehouse value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _Warehouse() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _Warehouse value)  $default,){
final _that = this;
switch (_that) {
case _Warehouse():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _Warehouse value)?  $default,){
final _that = this;
switch (_that) {
case _Warehouse() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String name,  bool isActive,  bool isDefault,  bool posts)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _Warehouse() when $default != null:
return $default(_that.id,_that.name,_that.isActive,_that.isDefault,_that.posts);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String name,  bool isActive,  bool isDefault,  bool posts)  $default,) {final _that = this;
switch (_that) {
case _Warehouse():
return $default(_that.id,_that.name,_that.isActive,_that.isDefault,_that.posts);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String name,  bool isActive,  bool isDefault,  bool posts)?  $default,) {final _that = this;
switch (_that) {
case _Warehouse() when $default != null:
return $default(_that.id,_that.name,_that.isActive,_that.isDefault,_that.posts);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _Warehouse implements Warehouse {
  const _Warehouse({required this.id, required this.name, this.isActive = true, this.isDefault = false, this.posts = false});
  factory _Warehouse.fromJson(Map<String, dynamic> json) => _$WarehouseFromJson(json);

@override final  String id;
@override final  String name;
@override@JsonKey() final  bool isActive;
@override@JsonKey() final  bool isDefault;
/// Пользователь может проводить операции на этом складе.
@override@JsonKey() final  bool posts;

/// Create a copy of Warehouse
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WarehouseCopyWith<_Warehouse> get copyWith => __$WarehouseCopyWithImpl<_Warehouse>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WarehouseToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _Warehouse&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.isActive, isActive) || other.isActive == isActive)&&(identical(other.isDefault, isDefault) || other.isDefault == isDefault)&&(identical(other.posts, posts) || other.posts == posts));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name,isActive,isDefault,posts);
}

@override
String toString() {
    return 'Warehouse(id: $id, name: $name, isActive: $isActive, isDefault: $isDefault, posts: $posts)';
}


}

/// @nodoc
abstract mixin class _$WarehouseCopyWith<$Res> implements $WarehouseCopyWith<$Res> {
  factory _$WarehouseCopyWith(_Warehouse value, $Res Function(_Warehouse) _then) = __$WarehouseCopyWithImpl;
@override @useResult
$Res call({
 String id, String name, bool isActive, bool isDefault, bool posts
});




}
/// @nodoc
class __$WarehouseCopyWithImpl<$Res>
    implements _$WarehouseCopyWith<$Res> {
  __$WarehouseCopyWithImpl(this._self, this._then);

  final _Warehouse _self;
  final $Res Function(_Warehouse) _then;

/// Create a copy of Warehouse
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,Object? isActive = null,Object? isDefault = null,Object? posts = null,}) {
  return _then(_Warehouse(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,isActive: null == isActive ? _self.isActive : isActive // ignore: cast_nullable_to_non_nullable
as bool,isDefault: null == isDefault ? _self.isDefault : isDefault // ignore: cast_nullable_to_non_nullable
as bool,posts: null == posts ? _self.posts : posts // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$Department {

 String get id; String get code; String get name;
/// Create a copy of Department
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$DepartmentCopyWith<Department> get copyWith => _$DepartmentCopyWithImpl<Department>(this as Department, _$identity);

  /// Serializes this Department to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as Department;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is Department&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.name, _this.name) || other.name == _this.name));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as Department;
  return Object.hash(runtimeType,_this.id,_this.code,_this.name);
}

@override
String toString() {
  final _this = this as Department;
  return 'Department(id: ${_this.id}, code: ${_this.code}, name: ${_this.name})';
}


}

/// @nodoc
abstract mixin class $DepartmentCopyWith<$Res>  {
  factory $DepartmentCopyWith(Department value, $Res Function(Department) _then) = _$DepartmentCopyWithImpl;
@useResult
$Res call({
 String id, String code, String name
});




}
/// @nodoc
class _$DepartmentCopyWithImpl<$Res>
    implements $DepartmentCopyWith<$Res> {
  _$DepartmentCopyWithImpl(this._self, this._then);

  final Department _self;
  final $Res Function(Department) _then;

/// Create a copy of Department
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? code = null,Object? name = null,}) {
  return _then(Department(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [Department].
extension DepartmentPatterns on Department {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _Department value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _Department() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _Department value)  $default,){
final _that = this;
switch (_that) {
case _Department():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _Department value)?  $default,){
final _that = this;
switch (_that) {
case _Department() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String code,  String name)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _Department() when $default != null:
return $default(_that.id,_that.code,_that.name);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String code,  String name)  $default,) {final _that = this;
switch (_that) {
case _Department():
return $default(_that.id,_that.code,_that.name);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String code,  String name)?  $default,) {final _that = this;
switch (_that) {
case _Department() when $default != null:
return $default(_that.id,_that.code,_that.name);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _Department implements Department {
  const _Department({required this.id, this.code = '', required this.name});
  factory _Department.fromJson(Map<String, dynamic> json) => _$DepartmentFromJson(json);

@override final  String id;
@override@JsonKey() final  String code;
@override final  String name;

/// Create a copy of Department
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$DepartmentCopyWith<_Department> get copyWith => __$DepartmentCopyWithImpl<_Department>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$DepartmentToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _Department&&(identical(other.id, id) || other.id == id)&&(identical(other.code, code) || other.code == code)&&(identical(other.name, name) || other.name == name));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,code,name);
}

@override
String toString() {
    return 'Department(id: $id, code: $code, name: $name)';
}


}

/// @nodoc
abstract mixin class _$DepartmentCopyWith<$Res> implements $DepartmentCopyWith<$Res> {
  factory _$DepartmentCopyWith(_Department value, $Res Function(_Department) _then) = __$DepartmentCopyWithImpl;
@override @useResult
$Res call({
 String id, String code, String name
});




}
/// @nodoc
class __$DepartmentCopyWithImpl<$Res>
    implements _$DepartmentCopyWith<$Res> {
  __$DepartmentCopyWithImpl(this._self, this._then);

  final _Department _self;
  final $Res Function(_Department) _then;

/// Create a copy of Department
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? code = null,Object? name = null,}) {
  return _then(_Department(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$Warehouses {

 List<Warehouse> get warehouses; List<Department> get departments;
/// Create a copy of Warehouses
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WarehousesCopyWith<Warehouses> get copyWith => _$WarehousesCopyWithImpl<Warehouses>(this as Warehouses, _$identity);

  /// Serializes this Warehouses to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as Warehouses;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is Warehouses&&const DeepCollectionEquality().equals(other.warehouses, _this.warehouses)&&const DeepCollectionEquality().equals(other.departments, _this.departments));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as Warehouses;
  return Object.hash(runtimeType,const DeepCollectionEquality().hash(_this.warehouses),const DeepCollectionEquality().hash(_this.departments));
}

@override
String toString() {
  final _this = this as Warehouses;
  return 'Warehouses(warehouses: ${_this.warehouses}, departments: ${_this.departments})';
}


}

/// @nodoc
abstract mixin class $WarehousesCopyWith<$Res>  {
  factory $WarehousesCopyWith(Warehouses value, $Res Function(Warehouses) _then) = _$WarehousesCopyWithImpl;
@useResult
$Res call({
 List<Warehouse> warehouses, List<Department> departments
});




}
/// @nodoc
class _$WarehousesCopyWithImpl<$Res>
    implements $WarehousesCopyWith<$Res> {
  _$WarehousesCopyWithImpl(this._self, this._then);

  final Warehouses _self;
  final $Res Function(Warehouses) _then;

/// Create a copy of Warehouses
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? warehouses = null,Object? departments = null,}) {
  return _then(Warehouses(
warehouses: null == warehouses ? _self.warehouses : warehouses // ignore: cast_nullable_to_non_nullable
as List<Warehouse>,departments: null == departments ? _self.departments : departments // ignore: cast_nullable_to_non_nullable
as List<Department>,
  ));
}

}


/// Adds pattern-matching-related methods to [Warehouses].
extension WarehousesPatterns on Warehouses {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _Warehouses value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _Warehouses() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _Warehouses value)  $default,){
final _that = this;
switch (_that) {
case _Warehouses():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _Warehouses value)?  $default,){
final _that = this;
switch (_that) {
case _Warehouses() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( List<Warehouse> warehouses,  List<Department> departments)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _Warehouses() when $default != null:
return $default(_that.warehouses,_that.departments);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( List<Warehouse> warehouses,  List<Department> departments)  $default,) {final _that = this;
switch (_that) {
case _Warehouses():
return $default(_that.warehouses,_that.departments);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( List<Warehouse> warehouses,  List<Department> departments)?  $default,) {final _that = this;
switch (_that) {
case _Warehouses() when $default != null:
return $default(_that.warehouses,_that.departments);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _Warehouses implements Warehouses {
  const _Warehouses({ List<Warehouse> warehouses = const <Warehouse>[],  List<Department> departments = const <Department>[]}): _warehouses = warehouses,_departments = departments;
  factory _Warehouses.fromJson(Map<String, dynamic> json) => _$WarehousesFromJson(json);

 final  List<Warehouse> _warehouses;
@override@JsonKey() List<Warehouse> get warehouses {
  if (_warehouses is EqualUnmodifiableListView) return _warehouses;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_warehouses);
}

 final  List<Department> _departments;
@override@JsonKey() List<Department> get departments {
  if (_departments is EqualUnmodifiableListView) return _departments;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_departments);
}


/// Create a copy of Warehouses
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WarehousesCopyWith<_Warehouses> get copyWith => __$WarehousesCopyWithImpl<_Warehouses>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WarehousesToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _Warehouses&&const DeepCollectionEquality().equals(other.warehouses, _warehouses)&&const DeepCollectionEquality().equals(other.departments, _departments));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,const DeepCollectionEquality().hash(_warehouses),const DeepCollectionEquality().hash(_departments));
}

@override
String toString() {
    return 'Warehouses(warehouses: $warehouses, departments: $departments)';
}


}

/// @nodoc
abstract mixin class _$WarehousesCopyWith<$Res> implements $WarehousesCopyWith<$Res> {
  factory _$WarehousesCopyWith(_Warehouses value, $Res Function(_Warehouses) _then) = __$WarehousesCopyWithImpl;
@override @useResult
$Res call({
 List<Warehouse> warehouses, List<Department> departments
});




}
/// @nodoc
class __$WarehousesCopyWithImpl<$Res>
    implements _$WarehousesCopyWith<$Res> {
  __$WarehousesCopyWithImpl(this._self, this._then);

  final _Warehouses _self;
  final $Res Function(_Warehouses) _then;

/// Create a copy of Warehouses
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? warehouses = null,Object? departments = null,}) {
  return _then(_Warehouses(
warehouses: null == warehouses ? _self._warehouses : warehouses // ignore: cast_nullable_to_non_nullable
as List<Warehouse>,departments: null == departments ? _self._departments : departments // ignore: cast_nullable_to_non_nullable
as List<Department>,
  ));
}


}


/// @nodoc
mixin _$CatalogItem {

 String get id; String get sku; String get name; String get unitCode; double? get available;
/// Create a copy of CatalogItem
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CatalogItemCopyWith<CatalogItem> get copyWith => _$CatalogItemCopyWithImpl<CatalogItem>(this as CatalogItem, _$identity);

  /// Serializes this CatalogItem to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CatalogItem;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CatalogItem&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.sku, _this.sku) || other.sku == _this.sku)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.unitCode, _this.unitCode) || other.unitCode == _this.unitCode)&&(identical(other.available, _this.available) || other.available == _this.available));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CatalogItem;
  return Object.hash(runtimeType,_this.id,_this.sku,_this.name,_this.unitCode,_this.available);
}

@override
String toString() {
  final _this = this as CatalogItem;
  return 'CatalogItem(id: ${_this.id}, sku: ${_this.sku}, name: ${_this.name}, unitCode: ${_this.unitCode}, available: ${_this.available})';
}


}

/// @nodoc
abstract mixin class $CatalogItemCopyWith<$Res>  {
  factory $CatalogItemCopyWith(CatalogItem value, $Res Function(CatalogItem) _then) = _$CatalogItemCopyWithImpl;
@useResult
$Res call({
 String id, String sku, String name, String unitCode, double? available
});




}
/// @nodoc
class _$CatalogItemCopyWithImpl<$Res>
    implements $CatalogItemCopyWith<$Res> {
  _$CatalogItemCopyWithImpl(this._self, this._then);

  final CatalogItem _self;
  final $Res Function(CatalogItem) _then;

/// Create a copy of CatalogItem
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? sku = null,Object? name = null,Object? unitCode = null,Object? available = freezed,}) {
  return _then(CatalogItem(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,available: freezed == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [CatalogItem].
extension CatalogItemPatterns on CatalogItem {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CatalogItem value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CatalogItem() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CatalogItem value)  $default,){
final _that = this;
switch (_that) {
case _CatalogItem():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CatalogItem value)?  $default,){
final _that = this;
switch (_that) {
case _CatalogItem() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String sku,  String name,  String unitCode,  double? available)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CatalogItem() when $default != null:
return $default(_that.id,_that.sku,_that.name,_that.unitCode,_that.available);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String sku,  String name,  String unitCode,  double? available)  $default,) {final _that = this;
switch (_that) {
case _CatalogItem():
return $default(_that.id,_that.sku,_that.name,_that.unitCode,_that.available);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String sku,  String name,  String unitCode,  double? available)?  $default,) {final _that = this;
switch (_that) {
case _CatalogItem() when $default != null:
return $default(_that.id,_that.sku,_that.name,_that.unitCode,_that.available);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CatalogItem implements CatalogItem {
  const _CatalogItem({required this.id, this.sku = '', required this.name, this.unitCode = '', this.available});
  factory _CatalogItem.fromJson(Map<String, dynamic> json) => _$CatalogItemFromJson(json);

@override final  String id;
@override@JsonKey() final  String sku;
@override final  String name;
@override@JsonKey() final  String unitCode;
@override final  double? available;

/// Create a copy of CatalogItem
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CatalogItemCopyWith<_CatalogItem> get copyWith => __$CatalogItemCopyWithImpl<_CatalogItem>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CatalogItemToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CatalogItem&&(identical(other.id, id) || other.id == id)&&(identical(other.sku, sku) || other.sku == sku)&&(identical(other.name, name) || other.name == name)&&(identical(other.unitCode, unitCode) || other.unitCode == unitCode)&&(identical(other.available, available) || other.available == available));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,sku,name,unitCode,available);
}

@override
String toString() {
    return 'CatalogItem(id: $id, sku: $sku, name: $name, unitCode: $unitCode, available: $available)';
}


}

/// @nodoc
abstract mixin class _$CatalogItemCopyWith<$Res> implements $CatalogItemCopyWith<$Res> {
  factory _$CatalogItemCopyWith(_CatalogItem value, $Res Function(_CatalogItem) _then) = __$CatalogItemCopyWithImpl;
@override @useResult
$Res call({
 String id, String sku, String name, String unitCode, double? available
});




}
/// @nodoc
class __$CatalogItemCopyWithImpl<$Res>
    implements _$CatalogItemCopyWith<$Res> {
  __$CatalogItemCopyWithImpl(this._self, this._then);

  final _CatalogItem _self;
  final $Res Function(_CatalogItem) _then;

/// Create a copy of CatalogItem
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? sku = null,Object? name = null,Object? unitCode = null,Object? available = freezed,}) {
  return _then(_CatalogItem(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,sku: null == sku ? _self.sku : sku // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,unitCode: null == unitCode ? _self.unitCode : unitCode // ignore: cast_nullable_to_non_nullable
as String,available: freezed == available ? _self.available : available // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$RequestRow {

 String get id; String get number; String get title; String get status; String get priority; String get departmentName; String get initiatorName; int get itemsCount; String? get firstItem; double get estimatedTotal; String? get desiredDate; String get createdAt;
/// Create a copy of RequestRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$RequestRowCopyWith<RequestRow> get copyWith => _$RequestRowCopyWithImpl<RequestRow>(this as RequestRow, _$identity);

  /// Serializes this RequestRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as RequestRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is RequestRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.number, _this.number) || other.number == _this.number)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.priority, _this.priority) || other.priority == _this.priority)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&(identical(other.initiatorName, _this.initiatorName) || other.initiatorName == _this.initiatorName)&&(identical(other.itemsCount, _this.itemsCount) || other.itemsCount == _this.itemsCount)&&(identical(other.firstItem, _this.firstItem) || other.firstItem == _this.firstItem)&&(identical(other.estimatedTotal, _this.estimatedTotal) || other.estimatedTotal == _this.estimatedTotal)&&(identical(other.desiredDate, _this.desiredDate) || other.desiredDate == _this.desiredDate)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as RequestRow;
  return Object.hash(runtimeType,_this.id,_this.number,_this.title,_this.status,_this.priority,_this.departmentName,_this.initiatorName,_this.itemsCount,_this.firstItem,_this.estimatedTotal,_this.desiredDate,_this.createdAt);
}

@override
String toString() {
  final _this = this as RequestRow;
  return 'RequestRow(id: ${_this.id}, number: ${_this.number}, title: ${_this.title}, status: ${_this.status}, priority: ${_this.priority}, departmentName: ${_this.departmentName}, initiatorName: ${_this.initiatorName}, itemsCount: ${_this.itemsCount}, firstItem: ${_this.firstItem}, estimatedTotal: ${_this.estimatedTotal}, desiredDate: ${_this.desiredDate}, createdAt: ${_this.createdAt})';
}


}

/// @nodoc
abstract mixin class $RequestRowCopyWith<$Res>  {
  factory $RequestRowCopyWith(RequestRow value, $Res Function(RequestRow) _then) = _$RequestRowCopyWithImpl;
@useResult
$Res call({
 String id, String number, String title, String status, String priority, String departmentName, String initiatorName, int itemsCount, String? firstItem, double estimatedTotal, String? desiredDate, String createdAt
});




}
/// @nodoc
class _$RequestRowCopyWithImpl<$Res>
    implements $RequestRowCopyWith<$Res> {
  _$RequestRowCopyWithImpl(this._self, this._then);

  final RequestRow _self;
  final $Res Function(RequestRow) _then;

/// Create a copy of RequestRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? number = null,Object? title = null,Object? status = null,Object? priority = null,Object? departmentName = null,Object? initiatorName = null,Object? itemsCount = null,Object? firstItem = freezed,Object? estimatedTotal = null,Object? desiredDate = freezed,Object? createdAt = null,}) {
  return _then(RequestRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,initiatorName: null == initiatorName ? _self.initiatorName : initiatorName // ignore: cast_nullable_to_non_nullable
as String,itemsCount: null == itemsCount ? _self.itemsCount : itemsCount // ignore: cast_nullable_to_non_nullable
as int,firstItem: freezed == firstItem ? _self.firstItem : firstItem // ignore: cast_nullable_to_non_nullable
as String?,estimatedTotal: null == estimatedTotal ? _self.estimatedTotal : estimatedTotal // ignore: cast_nullable_to_non_nullable
as double,desiredDate: freezed == desiredDate ? _self.desiredDate : desiredDate // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [RequestRow].
extension RequestRowPatterns on RequestRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _RequestRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _RequestRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _RequestRow value)  $default,){
final _that = this;
switch (_that) {
case _RequestRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _RequestRow value)?  $default,){
final _that = this;
switch (_that) {
case _RequestRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String number,  String title,  String status,  String priority,  String departmentName,  String initiatorName,  int itemsCount,  String? firstItem,  double estimatedTotal,  String? desiredDate,  String createdAt)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _RequestRow() when $default != null:
return $default(_that.id,_that.number,_that.title,_that.status,_that.priority,_that.departmentName,_that.initiatorName,_that.itemsCount,_that.firstItem,_that.estimatedTotal,_that.desiredDate,_that.createdAt);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String number,  String title,  String status,  String priority,  String departmentName,  String initiatorName,  int itemsCount,  String? firstItem,  double estimatedTotal,  String? desiredDate,  String createdAt)  $default,) {final _that = this;
switch (_that) {
case _RequestRow():
return $default(_that.id,_that.number,_that.title,_that.status,_that.priority,_that.departmentName,_that.initiatorName,_that.itemsCount,_that.firstItem,_that.estimatedTotal,_that.desiredDate,_that.createdAt);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String number,  String title,  String status,  String priority,  String departmentName,  String initiatorName,  int itemsCount,  String? firstItem,  double estimatedTotal,  String? desiredDate,  String createdAt)?  $default,) {final _that = this;
switch (_that) {
case _RequestRow() when $default != null:
return $default(_that.id,_that.number,_that.title,_that.status,_that.priority,_that.departmentName,_that.initiatorName,_that.itemsCount,_that.firstItem,_that.estimatedTotal,_that.desiredDate,_that.createdAt);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _RequestRow implements RequestRow {
  const _RequestRow({required this.id, required this.number, required this.title, required this.status, this.priority = 'Normal', this.departmentName = '', this.initiatorName = '', this.itemsCount = 0, this.firstItem, this.estimatedTotal = 0, this.desiredDate, required this.createdAt});
  factory _RequestRow.fromJson(Map<String, dynamic> json) => _$RequestRowFromJson(json);

@override final  String id;
@override final  String number;
@override final  String title;
@override final  String status;
@override@JsonKey() final  String priority;
@override@JsonKey() final  String departmentName;
@override@JsonKey() final  String initiatorName;
@override@JsonKey() final  int itemsCount;
@override final  String? firstItem;
@override@JsonKey() final  double estimatedTotal;
@override final  String? desiredDate;
@override final  String createdAt;

/// Create a copy of RequestRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$RequestRowCopyWith<_RequestRow> get copyWith => __$RequestRowCopyWithImpl<_RequestRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$RequestRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _RequestRow&&(identical(other.id, id) || other.id == id)&&(identical(other.number, number) || other.number == number)&&(identical(other.title, title) || other.title == title)&&(identical(other.status, status) || other.status == status)&&(identical(other.priority, priority) || other.priority == priority)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&(identical(other.initiatorName, initiatorName) || other.initiatorName == initiatorName)&&(identical(other.itemsCount, itemsCount) || other.itemsCount == itemsCount)&&(identical(other.firstItem, firstItem) || other.firstItem == firstItem)&&(identical(other.estimatedTotal, estimatedTotal) || other.estimatedTotal == estimatedTotal)&&(identical(other.desiredDate, desiredDate) || other.desiredDate == desiredDate)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,number,title,status,priority,departmentName,initiatorName,itemsCount,firstItem,estimatedTotal,desiredDate,createdAt);
}

@override
String toString() {
    return 'RequestRow(id: $id, number: $number, title: $title, status: $status, priority: $priority, departmentName: $departmentName, initiatorName: $initiatorName, itemsCount: $itemsCount, firstItem: $firstItem, estimatedTotal: $estimatedTotal, desiredDate: $desiredDate, createdAt: $createdAt)';
}


}

/// @nodoc
abstract mixin class _$RequestRowCopyWith<$Res> implements $RequestRowCopyWith<$Res> {
  factory _$RequestRowCopyWith(_RequestRow value, $Res Function(_RequestRow) _then) = __$RequestRowCopyWithImpl;
@override @useResult
$Res call({
 String id, String number, String title, String status, String priority, String departmentName, String initiatorName, int itemsCount, String? firstItem, double estimatedTotal, String? desiredDate, String createdAt
});




}
/// @nodoc
class __$RequestRowCopyWithImpl<$Res>
    implements _$RequestRowCopyWith<$Res> {
  __$RequestRowCopyWithImpl(this._self, this._then);

  final _RequestRow _self;
  final $Res Function(_RequestRow) _then;

/// Create a copy of RequestRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? number = null,Object? title = null,Object? status = null,Object? priority = null,Object? departmentName = null,Object? initiatorName = null,Object? itemsCount = null,Object? firstItem = freezed,Object? estimatedTotal = null,Object? desiredDate = freezed,Object? createdAt = null,}) {
  return _then(_RequestRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,initiatorName: null == initiatorName ? _self.initiatorName : initiatorName // ignore: cast_nullable_to_non_nullable
as String,itemsCount: null == itemsCount ? _self.itemsCount : itemsCount // ignore: cast_nullable_to_non_nullable
as int,firstItem: freezed == firstItem ? _self.firstItem : firstItem // ignore: cast_nullable_to_non_nullable
as String?,estimatedTotal: null == estimatedTotal ? _self.estimatedTotal : estimatedTotal // ignore: cast_nullable_to_non_nullable
as double,desiredDate: freezed == desiredDate ? _self.desiredDate : desiredDate // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}

// dart format on
