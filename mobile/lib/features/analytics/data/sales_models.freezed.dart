// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'sales_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$AgentFlag {

 String get kind;/// Critical | Risk | Info
 String get severity; String get label; String get title; String get explanation;
/// Create a copy of AgentFlag
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AgentFlagCopyWith<AgentFlag> get copyWith => _$AgentFlagCopyWithImpl<AgentFlag>(this as AgentFlag, _$identity);

  /// Serializes this AgentFlag to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AgentFlag;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AgentFlag&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.severity, _this.severity) || other.severity == _this.severity)&&(identical(other.label, _this.label) || other.label == _this.label)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.explanation, _this.explanation) || other.explanation == _this.explanation));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AgentFlag;
  return Object.hash(runtimeType,_this.kind,_this.severity,_this.label,_this.title,_this.explanation);
}

@override
String toString() {
  final _this = this as AgentFlag;
  return 'AgentFlag(kind: ${_this.kind}, severity: ${_this.severity}, label: ${_this.label}, title: ${_this.title}, explanation: ${_this.explanation})';
}


}

/// @nodoc
abstract mixin class $AgentFlagCopyWith<$Res>  {
  factory $AgentFlagCopyWith(AgentFlag value, $Res Function(AgentFlag) _then) = _$AgentFlagCopyWithImpl;
@useResult
$Res call({
 String kind, String severity, String label, String title, String explanation
});




}
/// @nodoc
class _$AgentFlagCopyWithImpl<$Res>
    implements $AgentFlagCopyWith<$Res> {
  _$AgentFlagCopyWithImpl(this._self, this._then);

  final AgentFlag _self;
  final $Res Function(AgentFlag) _then;

/// Create a copy of AgentFlag
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? kind = null,Object? severity = null,Object? label = null,Object? title = null,Object? explanation = null,}) {
  return _then(AgentFlag(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String,label: null == label ? _self.label : label // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,explanation: null == explanation ? _self.explanation : explanation // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [AgentFlag].
extension AgentFlagPatterns on AgentFlag {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AgentFlag value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AgentFlag() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AgentFlag value)  $default,){
final _that = this;
switch (_that) {
case _AgentFlag():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AgentFlag value)?  $default,){
final _that = this;
switch (_that) {
case _AgentFlag() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String kind,  String severity,  String label,  String title,  String explanation)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AgentFlag() when $default != null:
return $default(_that.kind,_that.severity,_that.label,_that.title,_that.explanation);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String kind,  String severity,  String label,  String title,  String explanation)  $default,) {final _that = this;
switch (_that) {
case _AgentFlag():
return $default(_that.kind,_that.severity,_that.label,_that.title,_that.explanation);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String kind,  String severity,  String label,  String title,  String explanation)?  $default,) {final _that = this;
switch (_that) {
case _AgentFlag() when $default != null:
return $default(_that.kind,_that.severity,_that.label,_that.title,_that.explanation);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AgentFlag implements AgentFlag {
  const _AgentFlag({required this.kind, required this.severity, required this.label, required this.title, this.explanation = ''});
  factory _AgentFlag.fromJson(Map<String, dynamic> json) => _$AgentFlagFromJson(json);

@override final  String kind;
/// Critical | Risk | Info
@override final  String severity;
@override final  String label;
@override final  String title;
@override@JsonKey() final  String explanation;

/// Create a copy of AgentFlag
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AgentFlagCopyWith<_AgentFlag> get copyWith => __$AgentFlagCopyWithImpl<_AgentFlag>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AgentFlagToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AgentFlag&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.severity, severity) || other.severity == severity)&&(identical(other.label, label) || other.label == label)&&(identical(other.title, title) || other.title == title)&&(identical(other.explanation, explanation) || other.explanation == explanation));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,kind,severity,label,title,explanation);
}

@override
String toString() {
    return 'AgentFlag(kind: $kind, severity: $severity, label: $label, title: $title, explanation: $explanation)';
}


}

/// @nodoc
abstract mixin class _$AgentFlagCopyWith<$Res> implements $AgentFlagCopyWith<$Res> {
  factory _$AgentFlagCopyWith(_AgentFlag value, $Res Function(_AgentFlag) _then) = __$AgentFlagCopyWithImpl;
@override @useResult
$Res call({
 String kind, String severity, String label, String title, String explanation
});




}
/// @nodoc
class __$AgentFlagCopyWithImpl<$Res>
    implements _$AgentFlagCopyWith<$Res> {
  __$AgentFlagCopyWithImpl(this._self, this._then);

  final _AgentFlag _self;
  final $Res Function(_AgentFlag) _then;

/// Create a copy of AgentFlag
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? kind = null,Object? severity = null,Object? label = null,Object? title = null,Object? explanation = null,}) {
  return _then(_AgentFlag(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String,label: null == label ? _self.label : label // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,explanation: null == explanation ? _self.explanation : explanation // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$UnitRow {

 String get id; String get name; String? get subtitle; double? get planKg; double get factKg; double? get execution; double? get forecastKg; double? get forecastExecution; double get revenue; int get akb; double? get strike; int get visitsWithoutOrder; int get agents; int get regionCount; FlagCounts get flags;/// republic | direction | region
 String get kind; String? get executionLevel; String? get forecastExecutionLevel; bool get isChannel;
/// Create a copy of UnitRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$UnitRowCopyWith<UnitRow> get copyWith => _$UnitRowCopyWithImpl<UnitRow>(this as UnitRow, _$identity);

  /// Serializes this UnitRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as UnitRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is UnitRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.subtitle, _this.subtitle) || other.subtitle == _this.subtitle)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.execution, _this.execution) || other.execution == _this.execution)&&(identical(other.forecastKg, _this.forecastKg) || other.forecastKg == _this.forecastKg)&&(identical(other.forecastExecution, _this.forecastExecution) || other.forecastExecution == _this.forecastExecution)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.akb, _this.akb) || other.akb == _this.akb)&&(identical(other.strike, _this.strike) || other.strike == _this.strike)&&(identical(other.visitsWithoutOrder, _this.visitsWithoutOrder) || other.visitsWithoutOrder == _this.visitsWithoutOrder)&&(identical(other.agents, _this.agents) || other.agents == _this.agents)&&(identical(other.regionCount, _this.regionCount) || other.regionCount == _this.regionCount)&&(identical(other.flags, _this.flags) || other.flags == _this.flags)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.executionLevel, _this.executionLevel) || other.executionLevel == _this.executionLevel)&&(identical(other.forecastExecutionLevel, _this.forecastExecutionLevel) || other.forecastExecutionLevel == _this.forecastExecutionLevel)&&(identical(other.isChannel, _this.isChannel) || other.isChannel == _this.isChannel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as UnitRow;
  return Object.hashAll([runtimeType,_this.id,_this.name,_this.subtitle,_this.planKg,_this.factKg,_this.execution,_this.forecastKg,_this.forecastExecution,_this.revenue,_this.akb,_this.strike,_this.visitsWithoutOrder,_this.agents,_this.regionCount,_this.flags,_this.kind,_this.executionLevel,_this.forecastExecutionLevel,_this.isChannel]);
}

@override
String toString() {
  final _this = this as UnitRow;
  return 'UnitRow(id: ${_this.id}, name: ${_this.name}, subtitle: ${_this.subtitle}, planKg: ${_this.planKg}, factKg: ${_this.factKg}, execution: ${_this.execution}, forecastKg: ${_this.forecastKg}, forecastExecution: ${_this.forecastExecution}, revenue: ${_this.revenue}, akb: ${_this.akb}, strike: ${_this.strike}, visitsWithoutOrder: ${_this.visitsWithoutOrder}, agents: ${_this.agents}, regionCount: ${_this.regionCount}, flags: ${_this.flags}, kind: ${_this.kind}, executionLevel: ${_this.executionLevel}, forecastExecutionLevel: ${_this.forecastExecutionLevel}, isChannel: ${_this.isChannel})';
}


}

/// @nodoc
abstract mixin class $UnitRowCopyWith<$Res>  {
  factory $UnitRowCopyWith(UnitRow value, $Res Function(UnitRow) _then) = _$UnitRowCopyWithImpl;
@useResult
$Res call({
 String id, String name, String? subtitle, double? planKg, double factKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int akb, double? strike, int visitsWithoutOrder, int agents, int regionCount, FlagCounts flags, String kind, String? executionLevel, String? forecastExecutionLevel, bool isChannel
});


$FlagCountsCopyWith<$Res> get flags;

}
/// @nodoc
class _$UnitRowCopyWithImpl<$Res>
    implements $UnitRowCopyWith<$Res> {
  _$UnitRowCopyWithImpl(this._self, this._then);

  final UnitRow _self;
  final $Res Function(UnitRow) _then;

/// Create a copy of UnitRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,Object? subtitle = freezed,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? akb = null,Object? strike = freezed,Object? visitsWithoutOrder = null,Object? agents = null,Object? regionCount = null,Object? flags = null,Object? kind = null,Object? executionLevel = freezed,Object? forecastExecutionLevel = freezed,Object? isChannel = null,}) {
  return _then(UnitRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,subtitle: freezed == subtitle ? _self.subtitle : subtitle // ignore: cast_nullable_to_non_nullable
as String?,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,strike: freezed == strike ? _self.strike : strike // ignore: cast_nullable_to_non_nullable
as double?,visitsWithoutOrder: null == visitsWithoutOrder ? _self.visitsWithoutOrder : visitsWithoutOrder // ignore: cast_nullable_to_non_nullable
as int,agents: null == agents ? _self.agents : agents // ignore: cast_nullable_to_non_nullable
as int,regionCount: null == regionCount ? _self.regionCount : regionCount // ignore: cast_nullable_to_non_nullable
as int,flags: null == flags ? _self.flags : flags // ignore: cast_nullable_to_non_nullable
as FlagCounts,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,forecastExecutionLevel: freezed == forecastExecutionLevel ? _self.forecastExecutionLevel : forecastExecutionLevel // ignore: cast_nullable_to_non_nullable
as String?,isChannel: null == isChannel ? _self.isChannel : isChannel // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}
/// Create a copy of UnitRow
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FlagCountsCopyWith<$Res> get flags {
  
  return $FlagCountsCopyWith<$Res>(_self.flags, (value) {
    return _then(_self.copyWith(flags: value));
  });
}
}


/// Adds pattern-matching-related methods to [UnitRow].
extension UnitRowPatterns on UnitRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _UnitRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _UnitRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _UnitRow value)  $default,){
final _that = this;
switch (_that) {
case _UnitRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _UnitRow value)?  $default,){
final _that = this;
switch (_that) {
case _UnitRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String name,  String? subtitle,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  double? strike,  int visitsWithoutOrder,  int agents,  int regionCount,  FlagCounts flags,  String kind,  String? executionLevel,  String? forecastExecutionLevel,  bool isChannel)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _UnitRow() when $default != null:
return $default(_that.id,_that.name,_that.subtitle,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.strike,_that.visitsWithoutOrder,_that.agents,_that.regionCount,_that.flags,_that.kind,_that.executionLevel,_that.forecastExecutionLevel,_that.isChannel);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String name,  String? subtitle,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  double? strike,  int visitsWithoutOrder,  int agents,  int regionCount,  FlagCounts flags,  String kind,  String? executionLevel,  String? forecastExecutionLevel,  bool isChannel)  $default,) {final _that = this;
switch (_that) {
case _UnitRow():
return $default(_that.id,_that.name,_that.subtitle,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.strike,_that.visitsWithoutOrder,_that.agents,_that.regionCount,_that.flags,_that.kind,_that.executionLevel,_that.forecastExecutionLevel,_that.isChannel);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String name,  String? subtitle,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  double? strike,  int visitsWithoutOrder,  int agents,  int regionCount,  FlagCounts flags,  String kind,  String? executionLevel,  String? forecastExecutionLevel,  bool isChannel)?  $default,) {final _that = this;
switch (_that) {
case _UnitRow() when $default != null:
return $default(_that.id,_that.name,_that.subtitle,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.strike,_that.visitsWithoutOrder,_that.agents,_that.regionCount,_that.flags,_that.kind,_that.executionLevel,_that.forecastExecutionLevel,_that.isChannel);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _UnitRow implements UnitRow {
  const _UnitRow({required this.id, required this.name, this.subtitle, this.planKg, this.factKg = 0, this.execution, this.forecastKg, this.forecastExecution, this.revenue = 0, this.akb = 0, this.strike, this.visitsWithoutOrder = 0, this.agents = 0, this.regionCount = 0, this.flags = const FlagCounts(), this.kind = 'region', this.executionLevel, this.forecastExecutionLevel, this.isChannel = false});
  factory _UnitRow.fromJson(Map<String, dynamic> json) => _$UnitRowFromJson(json);

@override final  String id;
@override final  String name;
@override final  String? subtitle;
@override final  double? planKg;
@override@JsonKey() final  double factKg;
@override final  double? execution;
@override final  double? forecastKg;
@override final  double? forecastExecution;
@override@JsonKey() final  double revenue;
@override@JsonKey() final  int akb;
@override final  double? strike;
@override@JsonKey() final  int visitsWithoutOrder;
@override@JsonKey() final  int agents;
@override@JsonKey() final  int regionCount;
@override@JsonKey() final  FlagCounts flags;
/// republic | direction | region
@override@JsonKey() final  String kind;
@override final  String? executionLevel;
@override final  String? forecastExecutionLevel;
@override@JsonKey() final  bool isChannel;

/// Create a copy of UnitRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$UnitRowCopyWith<_UnitRow> get copyWith => __$UnitRowCopyWithImpl<_UnitRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$UnitRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _UnitRow&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.subtitle, subtitle) || other.subtitle == subtitle)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.execution, execution) || other.execution == execution)&&(identical(other.forecastKg, forecastKg) || other.forecastKg == forecastKg)&&(identical(other.forecastExecution, forecastExecution) || other.forecastExecution == forecastExecution)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.akb, akb) || other.akb == akb)&&(identical(other.strike, strike) || other.strike == strike)&&(identical(other.visitsWithoutOrder, visitsWithoutOrder) || other.visitsWithoutOrder == visitsWithoutOrder)&&(identical(other.agents, agents) || other.agents == agents)&&(identical(other.regionCount, regionCount) || other.regionCount == regionCount)&&(identical(other.flags, flags) || other.flags == flags)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.executionLevel, executionLevel) || other.executionLevel == executionLevel)&&(identical(other.forecastExecutionLevel, forecastExecutionLevel) || other.forecastExecutionLevel == forecastExecutionLevel)&&(identical(other.isChannel, isChannel) || other.isChannel == isChannel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hashAll([runtimeType,id,name,subtitle,planKg,factKg,execution,forecastKg,forecastExecution,revenue,akb,strike,visitsWithoutOrder,agents,regionCount,flags,kind,executionLevel,forecastExecutionLevel,isChannel]);
}

@override
String toString() {
    return 'UnitRow(id: $id, name: $name, subtitle: $subtitle, planKg: $planKg, factKg: $factKg, execution: $execution, forecastKg: $forecastKg, forecastExecution: $forecastExecution, revenue: $revenue, akb: $akb, strike: $strike, visitsWithoutOrder: $visitsWithoutOrder, agents: $agents, regionCount: $regionCount, flags: $flags, kind: $kind, executionLevel: $executionLevel, forecastExecutionLevel: $forecastExecutionLevel, isChannel: $isChannel)';
}


}

/// @nodoc
abstract mixin class _$UnitRowCopyWith<$Res> implements $UnitRowCopyWith<$Res> {
  factory _$UnitRowCopyWith(_UnitRow value, $Res Function(_UnitRow) _then) = __$UnitRowCopyWithImpl;
@override @useResult
$Res call({
 String id, String name, String? subtitle, double? planKg, double factKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int akb, double? strike, int visitsWithoutOrder, int agents, int regionCount, FlagCounts flags, String kind, String? executionLevel, String? forecastExecutionLevel, bool isChannel
});


@override $FlagCountsCopyWith<$Res> get flags;

}
/// @nodoc
class __$UnitRowCopyWithImpl<$Res>
    implements _$UnitRowCopyWith<$Res> {
  __$UnitRowCopyWithImpl(this._self, this._then);

  final _UnitRow _self;
  final $Res Function(_UnitRow) _then;

/// Create a copy of UnitRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,Object? subtitle = freezed,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? akb = null,Object? strike = freezed,Object? visitsWithoutOrder = null,Object? agents = null,Object? regionCount = null,Object? flags = null,Object? kind = null,Object? executionLevel = freezed,Object? forecastExecutionLevel = freezed,Object? isChannel = null,}) {
  return _then(_UnitRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,subtitle: freezed == subtitle ? _self.subtitle : subtitle // ignore: cast_nullable_to_non_nullable
as String?,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,strike: freezed == strike ? _self.strike : strike // ignore: cast_nullable_to_non_nullable
as double?,visitsWithoutOrder: null == visitsWithoutOrder ? _self.visitsWithoutOrder : visitsWithoutOrder // ignore: cast_nullable_to_non_nullable
as int,agents: null == agents ? _self.agents : agents // ignore: cast_nullable_to_non_nullable
as int,regionCount: null == regionCount ? _self.regionCount : regionCount // ignore: cast_nullable_to_non_nullable
as int,flags: null == flags ? _self.flags : flags // ignore: cast_nullable_to_non_nullable
as FlagCounts,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,forecastExecutionLevel: freezed == forecastExecutionLevel ? _self.forecastExecutionLevel : forecastExecutionLevel // ignore: cast_nullable_to_non_nullable
as String?,isChannel: null == isChannel ? _self.isChannel : isChannel // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

/// Create a copy of UnitRow
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
mixin _$SameDaysRow {

 String get id; String get name; double get kgBefore; double get kgNow; double? get kgDelta; double get sumBefore; double get sumNow; double? get sumDelta; int get akbNow; double? get akbDelta;
/// Create a copy of SameDaysRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SameDaysRowCopyWith<SameDaysRow> get copyWith => _$SameDaysRowCopyWithImpl<SameDaysRow>(this as SameDaysRow, _$identity);

  /// Serializes this SameDaysRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SameDaysRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SameDaysRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.kgBefore, _this.kgBefore) || other.kgBefore == _this.kgBefore)&&(identical(other.kgNow, _this.kgNow) || other.kgNow == _this.kgNow)&&(identical(other.kgDelta, _this.kgDelta) || other.kgDelta == _this.kgDelta)&&(identical(other.sumBefore, _this.sumBefore) || other.sumBefore == _this.sumBefore)&&(identical(other.sumNow, _this.sumNow) || other.sumNow == _this.sumNow)&&(identical(other.sumDelta, _this.sumDelta) || other.sumDelta == _this.sumDelta)&&(identical(other.akbNow, _this.akbNow) || other.akbNow == _this.akbNow)&&(identical(other.akbDelta, _this.akbDelta) || other.akbDelta == _this.akbDelta));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SameDaysRow;
  return Object.hash(runtimeType,_this.id,_this.name,_this.kgBefore,_this.kgNow,_this.kgDelta,_this.sumBefore,_this.sumNow,_this.sumDelta,_this.akbNow,_this.akbDelta);
}

@override
String toString() {
  final _this = this as SameDaysRow;
  return 'SameDaysRow(id: ${_this.id}, name: ${_this.name}, kgBefore: ${_this.kgBefore}, kgNow: ${_this.kgNow}, kgDelta: ${_this.kgDelta}, sumBefore: ${_this.sumBefore}, sumNow: ${_this.sumNow}, sumDelta: ${_this.sumDelta}, akbNow: ${_this.akbNow}, akbDelta: ${_this.akbDelta})';
}


}

/// @nodoc
abstract mixin class $SameDaysRowCopyWith<$Res>  {
  factory $SameDaysRowCopyWith(SameDaysRow value, $Res Function(SameDaysRow) _then) = _$SameDaysRowCopyWithImpl;
@useResult
$Res call({
 String id, String name, double kgBefore, double kgNow, double? kgDelta, double sumBefore, double sumNow, double? sumDelta, int akbNow, double? akbDelta
});




}
/// @nodoc
class _$SameDaysRowCopyWithImpl<$Res>
    implements $SameDaysRowCopyWith<$Res> {
  _$SameDaysRowCopyWithImpl(this._self, this._then);

  final SameDaysRow _self;
  final $Res Function(SameDaysRow) _then;

/// Create a copy of SameDaysRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,Object? kgBefore = null,Object? kgNow = null,Object? kgDelta = freezed,Object? sumBefore = null,Object? sumNow = null,Object? sumDelta = freezed,Object? akbNow = null,Object? akbDelta = freezed,}) {
  return _then(SameDaysRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,kgBefore: null == kgBefore ? _self.kgBefore : kgBefore // ignore: cast_nullable_to_non_nullable
as double,kgNow: null == kgNow ? _self.kgNow : kgNow // ignore: cast_nullable_to_non_nullable
as double,kgDelta: freezed == kgDelta ? _self.kgDelta : kgDelta // ignore: cast_nullable_to_non_nullable
as double?,sumBefore: null == sumBefore ? _self.sumBefore : sumBefore // ignore: cast_nullable_to_non_nullable
as double,sumNow: null == sumNow ? _self.sumNow : sumNow // ignore: cast_nullable_to_non_nullable
as double,sumDelta: freezed == sumDelta ? _self.sumDelta : sumDelta // ignore: cast_nullable_to_non_nullable
as double?,akbNow: null == akbNow ? _self.akbNow : akbNow // ignore: cast_nullable_to_non_nullable
as int,akbDelta: freezed == akbDelta ? _self.akbDelta : akbDelta // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [SameDaysRow].
extension SameDaysRowPatterns on SameDaysRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SameDaysRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SameDaysRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SameDaysRow value)  $default,){
final _that = this;
switch (_that) {
case _SameDaysRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SameDaysRow value)?  $default,){
final _that = this;
switch (_that) {
case _SameDaysRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String name,  double kgBefore,  double kgNow,  double? kgDelta,  double sumBefore,  double sumNow,  double? sumDelta,  int akbNow,  double? akbDelta)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SameDaysRow() when $default != null:
return $default(_that.id,_that.name,_that.kgBefore,_that.kgNow,_that.kgDelta,_that.sumBefore,_that.sumNow,_that.sumDelta,_that.akbNow,_that.akbDelta);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String name,  double kgBefore,  double kgNow,  double? kgDelta,  double sumBefore,  double sumNow,  double? sumDelta,  int akbNow,  double? akbDelta)  $default,) {final _that = this;
switch (_that) {
case _SameDaysRow():
return $default(_that.id,_that.name,_that.kgBefore,_that.kgNow,_that.kgDelta,_that.sumBefore,_that.sumNow,_that.sumDelta,_that.akbNow,_that.akbDelta);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String name,  double kgBefore,  double kgNow,  double? kgDelta,  double sumBefore,  double sumNow,  double? sumDelta,  int akbNow,  double? akbDelta)?  $default,) {final _that = this;
switch (_that) {
case _SameDaysRow() when $default != null:
return $default(_that.id,_that.name,_that.kgBefore,_that.kgNow,_that.kgDelta,_that.sumBefore,_that.sumNow,_that.sumDelta,_that.akbNow,_that.akbDelta);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SameDaysRow implements SameDaysRow {
  const _SameDaysRow({required this.id, required this.name, this.kgBefore = 0, this.kgNow = 0, this.kgDelta, this.sumBefore = 0, this.sumNow = 0, this.sumDelta, this.akbNow = 0, this.akbDelta});
  factory _SameDaysRow.fromJson(Map<String, dynamic> json) => _$SameDaysRowFromJson(json);

@override final  String id;
@override final  String name;
@override@JsonKey() final  double kgBefore;
@override@JsonKey() final  double kgNow;
@override final  double? kgDelta;
@override@JsonKey() final  double sumBefore;
@override@JsonKey() final  double sumNow;
@override final  double? sumDelta;
@override@JsonKey() final  int akbNow;
@override final  double? akbDelta;

/// Create a copy of SameDaysRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SameDaysRowCopyWith<_SameDaysRow> get copyWith => __$SameDaysRowCopyWithImpl<_SameDaysRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SameDaysRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SameDaysRow&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.kgBefore, kgBefore) || other.kgBefore == kgBefore)&&(identical(other.kgNow, kgNow) || other.kgNow == kgNow)&&(identical(other.kgDelta, kgDelta) || other.kgDelta == kgDelta)&&(identical(other.sumBefore, sumBefore) || other.sumBefore == sumBefore)&&(identical(other.sumNow, sumNow) || other.sumNow == sumNow)&&(identical(other.sumDelta, sumDelta) || other.sumDelta == sumDelta)&&(identical(other.akbNow, akbNow) || other.akbNow == akbNow)&&(identical(other.akbDelta, akbDelta) || other.akbDelta == akbDelta));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name,kgBefore,kgNow,kgDelta,sumBefore,sumNow,sumDelta,akbNow,akbDelta);
}

@override
String toString() {
    return 'SameDaysRow(id: $id, name: $name, kgBefore: $kgBefore, kgNow: $kgNow, kgDelta: $kgDelta, sumBefore: $sumBefore, sumNow: $sumNow, sumDelta: $sumDelta, akbNow: $akbNow, akbDelta: $akbDelta)';
}


}

/// @nodoc
abstract mixin class _$SameDaysRowCopyWith<$Res> implements $SameDaysRowCopyWith<$Res> {
  factory _$SameDaysRowCopyWith(_SameDaysRow value, $Res Function(_SameDaysRow) _then) = __$SameDaysRowCopyWithImpl;
@override @useResult
$Res call({
 String id, String name, double kgBefore, double kgNow, double? kgDelta, double sumBefore, double sumNow, double? sumDelta, int akbNow, double? akbDelta
});




}
/// @nodoc
class __$SameDaysRowCopyWithImpl<$Res>
    implements _$SameDaysRowCopyWith<$Res> {
  __$SameDaysRowCopyWithImpl(this._self, this._then);

  final _SameDaysRow _self;
  final $Res Function(_SameDaysRow) _then;

/// Create a copy of SameDaysRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,Object? kgBefore = null,Object? kgNow = null,Object? kgDelta = freezed,Object? sumBefore = null,Object? sumNow = null,Object? sumDelta = freezed,Object? akbNow = null,Object? akbDelta = freezed,}) {
  return _then(_SameDaysRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,kgBefore: null == kgBefore ? _self.kgBefore : kgBefore // ignore: cast_nullable_to_non_nullable
as double,kgNow: null == kgNow ? _self.kgNow : kgNow // ignore: cast_nullable_to_non_nullable
as double,kgDelta: freezed == kgDelta ? _self.kgDelta : kgDelta // ignore: cast_nullable_to_non_nullable
as double?,sumBefore: null == sumBefore ? _self.sumBefore : sumBefore // ignore: cast_nullable_to_non_nullable
as double,sumNow: null == sumNow ? _self.sumNow : sumNow // ignore: cast_nullable_to_non_nullable
as double,sumDelta: freezed == sumDelta ? _self.sumDelta : sumDelta // ignore: cast_nullable_to_non_nullable
as double?,akbNow: null == akbNow ? _self.akbNow : akbNow // ignore: cast_nullable_to_non_nullable
as int,akbDelta: freezed == akbDelta ? _self.akbDelta : akbDelta // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$CategoryCard {

 String get id; String get name; int get skuSold; int get skuTotal; double get factKg; double? get weightShare; double get revenue; int get akb; double? get distribution; double? get forecastKg; double? get vsPrevMonth; int get silent; int get lost;
/// Create a copy of CategoryCard
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CategoryCardCopyWith<CategoryCard> get copyWith => _$CategoryCardCopyWithImpl<CategoryCard>(this as CategoryCard, _$identity);

  /// Serializes this CategoryCard to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CategoryCard;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CategoryCard&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.skuSold, _this.skuSold) || other.skuSold == _this.skuSold)&&(identical(other.skuTotal, _this.skuTotal) || other.skuTotal == _this.skuTotal)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.weightShare, _this.weightShare) || other.weightShare == _this.weightShare)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.akb, _this.akb) || other.akb == _this.akb)&&(identical(other.distribution, _this.distribution) || other.distribution == _this.distribution)&&(identical(other.forecastKg, _this.forecastKg) || other.forecastKg == _this.forecastKg)&&(identical(other.vsPrevMonth, _this.vsPrevMonth) || other.vsPrevMonth == _this.vsPrevMonth)&&(identical(other.silent, _this.silent) || other.silent == _this.silent)&&(identical(other.lost, _this.lost) || other.lost == _this.lost));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CategoryCard;
  return Object.hash(runtimeType,_this.id,_this.name,_this.skuSold,_this.skuTotal,_this.factKg,_this.weightShare,_this.revenue,_this.akb,_this.distribution,_this.forecastKg,_this.vsPrevMonth,_this.silent,_this.lost);
}

@override
String toString() {
  final _this = this as CategoryCard;
  return 'CategoryCard(id: ${_this.id}, name: ${_this.name}, skuSold: ${_this.skuSold}, skuTotal: ${_this.skuTotal}, factKg: ${_this.factKg}, weightShare: ${_this.weightShare}, revenue: ${_this.revenue}, akb: ${_this.akb}, distribution: ${_this.distribution}, forecastKg: ${_this.forecastKg}, vsPrevMonth: ${_this.vsPrevMonth}, silent: ${_this.silent}, lost: ${_this.lost})';
}


}

/// @nodoc
abstract mixin class $CategoryCardCopyWith<$Res>  {
  factory $CategoryCardCopyWith(CategoryCard value, $Res Function(CategoryCard) _then) = _$CategoryCardCopyWithImpl;
@useResult
$Res call({
 String id, String name, int skuSold, int skuTotal, double factKg, double? weightShare, double revenue, int akb, double? distribution, double? forecastKg, double? vsPrevMonth, int silent, int lost
});




}
/// @nodoc
class _$CategoryCardCopyWithImpl<$Res>
    implements $CategoryCardCopyWith<$Res> {
  _$CategoryCardCopyWithImpl(this._self, this._then);

  final CategoryCard _self;
  final $Res Function(CategoryCard) _then;

/// Create a copy of CategoryCard
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,Object? skuSold = null,Object? skuTotal = null,Object? factKg = null,Object? weightShare = freezed,Object? revenue = null,Object? akb = null,Object? distribution = freezed,Object? forecastKg = freezed,Object? vsPrevMonth = freezed,Object? silent = null,Object? lost = null,}) {
  return _then(CategoryCard(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,skuSold: null == skuSold ? _self.skuSold : skuSold // ignore: cast_nullable_to_non_nullable
as int,skuTotal: null == skuTotal ? _self.skuTotal : skuTotal // ignore: cast_nullable_to_non_nullable
as int,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,weightShare: freezed == weightShare ? _self.weightShare : weightShare // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,distribution: freezed == distribution ? _self.distribution : distribution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,vsPrevMonth: freezed == vsPrevMonth ? _self.vsPrevMonth : vsPrevMonth // ignore: cast_nullable_to_non_nullable
as double?,silent: null == silent ? _self.silent : silent // ignore: cast_nullable_to_non_nullable
as int,lost: null == lost ? _self.lost : lost // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [CategoryCard].
extension CategoryCardPatterns on CategoryCard {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CategoryCard value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CategoryCard() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CategoryCard value)  $default,){
final _that = this;
switch (_that) {
case _CategoryCard():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CategoryCard value)?  $default,){
final _that = this;
switch (_that) {
case _CategoryCard() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String name,  int skuSold,  int skuTotal,  double factKg,  double? weightShare,  double revenue,  int akb,  double? distribution,  double? forecastKg,  double? vsPrevMonth,  int silent,  int lost)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CategoryCard() when $default != null:
return $default(_that.id,_that.name,_that.skuSold,_that.skuTotal,_that.factKg,_that.weightShare,_that.revenue,_that.akb,_that.distribution,_that.forecastKg,_that.vsPrevMonth,_that.silent,_that.lost);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String name,  int skuSold,  int skuTotal,  double factKg,  double? weightShare,  double revenue,  int akb,  double? distribution,  double? forecastKg,  double? vsPrevMonth,  int silent,  int lost)  $default,) {final _that = this;
switch (_that) {
case _CategoryCard():
return $default(_that.id,_that.name,_that.skuSold,_that.skuTotal,_that.factKg,_that.weightShare,_that.revenue,_that.akb,_that.distribution,_that.forecastKg,_that.vsPrevMonth,_that.silent,_that.lost);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String name,  int skuSold,  int skuTotal,  double factKg,  double? weightShare,  double revenue,  int akb,  double? distribution,  double? forecastKg,  double? vsPrevMonth,  int silent,  int lost)?  $default,) {final _that = this;
switch (_that) {
case _CategoryCard() when $default != null:
return $default(_that.id,_that.name,_that.skuSold,_that.skuTotal,_that.factKg,_that.weightShare,_that.revenue,_that.akb,_that.distribution,_that.forecastKg,_that.vsPrevMonth,_that.silent,_that.lost);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CategoryCard implements CategoryCard {
  const _CategoryCard({required this.id, required this.name, this.skuSold = 0, this.skuTotal = 0, this.factKg = 0, this.weightShare, this.revenue = 0, this.akb = 0, this.distribution, this.forecastKg, this.vsPrevMonth, this.silent = 0, this.lost = 0});
  factory _CategoryCard.fromJson(Map<String, dynamic> json) => _$CategoryCardFromJson(json);

@override final  String id;
@override final  String name;
@override@JsonKey() final  int skuSold;
@override@JsonKey() final  int skuTotal;
@override@JsonKey() final  double factKg;
@override final  double? weightShare;
@override@JsonKey() final  double revenue;
@override@JsonKey() final  int akb;
@override final  double? distribution;
@override final  double? forecastKg;
@override final  double? vsPrevMonth;
@override@JsonKey() final  int silent;
@override@JsonKey() final  int lost;

/// Create a copy of CategoryCard
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CategoryCardCopyWith<_CategoryCard> get copyWith => __$CategoryCardCopyWithImpl<_CategoryCard>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CategoryCardToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CategoryCard&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.skuSold, skuSold) || other.skuSold == skuSold)&&(identical(other.skuTotal, skuTotal) || other.skuTotal == skuTotal)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.weightShare, weightShare) || other.weightShare == weightShare)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.akb, akb) || other.akb == akb)&&(identical(other.distribution, distribution) || other.distribution == distribution)&&(identical(other.forecastKg, forecastKg) || other.forecastKg == forecastKg)&&(identical(other.vsPrevMonth, vsPrevMonth) || other.vsPrevMonth == vsPrevMonth)&&(identical(other.silent, silent) || other.silent == silent)&&(identical(other.lost, lost) || other.lost == lost));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name,skuSold,skuTotal,factKg,weightShare,revenue,akb,distribution,forecastKg,vsPrevMonth,silent,lost);
}

@override
String toString() {
    return 'CategoryCard(id: $id, name: $name, skuSold: $skuSold, skuTotal: $skuTotal, factKg: $factKg, weightShare: $weightShare, revenue: $revenue, akb: $akb, distribution: $distribution, forecastKg: $forecastKg, vsPrevMonth: $vsPrevMonth, silent: $silent, lost: $lost)';
}


}

/// @nodoc
abstract mixin class _$CategoryCardCopyWith<$Res> implements $CategoryCardCopyWith<$Res> {
  factory _$CategoryCardCopyWith(_CategoryCard value, $Res Function(_CategoryCard) _then) = __$CategoryCardCopyWithImpl;
@override @useResult
$Res call({
 String id, String name, int skuSold, int skuTotal, double factKg, double? weightShare, double revenue, int akb, double? distribution, double? forecastKg, double? vsPrevMonth, int silent, int lost
});




}
/// @nodoc
class __$CategoryCardCopyWithImpl<$Res>
    implements _$CategoryCardCopyWith<$Res> {
  __$CategoryCardCopyWithImpl(this._self, this._then);

  final _CategoryCard _self;
  final $Res Function(_CategoryCard) _then;

/// Create a copy of CategoryCard
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,Object? skuSold = null,Object? skuTotal = null,Object? factKg = null,Object? weightShare = freezed,Object? revenue = null,Object? akb = null,Object? distribution = freezed,Object? forecastKg = freezed,Object? vsPrevMonth = freezed,Object? silent = null,Object? lost = null,}) {
  return _then(_CategoryCard(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,skuSold: null == skuSold ? _self.skuSold : skuSold // ignore: cast_nullable_to_non_nullable
as int,skuTotal: null == skuTotal ? _self.skuTotal : skuTotal // ignore: cast_nullable_to_non_nullable
as int,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,weightShare: freezed == weightShare ? _self.weightShare : weightShare // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,distribution: freezed == distribution ? _self.distribution : distribution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,vsPrevMonth: freezed == vsPrevMonth ? _self.vsPrevMonth : vsPrevMonth // ignore: cast_nullable_to_non_nullable
as double?,silent: null == silent ? _self.silent : silent // ignore: cast_nullable_to_non_nullable
as int,lost: null == lost ? _self.lost : lost // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$GroupView {

 SalesPeriod get period; String get name; String? get subtitle; SalesKpi get kpi; List<UnitRow> get cards; List<UnitRow> get regions; List<SameDaysRow> get sameDays; List<CategoryCard> get categoryCards;/// Кг по дням месяца (календарь всего уровня).
 List<double?> get days;
/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$GroupViewCopyWith<GroupView> get copyWith => _$GroupViewCopyWithImpl<GroupView>(this as GroupView, _$identity);

  /// Serializes this GroupView to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as GroupView;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is GroupView&&(identical(other.period, _this.period) || other.period == _this.period)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.subtitle, _this.subtitle) || other.subtitle == _this.subtitle)&&(identical(other.kpi, _this.kpi) || other.kpi == _this.kpi)&&const DeepCollectionEquality().equals(other.cards, _this.cards)&&const DeepCollectionEquality().equals(other.regions, _this.regions)&&const DeepCollectionEquality().equals(other.sameDays, _this.sameDays)&&const DeepCollectionEquality().equals(other.categoryCards, _this.categoryCards)&&const DeepCollectionEquality().equals(other.days, _this.days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as GroupView;
  return Object.hash(runtimeType,_this.period,_this.name,_this.subtitle,_this.kpi,const DeepCollectionEquality().hash(_this.cards),const DeepCollectionEquality().hash(_this.regions),const DeepCollectionEquality().hash(_this.sameDays),const DeepCollectionEquality().hash(_this.categoryCards),const DeepCollectionEquality().hash(_this.days));
}

@override
String toString() {
  final _this = this as GroupView;
  return 'GroupView(period: ${_this.period}, name: ${_this.name}, subtitle: ${_this.subtitle}, kpi: ${_this.kpi}, cards: ${_this.cards}, regions: ${_this.regions}, sameDays: ${_this.sameDays}, categoryCards: ${_this.categoryCards}, days: ${_this.days})';
}


}

/// @nodoc
abstract mixin class $GroupViewCopyWith<$Res>  {
  factory $GroupViewCopyWith(GroupView value, $Res Function(GroupView) _then) = _$GroupViewCopyWithImpl;
@useResult
$Res call({
 SalesPeriod period, String name, String? subtitle, SalesKpi kpi, List<UnitRow> cards, List<UnitRow> regions, List<SameDaysRow> sameDays, List<CategoryCard> categoryCards, List<double?> days
});


$SalesPeriodCopyWith<$Res> get period;$SalesKpiCopyWith<$Res> get kpi;

}
/// @nodoc
class _$GroupViewCopyWithImpl<$Res>
    implements $GroupViewCopyWith<$Res> {
  _$GroupViewCopyWithImpl(this._self, this._then);

  final GroupView _self;
  final $Res Function(GroupView) _then;

/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? period = null,Object? name = null,Object? subtitle = freezed,Object? kpi = null,Object? cards = null,Object? regions = null,Object? sameDays = null,Object? categoryCards = null,Object? days = null,}) {
  return _then(GroupView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,subtitle: freezed == subtitle ? _self.subtitle : subtitle // ignore: cast_nullable_to_non_nullable
as String?,kpi: null == kpi ? _self.kpi : kpi // ignore: cast_nullable_to_non_nullable
as SalesKpi,cards: null == cards ? _self.cards : cards // ignore: cast_nullable_to_non_nullable
as List<UnitRow>,regions: null == regions ? _self.regions : regions // ignore: cast_nullable_to_non_nullable
as List<UnitRow>,sameDays: null == sameDays ? _self.sameDays : sameDays // ignore: cast_nullable_to_non_nullable
as List<SameDaysRow>,categoryCards: null == categoryCards ? _self.categoryCards : categoryCards // ignore: cast_nullable_to_non_nullable
as List<CategoryCard>,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as List<double?>,
  ));
}
/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesKpiCopyWith<$Res> get kpi {
  
  return $SalesKpiCopyWith<$Res>(_self.kpi, (value) {
    return _then(_self.copyWith(kpi: value));
  });
}
}


/// Adds pattern-matching-related methods to [GroupView].
extension GroupViewPatterns on GroupView {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _GroupView value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _GroupView() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _GroupView value)  $default,){
final _that = this;
switch (_that) {
case _GroupView():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _GroupView value)?  $default,){
final _that = this;
switch (_that) {
case _GroupView() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( SalesPeriod period,  String name,  String? subtitle,  SalesKpi kpi,  List<UnitRow> cards,  List<UnitRow> regions,  List<SameDaysRow> sameDays,  List<CategoryCard> categoryCards,  List<double?> days)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _GroupView() when $default != null:
return $default(_that.period,_that.name,_that.subtitle,_that.kpi,_that.cards,_that.regions,_that.sameDays,_that.categoryCards,_that.days);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( SalesPeriod period,  String name,  String? subtitle,  SalesKpi kpi,  List<UnitRow> cards,  List<UnitRow> regions,  List<SameDaysRow> sameDays,  List<CategoryCard> categoryCards,  List<double?> days)  $default,) {final _that = this;
switch (_that) {
case _GroupView():
return $default(_that.period,_that.name,_that.subtitle,_that.kpi,_that.cards,_that.regions,_that.sameDays,_that.categoryCards,_that.days);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( SalesPeriod period,  String name,  String? subtitle,  SalesKpi kpi,  List<UnitRow> cards,  List<UnitRow> regions,  List<SameDaysRow> sameDays,  List<CategoryCard> categoryCards,  List<double?> days)?  $default,) {final _that = this;
switch (_that) {
case _GroupView() when $default != null:
return $default(_that.period,_that.name,_that.subtitle,_that.kpi,_that.cards,_that.regions,_that.sameDays,_that.categoryCards,_that.days);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _GroupView implements GroupView {
  const _GroupView({required this.period, required this.name, this.subtitle, required this.kpi,  List<UnitRow> cards = const <UnitRow>[],  List<UnitRow> regions = const <UnitRow>[],  List<SameDaysRow> sameDays = const <SameDaysRow>[],  List<CategoryCard> categoryCards = const <CategoryCard>[],  List<double?> days = const <double?>[]}): _cards = cards,_regions = regions,_sameDays = sameDays,_categoryCards = categoryCards,_days = days;
  factory _GroupView.fromJson(Map<String, dynamic> json) => _$GroupViewFromJson(json);

@override final  SalesPeriod period;
@override final  String name;
@override final  String? subtitle;
@override final  SalesKpi kpi;
 final  List<UnitRow> _cards;
@override@JsonKey() List<UnitRow> get cards {
  if (_cards is EqualUnmodifiableListView) return _cards;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_cards);
}

 final  List<UnitRow> _regions;
@override@JsonKey() List<UnitRow> get regions {
  if (_regions is EqualUnmodifiableListView) return _regions;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_regions);
}

 final  List<SameDaysRow> _sameDays;
@override@JsonKey() List<SameDaysRow> get sameDays {
  if (_sameDays is EqualUnmodifiableListView) return _sameDays;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_sameDays);
}

 final  List<CategoryCard> _categoryCards;
@override@JsonKey() List<CategoryCard> get categoryCards {
  if (_categoryCards is EqualUnmodifiableListView) return _categoryCards;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_categoryCards);
}

/// Кг по дням месяца (календарь всего уровня).
 final  List<double?> _days;
/// Кг по дням месяца (календарь всего уровня).
@override@JsonKey() List<double?> get days {
  if (_days is EqualUnmodifiableListView) return _days;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_days);
}


/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$GroupViewCopyWith<_GroupView> get copyWith => __$GroupViewCopyWithImpl<_GroupView>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$GroupViewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _GroupView&&(identical(other.period, period) || other.period == period)&&(identical(other.name, name) || other.name == name)&&(identical(other.subtitle, subtitle) || other.subtitle == subtitle)&&(identical(other.kpi, kpi) || other.kpi == kpi)&&const DeepCollectionEquality().equals(other.cards, _cards)&&const DeepCollectionEquality().equals(other.regions, _regions)&&const DeepCollectionEquality().equals(other.sameDays, _sameDays)&&const DeepCollectionEquality().equals(other.categoryCards, _categoryCards)&&const DeepCollectionEquality().equals(other.days, _days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,period,name,subtitle,kpi,const DeepCollectionEquality().hash(_cards),const DeepCollectionEquality().hash(_regions),const DeepCollectionEquality().hash(_sameDays),const DeepCollectionEquality().hash(_categoryCards),const DeepCollectionEquality().hash(_days));
}

@override
String toString() {
    return 'GroupView(period: $period, name: $name, subtitle: $subtitle, kpi: $kpi, cards: $cards, regions: $regions, sameDays: $sameDays, categoryCards: $categoryCards, days: $days)';
}


}

/// @nodoc
abstract mixin class _$GroupViewCopyWith<$Res> implements $GroupViewCopyWith<$Res> {
  factory _$GroupViewCopyWith(_GroupView value, $Res Function(_GroupView) _then) = __$GroupViewCopyWithImpl;
@override @useResult
$Res call({
 SalesPeriod period, String name, String? subtitle, SalesKpi kpi, List<UnitRow> cards, List<UnitRow> regions, List<SameDaysRow> sameDays, List<CategoryCard> categoryCards, List<double?> days
});


@override $SalesPeriodCopyWith<$Res> get period;@override $SalesKpiCopyWith<$Res> get kpi;

}
/// @nodoc
class __$GroupViewCopyWithImpl<$Res>
    implements _$GroupViewCopyWith<$Res> {
  __$GroupViewCopyWithImpl(this._self, this._then);

  final _GroupView _self;
  final $Res Function(_GroupView) _then;

/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? period = null,Object? name = null,Object? subtitle = freezed,Object? kpi = null,Object? cards = null,Object? regions = null,Object? sameDays = null,Object? categoryCards = null,Object? days = null,}) {
  return _then(_GroupView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,subtitle: freezed == subtitle ? _self.subtitle : subtitle // ignore: cast_nullable_to_non_nullable
as String?,kpi: null == kpi ? _self.kpi : kpi // ignore: cast_nullable_to_non_nullable
as SalesKpi,cards: null == cards ? _self._cards : cards // ignore: cast_nullable_to_non_nullable
as List<UnitRow>,regions: null == regions ? _self._regions : regions // ignore: cast_nullable_to_non_nullable
as List<UnitRow>,sameDays: null == sameDays ? _self._sameDays : sameDays // ignore: cast_nullable_to_non_nullable
as List<SameDaysRow>,categoryCards: null == categoryCards ? _self._categoryCards : categoryCards // ignore: cast_nullable_to_non_nullable
as List<CategoryCard>,days: null == days ? _self._days : days // ignore: cast_nullable_to_non_nullable
as List<double?>,
  ));
}

/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of GroupView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesKpiCopyWith<$Res> get kpi {
  
  return $SalesKpiCopyWith<$Res>(_self.kpi, (value) {
    return _then(_self.copyWith(kpi: value));
  });
}
}


/// @nodoc
mixin _$TeamRow {

 int get agentId; String get name; double? get planKg; double get factKg; double? get execution; double? get forecastKg; double? get forecastExecution; double get revenue; int get visits; int get orders; double? get strike; bool get isVacancy; List<AgentFlag> get flags; String? get executionLevel; int get akb;
/// Create a copy of TeamRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TeamRowCopyWith<TeamRow> get copyWith => _$TeamRowCopyWithImpl<TeamRow>(this as TeamRow, _$identity);

  /// Serializes this TeamRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TeamRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TeamRow&&(identical(other.agentId, _this.agentId) || other.agentId == _this.agentId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.execution, _this.execution) || other.execution == _this.execution)&&(identical(other.forecastKg, _this.forecastKg) || other.forecastKg == _this.forecastKg)&&(identical(other.forecastExecution, _this.forecastExecution) || other.forecastExecution == _this.forecastExecution)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.visits, _this.visits) || other.visits == _this.visits)&&(identical(other.orders, _this.orders) || other.orders == _this.orders)&&(identical(other.strike, _this.strike) || other.strike == _this.strike)&&(identical(other.isVacancy, _this.isVacancy) || other.isVacancy == _this.isVacancy)&&const DeepCollectionEquality().equals(other.flags, _this.flags)&&(identical(other.executionLevel, _this.executionLevel) || other.executionLevel == _this.executionLevel)&&(identical(other.akb, _this.akb) || other.akb == _this.akb));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TeamRow;
  return Object.hash(runtimeType,_this.agentId,_this.name,_this.planKg,_this.factKg,_this.execution,_this.forecastKg,_this.forecastExecution,_this.revenue,_this.visits,_this.orders,_this.strike,_this.isVacancy,const DeepCollectionEquality().hash(_this.flags),_this.executionLevel,_this.akb);
}

@override
String toString() {
  final _this = this as TeamRow;
  return 'TeamRow(agentId: ${_this.agentId}, name: ${_this.name}, planKg: ${_this.planKg}, factKg: ${_this.factKg}, execution: ${_this.execution}, forecastKg: ${_this.forecastKg}, forecastExecution: ${_this.forecastExecution}, revenue: ${_this.revenue}, visits: ${_this.visits}, orders: ${_this.orders}, strike: ${_this.strike}, isVacancy: ${_this.isVacancy}, flags: ${_this.flags}, executionLevel: ${_this.executionLevel}, akb: ${_this.akb})';
}


}

/// @nodoc
abstract mixin class $TeamRowCopyWith<$Res>  {
  factory $TeamRowCopyWith(TeamRow value, $Res Function(TeamRow) _then) = _$TeamRowCopyWithImpl;
@useResult
$Res call({
 int agentId, String name, double? planKg, double factKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int visits, int orders, double? strike, bool isVacancy, List<AgentFlag> flags, String? executionLevel, int akb
});




}
/// @nodoc
class _$TeamRowCopyWithImpl<$Res>
    implements $TeamRowCopyWith<$Res> {
  _$TeamRowCopyWithImpl(this._self, this._then);

  final TeamRow _self;
  final $Res Function(TeamRow) _then;

/// Create a copy of TeamRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? agentId = null,Object? name = null,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? visits = null,Object? orders = null,Object? strike = freezed,Object? isVacancy = null,Object? flags = null,Object? executionLevel = freezed,Object? akb = null,}) {
  return _then(TeamRow(
agentId: null == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,visits: null == visits ? _self.visits : visits // ignore: cast_nullable_to_non_nullable
as int,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,strike: freezed == strike ? _self.strike : strike // ignore: cast_nullable_to_non_nullable
as double?,isVacancy: null == isVacancy ? _self.isVacancy : isVacancy // ignore: cast_nullable_to_non_nullable
as bool,flags: null == flags ? _self.flags : flags // ignore: cast_nullable_to_non_nullable
as List<AgentFlag>,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [TeamRow].
extension TeamRowPatterns on TeamRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TeamRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TeamRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TeamRow value)  $default,){
final _that = this;
switch (_that) {
case _TeamRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TeamRow value)?  $default,){
final _that = this;
switch (_that) {
case _TeamRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int agentId,  String name,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int visits,  int orders,  double? strike,  bool isVacancy,  List<AgentFlag> flags,  String? executionLevel,  int akb)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TeamRow() when $default != null:
return $default(_that.agentId,_that.name,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.visits,_that.orders,_that.strike,_that.isVacancy,_that.flags,_that.executionLevel,_that.akb);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int agentId,  String name,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int visits,  int orders,  double? strike,  bool isVacancy,  List<AgentFlag> flags,  String? executionLevel,  int akb)  $default,) {final _that = this;
switch (_that) {
case _TeamRow():
return $default(_that.agentId,_that.name,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.visits,_that.orders,_that.strike,_that.isVacancy,_that.flags,_that.executionLevel,_that.akb);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int agentId,  String name,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int visits,  int orders,  double? strike,  bool isVacancy,  List<AgentFlag> flags,  String? executionLevel,  int akb)?  $default,) {final _that = this;
switch (_that) {
case _TeamRow() when $default != null:
return $default(_that.agentId,_that.name,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.visits,_that.orders,_that.strike,_that.isVacancy,_that.flags,_that.executionLevel,_that.akb);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TeamRow implements TeamRow {
  const _TeamRow({required this.agentId, required this.name, this.planKg, this.factKg = 0, this.execution, this.forecastKg, this.forecastExecution, this.revenue = 0, this.visits = 0, this.orders = 0, this.strike, this.isVacancy = false,  List<AgentFlag> flags = const <AgentFlag>[], this.executionLevel, this.akb = 0}): _flags = flags;
  factory _TeamRow.fromJson(Map<String, dynamic> json) => _$TeamRowFromJson(json);

@override final  int agentId;
@override final  String name;
@override final  double? planKg;
@override@JsonKey() final  double factKg;
@override final  double? execution;
@override final  double? forecastKg;
@override final  double? forecastExecution;
@override@JsonKey() final  double revenue;
@override@JsonKey() final  int visits;
@override@JsonKey() final  int orders;
@override final  double? strike;
@override@JsonKey() final  bool isVacancy;
 final  List<AgentFlag> _flags;
@override@JsonKey() List<AgentFlag> get flags {
  if (_flags is EqualUnmodifiableListView) return _flags;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_flags);
}

@override final  String? executionLevel;
@override@JsonKey() final  int akb;

/// Create a copy of TeamRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TeamRowCopyWith<_TeamRow> get copyWith => __$TeamRowCopyWithImpl<_TeamRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TeamRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TeamRow&&(identical(other.agentId, agentId) || other.agentId == agentId)&&(identical(other.name, name) || other.name == name)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.execution, execution) || other.execution == execution)&&(identical(other.forecastKg, forecastKg) || other.forecastKg == forecastKg)&&(identical(other.forecastExecution, forecastExecution) || other.forecastExecution == forecastExecution)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.visits, visits) || other.visits == visits)&&(identical(other.orders, orders) || other.orders == orders)&&(identical(other.strike, strike) || other.strike == strike)&&(identical(other.isVacancy, isVacancy) || other.isVacancy == isVacancy)&&const DeepCollectionEquality().equals(other.flags, _flags)&&(identical(other.executionLevel, executionLevel) || other.executionLevel == executionLevel)&&(identical(other.akb, akb) || other.akb == akb));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,agentId,name,planKg,factKg,execution,forecastKg,forecastExecution,revenue,visits,orders,strike,isVacancy,const DeepCollectionEquality().hash(_flags),executionLevel,akb);
}

@override
String toString() {
    return 'TeamRow(agentId: $agentId, name: $name, planKg: $planKg, factKg: $factKg, execution: $execution, forecastKg: $forecastKg, forecastExecution: $forecastExecution, revenue: $revenue, visits: $visits, orders: $orders, strike: $strike, isVacancy: $isVacancy, flags: $flags, executionLevel: $executionLevel, akb: $akb)';
}


}

/// @nodoc
abstract mixin class _$TeamRowCopyWith<$Res> implements $TeamRowCopyWith<$Res> {
  factory _$TeamRowCopyWith(_TeamRow value, $Res Function(_TeamRow) _then) = __$TeamRowCopyWithImpl;
@override @useResult
$Res call({
 int agentId, String name, double? planKg, double factKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int visits, int orders, double? strike, bool isVacancy, List<AgentFlag> flags, String? executionLevel, int akb
});




}
/// @nodoc
class __$TeamRowCopyWithImpl<$Res>
    implements _$TeamRowCopyWith<$Res> {
  __$TeamRowCopyWithImpl(this._self, this._then);

  final _TeamRow _self;
  final $Res Function(_TeamRow) _then;

/// Create a copy of TeamRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? agentId = null,Object? name = null,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? visits = null,Object? orders = null,Object? strike = freezed,Object? isVacancy = null,Object? flags = null,Object? executionLevel = freezed,Object? akb = null,}) {
  return _then(_TeamRow(
agentId: null == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,visits: null == visits ? _self.visits : visits // ignore: cast_nullable_to_non_nullable
as int,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,strike: freezed == strike ? _self.strike : strike // ignore: cast_nullable_to_non_nullable
as double?,isVacancy: null == isVacancy ? _self.isVacancy : isVacancy // ignore: cast_nullable_to_non_nullable
as bool,flags: null == flags ? _self._flags : flags // ignore: cast_nullable_to_non_nullable
as List<AgentFlag>,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$PlanFactMonth {

 int get month; double? get planKg; double? get factKg;
/// Create a copy of PlanFactMonth
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$PlanFactMonthCopyWith<PlanFactMonth> get copyWith => _$PlanFactMonthCopyWithImpl<PlanFactMonth>(this as PlanFactMonth, _$identity);

  /// Serializes this PlanFactMonth to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as PlanFactMonth;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is PlanFactMonth&&(identical(other.month, _this.month) || other.month == _this.month)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as PlanFactMonth;
  return Object.hash(runtimeType,_this.month,_this.planKg,_this.factKg);
}

@override
String toString() {
  final _this = this as PlanFactMonth;
  return 'PlanFactMonth(month: ${_this.month}, planKg: ${_this.planKg}, factKg: ${_this.factKg})';
}


}

/// @nodoc
abstract mixin class $PlanFactMonthCopyWith<$Res>  {
  factory $PlanFactMonthCopyWith(PlanFactMonth value, $Res Function(PlanFactMonth) _then) = _$PlanFactMonthCopyWithImpl;
@useResult
$Res call({
 int month, double? planKg, double? factKg
});




}
/// @nodoc
class _$PlanFactMonthCopyWithImpl<$Res>
    implements $PlanFactMonthCopyWith<$Res> {
  _$PlanFactMonthCopyWithImpl(this._self, this._then);

  final PlanFactMonth _self;
  final $Res Function(PlanFactMonth) _then;

/// Create a copy of PlanFactMonth
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? month = null,Object? planKg = freezed,Object? factKg = freezed,}) {
  return _then(PlanFactMonth(
month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: freezed == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [PlanFactMonth].
extension PlanFactMonthPatterns on PlanFactMonth {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _PlanFactMonth value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _PlanFactMonth() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _PlanFactMonth value)  $default,){
final _that = this;
switch (_that) {
case _PlanFactMonth():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _PlanFactMonth value)?  $default,){
final _that = this;
switch (_that) {
case _PlanFactMonth() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int month,  double? planKg,  double? factKg)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _PlanFactMonth() when $default != null:
return $default(_that.month,_that.planKg,_that.factKg);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int month,  double? planKg,  double? factKg)  $default,) {final _that = this;
switch (_that) {
case _PlanFactMonth():
return $default(_that.month,_that.planKg,_that.factKg);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int month,  double? planKg,  double? factKg)?  $default,) {final _that = this;
switch (_that) {
case _PlanFactMonth() when $default != null:
return $default(_that.month,_that.planKg,_that.factKg);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _PlanFactMonth implements PlanFactMonth {
  const _PlanFactMonth({required this.month, this.planKg, this.factKg});
  factory _PlanFactMonth.fromJson(Map<String, dynamic> json) => _$PlanFactMonthFromJson(json);

@override final  int month;
@override final  double? planKg;
@override final  double? factKg;

/// Create a copy of PlanFactMonth
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$PlanFactMonthCopyWith<_PlanFactMonth> get copyWith => __$PlanFactMonthCopyWithImpl<_PlanFactMonth>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$PlanFactMonthToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _PlanFactMonth&&(identical(other.month, month) || other.month == month)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.factKg, factKg) || other.factKg == factKg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,month,planKg,factKg);
}

@override
String toString() {
    return 'PlanFactMonth(month: $month, planKg: $planKg, factKg: $factKg)';
}


}

/// @nodoc
abstract mixin class _$PlanFactMonthCopyWith<$Res> implements $PlanFactMonthCopyWith<$Res> {
  factory _$PlanFactMonthCopyWith(_PlanFactMonth value, $Res Function(_PlanFactMonth) _then) = __$PlanFactMonthCopyWithImpl;
@override @useResult
$Res call({
 int month, double? planKg, double? factKg
});




}
/// @nodoc
class __$PlanFactMonthCopyWithImpl<$Res>
    implements _$PlanFactMonthCopyWith<$Res> {
  __$PlanFactMonthCopyWithImpl(this._self, this._then);

  final _PlanFactMonth _self;
  final $Res Function(_PlanFactMonth) _then;

/// Create a copy of PlanFactMonth
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? month = null,Object? planKg = freezed,Object? factKg = freezed,}) {
  return _then(_PlanFactMonth(
month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: freezed == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$RevenueShare {

 String get name; double get revenue; double? get share;
/// Create a copy of RevenueShare
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$RevenueShareCopyWith<RevenueShare> get copyWith => _$RevenueShareCopyWithImpl<RevenueShare>(this as RevenueShare, _$identity);

  /// Serializes this RevenueShare to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as RevenueShare;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is RevenueShare&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.share, _this.share) || other.share == _this.share));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as RevenueShare;
  return Object.hash(runtimeType,_this.name,_this.revenue,_this.share);
}

@override
String toString() {
  final _this = this as RevenueShare;
  return 'RevenueShare(name: ${_this.name}, revenue: ${_this.revenue}, share: ${_this.share})';
}


}

/// @nodoc
abstract mixin class $RevenueShareCopyWith<$Res>  {
  factory $RevenueShareCopyWith(RevenueShare value, $Res Function(RevenueShare) _then) = _$RevenueShareCopyWithImpl;
@useResult
$Res call({
 String name, double revenue, double? share
});




}
/// @nodoc
class _$RevenueShareCopyWithImpl<$Res>
    implements $RevenueShareCopyWith<$Res> {
  _$RevenueShareCopyWithImpl(this._self, this._then);

  final RevenueShare _self;
  final $Res Function(RevenueShare) _then;

/// Create a copy of RevenueShare
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? name = null,Object? revenue = null,Object? share = freezed,}) {
  return _then(RevenueShare(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,share: freezed == share ? _self.share : share // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [RevenueShare].
extension RevenueSharePatterns on RevenueShare {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _RevenueShare value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _RevenueShare() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _RevenueShare value)  $default,){
final _that = this;
switch (_that) {
case _RevenueShare():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _RevenueShare value)?  $default,){
final _that = this;
switch (_that) {
case _RevenueShare() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String name,  double revenue,  double? share)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _RevenueShare() when $default != null:
return $default(_that.name,_that.revenue,_that.share);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String name,  double revenue,  double? share)  $default,) {final _that = this;
switch (_that) {
case _RevenueShare():
return $default(_that.name,_that.revenue,_that.share);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String name,  double revenue,  double? share)?  $default,) {final _that = this;
switch (_that) {
case _RevenueShare() when $default != null:
return $default(_that.name,_that.revenue,_that.share);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _RevenueShare implements RevenueShare {
  const _RevenueShare({required this.name, this.revenue = 0, this.share});
  factory _RevenueShare.fromJson(Map<String, dynamic> json) => _$RevenueShareFromJson(json);

@override final  String name;
@override@JsonKey() final  double revenue;
@override final  double? share;

/// Create a copy of RevenueShare
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$RevenueShareCopyWith<_RevenueShare> get copyWith => __$RevenueShareCopyWithImpl<_RevenueShare>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$RevenueShareToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _RevenueShare&&(identical(other.name, name) || other.name == name)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.share, share) || other.share == share));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,name,revenue,share);
}

@override
String toString() {
    return 'RevenueShare(name: $name, revenue: $revenue, share: $share)';
}


}

/// @nodoc
abstract mixin class _$RevenueShareCopyWith<$Res> implements $RevenueShareCopyWith<$Res> {
  factory _$RevenueShareCopyWith(_RevenueShare value, $Res Function(_RevenueShare) _then) = __$RevenueShareCopyWithImpl;
@override @useResult
$Res call({
 String name, double revenue, double? share
});




}
/// @nodoc
class __$RevenueShareCopyWithImpl<$Res>
    implements _$RevenueShareCopyWith<$Res> {
  __$RevenueShareCopyWithImpl(this._self, this._then);

  final _RevenueShare _self;
  final $Res Function(_RevenueShare) _then;

/// Create a copy of RevenueShare
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? name = null,Object? revenue = null,Object? share = freezed,}) {
  return _then(_RevenueShare(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,share: freezed == share ? _self.share : share // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$RegionView {

 SalesPeriod get period; String get id; String get name; String? get directionId; String? get directionName; String? get supervisor; String? get dealer; SalesKpi get kpi; List<PlanFactMonth> get months; List<RevenueShare> get categories; List<TeamRow> get team; List<SameDaysRow> get sameDays; List<CategoryCard> get categoryCards; List<double?> get days;
/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$RegionViewCopyWith<RegionView> get copyWith => _$RegionViewCopyWithImpl<RegionView>(this as RegionView, _$identity);

  /// Serializes this RegionView to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as RegionView;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is RegionView&&(identical(other.period, _this.period) || other.period == _this.period)&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.directionId, _this.directionId) || other.directionId == _this.directionId)&&(identical(other.directionName, _this.directionName) || other.directionName == _this.directionName)&&(identical(other.supervisor, _this.supervisor) || other.supervisor == _this.supervisor)&&(identical(other.dealer, _this.dealer) || other.dealer == _this.dealer)&&(identical(other.kpi, _this.kpi) || other.kpi == _this.kpi)&&const DeepCollectionEquality().equals(other.months, _this.months)&&const DeepCollectionEquality().equals(other.categories, _this.categories)&&const DeepCollectionEquality().equals(other.team, _this.team)&&const DeepCollectionEquality().equals(other.sameDays, _this.sameDays)&&const DeepCollectionEquality().equals(other.categoryCards, _this.categoryCards)&&const DeepCollectionEquality().equals(other.days, _this.days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as RegionView;
  return Object.hash(runtimeType,_this.period,_this.id,_this.name,_this.directionId,_this.directionName,_this.supervisor,_this.dealer,_this.kpi,const DeepCollectionEquality().hash(_this.months),const DeepCollectionEquality().hash(_this.categories),const DeepCollectionEquality().hash(_this.team),const DeepCollectionEquality().hash(_this.sameDays),const DeepCollectionEquality().hash(_this.categoryCards),const DeepCollectionEquality().hash(_this.days));
}

@override
String toString() {
  final _this = this as RegionView;
  return 'RegionView(period: ${_this.period}, id: ${_this.id}, name: ${_this.name}, directionId: ${_this.directionId}, directionName: ${_this.directionName}, supervisor: ${_this.supervisor}, dealer: ${_this.dealer}, kpi: ${_this.kpi}, months: ${_this.months}, categories: ${_this.categories}, team: ${_this.team}, sameDays: ${_this.sameDays}, categoryCards: ${_this.categoryCards}, days: ${_this.days})';
}


}

/// @nodoc
abstract mixin class $RegionViewCopyWith<$Res>  {
  factory $RegionViewCopyWith(RegionView value, $Res Function(RegionView) _then) = _$RegionViewCopyWithImpl;
@useResult
$Res call({
 SalesPeriod period, String id, String name, String? directionId, String? directionName, String? supervisor, String? dealer, SalesKpi kpi, List<PlanFactMonth> months, List<RevenueShare> categories, List<TeamRow> team, List<SameDaysRow> sameDays, List<CategoryCard> categoryCards, List<double?> days
});


$SalesPeriodCopyWith<$Res> get period;$SalesKpiCopyWith<$Res> get kpi;

}
/// @nodoc
class _$RegionViewCopyWithImpl<$Res>
    implements $RegionViewCopyWith<$Res> {
  _$RegionViewCopyWithImpl(this._self, this._then);

  final RegionView _self;
  final $Res Function(RegionView) _then;

/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? period = null,Object? id = null,Object? name = null,Object? directionId = freezed,Object? directionName = freezed,Object? supervisor = freezed,Object? dealer = freezed,Object? kpi = null,Object? months = null,Object? categories = null,Object? team = null,Object? sameDays = null,Object? categoryCards = null,Object? days = null,}) {
  return _then(RegionView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,directionId: freezed == directionId ? _self.directionId : directionId // ignore: cast_nullable_to_non_nullable
as String?,directionName: freezed == directionName ? _self.directionName : directionName // ignore: cast_nullable_to_non_nullable
as String?,supervisor: freezed == supervisor ? _self.supervisor : supervisor // ignore: cast_nullable_to_non_nullable
as String?,dealer: freezed == dealer ? _self.dealer : dealer // ignore: cast_nullable_to_non_nullable
as String?,kpi: null == kpi ? _self.kpi : kpi // ignore: cast_nullable_to_non_nullable
as SalesKpi,months: null == months ? _self.months : months // ignore: cast_nullable_to_non_nullable
as List<PlanFactMonth>,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as List<RevenueShare>,team: null == team ? _self.team : team // ignore: cast_nullable_to_non_nullable
as List<TeamRow>,sameDays: null == sameDays ? _self.sameDays : sameDays // ignore: cast_nullable_to_non_nullable
as List<SameDaysRow>,categoryCards: null == categoryCards ? _self.categoryCards : categoryCards // ignore: cast_nullable_to_non_nullable
as List<CategoryCard>,days: null == days ? _self.days : days // ignore: cast_nullable_to_non_nullable
as List<double?>,
  ));
}
/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesKpiCopyWith<$Res> get kpi {
  
  return $SalesKpiCopyWith<$Res>(_self.kpi, (value) {
    return _then(_self.copyWith(kpi: value));
  });
}
}


/// Adds pattern-matching-related methods to [RegionView].
extension RegionViewPatterns on RegionView {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _RegionView value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _RegionView() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _RegionView value)  $default,){
final _that = this;
switch (_that) {
case _RegionView():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _RegionView value)?  $default,){
final _that = this;
switch (_that) {
case _RegionView() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( SalesPeriod period,  String id,  String name,  String? directionId,  String? directionName,  String? supervisor,  String? dealer,  SalesKpi kpi,  List<PlanFactMonth> months,  List<RevenueShare> categories,  List<TeamRow> team,  List<SameDaysRow> sameDays,  List<CategoryCard> categoryCards,  List<double?> days)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _RegionView() when $default != null:
return $default(_that.period,_that.id,_that.name,_that.directionId,_that.directionName,_that.supervisor,_that.dealer,_that.kpi,_that.months,_that.categories,_that.team,_that.sameDays,_that.categoryCards,_that.days);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( SalesPeriod period,  String id,  String name,  String? directionId,  String? directionName,  String? supervisor,  String? dealer,  SalesKpi kpi,  List<PlanFactMonth> months,  List<RevenueShare> categories,  List<TeamRow> team,  List<SameDaysRow> sameDays,  List<CategoryCard> categoryCards,  List<double?> days)  $default,) {final _that = this;
switch (_that) {
case _RegionView():
return $default(_that.period,_that.id,_that.name,_that.directionId,_that.directionName,_that.supervisor,_that.dealer,_that.kpi,_that.months,_that.categories,_that.team,_that.sameDays,_that.categoryCards,_that.days);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( SalesPeriod period,  String id,  String name,  String? directionId,  String? directionName,  String? supervisor,  String? dealer,  SalesKpi kpi,  List<PlanFactMonth> months,  List<RevenueShare> categories,  List<TeamRow> team,  List<SameDaysRow> sameDays,  List<CategoryCard> categoryCards,  List<double?> days)?  $default,) {final _that = this;
switch (_that) {
case _RegionView() when $default != null:
return $default(_that.period,_that.id,_that.name,_that.directionId,_that.directionName,_that.supervisor,_that.dealer,_that.kpi,_that.months,_that.categories,_that.team,_that.sameDays,_that.categoryCards,_that.days);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _RegionView implements RegionView {
  const _RegionView({required this.period, required this.id, required this.name, this.directionId, this.directionName, this.supervisor, this.dealer, required this.kpi,  List<PlanFactMonth> months = const <PlanFactMonth>[],  List<RevenueShare> categories = const <RevenueShare>[],  List<TeamRow> team = const <TeamRow>[],  List<SameDaysRow> sameDays = const <SameDaysRow>[],  List<CategoryCard> categoryCards = const <CategoryCard>[],  List<double?> days = const <double?>[]}): _months = months,_categories = categories,_team = team,_sameDays = sameDays,_categoryCards = categoryCards,_days = days;
  factory _RegionView.fromJson(Map<String, dynamic> json) => _$RegionViewFromJson(json);

@override final  SalesPeriod period;
@override final  String id;
@override final  String name;
@override final  String? directionId;
@override final  String? directionName;
@override final  String? supervisor;
@override final  String? dealer;
@override final  SalesKpi kpi;
 final  List<PlanFactMonth> _months;
@override@JsonKey() List<PlanFactMonth> get months {
  if (_months is EqualUnmodifiableListView) return _months;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_months);
}

 final  List<RevenueShare> _categories;
@override@JsonKey() List<RevenueShare> get categories {
  if (_categories is EqualUnmodifiableListView) return _categories;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_categories);
}

 final  List<TeamRow> _team;
@override@JsonKey() List<TeamRow> get team {
  if (_team is EqualUnmodifiableListView) return _team;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_team);
}

 final  List<SameDaysRow> _sameDays;
@override@JsonKey() List<SameDaysRow> get sameDays {
  if (_sameDays is EqualUnmodifiableListView) return _sameDays;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_sameDays);
}

 final  List<CategoryCard> _categoryCards;
@override@JsonKey() List<CategoryCard> get categoryCards {
  if (_categoryCards is EqualUnmodifiableListView) return _categoryCards;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_categoryCards);
}

 final  List<double?> _days;
@override@JsonKey() List<double?> get days {
  if (_days is EqualUnmodifiableListView) return _days;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_days);
}


/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$RegionViewCopyWith<_RegionView> get copyWith => __$RegionViewCopyWithImpl<_RegionView>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$RegionViewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _RegionView&&(identical(other.period, period) || other.period == period)&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.directionId, directionId) || other.directionId == directionId)&&(identical(other.directionName, directionName) || other.directionName == directionName)&&(identical(other.supervisor, supervisor) || other.supervisor == supervisor)&&(identical(other.dealer, dealer) || other.dealer == dealer)&&(identical(other.kpi, kpi) || other.kpi == kpi)&&const DeepCollectionEquality().equals(other.months, _months)&&const DeepCollectionEquality().equals(other.categories, _categories)&&const DeepCollectionEquality().equals(other.team, _team)&&const DeepCollectionEquality().equals(other.sameDays, _sameDays)&&const DeepCollectionEquality().equals(other.categoryCards, _categoryCards)&&const DeepCollectionEquality().equals(other.days, _days));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,period,id,name,directionId,directionName,supervisor,dealer,kpi,const DeepCollectionEquality().hash(_months),const DeepCollectionEquality().hash(_categories),const DeepCollectionEquality().hash(_team),const DeepCollectionEquality().hash(_sameDays),const DeepCollectionEquality().hash(_categoryCards),const DeepCollectionEquality().hash(_days));
}

@override
String toString() {
    return 'RegionView(period: $period, id: $id, name: $name, directionId: $directionId, directionName: $directionName, supervisor: $supervisor, dealer: $dealer, kpi: $kpi, months: $months, categories: $categories, team: $team, sameDays: $sameDays, categoryCards: $categoryCards, days: $days)';
}


}

/// @nodoc
abstract mixin class _$RegionViewCopyWith<$Res> implements $RegionViewCopyWith<$Res> {
  factory _$RegionViewCopyWith(_RegionView value, $Res Function(_RegionView) _then) = __$RegionViewCopyWithImpl;
@override @useResult
$Res call({
 SalesPeriod period, String id, String name, String? directionId, String? directionName, String? supervisor, String? dealer, SalesKpi kpi, List<PlanFactMonth> months, List<RevenueShare> categories, List<TeamRow> team, List<SameDaysRow> sameDays, List<CategoryCard> categoryCards, List<double?> days
});


@override $SalesPeriodCopyWith<$Res> get period;@override $SalesKpiCopyWith<$Res> get kpi;

}
/// @nodoc
class __$RegionViewCopyWithImpl<$Res>
    implements _$RegionViewCopyWith<$Res> {
  __$RegionViewCopyWithImpl(this._self, this._then);

  final _RegionView _self;
  final $Res Function(_RegionView) _then;

/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? period = null,Object? id = null,Object? name = null,Object? directionId = freezed,Object? directionName = freezed,Object? supervisor = freezed,Object? dealer = freezed,Object? kpi = null,Object? months = null,Object? categories = null,Object? team = null,Object? sameDays = null,Object? categoryCards = null,Object? days = null,}) {
  return _then(_RegionView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,directionId: freezed == directionId ? _self.directionId : directionId // ignore: cast_nullable_to_non_nullable
as String?,directionName: freezed == directionName ? _self.directionName : directionName // ignore: cast_nullable_to_non_nullable
as String?,supervisor: freezed == supervisor ? _self.supervisor : supervisor // ignore: cast_nullable_to_non_nullable
as String?,dealer: freezed == dealer ? _self.dealer : dealer // ignore: cast_nullable_to_non_nullable
as String?,kpi: null == kpi ? _self.kpi : kpi // ignore: cast_nullable_to_non_nullable
as SalesKpi,months: null == months ? _self._months : months // ignore: cast_nullable_to_non_nullable
as List<PlanFactMonth>,categories: null == categories ? _self._categories : categories // ignore: cast_nullable_to_non_nullable
as List<RevenueShare>,team: null == team ? _self._team : team // ignore: cast_nullable_to_non_nullable
as List<TeamRow>,sameDays: null == sameDays ? _self._sameDays : sameDays // ignore: cast_nullable_to_non_nullable
as List<SameDaysRow>,categoryCards: null == categoryCards ? _self._categoryCards : categoryCards // ignore: cast_nullable_to_non_nullable
as List<CategoryCard>,days: null == days ? _self._days : days // ignore: cast_nullable_to_non_nullable
as List<double?>,
  ));
}

/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of RegionView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesKpiCopyWith<$Res> get kpi {
  
  return $SalesKpiCopyWith<$Res>(_self.kpi, (value) {
    return _then(_self.copyWith(kpi: value));
  });
}
}


/// @nodoc
mixin _$UnitRef {

 String get id; String get name;
/// Create a copy of UnitRef
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$UnitRefCopyWith<UnitRef> get copyWith => _$UnitRefCopyWithImpl<UnitRef>(this as UnitRef, _$identity);

  /// Serializes this UnitRef to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as UnitRef;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is UnitRef&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as UnitRef;
  return Object.hash(runtimeType,_this.id,_this.name);
}

@override
String toString() {
  final _this = this as UnitRef;
  return 'UnitRef(id: ${_this.id}, name: ${_this.name})';
}


}

/// @nodoc
abstract mixin class $UnitRefCopyWith<$Res>  {
  factory $UnitRefCopyWith(UnitRef value, $Res Function(UnitRef) _then) = _$UnitRefCopyWithImpl;
@useResult
$Res call({
 String id, String name
});




}
/// @nodoc
class _$UnitRefCopyWithImpl<$Res>
    implements $UnitRefCopyWith<$Res> {
  _$UnitRefCopyWithImpl(this._self, this._then);

  final UnitRef _self;
  final $Res Function(UnitRef) _then;

/// Create a copy of UnitRef
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,}) {
  return _then(UnitRef(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [UnitRef].
extension UnitRefPatterns on UnitRef {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _UnitRef value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _UnitRef() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _UnitRef value)  $default,){
final _that = this;
switch (_that) {
case _UnitRef():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _UnitRef value)?  $default,){
final _that = this;
switch (_that) {
case _UnitRef() when $default != null:
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
case _UnitRef() when $default != null:
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
case _UnitRef():
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
case _UnitRef() when $default != null:
return $default(_that.id,_that.name);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _UnitRef implements UnitRef {
  const _UnitRef({required this.id, required this.name});
  factory _UnitRef.fromJson(Map<String, dynamic> json) => _$UnitRefFromJson(json);

@override final  String id;
@override final  String name;

/// Create a copy of UnitRef
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$UnitRefCopyWith<_UnitRef> get copyWith => __$UnitRefCopyWithImpl<_UnitRef>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$UnitRefToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _UnitRef&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name);
}

@override
String toString() {
    return 'UnitRef(id: $id, name: $name)';
}


}

/// @nodoc
abstract mixin class _$UnitRefCopyWith<$Res> implements $UnitRefCopyWith<$Res> {
  factory _$UnitRefCopyWith(_UnitRef value, $Res Function(_UnitRef) _then) = __$UnitRefCopyWithImpl;
@override @useResult
$Res call({
 String id, String name
});




}
/// @nodoc
class __$UnitRefCopyWithImpl<$Res>
    implements _$UnitRefCopyWith<$Res> {
  __$UnitRefCopyWithImpl(this._self, this._then);

  final _UnitRef _self;
  final $Res Function(_UnitRef) _then;

/// Create a copy of UnitRef
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,}) {
  return _then(_UnitRef(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$AgentCardRow {

 int get agentId; String get name; String? get regionId; String get regionName; String? get directionName; String? get supervisor; bool get isVacancy; bool get isSalesRep; double? get planKg; double get factKg; double? get execution; double? get forecastKg; double? get forecastExecution; double get revenue; int get akb; int get visits; int get visitsWithOrder; int get orders; double? get conversion; double? get sumPerVisit; int get categories; double? get tempo; List<AgentFlag> get flags; String? get executionLevel;
/// Create a copy of AgentCardRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AgentCardRowCopyWith<AgentCardRow> get copyWith => _$AgentCardRowCopyWithImpl<AgentCardRow>(this as AgentCardRow, _$identity);

  /// Serializes this AgentCardRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AgentCardRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AgentCardRow&&(identical(other.agentId, _this.agentId) || other.agentId == _this.agentId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.regionId, _this.regionId) || other.regionId == _this.regionId)&&(identical(other.regionName, _this.regionName) || other.regionName == _this.regionName)&&(identical(other.directionName, _this.directionName) || other.directionName == _this.directionName)&&(identical(other.supervisor, _this.supervisor) || other.supervisor == _this.supervisor)&&(identical(other.isVacancy, _this.isVacancy) || other.isVacancy == _this.isVacancy)&&(identical(other.isSalesRep, _this.isSalesRep) || other.isSalesRep == _this.isSalesRep)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.execution, _this.execution) || other.execution == _this.execution)&&(identical(other.forecastKg, _this.forecastKg) || other.forecastKg == _this.forecastKg)&&(identical(other.forecastExecution, _this.forecastExecution) || other.forecastExecution == _this.forecastExecution)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.akb, _this.akb) || other.akb == _this.akb)&&(identical(other.visits, _this.visits) || other.visits == _this.visits)&&(identical(other.visitsWithOrder, _this.visitsWithOrder) || other.visitsWithOrder == _this.visitsWithOrder)&&(identical(other.orders, _this.orders) || other.orders == _this.orders)&&(identical(other.conversion, _this.conversion) || other.conversion == _this.conversion)&&(identical(other.sumPerVisit, _this.sumPerVisit) || other.sumPerVisit == _this.sumPerVisit)&&(identical(other.categories, _this.categories) || other.categories == _this.categories)&&(identical(other.tempo, _this.tempo) || other.tempo == _this.tempo)&&const DeepCollectionEquality().equals(other.flags, _this.flags)&&(identical(other.executionLevel, _this.executionLevel) || other.executionLevel == _this.executionLevel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AgentCardRow;
  return Object.hashAll([runtimeType,_this.agentId,_this.name,_this.regionId,_this.regionName,_this.directionName,_this.supervisor,_this.isVacancy,_this.isSalesRep,_this.planKg,_this.factKg,_this.execution,_this.forecastKg,_this.forecastExecution,_this.revenue,_this.akb,_this.visits,_this.visitsWithOrder,_this.orders,_this.conversion,_this.sumPerVisit,_this.categories,_this.tempo,const DeepCollectionEquality().hash(_this.flags),_this.executionLevel]);
}

@override
String toString() {
  final _this = this as AgentCardRow;
  return 'AgentCardRow(agentId: ${_this.agentId}, name: ${_this.name}, regionId: ${_this.regionId}, regionName: ${_this.regionName}, directionName: ${_this.directionName}, supervisor: ${_this.supervisor}, isVacancy: ${_this.isVacancy}, isSalesRep: ${_this.isSalesRep}, planKg: ${_this.planKg}, factKg: ${_this.factKg}, execution: ${_this.execution}, forecastKg: ${_this.forecastKg}, forecastExecution: ${_this.forecastExecution}, revenue: ${_this.revenue}, akb: ${_this.akb}, visits: ${_this.visits}, visitsWithOrder: ${_this.visitsWithOrder}, orders: ${_this.orders}, conversion: ${_this.conversion}, sumPerVisit: ${_this.sumPerVisit}, categories: ${_this.categories}, tempo: ${_this.tempo}, flags: ${_this.flags}, executionLevel: ${_this.executionLevel})';
}


}

/// @nodoc
abstract mixin class $AgentCardRowCopyWith<$Res>  {
  factory $AgentCardRowCopyWith(AgentCardRow value, $Res Function(AgentCardRow) _then) = _$AgentCardRowCopyWithImpl;
@useResult
$Res call({
 int agentId, String name, String? regionId, String regionName, String? directionName, String? supervisor, bool isVacancy, bool isSalesRep, double? planKg, double factKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int akb, int visits, int visitsWithOrder, int orders, double? conversion, double? sumPerVisit, int categories, double? tempo, List<AgentFlag> flags, String? executionLevel
});




}
/// @nodoc
class _$AgentCardRowCopyWithImpl<$Res>
    implements $AgentCardRowCopyWith<$Res> {
  _$AgentCardRowCopyWithImpl(this._self, this._then);

  final AgentCardRow _self;
  final $Res Function(AgentCardRow) _then;

/// Create a copy of AgentCardRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? agentId = null,Object? name = null,Object? regionId = freezed,Object? regionName = null,Object? directionName = freezed,Object? supervisor = freezed,Object? isVacancy = null,Object? isSalesRep = null,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? akb = null,Object? visits = null,Object? visitsWithOrder = null,Object? orders = null,Object? conversion = freezed,Object? sumPerVisit = freezed,Object? categories = null,Object? tempo = freezed,Object? flags = null,Object? executionLevel = freezed,}) {
  return _then(AgentCardRow(
agentId: null == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,regionId: freezed == regionId ? _self.regionId : regionId // ignore: cast_nullable_to_non_nullable
as String?,regionName: null == regionName ? _self.regionName : regionName // ignore: cast_nullable_to_non_nullable
as String,directionName: freezed == directionName ? _self.directionName : directionName // ignore: cast_nullable_to_non_nullable
as String?,supervisor: freezed == supervisor ? _self.supervisor : supervisor // ignore: cast_nullable_to_non_nullable
as String?,isVacancy: null == isVacancy ? _self.isVacancy : isVacancy // ignore: cast_nullable_to_non_nullable
as bool,isSalesRep: null == isSalesRep ? _self.isSalesRep : isSalesRep // ignore: cast_nullable_to_non_nullable
as bool,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,visits: null == visits ? _self.visits : visits // ignore: cast_nullable_to_non_nullable
as int,visitsWithOrder: null == visitsWithOrder ? _self.visitsWithOrder : visitsWithOrder // ignore: cast_nullable_to_non_nullable
as int,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,conversion: freezed == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as double?,sumPerVisit: freezed == sumPerVisit ? _self.sumPerVisit : sumPerVisit // ignore: cast_nullable_to_non_nullable
as double?,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,tempo: freezed == tempo ? _self.tempo : tempo // ignore: cast_nullable_to_non_nullable
as double?,flags: null == flags ? _self.flags : flags // ignore: cast_nullable_to_non_nullable
as List<AgentFlag>,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [AgentCardRow].
extension AgentCardRowPatterns on AgentCardRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AgentCardRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AgentCardRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AgentCardRow value)  $default,){
final _that = this;
switch (_that) {
case _AgentCardRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AgentCardRow value)?  $default,){
final _that = this;
switch (_that) {
case _AgentCardRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int agentId,  String name,  String? regionId,  String regionName,  String? directionName,  String? supervisor,  bool isVacancy,  bool isSalesRep,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  int visits,  int visitsWithOrder,  int orders,  double? conversion,  double? sumPerVisit,  int categories,  double? tempo,  List<AgentFlag> flags,  String? executionLevel)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AgentCardRow() when $default != null:
return $default(_that.agentId,_that.name,_that.regionId,_that.regionName,_that.directionName,_that.supervisor,_that.isVacancy,_that.isSalesRep,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.visits,_that.visitsWithOrder,_that.orders,_that.conversion,_that.sumPerVisit,_that.categories,_that.tempo,_that.flags,_that.executionLevel);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int agentId,  String name,  String? regionId,  String regionName,  String? directionName,  String? supervisor,  bool isVacancy,  bool isSalesRep,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  int visits,  int visitsWithOrder,  int orders,  double? conversion,  double? sumPerVisit,  int categories,  double? tempo,  List<AgentFlag> flags,  String? executionLevel)  $default,) {final _that = this;
switch (_that) {
case _AgentCardRow():
return $default(_that.agentId,_that.name,_that.regionId,_that.regionName,_that.directionName,_that.supervisor,_that.isVacancy,_that.isSalesRep,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.visits,_that.visitsWithOrder,_that.orders,_that.conversion,_that.sumPerVisit,_that.categories,_that.tempo,_that.flags,_that.executionLevel);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int agentId,  String name,  String? regionId,  String regionName,  String? directionName,  String? supervisor,  bool isVacancy,  bool isSalesRep,  double? planKg,  double factKg,  double? execution,  double? forecastKg,  double? forecastExecution,  double revenue,  int akb,  int visits,  int visitsWithOrder,  int orders,  double? conversion,  double? sumPerVisit,  int categories,  double? tempo,  List<AgentFlag> flags,  String? executionLevel)?  $default,) {final _that = this;
switch (_that) {
case _AgentCardRow() when $default != null:
return $default(_that.agentId,_that.name,_that.regionId,_that.regionName,_that.directionName,_that.supervisor,_that.isVacancy,_that.isSalesRep,_that.planKg,_that.factKg,_that.execution,_that.forecastKg,_that.forecastExecution,_that.revenue,_that.akb,_that.visits,_that.visitsWithOrder,_that.orders,_that.conversion,_that.sumPerVisit,_that.categories,_that.tempo,_that.flags,_that.executionLevel);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AgentCardRow implements AgentCardRow {
  const _AgentCardRow({required this.agentId, required this.name, this.regionId, this.regionName = '', this.directionName, this.supervisor, this.isVacancy = false, this.isSalesRep = true, this.planKg, this.factKg = 0, this.execution, this.forecastKg, this.forecastExecution, this.revenue = 0, this.akb = 0, this.visits = 0, this.visitsWithOrder = 0, this.orders = 0, this.conversion, this.sumPerVisit, this.categories = 0, this.tempo,  List<AgentFlag> flags = const <AgentFlag>[], this.executionLevel}): _flags = flags;
  factory _AgentCardRow.fromJson(Map<String, dynamic> json) => _$AgentCardRowFromJson(json);

@override final  int agentId;
@override final  String name;
@override final  String? regionId;
@override@JsonKey() final  String regionName;
@override final  String? directionName;
@override final  String? supervisor;
@override@JsonKey() final  bool isVacancy;
@override@JsonKey() final  bool isSalesRep;
@override final  double? planKg;
@override@JsonKey() final  double factKg;
@override final  double? execution;
@override final  double? forecastKg;
@override final  double? forecastExecution;
@override@JsonKey() final  double revenue;
@override@JsonKey() final  int akb;
@override@JsonKey() final  int visits;
@override@JsonKey() final  int visitsWithOrder;
@override@JsonKey() final  int orders;
@override final  double? conversion;
@override final  double? sumPerVisit;
@override@JsonKey() final  int categories;
@override final  double? tempo;
 final  List<AgentFlag> _flags;
@override@JsonKey() List<AgentFlag> get flags {
  if (_flags is EqualUnmodifiableListView) return _flags;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_flags);
}

@override final  String? executionLevel;

/// Create a copy of AgentCardRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AgentCardRowCopyWith<_AgentCardRow> get copyWith => __$AgentCardRowCopyWithImpl<_AgentCardRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AgentCardRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AgentCardRow&&(identical(other.agentId, agentId) || other.agentId == agentId)&&(identical(other.name, name) || other.name == name)&&(identical(other.regionId, regionId) || other.regionId == regionId)&&(identical(other.regionName, regionName) || other.regionName == regionName)&&(identical(other.directionName, directionName) || other.directionName == directionName)&&(identical(other.supervisor, supervisor) || other.supervisor == supervisor)&&(identical(other.isVacancy, isVacancy) || other.isVacancy == isVacancy)&&(identical(other.isSalesRep, isSalesRep) || other.isSalesRep == isSalesRep)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.execution, execution) || other.execution == execution)&&(identical(other.forecastKg, forecastKg) || other.forecastKg == forecastKg)&&(identical(other.forecastExecution, forecastExecution) || other.forecastExecution == forecastExecution)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.akb, akb) || other.akb == akb)&&(identical(other.visits, visits) || other.visits == visits)&&(identical(other.visitsWithOrder, visitsWithOrder) || other.visitsWithOrder == visitsWithOrder)&&(identical(other.orders, orders) || other.orders == orders)&&(identical(other.conversion, conversion) || other.conversion == conversion)&&(identical(other.sumPerVisit, sumPerVisit) || other.sumPerVisit == sumPerVisit)&&(identical(other.categories, categories) || other.categories == categories)&&(identical(other.tempo, tempo) || other.tempo == tempo)&&const DeepCollectionEquality().equals(other.flags, _flags)&&(identical(other.executionLevel, executionLevel) || other.executionLevel == executionLevel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hashAll([runtimeType,agentId,name,regionId,regionName,directionName,supervisor,isVacancy,isSalesRep,planKg,factKg,execution,forecastKg,forecastExecution,revenue,akb,visits,visitsWithOrder,orders,conversion,sumPerVisit,categories,tempo,const DeepCollectionEquality().hash(_flags),executionLevel]);
}

@override
String toString() {
    return 'AgentCardRow(agentId: $agentId, name: $name, regionId: $regionId, regionName: $regionName, directionName: $directionName, supervisor: $supervisor, isVacancy: $isVacancy, isSalesRep: $isSalesRep, planKg: $planKg, factKg: $factKg, execution: $execution, forecastKg: $forecastKg, forecastExecution: $forecastExecution, revenue: $revenue, akb: $akb, visits: $visits, visitsWithOrder: $visitsWithOrder, orders: $orders, conversion: $conversion, sumPerVisit: $sumPerVisit, categories: $categories, tempo: $tempo, flags: $flags, executionLevel: $executionLevel)';
}


}

/// @nodoc
abstract mixin class _$AgentCardRowCopyWith<$Res> implements $AgentCardRowCopyWith<$Res> {
  factory _$AgentCardRowCopyWith(_AgentCardRow value, $Res Function(_AgentCardRow) _then) = __$AgentCardRowCopyWithImpl;
@override @useResult
$Res call({
 int agentId, String name, String? regionId, String regionName, String? directionName, String? supervisor, bool isVacancy, bool isSalesRep, double? planKg, double factKg, double? execution, double? forecastKg, double? forecastExecution, double revenue, int akb, int visits, int visitsWithOrder, int orders, double? conversion, double? sumPerVisit, int categories, double? tempo, List<AgentFlag> flags, String? executionLevel
});




}
/// @nodoc
class __$AgentCardRowCopyWithImpl<$Res>
    implements _$AgentCardRowCopyWith<$Res> {
  __$AgentCardRowCopyWithImpl(this._self, this._then);

  final _AgentCardRow _self;
  final $Res Function(_AgentCardRow) _then;

/// Create a copy of AgentCardRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? agentId = null,Object? name = null,Object? regionId = freezed,Object? regionName = null,Object? directionName = freezed,Object? supervisor = freezed,Object? isVacancy = null,Object? isSalesRep = null,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastKg = freezed,Object? forecastExecution = freezed,Object? revenue = null,Object? akb = null,Object? visits = null,Object? visitsWithOrder = null,Object? orders = null,Object? conversion = freezed,Object? sumPerVisit = freezed,Object? categories = null,Object? tempo = freezed,Object? flags = null,Object? executionLevel = freezed,}) {
  return _then(_AgentCardRow(
agentId: null == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,regionId: freezed == regionId ? _self.regionId : regionId // ignore: cast_nullable_to_non_nullable
as String?,regionName: null == regionName ? _self.regionName : regionName // ignore: cast_nullable_to_non_nullable
as String,directionName: freezed == directionName ? _self.directionName : directionName // ignore: cast_nullable_to_non_nullable
as String?,supervisor: freezed == supervisor ? _self.supervisor : supervisor // ignore: cast_nullable_to_non_nullable
as String?,isVacancy: null == isVacancy ? _self.isVacancy : isVacancy // ignore: cast_nullable_to_non_nullable
as bool,isSalesRep: null == isSalesRep ? _self.isSalesRep : isSalesRep // ignore: cast_nullable_to_non_nullable
as bool,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastKg: freezed == forecastKg ? _self.forecastKg : forecastKg // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,visits: null == visits ? _self.visits : visits // ignore: cast_nullable_to_non_nullable
as int,visitsWithOrder: null == visitsWithOrder ? _self.visitsWithOrder : visitsWithOrder // ignore: cast_nullable_to_non_nullable
as int,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,conversion: freezed == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as double?,sumPerVisit: freezed == sumPerVisit ? _self.sumPerVisit : sumPerVisit // ignore: cast_nullable_to_non_nullable
as double?,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,tempo: freezed == tempo ? _self.tempo : tempo // ignore: cast_nullable_to_non_nullable
as double?,flags: null == flags ? _self._flags : flags // ignore: cast_nullable_to_non_nullable
as List<AgentFlag>,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$AgentTargets {

 double get conversion; double get revenuePerOutlet; double get akbPerAgent; double get categoriesPerOutlet;
/// Create a copy of AgentTargets
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AgentTargetsCopyWith<AgentTargets> get copyWith => _$AgentTargetsCopyWithImpl<AgentTargets>(this as AgentTargets, _$identity);

  /// Serializes this AgentTargets to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AgentTargets;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AgentTargets&&(identical(other.conversion, _this.conversion) || other.conversion == _this.conversion)&&(identical(other.revenuePerOutlet, _this.revenuePerOutlet) || other.revenuePerOutlet == _this.revenuePerOutlet)&&(identical(other.akbPerAgent, _this.akbPerAgent) || other.akbPerAgent == _this.akbPerAgent)&&(identical(other.categoriesPerOutlet, _this.categoriesPerOutlet) || other.categoriesPerOutlet == _this.categoriesPerOutlet));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AgentTargets;
  return Object.hash(runtimeType,_this.conversion,_this.revenuePerOutlet,_this.akbPerAgent,_this.categoriesPerOutlet);
}

@override
String toString() {
  final _this = this as AgentTargets;
  return 'AgentTargets(conversion: ${_this.conversion}, revenuePerOutlet: ${_this.revenuePerOutlet}, akbPerAgent: ${_this.akbPerAgent}, categoriesPerOutlet: ${_this.categoriesPerOutlet})';
}


}

/// @nodoc
abstract mixin class $AgentTargetsCopyWith<$Res>  {
  factory $AgentTargetsCopyWith(AgentTargets value, $Res Function(AgentTargets) _then) = _$AgentTargetsCopyWithImpl;
@useResult
$Res call({
 double conversion, double revenuePerOutlet, double akbPerAgent, double categoriesPerOutlet
});




}
/// @nodoc
class _$AgentTargetsCopyWithImpl<$Res>
    implements $AgentTargetsCopyWith<$Res> {
  _$AgentTargetsCopyWithImpl(this._self, this._then);

  final AgentTargets _self;
  final $Res Function(AgentTargets) _then;

/// Create a copy of AgentTargets
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? conversion = null,Object? revenuePerOutlet = null,Object? akbPerAgent = null,Object? categoriesPerOutlet = null,}) {
  return _then(AgentTargets(
conversion: null == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as double,revenuePerOutlet: null == revenuePerOutlet ? _self.revenuePerOutlet : revenuePerOutlet // ignore: cast_nullable_to_non_nullable
as double,akbPerAgent: null == akbPerAgent ? _self.akbPerAgent : akbPerAgent // ignore: cast_nullable_to_non_nullable
as double,categoriesPerOutlet: null == categoriesPerOutlet ? _self.categoriesPerOutlet : categoriesPerOutlet // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [AgentTargets].
extension AgentTargetsPatterns on AgentTargets {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AgentTargets value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AgentTargets() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AgentTargets value)  $default,){
final _that = this;
switch (_that) {
case _AgentTargets():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AgentTargets value)?  $default,){
final _that = this;
switch (_that) {
case _AgentTargets() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( double conversion,  double revenuePerOutlet,  double akbPerAgent,  double categoriesPerOutlet)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AgentTargets() when $default != null:
return $default(_that.conversion,_that.revenuePerOutlet,_that.akbPerAgent,_that.categoriesPerOutlet);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( double conversion,  double revenuePerOutlet,  double akbPerAgent,  double categoriesPerOutlet)  $default,) {final _that = this;
switch (_that) {
case _AgentTargets():
return $default(_that.conversion,_that.revenuePerOutlet,_that.akbPerAgent,_that.categoriesPerOutlet);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( double conversion,  double revenuePerOutlet,  double akbPerAgent,  double categoriesPerOutlet)?  $default,) {final _that = this;
switch (_that) {
case _AgentTargets() when $default != null:
return $default(_that.conversion,_that.revenuePerOutlet,_that.akbPerAgent,_that.categoriesPerOutlet);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AgentTargets implements AgentTargets {
  const _AgentTargets({this.conversion = 0.7, this.revenuePerOutlet = 0, this.akbPerAgent = 0, this.categoriesPerOutlet = 0});
  factory _AgentTargets.fromJson(Map<String, dynamic> json) => _$AgentTargetsFromJson(json);

@override@JsonKey() final  double conversion;
@override@JsonKey() final  double revenuePerOutlet;
@override@JsonKey() final  double akbPerAgent;
@override@JsonKey() final  double categoriesPerOutlet;

/// Create a copy of AgentTargets
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AgentTargetsCopyWith<_AgentTargets> get copyWith => __$AgentTargetsCopyWithImpl<_AgentTargets>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AgentTargetsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AgentTargets&&(identical(other.conversion, conversion) || other.conversion == conversion)&&(identical(other.revenuePerOutlet, revenuePerOutlet) || other.revenuePerOutlet == revenuePerOutlet)&&(identical(other.akbPerAgent, akbPerAgent) || other.akbPerAgent == akbPerAgent)&&(identical(other.categoriesPerOutlet, categoriesPerOutlet) || other.categoriesPerOutlet == categoriesPerOutlet));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,conversion,revenuePerOutlet,akbPerAgent,categoriesPerOutlet);
}

@override
String toString() {
    return 'AgentTargets(conversion: $conversion, revenuePerOutlet: $revenuePerOutlet, akbPerAgent: $akbPerAgent, categoriesPerOutlet: $categoriesPerOutlet)';
}


}

/// @nodoc
abstract mixin class _$AgentTargetsCopyWith<$Res> implements $AgentTargetsCopyWith<$Res> {
  factory _$AgentTargetsCopyWith(_AgentTargets value, $Res Function(_AgentTargets) _then) = __$AgentTargetsCopyWithImpl;
@override @useResult
$Res call({
 double conversion, double revenuePerOutlet, double akbPerAgent, double categoriesPerOutlet
});




}
/// @nodoc
class __$AgentTargetsCopyWithImpl<$Res>
    implements _$AgentTargetsCopyWith<$Res> {
  __$AgentTargetsCopyWithImpl(this._self, this._then);

  final _AgentTargets _self;
  final $Res Function(_AgentTargets) _then;

/// Create a copy of AgentTargets
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? conversion = null,Object? revenuePerOutlet = null,Object? akbPerAgent = null,Object? categoriesPerOutlet = null,}) {
  return _then(_AgentTargets(
conversion: null == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as double,revenuePerOutlet: null == revenuePerOutlet ? _self.revenuePerOutlet : revenuePerOutlet // ignore: cast_nullable_to_non_nullable
as double,akbPerAgent: null == akbPerAgent ? _self.akbPerAgent : akbPerAgent // ignore: cast_nullable_to_non_nullable
as double,categoriesPerOutlet: null == categoriesPerOutlet ? _self.categoriesPerOutlet : categoriesPerOutlet // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$AgentsView {

 SalesPeriod get period; AgentTargets get targets; List<AgentCardRow> get agents; List<UnitRef> get regions; List<UnitRef> get directions;
/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AgentsViewCopyWith<AgentsView> get copyWith => _$AgentsViewCopyWithImpl<AgentsView>(this as AgentsView, _$identity);

  /// Serializes this AgentsView to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AgentsView;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AgentsView&&(identical(other.period, _this.period) || other.period == _this.period)&&(identical(other.targets, _this.targets) || other.targets == _this.targets)&&const DeepCollectionEquality().equals(other.agents, _this.agents)&&const DeepCollectionEquality().equals(other.regions, _this.regions)&&const DeepCollectionEquality().equals(other.directions, _this.directions));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AgentsView;
  return Object.hash(runtimeType,_this.period,_this.targets,const DeepCollectionEquality().hash(_this.agents),const DeepCollectionEquality().hash(_this.regions),const DeepCollectionEquality().hash(_this.directions));
}

@override
String toString() {
  final _this = this as AgentsView;
  return 'AgentsView(period: ${_this.period}, targets: ${_this.targets}, agents: ${_this.agents}, regions: ${_this.regions}, directions: ${_this.directions})';
}


}

/// @nodoc
abstract mixin class $AgentsViewCopyWith<$Res>  {
  factory $AgentsViewCopyWith(AgentsView value, $Res Function(AgentsView) _then) = _$AgentsViewCopyWithImpl;
@useResult
$Res call({
 SalesPeriod period, AgentTargets targets, List<AgentCardRow> agents, List<UnitRef> regions, List<UnitRef> directions
});


$SalesPeriodCopyWith<$Res> get period;$AgentTargetsCopyWith<$Res> get targets;

}
/// @nodoc
class _$AgentsViewCopyWithImpl<$Res>
    implements $AgentsViewCopyWith<$Res> {
  _$AgentsViewCopyWithImpl(this._self, this._then);

  final AgentsView _self;
  final $Res Function(AgentsView) _then;

/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? period = null,Object? targets = null,Object? agents = null,Object? regions = null,Object? directions = null,}) {
  return _then(AgentsView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,targets: null == targets ? _self.targets : targets // ignore: cast_nullable_to_non_nullable
as AgentTargets,agents: null == agents ? _self.agents : agents // ignore: cast_nullable_to_non_nullable
as List<AgentCardRow>,regions: null == regions ? _self.regions : regions // ignore: cast_nullable_to_non_nullable
as List<UnitRef>,directions: null == directions ? _self.directions : directions // ignore: cast_nullable_to_non_nullable
as List<UnitRef>,
  ));
}
/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AgentTargetsCopyWith<$Res> get targets {
  
  return $AgentTargetsCopyWith<$Res>(_self.targets, (value) {
    return _then(_self.copyWith(targets: value));
  });
}
}


/// Adds pattern-matching-related methods to [AgentsView].
extension AgentsViewPatterns on AgentsView {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AgentsView value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AgentsView() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AgentsView value)  $default,){
final _that = this;
switch (_that) {
case _AgentsView():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AgentsView value)?  $default,){
final _that = this;
switch (_that) {
case _AgentsView() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( SalesPeriod period,  AgentTargets targets,  List<AgentCardRow> agents,  List<UnitRef> regions,  List<UnitRef> directions)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AgentsView() when $default != null:
return $default(_that.period,_that.targets,_that.agents,_that.regions,_that.directions);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( SalesPeriod period,  AgentTargets targets,  List<AgentCardRow> agents,  List<UnitRef> regions,  List<UnitRef> directions)  $default,) {final _that = this;
switch (_that) {
case _AgentsView():
return $default(_that.period,_that.targets,_that.agents,_that.regions,_that.directions);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( SalesPeriod period,  AgentTargets targets,  List<AgentCardRow> agents,  List<UnitRef> regions,  List<UnitRef> directions)?  $default,) {final _that = this;
switch (_that) {
case _AgentsView() when $default != null:
return $default(_that.period,_that.targets,_that.agents,_that.regions,_that.directions);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AgentsView implements AgentsView {
  const _AgentsView({required this.period, this.targets = const AgentTargets(),  List<AgentCardRow> agents = const <AgentCardRow>[],  List<UnitRef> regions = const <UnitRef>[],  List<UnitRef> directions = const <UnitRef>[]}): _agents = agents,_regions = regions,_directions = directions;
  factory _AgentsView.fromJson(Map<String, dynamic> json) => _$AgentsViewFromJson(json);

@override final  SalesPeriod period;
@override@JsonKey() final  AgentTargets targets;
 final  List<AgentCardRow> _agents;
@override@JsonKey() List<AgentCardRow> get agents {
  if (_agents is EqualUnmodifiableListView) return _agents;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_agents);
}

 final  List<UnitRef> _regions;
@override@JsonKey() List<UnitRef> get regions {
  if (_regions is EqualUnmodifiableListView) return _regions;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_regions);
}

 final  List<UnitRef> _directions;
@override@JsonKey() List<UnitRef> get directions {
  if (_directions is EqualUnmodifiableListView) return _directions;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_directions);
}


/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AgentsViewCopyWith<_AgentsView> get copyWith => __$AgentsViewCopyWithImpl<_AgentsView>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AgentsViewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AgentsView&&(identical(other.period, period) || other.period == period)&&(identical(other.targets, targets) || other.targets == targets)&&const DeepCollectionEquality().equals(other.agents, _agents)&&const DeepCollectionEquality().equals(other.regions, _regions)&&const DeepCollectionEquality().equals(other.directions, _directions));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,period,targets,const DeepCollectionEquality().hash(_agents),const DeepCollectionEquality().hash(_regions),const DeepCollectionEquality().hash(_directions));
}

@override
String toString() {
    return 'AgentsView(period: $period, targets: $targets, agents: $agents, regions: $regions, directions: $directions)';
}


}

/// @nodoc
abstract mixin class _$AgentsViewCopyWith<$Res> implements $AgentsViewCopyWith<$Res> {
  factory _$AgentsViewCopyWith(_AgentsView value, $Res Function(_AgentsView) _then) = __$AgentsViewCopyWithImpl;
@override @useResult
$Res call({
 SalesPeriod period, AgentTargets targets, List<AgentCardRow> agents, List<UnitRef> regions, List<UnitRef> directions
});


@override $SalesPeriodCopyWith<$Res> get period;@override $AgentTargetsCopyWith<$Res> get targets;

}
/// @nodoc
class __$AgentsViewCopyWithImpl<$Res>
    implements _$AgentsViewCopyWith<$Res> {
  __$AgentsViewCopyWithImpl(this._self, this._then);

  final _AgentsView _self;
  final $Res Function(_AgentsView) _then;

/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? period = null,Object? targets = null,Object? agents = null,Object? regions = null,Object? directions = null,}) {
  return _then(_AgentsView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,targets: null == targets ? _self.targets : targets // ignore: cast_nullable_to_non_nullable
as AgentTargets,agents: null == agents ? _self._agents : agents // ignore: cast_nullable_to_non_nullable
as List<AgentCardRow>,regions: null == regions ? _self._regions : regions // ignore: cast_nullable_to_non_nullable
as List<UnitRef>,directions: null == directions ? _self._directions : directions // ignore: cast_nullable_to_non_nullable
as List<UnitRef>,
  ));
}

/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of AgentsView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$AgentTargetsCopyWith<$Res> get targets {
  
  return $AgentTargetsCopyWith<$Res>(_self.targets, (value) {
    return _then(_self.copyWith(targets: value));
  });
}
}


/// @nodoc
mixin _$MedianValue {

 double? get value; double? get regionMedian;
/// Create a copy of MedianValue
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$MedianValueCopyWith<MedianValue> get copyWith => _$MedianValueCopyWithImpl<MedianValue>(this as MedianValue, _$identity);

  /// Serializes this MedianValue to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as MedianValue;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is MedianValue&&(identical(other.value, _this.value) || other.value == _this.value)&&(identical(other.regionMedian, _this.regionMedian) || other.regionMedian == _this.regionMedian));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as MedianValue;
  return Object.hash(runtimeType,_this.value,_this.regionMedian);
}

@override
String toString() {
  final _this = this as MedianValue;
  return 'MedianValue(value: ${_this.value}, regionMedian: ${_this.regionMedian})';
}


}

/// @nodoc
abstract mixin class $MedianValueCopyWith<$Res>  {
  factory $MedianValueCopyWith(MedianValue value, $Res Function(MedianValue) _then) = _$MedianValueCopyWithImpl;
@useResult
$Res call({
 double? value, double? regionMedian
});




}
/// @nodoc
class _$MedianValueCopyWithImpl<$Res>
    implements $MedianValueCopyWith<$Res> {
  _$MedianValueCopyWithImpl(this._self, this._then);

  final MedianValue _self;
  final $Res Function(MedianValue) _then;

/// Create a copy of MedianValue
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? value = freezed,Object? regionMedian = freezed,}) {
  return _then(MedianValue(
value: freezed == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double?,regionMedian: freezed == regionMedian ? _self.regionMedian : regionMedian // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [MedianValue].
extension MedianValuePatterns on MedianValue {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _MedianValue value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _MedianValue() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _MedianValue value)  $default,){
final _that = this;
switch (_that) {
case _MedianValue():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _MedianValue value)?  $default,){
final _that = this;
switch (_that) {
case _MedianValue() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( double? value,  double? regionMedian)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _MedianValue() when $default != null:
return $default(_that.value,_that.regionMedian);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( double? value,  double? regionMedian)  $default,) {final _that = this;
switch (_that) {
case _MedianValue():
return $default(_that.value,_that.regionMedian);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( double? value,  double? regionMedian)?  $default,) {final _that = this;
switch (_that) {
case _MedianValue() when $default != null:
return $default(_that.value,_that.regionMedian);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _MedianValue implements MedianValue {
  const _MedianValue({this.value, this.regionMedian});
  factory _MedianValue.fromJson(Map<String, dynamic> json) => _$MedianValueFromJson(json);

@override final  double? value;
@override final  double? regionMedian;

/// Create a copy of MedianValue
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$MedianValueCopyWith<_MedianValue> get copyWith => __$MedianValueCopyWithImpl<_MedianValue>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$MedianValueToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _MedianValue&&(identical(other.value, value) || other.value == value)&&(identical(other.regionMedian, regionMedian) || other.regionMedian == regionMedian));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,value,regionMedian);
}

@override
String toString() {
    return 'MedianValue(value: $value, regionMedian: $regionMedian)';
}


}

/// @nodoc
abstract mixin class _$MedianValueCopyWith<$Res> implements $MedianValueCopyWith<$Res> {
  factory _$MedianValueCopyWith(_MedianValue value, $Res Function(_MedianValue) _then) = __$MedianValueCopyWithImpl;
@override @useResult
$Res call({
 double? value, double? regionMedian
});




}
/// @nodoc
class __$MedianValueCopyWithImpl<$Res>
    implements _$MedianValueCopyWith<$Res> {
  __$MedianValueCopyWithImpl(this._self, this._then);

  final _MedianValue _self;
  final $Res Function(_MedianValue) _then;

/// Create a copy of MedianValue
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? value = freezed,Object? regionMedian = freezed,}) {
  return _then(_MedianValue(
value: freezed == value ? _self.value : value // ignore: cast_nullable_to_non_nullable
as double?,regionMedian: freezed == regionMedian ? _self.regionMedian : regionMedian // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$MarketBrief {

 int get marketId; String get name; double get kg; double get revenue;/// Для «ещё не купили»: прошлый месяц.
 double get prevKg; double get prevRevenue; int get categories; int get positions; double? get share;
/// Create a copy of MarketBrief
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$MarketBriefCopyWith<MarketBrief> get copyWith => _$MarketBriefCopyWithImpl<MarketBrief>(this as MarketBrief, _$identity);

  /// Serializes this MarketBrief to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as MarketBrief;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is MarketBrief&&(identical(other.marketId, _this.marketId) || other.marketId == _this.marketId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.kg, _this.kg) || other.kg == _this.kg)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.prevKg, _this.prevKg) || other.prevKg == _this.prevKg)&&(identical(other.prevRevenue, _this.prevRevenue) || other.prevRevenue == _this.prevRevenue)&&(identical(other.categories, _this.categories) || other.categories == _this.categories)&&(identical(other.positions, _this.positions) || other.positions == _this.positions)&&(identical(other.share, _this.share) || other.share == _this.share));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as MarketBrief;
  return Object.hash(runtimeType,_this.marketId,_this.name,_this.kg,_this.revenue,_this.prevKg,_this.prevRevenue,_this.categories,_this.positions,_this.share);
}

@override
String toString() {
  final _this = this as MarketBrief;
  return 'MarketBrief(marketId: ${_this.marketId}, name: ${_this.name}, kg: ${_this.kg}, revenue: ${_this.revenue}, prevKg: ${_this.prevKg}, prevRevenue: ${_this.prevRevenue}, categories: ${_this.categories}, positions: ${_this.positions}, share: ${_this.share})';
}


}

/// @nodoc
abstract mixin class $MarketBriefCopyWith<$Res>  {
  factory $MarketBriefCopyWith(MarketBrief value, $Res Function(MarketBrief) _then) = _$MarketBriefCopyWithImpl;
@useResult
$Res call({
 int marketId, String name, double kg, double revenue, double prevKg, double prevRevenue, int categories, int positions, double? share
});




}
/// @nodoc
class _$MarketBriefCopyWithImpl<$Res>
    implements $MarketBriefCopyWith<$Res> {
  _$MarketBriefCopyWithImpl(this._self, this._then);

  final MarketBrief _self;
  final $Res Function(MarketBrief) _then;

/// Create a copy of MarketBrief
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? marketId = null,Object? name = null,Object? kg = null,Object? revenue = null,Object? prevKg = null,Object? prevRevenue = null,Object? categories = null,Object? positions = null,Object? share = freezed,}) {
  return _then(MarketBrief(
marketId: null == marketId ? _self.marketId : marketId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,prevKg: null == prevKg ? _self.prevKg : prevKg // ignore: cast_nullable_to_non_nullable
as double,prevRevenue: null == prevRevenue ? _self.prevRevenue : prevRevenue // ignore: cast_nullable_to_non_nullable
as double,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,positions: null == positions ? _self.positions : positions // ignore: cast_nullable_to_non_nullable
as int,share: freezed == share ? _self.share : share // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [MarketBrief].
extension MarketBriefPatterns on MarketBrief {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _MarketBrief value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _MarketBrief() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _MarketBrief value)  $default,){
final _that = this;
switch (_that) {
case _MarketBrief():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _MarketBrief value)?  $default,){
final _that = this;
switch (_that) {
case _MarketBrief() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int marketId,  String name,  double kg,  double revenue,  double prevKg,  double prevRevenue,  int categories,  int positions,  double? share)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _MarketBrief() when $default != null:
return $default(_that.marketId,_that.name,_that.kg,_that.revenue,_that.prevKg,_that.prevRevenue,_that.categories,_that.positions,_that.share);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int marketId,  String name,  double kg,  double revenue,  double prevKg,  double prevRevenue,  int categories,  int positions,  double? share)  $default,) {final _that = this;
switch (_that) {
case _MarketBrief():
return $default(_that.marketId,_that.name,_that.kg,_that.revenue,_that.prevKg,_that.prevRevenue,_that.categories,_that.positions,_that.share);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int marketId,  String name,  double kg,  double revenue,  double prevKg,  double prevRevenue,  int categories,  int positions,  double? share)?  $default,) {final _that = this;
switch (_that) {
case _MarketBrief() when $default != null:
return $default(_that.marketId,_that.name,_that.kg,_that.revenue,_that.prevKg,_that.prevRevenue,_that.categories,_that.positions,_that.share);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _MarketBrief implements MarketBrief {
  const _MarketBrief({required this.marketId, required this.name, this.kg = 0, this.revenue = 0, this.prevKg = 0, this.prevRevenue = 0, this.categories = 0, this.positions = 0, this.share});
  factory _MarketBrief.fromJson(Map<String, dynamic> json) => _$MarketBriefFromJson(json);

@override final  int marketId;
@override final  String name;
@override@JsonKey() final  double kg;
@override@JsonKey() final  double revenue;
/// Для «ещё не купили»: прошлый месяц.
@override@JsonKey() final  double prevKg;
@override@JsonKey() final  double prevRevenue;
@override@JsonKey() final  int categories;
@override@JsonKey() final  int positions;
@override final  double? share;

/// Create a copy of MarketBrief
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$MarketBriefCopyWith<_MarketBrief> get copyWith => __$MarketBriefCopyWithImpl<_MarketBrief>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$MarketBriefToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _MarketBrief&&(identical(other.marketId, marketId) || other.marketId == marketId)&&(identical(other.name, name) || other.name == name)&&(identical(other.kg, kg) || other.kg == kg)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.prevKg, prevKg) || other.prevKg == prevKg)&&(identical(other.prevRevenue, prevRevenue) || other.prevRevenue == prevRevenue)&&(identical(other.categories, categories) || other.categories == categories)&&(identical(other.positions, positions) || other.positions == positions)&&(identical(other.share, share) || other.share == share));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,marketId,name,kg,revenue,prevKg,prevRevenue,categories,positions,share);
}

@override
String toString() {
    return 'MarketBrief(marketId: $marketId, name: $name, kg: $kg, revenue: $revenue, prevKg: $prevKg, prevRevenue: $prevRevenue, categories: $categories, positions: $positions, share: $share)';
}


}

/// @nodoc
abstract mixin class _$MarketBriefCopyWith<$Res> implements $MarketBriefCopyWith<$Res> {
  factory _$MarketBriefCopyWith(_MarketBrief value, $Res Function(_MarketBrief) _then) = __$MarketBriefCopyWithImpl;
@override @useResult
$Res call({
 int marketId, String name, double kg, double revenue, double prevKg, double prevRevenue, int categories, int positions, double? share
});




}
/// @nodoc
class __$MarketBriefCopyWithImpl<$Res>
    implements _$MarketBriefCopyWith<$Res> {
  __$MarketBriefCopyWithImpl(this._self, this._then);

  final _MarketBrief _self;
  final $Res Function(_MarketBrief) _then;

/// Create a copy of MarketBrief
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? marketId = null,Object? name = null,Object? kg = null,Object? revenue = null,Object? prevKg = null,Object? prevRevenue = null,Object? categories = null,Object? positions = null,Object? share = freezed,}) {
  return _then(_MarketBrief(
marketId: null == marketId ? _self.marketId : marketId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,prevKg: null == prevKg ? _self.prevKg : prevKg // ignore: cast_nullable_to_non_nullable
as double,prevRevenue: null == prevRevenue ? _self.prevRevenue : prevRevenue // ignore: cast_nullable_to_non_nullable
as double,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,positions: null == positions ? _self.positions : positions // ignore: cast_nullable_to_non_nullable
as int,share: freezed == share ? _self.share : share // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$CategoryPlanFact {

 int? get categoryId; String get name; double? get planKg; double get factKg; double? get execution; double? get forecastExecution; String? get executionLevel;
/// Create a copy of CategoryPlanFact
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CategoryPlanFactCopyWith<CategoryPlanFact> get copyWith => _$CategoryPlanFactCopyWithImpl<CategoryPlanFact>(this as CategoryPlanFact, _$identity);

  /// Serializes this CategoryPlanFact to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CategoryPlanFact;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CategoryPlanFact&&(identical(other.categoryId, _this.categoryId) || other.categoryId == _this.categoryId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.execution, _this.execution) || other.execution == _this.execution)&&(identical(other.forecastExecution, _this.forecastExecution) || other.forecastExecution == _this.forecastExecution)&&(identical(other.executionLevel, _this.executionLevel) || other.executionLevel == _this.executionLevel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CategoryPlanFact;
  return Object.hash(runtimeType,_this.categoryId,_this.name,_this.planKg,_this.factKg,_this.execution,_this.forecastExecution,_this.executionLevel);
}

@override
String toString() {
  final _this = this as CategoryPlanFact;
  return 'CategoryPlanFact(categoryId: ${_this.categoryId}, name: ${_this.name}, planKg: ${_this.planKg}, factKg: ${_this.factKg}, execution: ${_this.execution}, forecastExecution: ${_this.forecastExecution}, executionLevel: ${_this.executionLevel})';
}


}

/// @nodoc
abstract mixin class $CategoryPlanFactCopyWith<$Res>  {
  factory $CategoryPlanFactCopyWith(CategoryPlanFact value, $Res Function(CategoryPlanFact) _then) = _$CategoryPlanFactCopyWithImpl;
@useResult
$Res call({
 int? categoryId, String name, double? planKg, double factKg, double? execution, double? forecastExecution, String? executionLevel
});




}
/// @nodoc
class _$CategoryPlanFactCopyWithImpl<$Res>
    implements $CategoryPlanFactCopyWith<$Res> {
  _$CategoryPlanFactCopyWithImpl(this._self, this._then);

  final CategoryPlanFact _self;
  final $Res Function(CategoryPlanFact) _then;

/// Create a copy of CategoryPlanFact
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? categoryId = freezed,Object? name = null,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastExecution = freezed,Object? executionLevel = freezed,}) {
  return _then(CategoryPlanFact(
categoryId: freezed == categoryId ? _self.categoryId : categoryId // ignore: cast_nullable_to_non_nullable
as int?,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [CategoryPlanFact].
extension CategoryPlanFactPatterns on CategoryPlanFact {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CategoryPlanFact value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CategoryPlanFact() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CategoryPlanFact value)  $default,){
final _that = this;
switch (_that) {
case _CategoryPlanFact():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CategoryPlanFact value)?  $default,){
final _that = this;
switch (_that) {
case _CategoryPlanFact() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int? categoryId,  String name,  double? planKg,  double factKg,  double? execution,  double? forecastExecution,  String? executionLevel)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CategoryPlanFact() when $default != null:
return $default(_that.categoryId,_that.name,_that.planKg,_that.factKg,_that.execution,_that.forecastExecution,_that.executionLevel);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int? categoryId,  String name,  double? planKg,  double factKg,  double? execution,  double? forecastExecution,  String? executionLevel)  $default,) {final _that = this;
switch (_that) {
case _CategoryPlanFact():
return $default(_that.categoryId,_that.name,_that.planKg,_that.factKg,_that.execution,_that.forecastExecution,_that.executionLevel);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int? categoryId,  String name,  double? planKg,  double factKg,  double? execution,  double? forecastExecution,  String? executionLevel)?  $default,) {final _that = this;
switch (_that) {
case _CategoryPlanFact() when $default != null:
return $default(_that.categoryId,_that.name,_that.planKg,_that.factKg,_that.execution,_that.forecastExecution,_that.executionLevel);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CategoryPlanFact implements CategoryPlanFact {
  const _CategoryPlanFact({this.categoryId, required this.name, this.planKg, this.factKg = 0, this.execution, this.forecastExecution, this.executionLevel});
  factory _CategoryPlanFact.fromJson(Map<String, dynamic> json) => _$CategoryPlanFactFromJson(json);

@override final  int? categoryId;
@override final  String name;
@override final  double? planKg;
@override@JsonKey() final  double factKg;
@override final  double? execution;
@override final  double? forecastExecution;
@override final  String? executionLevel;

/// Create a copy of CategoryPlanFact
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CategoryPlanFactCopyWith<_CategoryPlanFact> get copyWith => __$CategoryPlanFactCopyWithImpl<_CategoryPlanFact>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CategoryPlanFactToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CategoryPlanFact&&(identical(other.categoryId, categoryId) || other.categoryId == categoryId)&&(identical(other.name, name) || other.name == name)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.execution, execution) || other.execution == execution)&&(identical(other.forecastExecution, forecastExecution) || other.forecastExecution == forecastExecution)&&(identical(other.executionLevel, executionLevel) || other.executionLevel == executionLevel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,categoryId,name,planKg,factKg,execution,forecastExecution,executionLevel);
}

@override
String toString() {
    return 'CategoryPlanFact(categoryId: $categoryId, name: $name, planKg: $planKg, factKg: $factKg, execution: $execution, forecastExecution: $forecastExecution, executionLevel: $executionLevel)';
}


}

/// @nodoc
abstract mixin class _$CategoryPlanFactCopyWith<$Res> implements $CategoryPlanFactCopyWith<$Res> {
  factory _$CategoryPlanFactCopyWith(_CategoryPlanFact value, $Res Function(_CategoryPlanFact) _then) = __$CategoryPlanFactCopyWithImpl;
@override @useResult
$Res call({
 int? categoryId, String name, double? planKg, double factKg, double? execution, double? forecastExecution, String? executionLevel
});




}
/// @nodoc
class __$CategoryPlanFactCopyWithImpl<$Res>
    implements _$CategoryPlanFactCopyWith<$Res> {
  __$CategoryPlanFactCopyWithImpl(this._self, this._then);

  final _CategoryPlanFact _self;
  final $Res Function(_CategoryPlanFact) _then;

/// Create a copy of CategoryPlanFact
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? categoryId = freezed,Object? name = null,Object? planKg = freezed,Object? factKg = null,Object? execution = freezed,Object? forecastExecution = freezed,Object? executionLevel = freezed,}) {
  return _then(_CategoryPlanFact(
categoryId: freezed == categoryId ? _self.categoryId : categoryId // ignore: cast_nullable_to_non_nullable
as int?,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,forecastExecution: freezed == forecastExecution ? _self.forecastExecution : forecastExecution // ignore: cast_nullable_to_non_nullable
as double?,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$AgentView {

 SalesPeriod get period; int get agentId; String get name; String? get regionId; String? get regionName; String? get directionName; bool get isVacancy; double? get planKg; double? get execution; double get factKg; double get revenue; int get visits; int get visitsWithOrder; MedianValue get conversion; MedianValue get sumPerVisit; MedianValue get avgCheck; int get categories; int? get planCategories; double get categoryTarget; double? get tempo; List<AgentFlag> get flags; List<CategoryPlanFact> get categoryPlan; SameDaysRow? get sameDays; int get silentBase; List<MarketBrief> get silent; List<MarketBrief> get newMarkets; List<MarketBrief> get stores; int get akb; int get orders; double? get revenuePerOutlet; double? get kgPerOutlet; String? get executionLevel;
/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AgentViewCopyWith<AgentView> get copyWith => _$AgentViewCopyWithImpl<AgentView>(this as AgentView, _$identity);

  /// Serializes this AgentView to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AgentView;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AgentView&&(identical(other.period, _this.period) || other.period == _this.period)&&(identical(other.agentId, _this.agentId) || other.agentId == _this.agentId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.regionId, _this.regionId) || other.regionId == _this.regionId)&&(identical(other.regionName, _this.regionName) || other.regionName == _this.regionName)&&(identical(other.directionName, _this.directionName) || other.directionName == _this.directionName)&&(identical(other.isVacancy, _this.isVacancy) || other.isVacancy == _this.isVacancy)&&(identical(other.planKg, _this.planKg) || other.planKg == _this.planKg)&&(identical(other.execution, _this.execution) || other.execution == _this.execution)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.visits, _this.visits) || other.visits == _this.visits)&&(identical(other.visitsWithOrder, _this.visitsWithOrder) || other.visitsWithOrder == _this.visitsWithOrder)&&(identical(other.conversion, _this.conversion) || other.conversion == _this.conversion)&&(identical(other.sumPerVisit, _this.sumPerVisit) || other.sumPerVisit == _this.sumPerVisit)&&(identical(other.avgCheck, _this.avgCheck) || other.avgCheck == _this.avgCheck)&&(identical(other.categories, _this.categories) || other.categories == _this.categories)&&(identical(other.planCategories, _this.planCategories) || other.planCategories == _this.planCategories)&&(identical(other.categoryTarget, _this.categoryTarget) || other.categoryTarget == _this.categoryTarget)&&(identical(other.tempo, _this.tempo) || other.tempo == _this.tempo)&&const DeepCollectionEquality().equals(other.flags, _this.flags)&&const DeepCollectionEquality().equals(other.categoryPlan, _this.categoryPlan)&&(identical(other.sameDays, _this.sameDays) || other.sameDays == _this.sameDays)&&(identical(other.silentBase, _this.silentBase) || other.silentBase == _this.silentBase)&&const DeepCollectionEquality().equals(other.silent, _this.silent)&&const DeepCollectionEquality().equals(other.newMarkets, _this.newMarkets)&&const DeepCollectionEquality().equals(other.stores, _this.stores)&&(identical(other.akb, _this.akb) || other.akb == _this.akb)&&(identical(other.orders, _this.orders) || other.orders == _this.orders)&&(identical(other.revenuePerOutlet, _this.revenuePerOutlet) || other.revenuePerOutlet == _this.revenuePerOutlet)&&(identical(other.kgPerOutlet, _this.kgPerOutlet) || other.kgPerOutlet == _this.kgPerOutlet)&&(identical(other.executionLevel, _this.executionLevel) || other.executionLevel == _this.executionLevel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AgentView;
  return Object.hashAll([runtimeType,_this.period,_this.agentId,_this.name,_this.regionId,_this.regionName,_this.directionName,_this.isVacancy,_this.planKg,_this.execution,_this.factKg,_this.revenue,_this.visits,_this.visitsWithOrder,_this.conversion,_this.sumPerVisit,_this.avgCheck,_this.categories,_this.planCategories,_this.categoryTarget,_this.tempo,const DeepCollectionEquality().hash(_this.flags),const DeepCollectionEquality().hash(_this.categoryPlan),_this.sameDays,_this.silentBase,const DeepCollectionEquality().hash(_this.silent),const DeepCollectionEquality().hash(_this.newMarkets),const DeepCollectionEquality().hash(_this.stores),_this.akb,_this.orders,_this.revenuePerOutlet,_this.kgPerOutlet,_this.executionLevel]);
}

@override
String toString() {
  final _this = this as AgentView;
  return 'AgentView(period: ${_this.period}, agentId: ${_this.agentId}, name: ${_this.name}, regionId: ${_this.regionId}, regionName: ${_this.regionName}, directionName: ${_this.directionName}, isVacancy: ${_this.isVacancy}, planKg: ${_this.planKg}, execution: ${_this.execution}, factKg: ${_this.factKg}, revenue: ${_this.revenue}, visits: ${_this.visits}, visitsWithOrder: ${_this.visitsWithOrder}, conversion: ${_this.conversion}, sumPerVisit: ${_this.sumPerVisit}, avgCheck: ${_this.avgCheck}, categories: ${_this.categories}, planCategories: ${_this.planCategories}, categoryTarget: ${_this.categoryTarget}, tempo: ${_this.tempo}, flags: ${_this.flags}, categoryPlan: ${_this.categoryPlan}, sameDays: ${_this.sameDays}, silentBase: ${_this.silentBase}, silent: ${_this.silent}, newMarkets: ${_this.newMarkets}, stores: ${_this.stores}, akb: ${_this.akb}, orders: ${_this.orders}, revenuePerOutlet: ${_this.revenuePerOutlet}, kgPerOutlet: ${_this.kgPerOutlet}, executionLevel: ${_this.executionLevel})';
}


}

/// @nodoc
abstract mixin class $AgentViewCopyWith<$Res>  {
  factory $AgentViewCopyWith(AgentView value, $Res Function(AgentView) _then) = _$AgentViewCopyWithImpl;
@useResult
$Res call({
 SalesPeriod period, int agentId, String name, String? regionId, String? regionName, String? directionName, bool isVacancy, double? planKg, double? execution, double factKg, double revenue, int visits, int visitsWithOrder, MedianValue conversion, MedianValue sumPerVisit, MedianValue avgCheck, int categories, int? planCategories, double categoryTarget, double? tempo, List<AgentFlag> flags, List<CategoryPlanFact> categoryPlan, SameDaysRow? sameDays, int silentBase, List<MarketBrief> silent, List<MarketBrief> newMarkets, List<MarketBrief> stores, int akb, int orders, double? revenuePerOutlet, double? kgPerOutlet, String? executionLevel
});


$SalesPeriodCopyWith<$Res> get period;$MedianValueCopyWith<$Res> get conversion;$MedianValueCopyWith<$Res> get sumPerVisit;$MedianValueCopyWith<$Res> get avgCheck;$SameDaysRowCopyWith<$Res>? get sameDays;

}
/// @nodoc
class _$AgentViewCopyWithImpl<$Res>
    implements $AgentViewCopyWith<$Res> {
  _$AgentViewCopyWithImpl(this._self, this._then);

  final AgentView _self;
  final $Res Function(AgentView) _then;

/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? period = null,Object? agentId = null,Object? name = null,Object? regionId = freezed,Object? regionName = freezed,Object? directionName = freezed,Object? isVacancy = null,Object? planKg = freezed,Object? execution = freezed,Object? factKg = null,Object? revenue = null,Object? visits = null,Object? visitsWithOrder = null,Object? conversion = null,Object? sumPerVisit = null,Object? avgCheck = null,Object? categories = null,Object? planCategories = freezed,Object? categoryTarget = null,Object? tempo = freezed,Object? flags = null,Object? categoryPlan = null,Object? sameDays = freezed,Object? silentBase = null,Object? silent = null,Object? newMarkets = null,Object? stores = null,Object? akb = null,Object? orders = null,Object? revenuePerOutlet = freezed,Object? kgPerOutlet = freezed,Object? executionLevel = freezed,}) {
  return _then(AgentView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,agentId: null == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,regionId: freezed == regionId ? _self.regionId : regionId // ignore: cast_nullable_to_non_nullable
as String?,regionName: freezed == regionName ? _self.regionName : regionName // ignore: cast_nullable_to_non_nullable
as String?,directionName: freezed == directionName ? _self.directionName : directionName // ignore: cast_nullable_to_non_nullable
as String?,isVacancy: null == isVacancy ? _self.isVacancy : isVacancy // ignore: cast_nullable_to_non_nullable
as bool,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,visits: null == visits ? _self.visits : visits // ignore: cast_nullable_to_non_nullable
as int,visitsWithOrder: null == visitsWithOrder ? _self.visitsWithOrder : visitsWithOrder // ignore: cast_nullable_to_non_nullable
as int,conversion: null == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as MedianValue,sumPerVisit: null == sumPerVisit ? _self.sumPerVisit : sumPerVisit // ignore: cast_nullable_to_non_nullable
as MedianValue,avgCheck: null == avgCheck ? _self.avgCheck : avgCheck // ignore: cast_nullable_to_non_nullable
as MedianValue,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,planCategories: freezed == planCategories ? _self.planCategories : planCategories // ignore: cast_nullable_to_non_nullable
as int?,categoryTarget: null == categoryTarget ? _self.categoryTarget : categoryTarget // ignore: cast_nullable_to_non_nullable
as double,tempo: freezed == tempo ? _self.tempo : tempo // ignore: cast_nullable_to_non_nullable
as double?,flags: null == flags ? _self.flags : flags // ignore: cast_nullable_to_non_nullable
as List<AgentFlag>,categoryPlan: null == categoryPlan ? _self.categoryPlan : categoryPlan // ignore: cast_nullable_to_non_nullable
as List<CategoryPlanFact>,sameDays: freezed == sameDays ? _self.sameDays : sameDays // ignore: cast_nullable_to_non_nullable
as SameDaysRow?,silentBase: null == silentBase ? _self.silentBase : silentBase // ignore: cast_nullable_to_non_nullable
as int,silent: null == silent ? _self.silent : silent // ignore: cast_nullable_to_non_nullable
as List<MarketBrief>,newMarkets: null == newMarkets ? _self.newMarkets : newMarkets // ignore: cast_nullable_to_non_nullable
as List<MarketBrief>,stores: null == stores ? _self.stores : stores // ignore: cast_nullable_to_non_nullable
as List<MarketBrief>,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,revenuePerOutlet: freezed == revenuePerOutlet ? _self.revenuePerOutlet : revenuePerOutlet // ignore: cast_nullable_to_non_nullable
as double?,kgPerOutlet: freezed == kgPerOutlet ? _self.kgPerOutlet : kgPerOutlet // ignore: cast_nullable_to_non_nullable
as double?,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}
/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$MedianValueCopyWith<$Res> get conversion {
  
  return $MedianValueCopyWith<$Res>(_self.conversion, (value) {
    return _then(_self.copyWith(conversion: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$MedianValueCopyWith<$Res> get sumPerVisit {
  
  return $MedianValueCopyWith<$Res>(_self.sumPerVisit, (value) {
    return _then(_self.copyWith(sumPerVisit: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$MedianValueCopyWith<$Res> get avgCheck {
  
  return $MedianValueCopyWith<$Res>(_self.avgCheck, (value) {
    return _then(_self.copyWith(avgCheck: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SameDaysRowCopyWith<$Res>? get sameDays {
    if (_self.sameDays == null) {
    return null;
  }

  return $SameDaysRowCopyWith<$Res>(_self.sameDays!, (value) {
    return _then(_self.copyWith(sameDays: value));
  });
}
}


/// Adds pattern-matching-related methods to [AgentView].
extension AgentViewPatterns on AgentView {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AgentView value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AgentView() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AgentView value)  $default,){
final _that = this;
switch (_that) {
case _AgentView():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AgentView value)?  $default,){
final _that = this;
switch (_that) {
case _AgentView() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( SalesPeriod period,  int agentId,  String name,  String? regionId,  String? regionName,  String? directionName,  bool isVacancy,  double? planKg,  double? execution,  double factKg,  double revenue,  int visits,  int visitsWithOrder,  MedianValue conversion,  MedianValue sumPerVisit,  MedianValue avgCheck,  int categories,  int? planCategories,  double categoryTarget,  double? tempo,  List<AgentFlag> flags,  List<CategoryPlanFact> categoryPlan,  SameDaysRow? sameDays,  int silentBase,  List<MarketBrief> silent,  List<MarketBrief> newMarkets,  List<MarketBrief> stores,  int akb,  int orders,  double? revenuePerOutlet,  double? kgPerOutlet,  String? executionLevel)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AgentView() when $default != null:
return $default(_that.period,_that.agentId,_that.name,_that.regionId,_that.regionName,_that.directionName,_that.isVacancy,_that.planKg,_that.execution,_that.factKg,_that.revenue,_that.visits,_that.visitsWithOrder,_that.conversion,_that.sumPerVisit,_that.avgCheck,_that.categories,_that.planCategories,_that.categoryTarget,_that.tempo,_that.flags,_that.categoryPlan,_that.sameDays,_that.silentBase,_that.silent,_that.newMarkets,_that.stores,_that.akb,_that.orders,_that.revenuePerOutlet,_that.kgPerOutlet,_that.executionLevel);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( SalesPeriod period,  int agentId,  String name,  String? regionId,  String? regionName,  String? directionName,  bool isVacancy,  double? planKg,  double? execution,  double factKg,  double revenue,  int visits,  int visitsWithOrder,  MedianValue conversion,  MedianValue sumPerVisit,  MedianValue avgCheck,  int categories,  int? planCategories,  double categoryTarget,  double? tempo,  List<AgentFlag> flags,  List<CategoryPlanFact> categoryPlan,  SameDaysRow? sameDays,  int silentBase,  List<MarketBrief> silent,  List<MarketBrief> newMarkets,  List<MarketBrief> stores,  int akb,  int orders,  double? revenuePerOutlet,  double? kgPerOutlet,  String? executionLevel)  $default,) {final _that = this;
switch (_that) {
case _AgentView():
return $default(_that.period,_that.agentId,_that.name,_that.regionId,_that.regionName,_that.directionName,_that.isVacancy,_that.planKg,_that.execution,_that.factKg,_that.revenue,_that.visits,_that.visitsWithOrder,_that.conversion,_that.sumPerVisit,_that.avgCheck,_that.categories,_that.planCategories,_that.categoryTarget,_that.tempo,_that.flags,_that.categoryPlan,_that.sameDays,_that.silentBase,_that.silent,_that.newMarkets,_that.stores,_that.akb,_that.orders,_that.revenuePerOutlet,_that.kgPerOutlet,_that.executionLevel);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( SalesPeriod period,  int agentId,  String name,  String? regionId,  String? regionName,  String? directionName,  bool isVacancy,  double? planKg,  double? execution,  double factKg,  double revenue,  int visits,  int visitsWithOrder,  MedianValue conversion,  MedianValue sumPerVisit,  MedianValue avgCheck,  int categories,  int? planCategories,  double categoryTarget,  double? tempo,  List<AgentFlag> flags,  List<CategoryPlanFact> categoryPlan,  SameDaysRow? sameDays,  int silentBase,  List<MarketBrief> silent,  List<MarketBrief> newMarkets,  List<MarketBrief> stores,  int akb,  int orders,  double? revenuePerOutlet,  double? kgPerOutlet,  String? executionLevel)?  $default,) {final _that = this;
switch (_that) {
case _AgentView() when $default != null:
return $default(_that.period,_that.agentId,_that.name,_that.regionId,_that.regionName,_that.directionName,_that.isVacancy,_that.planKg,_that.execution,_that.factKg,_that.revenue,_that.visits,_that.visitsWithOrder,_that.conversion,_that.sumPerVisit,_that.avgCheck,_that.categories,_that.planCategories,_that.categoryTarget,_that.tempo,_that.flags,_that.categoryPlan,_that.sameDays,_that.silentBase,_that.silent,_that.newMarkets,_that.stores,_that.akb,_that.orders,_that.revenuePerOutlet,_that.kgPerOutlet,_that.executionLevel);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AgentView implements AgentView {
  const _AgentView({required this.period, required this.agentId, required this.name, this.regionId, this.regionName, this.directionName, this.isVacancy = false, this.planKg, this.execution, this.factKg = 0, this.revenue = 0, this.visits = 0, this.visitsWithOrder = 0, this.conversion = const MedianValue(), this.sumPerVisit = const MedianValue(), this.avgCheck = const MedianValue(), this.categories = 0, this.planCategories, this.categoryTarget = 4, this.tempo,  List<AgentFlag> flags = const <AgentFlag>[],  List<CategoryPlanFact> categoryPlan = const <CategoryPlanFact>[], this.sameDays, this.silentBase = 0,  List<MarketBrief> silent = const <MarketBrief>[],  List<MarketBrief> newMarkets = const <MarketBrief>[],  List<MarketBrief> stores = const <MarketBrief>[], this.akb = 0, this.orders = 0, this.revenuePerOutlet, this.kgPerOutlet, this.executionLevel}): _flags = flags,_categoryPlan = categoryPlan,_silent = silent,_newMarkets = newMarkets,_stores = stores;
  factory _AgentView.fromJson(Map<String, dynamic> json) => _$AgentViewFromJson(json);

@override final  SalesPeriod period;
@override final  int agentId;
@override final  String name;
@override final  String? regionId;
@override final  String? regionName;
@override final  String? directionName;
@override@JsonKey() final  bool isVacancy;
@override final  double? planKg;
@override final  double? execution;
@override@JsonKey() final  double factKg;
@override@JsonKey() final  double revenue;
@override@JsonKey() final  int visits;
@override@JsonKey() final  int visitsWithOrder;
@override@JsonKey() final  MedianValue conversion;
@override@JsonKey() final  MedianValue sumPerVisit;
@override@JsonKey() final  MedianValue avgCheck;
@override@JsonKey() final  int categories;
@override final  int? planCategories;
@override@JsonKey() final  double categoryTarget;
@override final  double? tempo;
 final  List<AgentFlag> _flags;
@override@JsonKey() List<AgentFlag> get flags {
  if (_flags is EqualUnmodifiableListView) return _flags;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_flags);
}

 final  List<CategoryPlanFact> _categoryPlan;
@override@JsonKey() List<CategoryPlanFact> get categoryPlan {
  if (_categoryPlan is EqualUnmodifiableListView) return _categoryPlan;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_categoryPlan);
}

@override final  SameDaysRow? sameDays;
@override@JsonKey() final  int silentBase;
 final  List<MarketBrief> _silent;
@override@JsonKey() List<MarketBrief> get silent {
  if (_silent is EqualUnmodifiableListView) return _silent;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_silent);
}

 final  List<MarketBrief> _newMarkets;
@override@JsonKey() List<MarketBrief> get newMarkets {
  if (_newMarkets is EqualUnmodifiableListView) return _newMarkets;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_newMarkets);
}

 final  List<MarketBrief> _stores;
@override@JsonKey() List<MarketBrief> get stores {
  if (_stores is EqualUnmodifiableListView) return _stores;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_stores);
}

@override@JsonKey() final  int akb;
@override@JsonKey() final  int orders;
@override final  double? revenuePerOutlet;
@override final  double? kgPerOutlet;
@override final  String? executionLevel;

/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AgentViewCopyWith<_AgentView> get copyWith => __$AgentViewCopyWithImpl<_AgentView>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AgentViewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AgentView&&(identical(other.period, period) || other.period == period)&&(identical(other.agentId, agentId) || other.agentId == agentId)&&(identical(other.name, name) || other.name == name)&&(identical(other.regionId, regionId) || other.regionId == regionId)&&(identical(other.regionName, regionName) || other.regionName == regionName)&&(identical(other.directionName, directionName) || other.directionName == directionName)&&(identical(other.isVacancy, isVacancy) || other.isVacancy == isVacancy)&&(identical(other.planKg, planKg) || other.planKg == planKg)&&(identical(other.execution, execution) || other.execution == execution)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.visits, visits) || other.visits == visits)&&(identical(other.visitsWithOrder, visitsWithOrder) || other.visitsWithOrder == visitsWithOrder)&&(identical(other.conversion, conversion) || other.conversion == conversion)&&(identical(other.sumPerVisit, sumPerVisit) || other.sumPerVisit == sumPerVisit)&&(identical(other.avgCheck, avgCheck) || other.avgCheck == avgCheck)&&(identical(other.categories, categories) || other.categories == categories)&&(identical(other.planCategories, planCategories) || other.planCategories == planCategories)&&(identical(other.categoryTarget, categoryTarget) || other.categoryTarget == categoryTarget)&&(identical(other.tempo, tempo) || other.tempo == tempo)&&const DeepCollectionEquality().equals(other.flags, _flags)&&const DeepCollectionEquality().equals(other.categoryPlan, _categoryPlan)&&(identical(other.sameDays, sameDays) || other.sameDays == sameDays)&&(identical(other.silentBase, silentBase) || other.silentBase == silentBase)&&const DeepCollectionEquality().equals(other.silent, _silent)&&const DeepCollectionEquality().equals(other.newMarkets, _newMarkets)&&const DeepCollectionEquality().equals(other.stores, _stores)&&(identical(other.akb, akb) || other.akb == akb)&&(identical(other.orders, orders) || other.orders == orders)&&(identical(other.revenuePerOutlet, revenuePerOutlet) || other.revenuePerOutlet == revenuePerOutlet)&&(identical(other.kgPerOutlet, kgPerOutlet) || other.kgPerOutlet == kgPerOutlet)&&(identical(other.executionLevel, executionLevel) || other.executionLevel == executionLevel));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hashAll([runtimeType,period,agentId,name,regionId,regionName,directionName,isVacancy,planKg,execution,factKg,revenue,visits,visitsWithOrder,conversion,sumPerVisit,avgCheck,categories,planCategories,categoryTarget,tempo,const DeepCollectionEquality().hash(_flags),const DeepCollectionEquality().hash(_categoryPlan),sameDays,silentBase,const DeepCollectionEquality().hash(_silent),const DeepCollectionEquality().hash(_newMarkets),const DeepCollectionEquality().hash(_stores),akb,orders,revenuePerOutlet,kgPerOutlet,executionLevel]);
}

@override
String toString() {
    return 'AgentView(period: $period, agentId: $agentId, name: $name, regionId: $regionId, regionName: $regionName, directionName: $directionName, isVacancy: $isVacancy, planKg: $planKg, execution: $execution, factKg: $factKg, revenue: $revenue, visits: $visits, visitsWithOrder: $visitsWithOrder, conversion: $conversion, sumPerVisit: $sumPerVisit, avgCheck: $avgCheck, categories: $categories, planCategories: $planCategories, categoryTarget: $categoryTarget, tempo: $tempo, flags: $flags, categoryPlan: $categoryPlan, sameDays: $sameDays, silentBase: $silentBase, silent: $silent, newMarkets: $newMarkets, stores: $stores, akb: $akb, orders: $orders, revenuePerOutlet: $revenuePerOutlet, kgPerOutlet: $kgPerOutlet, executionLevel: $executionLevel)';
}


}

/// @nodoc
abstract mixin class _$AgentViewCopyWith<$Res> implements $AgentViewCopyWith<$Res> {
  factory _$AgentViewCopyWith(_AgentView value, $Res Function(_AgentView) _then) = __$AgentViewCopyWithImpl;
@override @useResult
$Res call({
 SalesPeriod period, int agentId, String name, String? regionId, String? regionName, String? directionName, bool isVacancy, double? planKg, double? execution, double factKg, double revenue, int visits, int visitsWithOrder, MedianValue conversion, MedianValue sumPerVisit, MedianValue avgCheck, int categories, int? planCategories, double categoryTarget, double? tempo, List<AgentFlag> flags, List<CategoryPlanFact> categoryPlan, SameDaysRow? sameDays, int silentBase, List<MarketBrief> silent, List<MarketBrief> newMarkets, List<MarketBrief> stores, int akb, int orders, double? revenuePerOutlet, double? kgPerOutlet, String? executionLevel
});


@override $SalesPeriodCopyWith<$Res> get period;@override $MedianValueCopyWith<$Res> get conversion;@override $MedianValueCopyWith<$Res> get sumPerVisit;@override $MedianValueCopyWith<$Res> get avgCheck;@override $SameDaysRowCopyWith<$Res>? get sameDays;

}
/// @nodoc
class __$AgentViewCopyWithImpl<$Res>
    implements _$AgentViewCopyWith<$Res> {
  __$AgentViewCopyWithImpl(this._self, this._then);

  final _AgentView _self;
  final $Res Function(_AgentView) _then;

/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? period = null,Object? agentId = null,Object? name = null,Object? regionId = freezed,Object? regionName = freezed,Object? directionName = freezed,Object? isVacancy = null,Object? planKg = freezed,Object? execution = freezed,Object? factKg = null,Object? revenue = null,Object? visits = null,Object? visitsWithOrder = null,Object? conversion = null,Object? sumPerVisit = null,Object? avgCheck = null,Object? categories = null,Object? planCategories = freezed,Object? categoryTarget = null,Object? tempo = freezed,Object? flags = null,Object? categoryPlan = null,Object? sameDays = freezed,Object? silentBase = null,Object? silent = null,Object? newMarkets = null,Object? stores = null,Object? akb = null,Object? orders = null,Object? revenuePerOutlet = freezed,Object? kgPerOutlet = freezed,Object? executionLevel = freezed,}) {
  return _then(_AgentView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,agentId: null == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,regionId: freezed == regionId ? _self.regionId : regionId // ignore: cast_nullable_to_non_nullable
as String?,regionName: freezed == regionName ? _self.regionName : regionName // ignore: cast_nullable_to_non_nullable
as String?,directionName: freezed == directionName ? _self.directionName : directionName // ignore: cast_nullable_to_non_nullable
as String?,isVacancy: null == isVacancy ? _self.isVacancy : isVacancy // ignore: cast_nullable_to_non_nullable
as bool,planKg: freezed == planKg ? _self.planKg : planKg // ignore: cast_nullable_to_non_nullable
as double?,execution: freezed == execution ? _self.execution : execution // ignore: cast_nullable_to_non_nullable
as double?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,visits: null == visits ? _self.visits : visits // ignore: cast_nullable_to_non_nullable
as int,visitsWithOrder: null == visitsWithOrder ? _self.visitsWithOrder : visitsWithOrder // ignore: cast_nullable_to_non_nullable
as int,conversion: null == conversion ? _self.conversion : conversion // ignore: cast_nullable_to_non_nullable
as MedianValue,sumPerVisit: null == sumPerVisit ? _self.sumPerVisit : sumPerVisit // ignore: cast_nullable_to_non_nullable
as MedianValue,avgCheck: null == avgCheck ? _self.avgCheck : avgCheck // ignore: cast_nullable_to_non_nullable
as MedianValue,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,planCategories: freezed == planCategories ? _self.planCategories : planCategories // ignore: cast_nullable_to_non_nullable
as int?,categoryTarget: null == categoryTarget ? _self.categoryTarget : categoryTarget // ignore: cast_nullable_to_non_nullable
as double,tempo: freezed == tempo ? _self.tempo : tempo // ignore: cast_nullable_to_non_nullable
as double?,flags: null == flags ? _self._flags : flags // ignore: cast_nullable_to_non_nullable
as List<AgentFlag>,categoryPlan: null == categoryPlan ? _self._categoryPlan : categoryPlan // ignore: cast_nullable_to_non_nullable
as List<CategoryPlanFact>,sameDays: freezed == sameDays ? _self.sameDays : sameDays // ignore: cast_nullable_to_non_nullable
as SameDaysRow?,silentBase: null == silentBase ? _self.silentBase : silentBase // ignore: cast_nullable_to_non_nullable
as int,silent: null == silent ? _self._silent : silent // ignore: cast_nullable_to_non_nullable
as List<MarketBrief>,newMarkets: null == newMarkets ? _self._newMarkets : newMarkets // ignore: cast_nullable_to_non_nullable
as List<MarketBrief>,stores: null == stores ? _self._stores : stores // ignore: cast_nullable_to_non_nullable
as List<MarketBrief>,akb: null == akb ? _self.akb : akb // ignore: cast_nullable_to_non_nullable
as int,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,revenuePerOutlet: freezed == revenuePerOutlet ? _self.revenuePerOutlet : revenuePerOutlet // ignore: cast_nullable_to_non_nullable
as double?,kgPerOutlet: freezed == kgPerOutlet ? _self.kgPerOutlet : kgPerOutlet // ignore: cast_nullable_to_non_nullable
as double?,executionLevel: freezed == executionLevel ? _self.executionLevel : executionLevel // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$MedianValueCopyWith<$Res> get conversion {
  
  return $MedianValueCopyWith<$Res>(_self.conversion, (value) {
    return _then(_self.copyWith(conversion: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$MedianValueCopyWith<$Res> get sumPerVisit {
  
  return $MedianValueCopyWith<$Res>(_self.sumPerVisit, (value) {
    return _then(_self.copyWith(sumPerVisit: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$MedianValueCopyWith<$Res> get avgCheck {
  
  return $MedianValueCopyWith<$Res>(_self.avgCheck, (value) {
    return _then(_self.copyWith(avgCheck: value));
  });
}/// Create a copy of AgentView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SameDaysRowCopyWith<$Res>? get sameDays {
    if (_self.sameDays == null) {
    return null;
  }

  return $SameDaysRowCopyWith<$Res>(_self.sameDays!, (value) {
    return _then(_self.copyWith(sameDays: value));
  });
}
}


/// @nodoc
mixin _$StoreCategory {

 String get name; double get kg; double get revenue; double? get share;
/// Create a copy of StoreCategory
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$StoreCategoryCopyWith<StoreCategory> get copyWith => _$StoreCategoryCopyWithImpl<StoreCategory>(this as StoreCategory, _$identity);

  /// Serializes this StoreCategory to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as StoreCategory;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is StoreCategory&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.kg, _this.kg) || other.kg == _this.kg)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.share, _this.share) || other.share == _this.share));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as StoreCategory;
  return Object.hash(runtimeType,_this.name,_this.kg,_this.revenue,_this.share);
}

@override
String toString() {
  final _this = this as StoreCategory;
  return 'StoreCategory(name: ${_this.name}, kg: ${_this.kg}, revenue: ${_this.revenue}, share: ${_this.share})';
}


}

/// @nodoc
abstract mixin class $StoreCategoryCopyWith<$Res>  {
  factory $StoreCategoryCopyWith(StoreCategory value, $Res Function(StoreCategory) _then) = _$StoreCategoryCopyWithImpl;
@useResult
$Res call({
 String name, double kg, double revenue, double? share
});




}
/// @nodoc
class _$StoreCategoryCopyWithImpl<$Res>
    implements $StoreCategoryCopyWith<$Res> {
  _$StoreCategoryCopyWithImpl(this._self, this._then);

  final StoreCategory _self;
  final $Res Function(StoreCategory) _then;

/// Create a copy of StoreCategory
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? name = null,Object? kg = null,Object? revenue = null,Object? share = freezed,}) {
  return _then(StoreCategory(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,share: freezed == share ? _self.share : share // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [StoreCategory].
extension StoreCategoryPatterns on StoreCategory {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _StoreCategory value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _StoreCategory() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _StoreCategory value)  $default,){
final _that = this;
switch (_that) {
case _StoreCategory():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _StoreCategory value)?  $default,){
final _that = this;
switch (_that) {
case _StoreCategory() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String name,  double kg,  double revenue,  double? share)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _StoreCategory() when $default != null:
return $default(_that.name,_that.kg,_that.revenue,_that.share);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String name,  double kg,  double revenue,  double? share)  $default,) {final _that = this;
switch (_that) {
case _StoreCategory():
return $default(_that.name,_that.kg,_that.revenue,_that.share);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String name,  double kg,  double revenue,  double? share)?  $default,) {final _that = this;
switch (_that) {
case _StoreCategory() when $default != null:
return $default(_that.name,_that.kg,_that.revenue,_that.share);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _StoreCategory implements StoreCategory {
  const _StoreCategory({required this.name, this.kg = 0, this.revenue = 0, this.share});
  factory _StoreCategory.fromJson(Map<String, dynamic> json) => _$StoreCategoryFromJson(json);

@override final  String name;
@override@JsonKey() final  double kg;
@override@JsonKey() final  double revenue;
@override final  double? share;

/// Create a copy of StoreCategory
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$StoreCategoryCopyWith<_StoreCategory> get copyWith => __$StoreCategoryCopyWithImpl<_StoreCategory>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$StoreCategoryToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _StoreCategory&&(identical(other.name, name) || other.name == name)&&(identical(other.kg, kg) || other.kg == kg)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.share, share) || other.share == share));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,name,kg,revenue,share);
}

@override
String toString() {
    return 'StoreCategory(name: $name, kg: $kg, revenue: $revenue, share: $share)';
}


}

/// @nodoc
abstract mixin class _$StoreCategoryCopyWith<$Res> implements $StoreCategoryCopyWith<$Res> {
  factory _$StoreCategoryCopyWith(_StoreCategory value, $Res Function(_StoreCategory) _then) = __$StoreCategoryCopyWithImpl;
@override @useResult
$Res call({
 String name, double kg, double revenue, double? share
});




}
/// @nodoc
class __$StoreCategoryCopyWithImpl<$Res>
    implements _$StoreCategoryCopyWith<$Res> {
  __$StoreCategoryCopyWithImpl(this._self, this._then);

  final _StoreCategory _self;
  final $Res Function(_StoreCategory) _then;

/// Create a copy of StoreCategory
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? name = null,Object? kg = null,Object? revenue = null,Object? share = freezed,}) {
  return _then(_StoreCategory(
name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,share: freezed == share ? _self.share : share // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$StoreProduct {

 int get productId; String get name; String? get category; double get kg; double get revenue; bool get isTop;
/// Create a copy of StoreProduct
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$StoreProductCopyWith<StoreProduct> get copyWith => _$StoreProductCopyWithImpl<StoreProduct>(this as StoreProduct, _$identity);

  /// Serializes this StoreProduct to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as StoreProduct;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is StoreProduct&&(identical(other.productId, _this.productId) || other.productId == _this.productId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.category, _this.category) || other.category == _this.category)&&(identical(other.kg, _this.kg) || other.kg == _this.kg)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.isTop, _this.isTop) || other.isTop == _this.isTop));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as StoreProduct;
  return Object.hash(runtimeType,_this.productId,_this.name,_this.category,_this.kg,_this.revenue,_this.isTop);
}

@override
String toString() {
  final _this = this as StoreProduct;
  return 'StoreProduct(productId: ${_this.productId}, name: ${_this.name}, category: ${_this.category}, kg: ${_this.kg}, revenue: ${_this.revenue}, isTop: ${_this.isTop})';
}


}

/// @nodoc
abstract mixin class $StoreProductCopyWith<$Res>  {
  factory $StoreProductCopyWith(StoreProduct value, $Res Function(StoreProduct) _then) = _$StoreProductCopyWithImpl;
@useResult
$Res call({
 int productId, String name, String? category, double kg, double revenue, bool isTop
});




}
/// @nodoc
class _$StoreProductCopyWithImpl<$Res>
    implements $StoreProductCopyWith<$Res> {
  _$StoreProductCopyWithImpl(this._self, this._then);

  final StoreProduct _self;
  final $Res Function(StoreProduct) _then;

/// Create a copy of StoreProduct
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? productId = null,Object? name = null,Object? category = freezed,Object? kg = null,Object? revenue = null,Object? isTop = null,}) {
  return _then(StoreProduct(
productId: null == productId ? _self.productId : productId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,category: freezed == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String?,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,isTop: null == isTop ? _self.isTop : isTop // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [StoreProduct].
extension StoreProductPatterns on StoreProduct {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _StoreProduct value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _StoreProduct() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _StoreProduct value)  $default,){
final _that = this;
switch (_that) {
case _StoreProduct():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _StoreProduct value)?  $default,){
final _that = this;
switch (_that) {
case _StoreProduct() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int productId,  String name,  String? category,  double kg,  double revenue,  bool isTop)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _StoreProduct() when $default != null:
return $default(_that.productId,_that.name,_that.category,_that.kg,_that.revenue,_that.isTop);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int productId,  String name,  String? category,  double kg,  double revenue,  bool isTop)  $default,) {final _that = this;
switch (_that) {
case _StoreProduct():
return $default(_that.productId,_that.name,_that.category,_that.kg,_that.revenue,_that.isTop);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int productId,  String name,  String? category,  double kg,  double revenue,  bool isTop)?  $default,) {final _that = this;
switch (_that) {
case _StoreProduct() when $default != null:
return $default(_that.productId,_that.name,_that.category,_that.kg,_that.revenue,_that.isTop);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _StoreProduct implements StoreProduct {
  const _StoreProduct({required this.productId, required this.name, this.category, this.kg = 0, this.revenue = 0, this.isTop = false});
  factory _StoreProduct.fromJson(Map<String, dynamic> json) => _$StoreProductFromJson(json);

@override final  int productId;
@override final  String name;
@override final  String? category;
@override@JsonKey() final  double kg;
@override@JsonKey() final  double revenue;
@override@JsonKey() final  bool isTop;

/// Create a copy of StoreProduct
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$StoreProductCopyWith<_StoreProduct> get copyWith => __$StoreProductCopyWithImpl<_StoreProduct>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$StoreProductToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _StoreProduct&&(identical(other.productId, productId) || other.productId == productId)&&(identical(other.name, name) || other.name == name)&&(identical(other.category, category) || other.category == category)&&(identical(other.kg, kg) || other.kg == kg)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.isTop, isTop) || other.isTop == isTop));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,productId,name,category,kg,revenue,isTop);
}

@override
String toString() {
    return 'StoreProduct(productId: $productId, name: $name, category: $category, kg: $kg, revenue: $revenue, isTop: $isTop)';
}


}

/// @nodoc
abstract mixin class _$StoreProductCopyWith<$Res> implements $StoreProductCopyWith<$Res> {
  factory _$StoreProductCopyWith(_StoreProduct value, $Res Function(_StoreProduct) _then) = __$StoreProductCopyWithImpl;
@override @useResult
$Res call({
 int productId, String name, String? category, double kg, double revenue, bool isTop
});




}
/// @nodoc
class __$StoreProductCopyWithImpl<$Res>
    implements _$StoreProductCopyWith<$Res> {
  __$StoreProductCopyWithImpl(this._self, this._then);

  final _StoreProduct _self;
  final $Res Function(_StoreProduct) _then;

/// Create a copy of StoreProduct
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? productId = null,Object? name = null,Object? category = freezed,Object? kg = null,Object? revenue = null,Object? isTop = null,}) {
  return _then(_StoreProduct(
productId: null == productId ? _self.productId : productId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,category: freezed == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String?,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,isTop: null == isTop ? _self.isTop : isTop // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$StoreView {

 SalesPeriod get period; int get marketId; String get name; String? get regionName; double get factKg; double get revenue; int get orders; int get categories; int get positions; double get prevMonthKg; double get prevMonthRevenue; List<StoreCategory> get categoryRows; List<StoreProduct> get products; List<String> get agents;
/// Create a copy of StoreView
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$StoreViewCopyWith<StoreView> get copyWith => _$StoreViewCopyWithImpl<StoreView>(this as StoreView, _$identity);

  /// Serializes this StoreView to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as StoreView;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is StoreView&&(identical(other.period, _this.period) || other.period == _this.period)&&(identical(other.marketId, _this.marketId) || other.marketId == _this.marketId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.regionName, _this.regionName) || other.regionName == _this.regionName)&&(identical(other.factKg, _this.factKg) || other.factKg == _this.factKg)&&(identical(other.revenue, _this.revenue) || other.revenue == _this.revenue)&&(identical(other.orders, _this.orders) || other.orders == _this.orders)&&(identical(other.categories, _this.categories) || other.categories == _this.categories)&&(identical(other.positions, _this.positions) || other.positions == _this.positions)&&(identical(other.prevMonthKg, _this.prevMonthKg) || other.prevMonthKg == _this.prevMonthKg)&&(identical(other.prevMonthRevenue, _this.prevMonthRevenue) || other.prevMonthRevenue == _this.prevMonthRevenue)&&const DeepCollectionEquality().equals(other.categoryRows, _this.categoryRows)&&const DeepCollectionEquality().equals(other.products, _this.products)&&const DeepCollectionEquality().equals(other.agents, _this.agents));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as StoreView;
  return Object.hash(runtimeType,_this.period,_this.marketId,_this.name,_this.regionName,_this.factKg,_this.revenue,_this.orders,_this.categories,_this.positions,_this.prevMonthKg,_this.prevMonthRevenue,const DeepCollectionEquality().hash(_this.categoryRows),const DeepCollectionEquality().hash(_this.products),const DeepCollectionEquality().hash(_this.agents));
}

@override
String toString() {
  final _this = this as StoreView;
  return 'StoreView(period: ${_this.period}, marketId: ${_this.marketId}, name: ${_this.name}, regionName: ${_this.regionName}, factKg: ${_this.factKg}, revenue: ${_this.revenue}, orders: ${_this.orders}, categories: ${_this.categories}, positions: ${_this.positions}, prevMonthKg: ${_this.prevMonthKg}, prevMonthRevenue: ${_this.prevMonthRevenue}, categoryRows: ${_this.categoryRows}, products: ${_this.products}, agents: ${_this.agents})';
}


}

/// @nodoc
abstract mixin class $StoreViewCopyWith<$Res>  {
  factory $StoreViewCopyWith(StoreView value, $Res Function(StoreView) _then) = _$StoreViewCopyWithImpl;
@useResult
$Res call({
 SalesPeriod period, int marketId, String name, String? regionName, double factKg, double revenue, int orders, int categories, int positions, double prevMonthKg, double prevMonthRevenue, List<StoreCategory> categoryRows, List<StoreProduct> products, List<String> agents
});


$SalesPeriodCopyWith<$Res> get period;

}
/// @nodoc
class _$StoreViewCopyWithImpl<$Res>
    implements $StoreViewCopyWith<$Res> {
  _$StoreViewCopyWithImpl(this._self, this._then);

  final StoreView _self;
  final $Res Function(StoreView) _then;

/// Create a copy of StoreView
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? period = null,Object? marketId = null,Object? name = null,Object? regionName = freezed,Object? factKg = null,Object? revenue = null,Object? orders = null,Object? categories = null,Object? positions = null,Object? prevMonthKg = null,Object? prevMonthRevenue = null,Object? categoryRows = null,Object? products = null,Object? agents = null,}) {
  return _then(StoreView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,marketId: null == marketId ? _self.marketId : marketId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,regionName: freezed == regionName ? _self.regionName : regionName // ignore: cast_nullable_to_non_nullable
as String?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,positions: null == positions ? _self.positions : positions // ignore: cast_nullable_to_non_nullable
as int,prevMonthKg: null == prevMonthKg ? _self.prevMonthKg : prevMonthKg // ignore: cast_nullable_to_non_nullable
as double,prevMonthRevenue: null == prevMonthRevenue ? _self.prevMonthRevenue : prevMonthRevenue // ignore: cast_nullable_to_non_nullable
as double,categoryRows: null == categoryRows ? _self.categoryRows : categoryRows // ignore: cast_nullable_to_non_nullable
as List<StoreCategory>,products: null == products ? _self.products : products // ignore: cast_nullable_to_non_nullable
as List<StoreProduct>,agents: null == agents ? _self.agents : agents // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}
/// Create a copy of StoreView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}
}


/// Adds pattern-matching-related methods to [StoreView].
extension StoreViewPatterns on StoreView {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _StoreView value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _StoreView() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _StoreView value)  $default,){
final _that = this;
switch (_that) {
case _StoreView():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _StoreView value)?  $default,){
final _that = this;
switch (_that) {
case _StoreView() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( SalesPeriod period,  int marketId,  String name,  String? regionName,  double factKg,  double revenue,  int orders,  int categories,  int positions,  double prevMonthKg,  double prevMonthRevenue,  List<StoreCategory> categoryRows,  List<StoreProduct> products,  List<String> agents)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _StoreView() when $default != null:
return $default(_that.period,_that.marketId,_that.name,_that.regionName,_that.factKg,_that.revenue,_that.orders,_that.categories,_that.positions,_that.prevMonthKg,_that.prevMonthRevenue,_that.categoryRows,_that.products,_that.agents);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( SalesPeriod period,  int marketId,  String name,  String? regionName,  double factKg,  double revenue,  int orders,  int categories,  int positions,  double prevMonthKg,  double prevMonthRevenue,  List<StoreCategory> categoryRows,  List<StoreProduct> products,  List<String> agents)  $default,) {final _that = this;
switch (_that) {
case _StoreView():
return $default(_that.period,_that.marketId,_that.name,_that.regionName,_that.factKg,_that.revenue,_that.orders,_that.categories,_that.positions,_that.prevMonthKg,_that.prevMonthRevenue,_that.categoryRows,_that.products,_that.agents);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( SalesPeriod period,  int marketId,  String name,  String? regionName,  double factKg,  double revenue,  int orders,  int categories,  int positions,  double prevMonthKg,  double prevMonthRevenue,  List<StoreCategory> categoryRows,  List<StoreProduct> products,  List<String> agents)?  $default,) {final _that = this;
switch (_that) {
case _StoreView() when $default != null:
return $default(_that.period,_that.marketId,_that.name,_that.regionName,_that.factKg,_that.revenue,_that.orders,_that.categories,_that.positions,_that.prevMonthKg,_that.prevMonthRevenue,_that.categoryRows,_that.products,_that.agents);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _StoreView implements StoreView {
  const _StoreView({required this.period, required this.marketId, required this.name, this.regionName, this.factKg = 0, this.revenue = 0, this.orders = 0, this.categories = 0, this.positions = 0, this.prevMonthKg = 0, this.prevMonthRevenue = 0,  List<StoreCategory> categoryRows = const <StoreCategory>[],  List<StoreProduct> products = const <StoreProduct>[],  List<String> agents = const <String>[]}): _categoryRows = categoryRows,_products = products,_agents = agents;
  factory _StoreView.fromJson(Map<String, dynamic> json) => _$StoreViewFromJson(json);

@override final  SalesPeriod period;
@override final  int marketId;
@override final  String name;
@override final  String? regionName;
@override@JsonKey() final  double factKg;
@override@JsonKey() final  double revenue;
@override@JsonKey() final  int orders;
@override@JsonKey() final  int categories;
@override@JsonKey() final  int positions;
@override@JsonKey() final  double prevMonthKg;
@override@JsonKey() final  double prevMonthRevenue;
 final  List<StoreCategory> _categoryRows;
@override@JsonKey() List<StoreCategory> get categoryRows {
  if (_categoryRows is EqualUnmodifiableListView) return _categoryRows;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_categoryRows);
}

 final  List<StoreProduct> _products;
@override@JsonKey() List<StoreProduct> get products {
  if (_products is EqualUnmodifiableListView) return _products;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_products);
}

 final  List<String> _agents;
@override@JsonKey() List<String> get agents {
  if (_agents is EqualUnmodifiableListView) return _agents;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_agents);
}


/// Create a copy of StoreView
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$StoreViewCopyWith<_StoreView> get copyWith => __$StoreViewCopyWithImpl<_StoreView>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$StoreViewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _StoreView&&(identical(other.period, period) || other.period == period)&&(identical(other.marketId, marketId) || other.marketId == marketId)&&(identical(other.name, name) || other.name == name)&&(identical(other.regionName, regionName) || other.regionName == regionName)&&(identical(other.factKg, factKg) || other.factKg == factKg)&&(identical(other.revenue, revenue) || other.revenue == revenue)&&(identical(other.orders, orders) || other.orders == orders)&&(identical(other.categories, categories) || other.categories == categories)&&(identical(other.positions, positions) || other.positions == positions)&&(identical(other.prevMonthKg, prevMonthKg) || other.prevMonthKg == prevMonthKg)&&(identical(other.prevMonthRevenue, prevMonthRevenue) || other.prevMonthRevenue == prevMonthRevenue)&&const DeepCollectionEquality().equals(other.categoryRows, _categoryRows)&&const DeepCollectionEquality().equals(other.products, _products)&&const DeepCollectionEquality().equals(other.agents, _agents));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,period,marketId,name,regionName,factKg,revenue,orders,categories,positions,prevMonthKg,prevMonthRevenue,const DeepCollectionEquality().hash(_categoryRows),const DeepCollectionEquality().hash(_products),const DeepCollectionEquality().hash(_agents));
}

@override
String toString() {
    return 'StoreView(period: $period, marketId: $marketId, name: $name, regionName: $regionName, factKg: $factKg, revenue: $revenue, orders: $orders, categories: $categories, positions: $positions, prevMonthKg: $prevMonthKg, prevMonthRevenue: $prevMonthRevenue, categoryRows: $categoryRows, products: $products, agents: $agents)';
}


}

/// @nodoc
abstract mixin class _$StoreViewCopyWith<$Res> implements $StoreViewCopyWith<$Res> {
  factory _$StoreViewCopyWith(_StoreView value, $Res Function(_StoreView) _then) = __$StoreViewCopyWithImpl;
@override @useResult
$Res call({
 SalesPeriod period, int marketId, String name, String? regionName, double factKg, double revenue, int orders, int categories, int positions, double prevMonthKg, double prevMonthRevenue, List<StoreCategory> categoryRows, List<StoreProduct> products, List<String> agents
});


@override $SalesPeriodCopyWith<$Res> get period;

}
/// @nodoc
class __$StoreViewCopyWithImpl<$Res>
    implements _$StoreViewCopyWith<$Res> {
  __$StoreViewCopyWithImpl(this._self, this._then);

  final _StoreView _self;
  final $Res Function(_StoreView) _then;

/// Create a copy of StoreView
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? period = null,Object? marketId = null,Object? name = null,Object? regionName = freezed,Object? factKg = null,Object? revenue = null,Object? orders = null,Object? categories = null,Object? positions = null,Object? prevMonthKg = null,Object? prevMonthRevenue = null,Object? categoryRows = null,Object? products = null,Object? agents = null,}) {
  return _then(_StoreView(
period: null == period ? _self.period : period // ignore: cast_nullable_to_non_nullable
as SalesPeriod,marketId: null == marketId ? _self.marketId : marketId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,regionName: freezed == regionName ? _self.regionName : regionName // ignore: cast_nullable_to_non_nullable
as String?,factKg: null == factKg ? _self.factKg : factKg // ignore: cast_nullable_to_non_nullable
as double,revenue: null == revenue ? _self.revenue : revenue // ignore: cast_nullable_to_non_nullable
as double,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,categories: null == categories ? _self.categories : categories // ignore: cast_nullable_to_non_nullable
as int,positions: null == positions ? _self.positions : positions // ignore: cast_nullable_to_non_nullable
as int,prevMonthKg: null == prevMonthKg ? _self.prevMonthKg : prevMonthKg // ignore: cast_nullable_to_non_nullable
as double,prevMonthRevenue: null == prevMonthRevenue ? _self.prevMonthRevenue : prevMonthRevenue // ignore: cast_nullable_to_non_nullable
as double,categoryRows: null == categoryRows ? _self._categoryRows : categoryRows // ignore: cast_nullable_to_non_nullable
as List<StoreCategory>,products: null == products ? _self._products : products // ignore: cast_nullable_to_non_nullable
as List<StoreProduct>,agents: null == agents ? _self._agents : agents // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}

/// Create a copy of StoreView
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$SalesPeriodCopyWith<$Res> get period {
  
  return $SalesPeriodCopyWith<$Res>(_self.period, (value) {
    return _then(_self.copyWith(period: value));
  });
}
}

// dart format on
