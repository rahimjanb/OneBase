// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'crm_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$FieldCustomerRow {

 int get marketId; String get name; String? get address; String? get branch; String? get type; double? get lat; double? get lon; String? get agentId; String? get agentName;/// Low | Medium | High | Urgent
 String get priority;/// Active | Problem | Inactive
 String get status; String? get lastVisitDate; String? get lastOrderDate; double get sales7; double get sales30; double get sales90;/// Изменение продаж, % (уже ×100).
 double? get trendPct; double? get monthlyTarget;
/// Create a copy of FieldCustomerRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FieldCustomerRowCopyWith<FieldCustomerRow> get copyWith => _$FieldCustomerRowCopyWithImpl<FieldCustomerRow>(this as FieldCustomerRow, _$identity);

  /// Serializes this FieldCustomerRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FieldCustomerRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FieldCustomerRow&&(identical(other.marketId, _this.marketId) || other.marketId == _this.marketId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.address, _this.address) || other.address == _this.address)&&(identical(other.branch, _this.branch) || other.branch == _this.branch)&&(identical(other.type, _this.type) || other.type == _this.type)&&(identical(other.lat, _this.lat) || other.lat == _this.lat)&&(identical(other.lon, _this.lon) || other.lon == _this.lon)&&(identical(other.agentId, _this.agentId) || other.agentId == _this.agentId)&&(identical(other.agentName, _this.agentName) || other.agentName == _this.agentName)&&(identical(other.priority, _this.priority) || other.priority == _this.priority)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.lastVisitDate, _this.lastVisitDate) || other.lastVisitDate == _this.lastVisitDate)&&(identical(other.lastOrderDate, _this.lastOrderDate) || other.lastOrderDate == _this.lastOrderDate)&&(identical(other.sales7, _this.sales7) || other.sales7 == _this.sales7)&&(identical(other.sales30, _this.sales30) || other.sales30 == _this.sales30)&&(identical(other.sales90, _this.sales90) || other.sales90 == _this.sales90)&&(identical(other.trendPct, _this.trendPct) || other.trendPct == _this.trendPct)&&(identical(other.monthlyTarget, _this.monthlyTarget) || other.monthlyTarget == _this.monthlyTarget));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FieldCustomerRow;
  return Object.hash(runtimeType,_this.marketId,_this.name,_this.address,_this.branch,_this.type,_this.lat,_this.lon,_this.agentId,_this.agentName,_this.priority,_this.status,_this.lastVisitDate,_this.lastOrderDate,_this.sales7,_this.sales30,_this.sales90,_this.trendPct,_this.monthlyTarget);
}

@override
String toString() {
  final _this = this as FieldCustomerRow;
  return 'FieldCustomerRow(marketId: ${_this.marketId}, name: ${_this.name}, address: ${_this.address}, branch: ${_this.branch}, type: ${_this.type}, lat: ${_this.lat}, lon: ${_this.lon}, agentId: ${_this.agentId}, agentName: ${_this.agentName}, priority: ${_this.priority}, status: ${_this.status}, lastVisitDate: ${_this.lastVisitDate}, lastOrderDate: ${_this.lastOrderDate}, sales7: ${_this.sales7}, sales30: ${_this.sales30}, sales90: ${_this.sales90}, trendPct: ${_this.trendPct}, monthlyTarget: ${_this.monthlyTarget})';
}


}

/// @nodoc
abstract mixin class $FieldCustomerRowCopyWith<$Res>  {
  factory $FieldCustomerRowCopyWith(FieldCustomerRow value, $Res Function(FieldCustomerRow) _then) = _$FieldCustomerRowCopyWithImpl;
@useResult
$Res call({
 int marketId, String name, String? address, String? branch, String? type, double? lat, double? lon, String? agentId, String? agentName, String priority, String status, String? lastVisitDate, String? lastOrderDate, double sales7, double sales30, double sales90, double? trendPct, double? monthlyTarget
});




}
/// @nodoc
class _$FieldCustomerRowCopyWithImpl<$Res>
    implements $FieldCustomerRowCopyWith<$Res> {
  _$FieldCustomerRowCopyWithImpl(this._self, this._then);

  final FieldCustomerRow _self;
  final $Res Function(FieldCustomerRow) _then;

/// Create a copy of FieldCustomerRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? marketId = null,Object? name = null,Object? address = freezed,Object? branch = freezed,Object? type = freezed,Object? lat = freezed,Object? lon = freezed,Object? agentId = freezed,Object? agentName = freezed,Object? priority = null,Object? status = null,Object? lastVisitDate = freezed,Object? lastOrderDate = freezed,Object? sales7 = null,Object? sales30 = null,Object? sales90 = null,Object? trendPct = freezed,Object? monthlyTarget = freezed,}) {
  return _then(FieldCustomerRow(
marketId: null == marketId ? _self.marketId : marketId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,address: freezed == address ? _self.address : address // ignore: cast_nullable_to_non_nullable
as String?,branch: freezed == branch ? _self.branch : branch // ignore: cast_nullable_to_non_nullable
as String?,type: freezed == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String?,lat: freezed == lat ? _self.lat : lat // ignore: cast_nullable_to_non_nullable
as double?,lon: freezed == lon ? _self.lon : lon // ignore: cast_nullable_to_non_nullable
as double?,agentId: freezed == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as String?,agentName: freezed == agentName ? _self.agentName : agentName // ignore: cast_nullable_to_non_nullable
as String?,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,lastVisitDate: freezed == lastVisitDate ? _self.lastVisitDate : lastVisitDate // ignore: cast_nullable_to_non_nullable
as String?,lastOrderDate: freezed == lastOrderDate ? _self.lastOrderDate : lastOrderDate // ignore: cast_nullable_to_non_nullable
as String?,sales7: null == sales7 ? _self.sales7 : sales7 // ignore: cast_nullable_to_non_nullable
as double,sales30: null == sales30 ? _self.sales30 : sales30 // ignore: cast_nullable_to_non_nullable
as double,sales90: null == sales90 ? _self.sales90 : sales90 // ignore: cast_nullable_to_non_nullable
as double,trendPct: freezed == trendPct ? _self.trendPct : trendPct // ignore: cast_nullable_to_non_nullable
as double?,monthlyTarget: freezed == monthlyTarget ? _self.monthlyTarget : monthlyTarget // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [FieldCustomerRow].
extension FieldCustomerRowPatterns on FieldCustomerRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FieldCustomerRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FieldCustomerRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FieldCustomerRow value)  $default,){
final _that = this;
switch (_that) {
case _FieldCustomerRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FieldCustomerRow value)?  $default,){
final _that = this;
switch (_that) {
case _FieldCustomerRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int marketId,  String name,  String? address,  String? branch,  String? type,  double? lat,  double? lon,  String? agentId,  String? agentName,  String priority,  String status,  String? lastVisitDate,  String? lastOrderDate,  double sales7,  double sales30,  double sales90,  double? trendPct,  double? monthlyTarget)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FieldCustomerRow() when $default != null:
return $default(_that.marketId,_that.name,_that.address,_that.branch,_that.type,_that.lat,_that.lon,_that.agentId,_that.agentName,_that.priority,_that.status,_that.lastVisitDate,_that.lastOrderDate,_that.sales7,_that.sales30,_that.sales90,_that.trendPct,_that.monthlyTarget);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int marketId,  String name,  String? address,  String? branch,  String? type,  double? lat,  double? lon,  String? agentId,  String? agentName,  String priority,  String status,  String? lastVisitDate,  String? lastOrderDate,  double sales7,  double sales30,  double sales90,  double? trendPct,  double? monthlyTarget)  $default,) {final _that = this;
switch (_that) {
case _FieldCustomerRow():
return $default(_that.marketId,_that.name,_that.address,_that.branch,_that.type,_that.lat,_that.lon,_that.agentId,_that.agentName,_that.priority,_that.status,_that.lastVisitDate,_that.lastOrderDate,_that.sales7,_that.sales30,_that.sales90,_that.trendPct,_that.monthlyTarget);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int marketId,  String name,  String? address,  String? branch,  String? type,  double? lat,  double? lon,  String? agentId,  String? agentName,  String priority,  String status,  String? lastVisitDate,  String? lastOrderDate,  double sales7,  double sales30,  double sales90,  double? trendPct,  double? monthlyTarget)?  $default,) {final _that = this;
switch (_that) {
case _FieldCustomerRow() when $default != null:
return $default(_that.marketId,_that.name,_that.address,_that.branch,_that.type,_that.lat,_that.lon,_that.agentId,_that.agentName,_that.priority,_that.status,_that.lastVisitDate,_that.lastOrderDate,_that.sales7,_that.sales30,_that.sales90,_that.trendPct,_that.monthlyTarget);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FieldCustomerRow implements FieldCustomerRow {
  const _FieldCustomerRow({required this.marketId, required this.name, this.address, this.branch, this.type, this.lat, this.lon, this.agentId, this.agentName, this.priority = 'Medium', this.status = 'Active', this.lastVisitDate, this.lastOrderDate, this.sales7 = 0, this.sales30 = 0, this.sales90 = 0, this.trendPct, this.monthlyTarget});
  factory _FieldCustomerRow.fromJson(Map<String, dynamic> json) => _$FieldCustomerRowFromJson(json);

@override final  int marketId;
@override final  String name;
@override final  String? address;
@override final  String? branch;
@override final  String? type;
@override final  double? lat;
@override final  double? lon;
@override final  String? agentId;
@override final  String? agentName;
/// Low | Medium | High | Urgent
@override@JsonKey() final  String priority;
/// Active | Problem | Inactive
@override@JsonKey() final  String status;
@override final  String? lastVisitDate;
@override final  String? lastOrderDate;
@override@JsonKey() final  double sales7;
@override@JsonKey() final  double sales30;
@override@JsonKey() final  double sales90;
/// Изменение продаж, % (уже ×100).
@override final  double? trendPct;
@override final  double? monthlyTarget;

/// Create a copy of FieldCustomerRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FieldCustomerRowCopyWith<_FieldCustomerRow> get copyWith => __$FieldCustomerRowCopyWithImpl<_FieldCustomerRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FieldCustomerRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FieldCustomerRow&&(identical(other.marketId, marketId) || other.marketId == marketId)&&(identical(other.name, name) || other.name == name)&&(identical(other.address, address) || other.address == address)&&(identical(other.branch, branch) || other.branch == branch)&&(identical(other.type, type) || other.type == type)&&(identical(other.lat, lat) || other.lat == lat)&&(identical(other.lon, lon) || other.lon == lon)&&(identical(other.agentId, agentId) || other.agentId == agentId)&&(identical(other.agentName, agentName) || other.agentName == agentName)&&(identical(other.priority, priority) || other.priority == priority)&&(identical(other.status, status) || other.status == status)&&(identical(other.lastVisitDate, lastVisitDate) || other.lastVisitDate == lastVisitDate)&&(identical(other.lastOrderDate, lastOrderDate) || other.lastOrderDate == lastOrderDate)&&(identical(other.sales7, sales7) || other.sales7 == sales7)&&(identical(other.sales30, sales30) || other.sales30 == sales30)&&(identical(other.sales90, sales90) || other.sales90 == sales90)&&(identical(other.trendPct, trendPct) || other.trendPct == trendPct)&&(identical(other.monthlyTarget, monthlyTarget) || other.monthlyTarget == monthlyTarget));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,marketId,name,address,branch,type,lat,lon,agentId,agentName,priority,status,lastVisitDate,lastOrderDate,sales7,sales30,sales90,trendPct,monthlyTarget);
}

@override
String toString() {
    return 'FieldCustomerRow(marketId: $marketId, name: $name, address: $address, branch: $branch, type: $type, lat: $lat, lon: $lon, agentId: $agentId, agentName: $agentName, priority: $priority, status: $status, lastVisitDate: $lastVisitDate, lastOrderDate: $lastOrderDate, sales7: $sales7, sales30: $sales30, sales90: $sales90, trendPct: $trendPct, monthlyTarget: $monthlyTarget)';
}


}

/// @nodoc
abstract mixin class _$FieldCustomerRowCopyWith<$Res> implements $FieldCustomerRowCopyWith<$Res> {
  factory _$FieldCustomerRowCopyWith(_FieldCustomerRow value, $Res Function(_FieldCustomerRow) _then) = __$FieldCustomerRowCopyWithImpl;
@override @useResult
$Res call({
 int marketId, String name, String? address, String? branch, String? type, double? lat, double? lon, String? agentId, String? agentName, String priority, String status, String? lastVisitDate, String? lastOrderDate, double sales7, double sales30, double sales90, double? trendPct, double? monthlyTarget
});




}
/// @nodoc
class __$FieldCustomerRowCopyWithImpl<$Res>
    implements _$FieldCustomerRowCopyWith<$Res> {
  __$FieldCustomerRowCopyWithImpl(this._self, this._then);

  final _FieldCustomerRow _self;
  final $Res Function(_FieldCustomerRow) _then;

/// Create a copy of FieldCustomerRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? marketId = null,Object? name = null,Object? address = freezed,Object? branch = freezed,Object? type = freezed,Object? lat = freezed,Object? lon = freezed,Object? agentId = freezed,Object? agentName = freezed,Object? priority = null,Object? status = null,Object? lastVisitDate = freezed,Object? lastOrderDate = freezed,Object? sales7 = null,Object? sales30 = null,Object? sales90 = null,Object? trendPct = freezed,Object? monthlyTarget = freezed,}) {
  return _then(_FieldCustomerRow(
marketId: null == marketId ? _self.marketId : marketId // ignore: cast_nullable_to_non_nullable
as int,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,address: freezed == address ? _self.address : address // ignore: cast_nullable_to_non_nullable
as String?,branch: freezed == branch ? _self.branch : branch // ignore: cast_nullable_to_non_nullable
as String?,type: freezed == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String?,lat: freezed == lat ? _self.lat : lat // ignore: cast_nullable_to_non_nullable
as double?,lon: freezed == lon ? _self.lon : lon // ignore: cast_nullable_to_non_nullable
as double?,agentId: freezed == agentId ? _self.agentId : agentId // ignore: cast_nullable_to_non_nullable
as String?,agentName: freezed == agentName ? _self.agentName : agentName // ignore: cast_nullable_to_non_nullable
as String?,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,lastVisitDate: freezed == lastVisitDate ? _self.lastVisitDate : lastVisitDate // ignore: cast_nullable_to_non_nullable
as String?,lastOrderDate: freezed == lastOrderDate ? _self.lastOrderDate : lastOrderDate // ignore: cast_nullable_to_non_nullable
as String?,sales7: null == sales7 ? _self.sales7 : sales7 // ignore: cast_nullable_to_non_nullable
as double,sales30: null == sales30 ? _self.sales30 : sales30 // ignore: cast_nullable_to_non_nullable
as double,sales90: null == sales90 ? _self.sales90 : sales90 // ignore: cast_nullable_to_non_nullable
as double,trendPct: freezed == trendPct ? _self.trendPct : trendPct // ignore: cast_nullable_to_non_nullable
as double?,monthlyTarget: freezed == monthlyTarget ? _self.monthlyTarget : monthlyTarget // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$CustomerMonth {

 int get year; int get month; double get sum; double get kg; int get orders;
/// Create a copy of CustomerMonth
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CustomerMonthCopyWith<CustomerMonth> get copyWith => _$CustomerMonthCopyWithImpl<CustomerMonth>(this as CustomerMonth, _$identity);

  /// Serializes this CustomerMonth to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CustomerMonth;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CustomerMonth&&(identical(other.year, _this.year) || other.year == _this.year)&&(identical(other.month, _this.month) || other.month == _this.month)&&(identical(other.sum, _this.sum) || other.sum == _this.sum)&&(identical(other.kg, _this.kg) || other.kg == _this.kg)&&(identical(other.orders, _this.orders) || other.orders == _this.orders));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CustomerMonth;
  return Object.hash(runtimeType,_this.year,_this.month,_this.sum,_this.kg,_this.orders);
}

@override
String toString() {
  final _this = this as CustomerMonth;
  return 'CustomerMonth(year: ${_this.year}, month: ${_this.month}, sum: ${_this.sum}, kg: ${_this.kg}, orders: ${_this.orders})';
}


}

/// @nodoc
abstract mixin class $CustomerMonthCopyWith<$Res>  {
  factory $CustomerMonthCopyWith(CustomerMonth value, $Res Function(CustomerMonth) _then) = _$CustomerMonthCopyWithImpl;
@useResult
$Res call({
 int year, int month, double sum, double kg, int orders
});




}
/// @nodoc
class _$CustomerMonthCopyWithImpl<$Res>
    implements $CustomerMonthCopyWith<$Res> {
  _$CustomerMonthCopyWithImpl(this._self, this._then);

  final CustomerMonth _self;
  final $Res Function(CustomerMonth) _then;

/// Create a copy of CustomerMonth
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? year = null,Object? month = null,Object? sum = null,Object? kg = null,Object? orders = null,}) {
  return _then(CustomerMonth(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,sum: null == sum ? _self.sum : sum // ignore: cast_nullable_to_non_nullable
as double,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [CustomerMonth].
extension CustomerMonthPatterns on CustomerMonth {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CustomerMonth value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CustomerMonth() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CustomerMonth value)  $default,){
final _that = this;
switch (_that) {
case _CustomerMonth():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CustomerMonth value)?  $default,){
final _that = this;
switch (_that) {
case _CustomerMonth() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int year,  int month,  double sum,  double kg,  int orders)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CustomerMonth() when $default != null:
return $default(_that.year,_that.month,_that.sum,_that.kg,_that.orders);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int year,  int month,  double sum,  double kg,  int orders)  $default,) {final _that = this;
switch (_that) {
case _CustomerMonth():
return $default(_that.year,_that.month,_that.sum,_that.kg,_that.orders);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int year,  int month,  double sum,  double kg,  int orders)?  $default,) {final _that = this;
switch (_that) {
case _CustomerMonth() when $default != null:
return $default(_that.year,_that.month,_that.sum,_that.kg,_that.orders);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CustomerMonth implements CustomerMonth {
  const _CustomerMonth({required this.year, required this.month, this.sum = 0, this.kg = 0, this.orders = 0});
  factory _CustomerMonth.fromJson(Map<String, dynamic> json) => _$CustomerMonthFromJson(json);

@override final  int year;
@override final  int month;
@override@JsonKey() final  double sum;
@override@JsonKey() final  double kg;
@override@JsonKey() final  int orders;

/// Create a copy of CustomerMonth
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CustomerMonthCopyWith<_CustomerMonth> get copyWith => __$CustomerMonthCopyWithImpl<_CustomerMonth>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CustomerMonthToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CustomerMonth&&(identical(other.year, year) || other.year == year)&&(identical(other.month, month) || other.month == month)&&(identical(other.sum, sum) || other.sum == sum)&&(identical(other.kg, kg) || other.kg == kg)&&(identical(other.orders, orders) || other.orders == orders));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,year,month,sum,kg,orders);
}

@override
String toString() {
    return 'CustomerMonth(year: $year, month: $month, sum: $sum, kg: $kg, orders: $orders)';
}


}

/// @nodoc
abstract mixin class _$CustomerMonthCopyWith<$Res> implements $CustomerMonthCopyWith<$Res> {
  factory _$CustomerMonthCopyWith(_CustomerMonth value, $Res Function(_CustomerMonth) _then) = __$CustomerMonthCopyWithImpl;
@override @useResult
$Res call({
 int year, int month, double sum, double kg, int orders
});




}
/// @nodoc
class __$CustomerMonthCopyWithImpl<$Res>
    implements _$CustomerMonthCopyWith<$Res> {
  __$CustomerMonthCopyWithImpl(this._self, this._then);

  final _CustomerMonth _self;
  final $Res Function(_CustomerMonth) _then;

/// Create a copy of CustomerMonth
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? year = null,Object? month = null,Object? sum = null,Object? kg = null,Object? orders = null,}) {
  return _then(_CustomerMonth(
year: null == year ? _self.year : year // ignore: cast_nullable_to_non_nullable
as int,month: null == month ? _self.month : month // ignore: cast_nullable_to_non_nullable
as int,sum: null == sum ? _self.sum : sum // ignore: cast_nullable_to_non_nullable
as double,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$CustomerOrder {

 int get id; String get date; String? get acceptedDate; String get status; String? get agent; double get sum; double get kg;
/// Create a copy of CustomerOrder
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CustomerOrderCopyWith<CustomerOrder> get copyWith => _$CustomerOrderCopyWithImpl<CustomerOrder>(this as CustomerOrder, _$identity);

  /// Serializes this CustomerOrder to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CustomerOrder;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CustomerOrder&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.acceptedDate, _this.acceptedDate) || other.acceptedDate == _this.acceptedDate)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.agent, _this.agent) || other.agent == _this.agent)&&(identical(other.sum, _this.sum) || other.sum == _this.sum)&&(identical(other.kg, _this.kg) || other.kg == _this.kg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CustomerOrder;
  return Object.hash(runtimeType,_this.id,_this.date,_this.acceptedDate,_this.status,_this.agent,_this.sum,_this.kg);
}

@override
String toString() {
  final _this = this as CustomerOrder;
  return 'CustomerOrder(id: ${_this.id}, date: ${_this.date}, acceptedDate: ${_this.acceptedDate}, status: ${_this.status}, agent: ${_this.agent}, sum: ${_this.sum}, kg: ${_this.kg})';
}


}

/// @nodoc
abstract mixin class $CustomerOrderCopyWith<$Res>  {
  factory $CustomerOrderCopyWith(CustomerOrder value, $Res Function(CustomerOrder) _then) = _$CustomerOrderCopyWithImpl;
@useResult
$Res call({
 int id, String date, String? acceptedDate, String status, String? agent, double sum, double kg
});




}
/// @nodoc
class _$CustomerOrderCopyWithImpl<$Res>
    implements $CustomerOrderCopyWith<$Res> {
  _$CustomerOrderCopyWithImpl(this._self, this._then);

  final CustomerOrder _self;
  final $Res Function(CustomerOrder) _then;

/// Create a copy of CustomerOrder
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? date = null,Object? acceptedDate = freezed,Object? status = null,Object? agent = freezed,Object? sum = null,Object? kg = null,}) {
  return _then(CustomerOrder(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as int,date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,acceptedDate: freezed == acceptedDate ? _self.acceptedDate : acceptedDate // ignore: cast_nullable_to_non_nullable
as String?,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,agent: freezed == agent ? _self.agent : agent // ignore: cast_nullable_to_non_nullable
as String?,sum: null == sum ? _self.sum : sum // ignore: cast_nullable_to_non_nullable
as double,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [CustomerOrder].
extension CustomerOrderPatterns on CustomerOrder {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CustomerOrder value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CustomerOrder() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CustomerOrder value)  $default,){
final _that = this;
switch (_that) {
case _CustomerOrder():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CustomerOrder value)?  $default,){
final _that = this;
switch (_that) {
case _CustomerOrder() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int id,  String date,  String? acceptedDate,  String status,  String? agent,  double sum,  double kg)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CustomerOrder() when $default != null:
return $default(_that.id,_that.date,_that.acceptedDate,_that.status,_that.agent,_that.sum,_that.kg);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int id,  String date,  String? acceptedDate,  String status,  String? agent,  double sum,  double kg)  $default,) {final _that = this;
switch (_that) {
case _CustomerOrder():
return $default(_that.id,_that.date,_that.acceptedDate,_that.status,_that.agent,_that.sum,_that.kg);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int id,  String date,  String? acceptedDate,  String status,  String? agent,  double sum,  double kg)?  $default,) {final _that = this;
switch (_that) {
case _CustomerOrder() when $default != null:
return $default(_that.id,_that.date,_that.acceptedDate,_that.status,_that.agent,_that.sum,_that.kg);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CustomerOrder implements CustomerOrder {
  const _CustomerOrder({required this.id, required this.date, this.acceptedDate, this.status = '', this.agent, this.sum = 0, this.kg = 0});
  factory _CustomerOrder.fromJson(Map<String, dynamic> json) => _$CustomerOrderFromJson(json);

@override final  int id;
@override final  String date;
@override final  String? acceptedDate;
@override@JsonKey() final  String status;
@override final  String? agent;
@override@JsonKey() final  double sum;
@override@JsonKey() final  double kg;

/// Create a copy of CustomerOrder
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CustomerOrderCopyWith<_CustomerOrder> get copyWith => __$CustomerOrderCopyWithImpl<_CustomerOrder>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CustomerOrderToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CustomerOrder&&(identical(other.id, id) || other.id == id)&&(identical(other.date, date) || other.date == date)&&(identical(other.acceptedDate, acceptedDate) || other.acceptedDate == acceptedDate)&&(identical(other.status, status) || other.status == status)&&(identical(other.agent, agent) || other.agent == agent)&&(identical(other.sum, sum) || other.sum == sum)&&(identical(other.kg, kg) || other.kg == kg));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,date,acceptedDate,status,agent,sum,kg);
}

@override
String toString() {
    return 'CustomerOrder(id: $id, date: $date, acceptedDate: $acceptedDate, status: $status, agent: $agent, sum: $sum, kg: $kg)';
}


}

/// @nodoc
abstract mixin class _$CustomerOrderCopyWith<$Res> implements $CustomerOrderCopyWith<$Res> {
  factory _$CustomerOrderCopyWith(_CustomerOrder value, $Res Function(_CustomerOrder) _then) = __$CustomerOrderCopyWithImpl;
@override @useResult
$Res call({
 int id, String date, String? acceptedDate, String status, String? agent, double sum, double kg
});




}
/// @nodoc
class __$CustomerOrderCopyWithImpl<$Res>
    implements _$CustomerOrderCopyWith<$Res> {
  __$CustomerOrderCopyWithImpl(this._self, this._then);

  final _CustomerOrder _self;
  final $Res Function(_CustomerOrder) _then;

/// Create a copy of CustomerOrder
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? date = null,Object? acceptedDate = freezed,Object? status = null,Object? agent = freezed,Object? sum = null,Object? kg = null,}) {
  return _then(_CustomerOrder(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as int,date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,acceptedDate: freezed == acceptedDate ? _self.acceptedDate : acceptedDate // ignore: cast_nullable_to_non_nullable
as String?,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,agent: freezed == agent ? _self.agent : agent // ignore: cast_nullable_to_non_nullable
as String?,sum: null == sum ? _self.sum : sum // ignore: cast_nullable_to_non_nullable
as double,kg: null == kg ? _self.kg : kg // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$CustomerVisit {

/// Linko | SalesBase
 String get source; String get at; String? get agent; String get status; String? get result; String? get comment;
/// Create a copy of CustomerVisit
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CustomerVisitCopyWith<CustomerVisit> get copyWith => _$CustomerVisitCopyWithImpl<CustomerVisit>(this as CustomerVisit, _$identity);

  /// Serializes this CustomerVisit to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CustomerVisit;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CustomerVisit&&(identical(other.source, _this.source) || other.source == _this.source)&&(identical(other.at, _this.at) || other.at == _this.at)&&(identical(other.agent, _this.agent) || other.agent == _this.agent)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.result, _this.result) || other.result == _this.result)&&(identical(other.comment, _this.comment) || other.comment == _this.comment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CustomerVisit;
  return Object.hash(runtimeType,_this.source,_this.at,_this.agent,_this.status,_this.result,_this.comment);
}

@override
String toString() {
  final _this = this as CustomerVisit;
  return 'CustomerVisit(source: ${_this.source}, at: ${_this.at}, agent: ${_this.agent}, status: ${_this.status}, result: ${_this.result}, comment: ${_this.comment})';
}


}

/// @nodoc
abstract mixin class $CustomerVisitCopyWith<$Res>  {
  factory $CustomerVisitCopyWith(CustomerVisit value, $Res Function(CustomerVisit) _then) = _$CustomerVisitCopyWithImpl;
@useResult
$Res call({
 String source, String at, String? agent, String status, String? result, String? comment
});




}
/// @nodoc
class _$CustomerVisitCopyWithImpl<$Res>
    implements $CustomerVisitCopyWith<$Res> {
  _$CustomerVisitCopyWithImpl(this._self, this._then);

  final CustomerVisit _self;
  final $Res Function(CustomerVisit) _then;

/// Create a copy of CustomerVisit
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? source = null,Object? at = null,Object? agent = freezed,Object? status = null,Object? result = freezed,Object? comment = freezed,}) {
  return _then(CustomerVisit(
source: null == source ? _self.source : source // ignore: cast_nullable_to_non_nullable
as String,at: null == at ? _self.at : at // ignore: cast_nullable_to_non_nullable
as String,agent: freezed == agent ? _self.agent : agent // ignore: cast_nullable_to_non_nullable
as String?,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,result: freezed == result ? _self.result : result // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [CustomerVisit].
extension CustomerVisitPatterns on CustomerVisit {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CustomerVisit value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CustomerVisit() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CustomerVisit value)  $default,){
final _that = this;
switch (_that) {
case _CustomerVisit():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CustomerVisit value)?  $default,){
final _that = this;
switch (_that) {
case _CustomerVisit() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String source,  String at,  String? agent,  String status,  String? result,  String? comment)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CustomerVisit() when $default != null:
return $default(_that.source,_that.at,_that.agent,_that.status,_that.result,_that.comment);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String source,  String at,  String? agent,  String status,  String? result,  String? comment)  $default,) {final _that = this;
switch (_that) {
case _CustomerVisit():
return $default(_that.source,_that.at,_that.agent,_that.status,_that.result,_that.comment);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String source,  String at,  String? agent,  String status,  String? result,  String? comment)?  $default,) {final _that = this;
switch (_that) {
case _CustomerVisit() when $default != null:
return $default(_that.source,_that.at,_that.agent,_that.status,_that.result,_that.comment);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CustomerVisit implements CustomerVisit {
  const _CustomerVisit({this.source = 'Linko', required this.at, this.agent, this.status = '', this.result, this.comment});
  factory _CustomerVisit.fromJson(Map<String, dynamic> json) => _$CustomerVisitFromJson(json);

/// Linko | SalesBase
@override@JsonKey() final  String source;
@override final  String at;
@override final  String? agent;
@override@JsonKey() final  String status;
@override final  String? result;
@override final  String? comment;

/// Create a copy of CustomerVisit
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CustomerVisitCopyWith<_CustomerVisit> get copyWith => __$CustomerVisitCopyWithImpl<_CustomerVisit>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CustomerVisitToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CustomerVisit&&(identical(other.source, source) || other.source == source)&&(identical(other.at, at) || other.at == at)&&(identical(other.agent, agent) || other.agent == agent)&&(identical(other.status, status) || other.status == status)&&(identical(other.result, result) || other.result == result)&&(identical(other.comment, comment) || other.comment == comment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,source,at,agent,status,result,comment);
}

@override
String toString() {
    return 'CustomerVisit(source: $source, at: $at, agent: $agent, status: $status, result: $result, comment: $comment)';
}


}

/// @nodoc
abstract mixin class _$CustomerVisitCopyWith<$Res> implements $CustomerVisitCopyWith<$Res> {
  factory _$CustomerVisitCopyWith(_CustomerVisit value, $Res Function(_CustomerVisit) _then) = __$CustomerVisitCopyWithImpl;
@override @useResult
$Res call({
 String source, String at, String? agent, String status, String? result, String? comment
});




}
/// @nodoc
class __$CustomerVisitCopyWithImpl<$Res>
    implements _$CustomerVisitCopyWith<$Res> {
  __$CustomerVisitCopyWithImpl(this._self, this._then);

  final _CustomerVisit _self;
  final $Res Function(_CustomerVisit) _then;

/// Create a copy of CustomerVisit
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? source = null,Object? at = null,Object? agent = freezed,Object? status = null,Object? result = freezed,Object? comment = freezed,}) {
  return _then(_CustomerVisit(
source: null == source ? _self.source : source // ignore: cast_nullable_to_non_nullable
as String,at: null == at ? _self.at : at // ignore: cast_nullable_to_non_nullable
as String,agent: freezed == agent ? _self.agent : agent // ignore: cast_nullable_to_non_nullable
as String?,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,result: freezed == result ? _self.result : result // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$FieldCustomerCard {

 FieldCustomerRow get customer; int? get linkoResponsibleId; String? get linkoResponsibleName; String? get supervisorName; String? get contactPerson; String? get phone; String? get note; double get monthToDate; int get orders30; int get visits30; List<CustomerMonth> get months; List<CustomerOrder> get orders; List<CustomerVisit> get visits;/// Статус, приоритет и план меняют СВ/РМ; контакты и заметку — и агент точки.
 bool get canEdit; bool get canChangeAgent;
/// Create a copy of FieldCustomerCard
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FieldCustomerCardCopyWith<FieldCustomerCard> get copyWith => _$FieldCustomerCardCopyWithImpl<FieldCustomerCard>(this as FieldCustomerCard, _$identity);

  /// Serializes this FieldCustomerCard to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FieldCustomerCard;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FieldCustomerCard&&(identical(other.customer, _this.customer) || other.customer == _this.customer)&&(identical(other.linkoResponsibleId, _this.linkoResponsibleId) || other.linkoResponsibleId == _this.linkoResponsibleId)&&(identical(other.linkoResponsibleName, _this.linkoResponsibleName) || other.linkoResponsibleName == _this.linkoResponsibleName)&&(identical(other.supervisorName, _this.supervisorName) || other.supervisorName == _this.supervisorName)&&(identical(other.contactPerson, _this.contactPerson) || other.contactPerson == _this.contactPerson)&&(identical(other.phone, _this.phone) || other.phone == _this.phone)&&(identical(other.note, _this.note) || other.note == _this.note)&&(identical(other.monthToDate, _this.monthToDate) || other.monthToDate == _this.monthToDate)&&(identical(other.orders30, _this.orders30) || other.orders30 == _this.orders30)&&(identical(other.visits30, _this.visits30) || other.visits30 == _this.visits30)&&const DeepCollectionEquality().equals(other.months, _this.months)&&const DeepCollectionEquality().equals(other.orders, _this.orders)&&const DeepCollectionEquality().equals(other.visits, _this.visits)&&(identical(other.canEdit, _this.canEdit) || other.canEdit == _this.canEdit)&&(identical(other.canChangeAgent, _this.canChangeAgent) || other.canChangeAgent == _this.canChangeAgent));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FieldCustomerCard;
  return Object.hash(runtimeType,_this.customer,_this.linkoResponsibleId,_this.linkoResponsibleName,_this.supervisorName,_this.contactPerson,_this.phone,_this.note,_this.monthToDate,_this.orders30,_this.visits30,const DeepCollectionEquality().hash(_this.months),const DeepCollectionEquality().hash(_this.orders),const DeepCollectionEquality().hash(_this.visits),_this.canEdit,_this.canChangeAgent);
}

@override
String toString() {
  final _this = this as FieldCustomerCard;
  return 'FieldCustomerCard(customer: ${_this.customer}, linkoResponsibleId: ${_this.linkoResponsibleId}, linkoResponsibleName: ${_this.linkoResponsibleName}, supervisorName: ${_this.supervisorName}, contactPerson: ${_this.contactPerson}, phone: ${_this.phone}, note: ${_this.note}, monthToDate: ${_this.monthToDate}, orders30: ${_this.orders30}, visits30: ${_this.visits30}, months: ${_this.months}, orders: ${_this.orders}, visits: ${_this.visits}, canEdit: ${_this.canEdit}, canChangeAgent: ${_this.canChangeAgent})';
}


}

/// @nodoc
abstract mixin class $FieldCustomerCardCopyWith<$Res>  {
  factory $FieldCustomerCardCopyWith(FieldCustomerCard value, $Res Function(FieldCustomerCard) _then) = _$FieldCustomerCardCopyWithImpl;
@useResult
$Res call({
 FieldCustomerRow customer, int? linkoResponsibleId, String? linkoResponsibleName, String? supervisorName, String? contactPerson, String? phone, String? note, double monthToDate, int orders30, int visits30, List<CustomerMonth> months, List<CustomerOrder> orders, List<CustomerVisit> visits, bool canEdit, bool canChangeAgent
});


$FieldCustomerRowCopyWith<$Res> get customer;

}
/// @nodoc
class _$FieldCustomerCardCopyWithImpl<$Res>
    implements $FieldCustomerCardCopyWith<$Res> {
  _$FieldCustomerCardCopyWithImpl(this._self, this._then);

  final FieldCustomerCard _self;
  final $Res Function(FieldCustomerCard) _then;

/// Create a copy of FieldCustomerCard
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? customer = null,Object? linkoResponsibleId = freezed,Object? linkoResponsibleName = freezed,Object? supervisorName = freezed,Object? contactPerson = freezed,Object? phone = freezed,Object? note = freezed,Object? monthToDate = null,Object? orders30 = null,Object? visits30 = null,Object? months = null,Object? orders = null,Object? visits = null,Object? canEdit = null,Object? canChangeAgent = null,}) {
  return _then(FieldCustomerCard(
customer: null == customer ? _self.customer : customer // ignore: cast_nullable_to_non_nullable
as FieldCustomerRow,linkoResponsibleId: freezed == linkoResponsibleId ? _self.linkoResponsibleId : linkoResponsibleId // ignore: cast_nullable_to_non_nullable
as int?,linkoResponsibleName: freezed == linkoResponsibleName ? _self.linkoResponsibleName : linkoResponsibleName // ignore: cast_nullable_to_non_nullable
as String?,supervisorName: freezed == supervisorName ? _self.supervisorName : supervisorName // ignore: cast_nullable_to_non_nullable
as String?,contactPerson: freezed == contactPerson ? _self.contactPerson : contactPerson // ignore: cast_nullable_to_non_nullable
as String?,phone: freezed == phone ? _self.phone : phone // ignore: cast_nullable_to_non_nullable
as String?,note: freezed == note ? _self.note : note // ignore: cast_nullable_to_non_nullable
as String?,monthToDate: null == monthToDate ? _self.monthToDate : monthToDate // ignore: cast_nullable_to_non_nullable
as double,orders30: null == orders30 ? _self.orders30 : orders30 // ignore: cast_nullable_to_non_nullable
as int,visits30: null == visits30 ? _self.visits30 : visits30 // ignore: cast_nullable_to_non_nullable
as int,months: null == months ? _self.months : months // ignore: cast_nullable_to_non_nullable
as List<CustomerMonth>,orders: null == orders ? _self.orders : orders // ignore: cast_nullable_to_non_nullable
as List<CustomerOrder>,visits: null == visits ? _self.visits : visits // ignore: cast_nullable_to_non_nullable
as List<CustomerVisit>,canEdit: null == canEdit ? _self.canEdit : canEdit // ignore: cast_nullable_to_non_nullable
as bool,canChangeAgent: null == canChangeAgent ? _self.canChangeAgent : canChangeAgent // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}
/// Create a copy of FieldCustomerCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FieldCustomerRowCopyWith<$Res> get customer {
  
  return $FieldCustomerRowCopyWith<$Res>(_self.customer, (value) {
    return _then(_self.copyWith(customer: value));
  });
}
}


/// Adds pattern-matching-related methods to [FieldCustomerCard].
extension FieldCustomerCardPatterns on FieldCustomerCard {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FieldCustomerCard value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FieldCustomerCard() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FieldCustomerCard value)  $default,){
final _that = this;
switch (_that) {
case _FieldCustomerCard():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FieldCustomerCard value)?  $default,){
final _that = this;
switch (_that) {
case _FieldCustomerCard() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FieldCustomerRow customer,  int? linkoResponsibleId,  String? linkoResponsibleName,  String? supervisorName,  String? contactPerson,  String? phone,  String? note,  double monthToDate,  int orders30,  int visits30,  List<CustomerMonth> months,  List<CustomerOrder> orders,  List<CustomerVisit> visits,  bool canEdit,  bool canChangeAgent)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FieldCustomerCard() when $default != null:
return $default(_that.customer,_that.linkoResponsibleId,_that.linkoResponsibleName,_that.supervisorName,_that.contactPerson,_that.phone,_that.note,_that.monthToDate,_that.orders30,_that.visits30,_that.months,_that.orders,_that.visits,_that.canEdit,_that.canChangeAgent);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FieldCustomerRow customer,  int? linkoResponsibleId,  String? linkoResponsibleName,  String? supervisorName,  String? contactPerson,  String? phone,  String? note,  double monthToDate,  int orders30,  int visits30,  List<CustomerMonth> months,  List<CustomerOrder> orders,  List<CustomerVisit> visits,  bool canEdit,  bool canChangeAgent)  $default,) {final _that = this;
switch (_that) {
case _FieldCustomerCard():
return $default(_that.customer,_that.linkoResponsibleId,_that.linkoResponsibleName,_that.supervisorName,_that.contactPerson,_that.phone,_that.note,_that.monthToDate,_that.orders30,_that.visits30,_that.months,_that.orders,_that.visits,_that.canEdit,_that.canChangeAgent);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FieldCustomerRow customer,  int? linkoResponsibleId,  String? linkoResponsibleName,  String? supervisorName,  String? contactPerson,  String? phone,  String? note,  double monthToDate,  int orders30,  int visits30,  List<CustomerMonth> months,  List<CustomerOrder> orders,  List<CustomerVisit> visits,  bool canEdit,  bool canChangeAgent)?  $default,) {final _that = this;
switch (_that) {
case _FieldCustomerCard() when $default != null:
return $default(_that.customer,_that.linkoResponsibleId,_that.linkoResponsibleName,_that.supervisorName,_that.contactPerson,_that.phone,_that.note,_that.monthToDate,_that.orders30,_that.visits30,_that.months,_that.orders,_that.visits,_that.canEdit,_that.canChangeAgent);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FieldCustomerCard implements FieldCustomerCard {
  const _FieldCustomerCard({required this.customer, this.linkoResponsibleId, this.linkoResponsibleName, this.supervisorName, this.contactPerson, this.phone, this.note, this.monthToDate = 0, this.orders30 = 0, this.visits30 = 0,  List<CustomerMonth> months = const <CustomerMonth>[],  List<CustomerOrder> orders = const <CustomerOrder>[],  List<CustomerVisit> visits = const <CustomerVisit>[], this.canEdit = false, this.canChangeAgent = false}): _months = months,_orders = orders,_visits = visits;
  factory _FieldCustomerCard.fromJson(Map<String, dynamic> json) => _$FieldCustomerCardFromJson(json);

@override final  FieldCustomerRow customer;
@override final  int? linkoResponsibleId;
@override final  String? linkoResponsibleName;
@override final  String? supervisorName;
@override final  String? contactPerson;
@override final  String? phone;
@override final  String? note;
@override@JsonKey() final  double monthToDate;
@override@JsonKey() final  int orders30;
@override@JsonKey() final  int visits30;
 final  List<CustomerMonth> _months;
@override@JsonKey() List<CustomerMonth> get months {
  if (_months is EqualUnmodifiableListView) return _months;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_months);
}

 final  List<CustomerOrder> _orders;
@override@JsonKey() List<CustomerOrder> get orders {
  if (_orders is EqualUnmodifiableListView) return _orders;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_orders);
}

 final  List<CustomerVisit> _visits;
@override@JsonKey() List<CustomerVisit> get visits {
  if (_visits is EqualUnmodifiableListView) return _visits;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_visits);
}

/// Статус, приоритет и план меняют СВ/РМ; контакты и заметку — и агент точки.
@override@JsonKey() final  bool canEdit;
@override@JsonKey() final  bool canChangeAgent;

/// Create a copy of FieldCustomerCard
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FieldCustomerCardCopyWith<_FieldCustomerCard> get copyWith => __$FieldCustomerCardCopyWithImpl<_FieldCustomerCard>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FieldCustomerCardToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FieldCustomerCard&&(identical(other.customer, customer) || other.customer == customer)&&(identical(other.linkoResponsibleId, linkoResponsibleId) || other.linkoResponsibleId == linkoResponsibleId)&&(identical(other.linkoResponsibleName, linkoResponsibleName) || other.linkoResponsibleName == linkoResponsibleName)&&(identical(other.supervisorName, supervisorName) || other.supervisorName == supervisorName)&&(identical(other.contactPerson, contactPerson) || other.contactPerson == contactPerson)&&(identical(other.phone, phone) || other.phone == phone)&&(identical(other.note, note) || other.note == note)&&(identical(other.monthToDate, monthToDate) || other.monthToDate == monthToDate)&&(identical(other.orders30, orders30) || other.orders30 == orders30)&&(identical(other.visits30, visits30) || other.visits30 == visits30)&&const DeepCollectionEquality().equals(other.months, _months)&&const DeepCollectionEquality().equals(other.orders, _orders)&&const DeepCollectionEquality().equals(other.visits, _visits)&&(identical(other.canEdit, canEdit) || other.canEdit == canEdit)&&(identical(other.canChangeAgent, canChangeAgent) || other.canChangeAgent == canChangeAgent));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,customer,linkoResponsibleId,linkoResponsibleName,supervisorName,contactPerson,phone,note,monthToDate,orders30,visits30,const DeepCollectionEquality().hash(_months),const DeepCollectionEquality().hash(_orders),const DeepCollectionEquality().hash(_visits),canEdit,canChangeAgent);
}

@override
String toString() {
    return 'FieldCustomerCard(customer: $customer, linkoResponsibleId: $linkoResponsibleId, linkoResponsibleName: $linkoResponsibleName, supervisorName: $supervisorName, contactPerson: $contactPerson, phone: $phone, note: $note, monthToDate: $monthToDate, orders30: $orders30, visits30: $visits30, months: $months, orders: $orders, visits: $visits, canEdit: $canEdit, canChangeAgent: $canChangeAgent)';
}


}

/// @nodoc
abstract mixin class _$FieldCustomerCardCopyWith<$Res> implements $FieldCustomerCardCopyWith<$Res> {
  factory _$FieldCustomerCardCopyWith(_FieldCustomerCard value, $Res Function(_FieldCustomerCard) _then) = __$FieldCustomerCardCopyWithImpl;
@override @useResult
$Res call({
 FieldCustomerRow customer, int? linkoResponsibleId, String? linkoResponsibleName, String? supervisorName, String? contactPerson, String? phone, String? note, double monthToDate, int orders30, int visits30, List<CustomerMonth> months, List<CustomerOrder> orders, List<CustomerVisit> visits, bool canEdit, bool canChangeAgent
});


@override $FieldCustomerRowCopyWith<$Res> get customer;

}
/// @nodoc
class __$FieldCustomerCardCopyWithImpl<$Res>
    implements _$FieldCustomerCardCopyWith<$Res> {
  __$FieldCustomerCardCopyWithImpl(this._self, this._then);

  final _FieldCustomerCard _self;
  final $Res Function(_FieldCustomerCard) _then;

/// Create a copy of FieldCustomerCard
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? customer = null,Object? linkoResponsibleId = freezed,Object? linkoResponsibleName = freezed,Object? supervisorName = freezed,Object? contactPerson = freezed,Object? phone = freezed,Object? note = freezed,Object? monthToDate = null,Object? orders30 = null,Object? visits30 = null,Object? months = null,Object? orders = null,Object? visits = null,Object? canEdit = null,Object? canChangeAgent = null,}) {
  return _then(_FieldCustomerCard(
customer: null == customer ? _self.customer : customer // ignore: cast_nullable_to_non_nullable
as FieldCustomerRow,linkoResponsibleId: freezed == linkoResponsibleId ? _self.linkoResponsibleId : linkoResponsibleId // ignore: cast_nullable_to_non_nullable
as int?,linkoResponsibleName: freezed == linkoResponsibleName ? _self.linkoResponsibleName : linkoResponsibleName // ignore: cast_nullable_to_non_nullable
as String?,supervisorName: freezed == supervisorName ? _self.supervisorName : supervisorName // ignore: cast_nullable_to_non_nullable
as String?,contactPerson: freezed == contactPerson ? _self.contactPerson : contactPerson // ignore: cast_nullable_to_non_nullable
as String?,phone: freezed == phone ? _self.phone : phone // ignore: cast_nullable_to_non_nullable
as String?,note: freezed == note ? _self.note : note // ignore: cast_nullable_to_non_nullable
as String?,monthToDate: null == monthToDate ? _self.monthToDate : monthToDate // ignore: cast_nullable_to_non_nullable
as double,orders30: null == orders30 ? _self.orders30 : orders30 // ignore: cast_nullable_to_non_nullable
as int,visits30: null == visits30 ? _self.visits30 : visits30 // ignore: cast_nullable_to_non_nullable
as int,months: null == months ? _self._months : months // ignore: cast_nullable_to_non_nullable
as List<CustomerMonth>,orders: null == orders ? _self._orders : orders // ignore: cast_nullable_to_non_nullable
as List<CustomerOrder>,visits: null == visits ? _self._visits : visits // ignore: cast_nullable_to_non_nullable
as List<CustomerVisit>,canEdit: null == canEdit ? _self.canEdit : canEdit // ignore: cast_nullable_to_non_nullable
as bool,canChangeAgent: null == canChangeAgent ? _self.canChangeAgent : canChangeAgent // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

/// Create a copy of FieldCustomerCard
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FieldCustomerRowCopyWith<$Res> get customer {
  
  return $FieldCustomerRowCopyWith<$Res>(_self.customer, (value) {
    return _then(_self.copyWith(customer: value));
  });
}
}


/// @nodoc
mixin _$AuditEntry {

 int get id; String get timestamp; String get actor; String get action; String get data;
/// Create a copy of AuditEntry
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AuditEntryCopyWith<AuditEntry> get copyWith => _$AuditEntryCopyWithImpl<AuditEntry>(this as AuditEntry, _$identity);

  /// Serializes this AuditEntry to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AuditEntry;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AuditEntry&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.timestamp, _this.timestamp) || other.timestamp == _this.timestamp)&&(identical(other.actor, _this.actor) || other.actor == _this.actor)&&(identical(other.action, _this.action) || other.action == _this.action)&&(identical(other.data, _this.data) || other.data == _this.data));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AuditEntry;
  return Object.hash(runtimeType,_this.id,_this.timestamp,_this.actor,_this.action,_this.data);
}

@override
String toString() {
  final _this = this as AuditEntry;
  return 'AuditEntry(id: ${_this.id}, timestamp: ${_this.timestamp}, actor: ${_this.actor}, action: ${_this.action}, data: ${_this.data})';
}


}

/// @nodoc
abstract mixin class $AuditEntryCopyWith<$Res>  {
  factory $AuditEntryCopyWith(AuditEntry value, $Res Function(AuditEntry) _then) = _$AuditEntryCopyWithImpl;
@useResult
$Res call({
 int id, String timestamp, String actor, String action, String data
});




}
/// @nodoc
class _$AuditEntryCopyWithImpl<$Res>
    implements $AuditEntryCopyWith<$Res> {
  _$AuditEntryCopyWithImpl(this._self, this._then);

  final AuditEntry _self;
  final $Res Function(AuditEntry) _then;

/// Create a copy of AuditEntry
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? timestamp = null,Object? actor = null,Object? action = null,Object? data = null,}) {
  return _then(AuditEntry(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as int,timestamp: null == timestamp ? _self.timestamp : timestamp // ignore: cast_nullable_to_non_nullable
as String,actor: null == actor ? _self.actor : actor // ignore: cast_nullable_to_non_nullable
as String,action: null == action ? _self.action : action // ignore: cast_nullable_to_non_nullable
as String,data: null == data ? _self.data : data // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [AuditEntry].
extension AuditEntryPatterns on AuditEntry {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AuditEntry value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AuditEntry() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AuditEntry value)  $default,){
final _that = this;
switch (_that) {
case _AuditEntry():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AuditEntry value)?  $default,){
final _that = this;
switch (_that) {
case _AuditEntry() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int id,  String timestamp,  String actor,  String action,  String data)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AuditEntry() when $default != null:
return $default(_that.id,_that.timestamp,_that.actor,_that.action,_that.data);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int id,  String timestamp,  String actor,  String action,  String data)  $default,) {final _that = this;
switch (_that) {
case _AuditEntry():
return $default(_that.id,_that.timestamp,_that.actor,_that.action,_that.data);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int id,  String timestamp,  String actor,  String action,  String data)?  $default,) {final _that = this;
switch (_that) {
case _AuditEntry() when $default != null:
return $default(_that.id,_that.timestamp,_that.actor,_that.action,_that.data);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AuditEntry implements AuditEntry {
  const _AuditEntry({required this.id, required this.timestamp, this.actor = '', required this.action, this.data = ''});
  factory _AuditEntry.fromJson(Map<String, dynamic> json) => _$AuditEntryFromJson(json);

@override final  int id;
@override final  String timestamp;
@override@JsonKey() final  String actor;
@override final  String action;
@override@JsonKey() final  String data;

/// Create a copy of AuditEntry
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AuditEntryCopyWith<_AuditEntry> get copyWith => __$AuditEntryCopyWithImpl<_AuditEntry>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AuditEntryToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AuditEntry&&(identical(other.id, id) || other.id == id)&&(identical(other.timestamp, timestamp) || other.timestamp == timestamp)&&(identical(other.actor, actor) || other.actor == actor)&&(identical(other.action, action) || other.action == action)&&(identical(other.data, data) || other.data == data));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,timestamp,actor,action,data);
}

@override
String toString() {
    return 'AuditEntry(id: $id, timestamp: $timestamp, actor: $actor, action: $action, data: $data)';
}


}

/// @nodoc
abstract mixin class _$AuditEntryCopyWith<$Res> implements $AuditEntryCopyWith<$Res> {
  factory _$AuditEntryCopyWith(_AuditEntry value, $Res Function(_AuditEntry) _then) = __$AuditEntryCopyWithImpl;
@override @useResult
$Res call({
 int id, String timestamp, String actor, String action, String data
});




}
/// @nodoc
class __$AuditEntryCopyWithImpl<$Res>
    implements _$AuditEntryCopyWith<$Res> {
  __$AuditEntryCopyWithImpl(this._self, this._then);

  final _AuditEntry _self;
  final $Res Function(_AuditEntry) _then;

/// Create a copy of AuditEntry
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? timestamp = null,Object? actor = null,Object? action = null,Object? data = null,}) {
  return _then(_AuditEntry(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as int,timestamp: null == timestamp ? _self.timestamp : timestamp // ignore: cast_nullable_to_non_nullable
as String,actor: null == actor ? _self.actor : actor // ignore: cast_nullable_to_non_nullable
as String,action: null == action ? _self.action : action // ignore: cast_nullable_to_non_nullable
as String,data: null == data ? _self.data : data // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}

// dart format on
