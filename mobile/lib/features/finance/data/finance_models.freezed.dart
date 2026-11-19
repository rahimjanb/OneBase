// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'finance_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$AmountRow {

 String get key; String get title; double get amount; double? get previous; double? get quantity; String? get currency; double? get currencyAmount;
/// Create a copy of AmountRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AmountRowCopyWith<AmountRow> get copyWith => _$AmountRowCopyWithImpl<AmountRow>(this as AmountRow, _$identity);

  /// Serializes this AmountRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AmountRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AmountRow&&(identical(other.key, _this.key) || other.key == _this.key)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.amount, _this.amount) || other.amount == _this.amount)&&(identical(other.previous, _this.previous) || other.previous == _this.previous)&&(identical(other.quantity, _this.quantity) || other.quantity == _this.quantity)&&(identical(other.currency, _this.currency) || other.currency == _this.currency)&&(identical(other.currencyAmount, _this.currencyAmount) || other.currencyAmount == _this.currencyAmount));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AmountRow;
  return Object.hash(runtimeType,_this.key,_this.title,_this.amount,_this.previous,_this.quantity,_this.currency,_this.currencyAmount);
}

@override
String toString() {
  final _this = this as AmountRow;
  return 'AmountRow(key: ${_this.key}, title: ${_this.title}, amount: ${_this.amount}, previous: ${_this.previous}, quantity: ${_this.quantity}, currency: ${_this.currency}, currencyAmount: ${_this.currencyAmount})';
}


}

/// @nodoc
abstract mixin class $AmountRowCopyWith<$Res>  {
  factory $AmountRowCopyWith(AmountRow value, $Res Function(AmountRow) _then) = _$AmountRowCopyWithImpl;
@useResult
$Res call({
 String key, String title, double amount, double? previous, double? quantity, String? currency, double? currencyAmount
});




}
/// @nodoc
class _$AmountRowCopyWithImpl<$Res>
    implements $AmountRowCopyWith<$Res> {
  _$AmountRowCopyWithImpl(this._self, this._then);

  final AmountRow _self;
  final $Res Function(AmountRow) _then;

/// Create a copy of AmountRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? key = null,Object? title = null,Object? amount = null,Object? previous = freezed,Object? quantity = freezed,Object? currency = freezed,Object? currencyAmount = freezed,}) {
  return _then(AmountRow(
key: null == key ? _self.key : key // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,quantity: freezed == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double?,currency: freezed == currency ? _self.currency : currency // ignore: cast_nullable_to_non_nullable
as String?,currencyAmount: freezed == currencyAmount ? _self.currencyAmount : currencyAmount // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [AmountRow].
extension AmountRowPatterns on AmountRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AmountRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AmountRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AmountRow value)  $default,){
final _that = this;
switch (_that) {
case _AmountRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AmountRow value)?  $default,){
final _that = this;
switch (_that) {
case _AmountRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String key,  String title,  double amount,  double? previous,  double? quantity,  String? currency,  double? currencyAmount)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AmountRow() when $default != null:
return $default(_that.key,_that.title,_that.amount,_that.previous,_that.quantity,_that.currency,_that.currencyAmount);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String key,  String title,  double amount,  double? previous,  double? quantity,  String? currency,  double? currencyAmount)  $default,) {final _that = this;
switch (_that) {
case _AmountRow():
return $default(_that.key,_that.title,_that.amount,_that.previous,_that.quantity,_that.currency,_that.currencyAmount);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String key,  String title,  double amount,  double? previous,  double? quantity,  String? currency,  double? currencyAmount)?  $default,) {final _that = this;
switch (_that) {
case _AmountRow() when $default != null:
return $default(_that.key,_that.title,_that.amount,_that.previous,_that.quantity,_that.currency,_that.currencyAmount);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AmountRow implements AmountRow {
  const _AmountRow({required this.key, required this.title, this.amount = 0, this.previous, this.quantity, this.currency, this.currencyAmount});
  factory _AmountRow.fromJson(Map<String, dynamic> json) => _$AmountRowFromJson(json);

@override final  String key;
@override final  String title;
@override@JsonKey() final  double amount;
@override final  double? previous;
@override final  double? quantity;
@override final  String? currency;
@override final  double? currencyAmount;

/// Create a copy of AmountRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AmountRowCopyWith<_AmountRow> get copyWith => __$AmountRowCopyWithImpl<_AmountRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AmountRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AmountRow&&(identical(other.key, key) || other.key == key)&&(identical(other.title, title) || other.title == title)&&(identical(other.amount, amount) || other.amount == amount)&&(identical(other.previous, previous) || other.previous == previous)&&(identical(other.quantity, quantity) || other.quantity == quantity)&&(identical(other.currency, currency) || other.currency == currency)&&(identical(other.currencyAmount, currencyAmount) || other.currencyAmount == currencyAmount));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,key,title,amount,previous,quantity,currency,currencyAmount);
}

@override
String toString() {
    return 'AmountRow(key: $key, title: $title, amount: $amount, previous: $previous, quantity: $quantity, currency: $currency, currencyAmount: $currencyAmount)';
}


}

/// @nodoc
abstract mixin class _$AmountRowCopyWith<$Res> implements $AmountRowCopyWith<$Res> {
  factory _$AmountRowCopyWith(_AmountRow value, $Res Function(_AmountRow) _then) = __$AmountRowCopyWithImpl;
@override @useResult
$Res call({
 String key, String title, double amount, double? previous, double? quantity, String? currency, double? currencyAmount
});




}
/// @nodoc
class __$AmountRowCopyWithImpl<$Res>
    implements _$AmountRowCopyWith<$Res> {
  __$AmountRowCopyWithImpl(this._self, this._then);

  final _AmountRow _self;
  final $Res Function(_AmountRow) _then;

/// Create a copy of AmountRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? key = null,Object? title = null,Object? amount = null,Object? previous = freezed,Object? quantity = freezed,Object? currency = freezed,Object? currencyAmount = freezed,}) {
  return _then(_AmountRow(
key: null == key ? _self.key : key // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,quantity: freezed == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double?,currency: freezed == currency ? _self.currency : currency // ignore: cast_nullable_to_non_nullable
as String?,currencyAmount: freezed == currencyAmount ? _self.currencyAmount : currencyAmount // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$FinOverview {

 FinanceMeta get meta; List<PnlValue> get pnl; List<MonthPoint> get months; double get cashTotal; List<AmountRow> get cash; double get receivables; double get payables; double get advancesGiven; double get advancesReceived; List<AmountRow> get inventory; String? get balancesMonth; List<AmountRow> get expenseGrowth;
/// Create a copy of FinOverview
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinOverviewCopyWith<FinOverview> get copyWith => _$FinOverviewCopyWithImpl<FinOverview>(this as FinOverview, _$identity);

  /// Serializes this FinOverview to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinOverview;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinOverview&&(identical(other.meta, _this.meta) || other.meta == _this.meta)&&const DeepCollectionEquality().equals(other.pnl, _this.pnl)&&const DeepCollectionEquality().equals(other.months, _this.months)&&(identical(other.cashTotal, _this.cashTotal) || other.cashTotal == _this.cashTotal)&&const DeepCollectionEquality().equals(other.cash, _this.cash)&&(identical(other.receivables, _this.receivables) || other.receivables == _this.receivables)&&(identical(other.payables, _this.payables) || other.payables == _this.payables)&&(identical(other.advancesGiven, _this.advancesGiven) || other.advancesGiven == _this.advancesGiven)&&(identical(other.advancesReceived, _this.advancesReceived) || other.advancesReceived == _this.advancesReceived)&&const DeepCollectionEquality().equals(other.inventory, _this.inventory)&&(identical(other.balancesMonth, _this.balancesMonth) || other.balancesMonth == _this.balancesMonth)&&const DeepCollectionEquality().equals(other.expenseGrowth, _this.expenseGrowth));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinOverview;
  return Object.hash(runtimeType,_this.meta,const DeepCollectionEquality().hash(_this.pnl),const DeepCollectionEquality().hash(_this.months),_this.cashTotal,const DeepCollectionEquality().hash(_this.cash),_this.receivables,_this.payables,_this.advancesGiven,_this.advancesReceived,const DeepCollectionEquality().hash(_this.inventory),_this.balancesMonth,const DeepCollectionEquality().hash(_this.expenseGrowth));
}

@override
String toString() {
  final _this = this as FinOverview;
  return 'FinOverview(meta: ${_this.meta}, pnl: ${_this.pnl}, months: ${_this.months}, cashTotal: ${_this.cashTotal}, cash: ${_this.cash}, receivables: ${_this.receivables}, payables: ${_this.payables}, advancesGiven: ${_this.advancesGiven}, advancesReceived: ${_this.advancesReceived}, inventory: ${_this.inventory}, balancesMonth: ${_this.balancesMonth}, expenseGrowth: ${_this.expenseGrowth})';
}


}

/// @nodoc
abstract mixin class $FinOverviewCopyWith<$Res>  {
  factory $FinOverviewCopyWith(FinOverview value, $Res Function(FinOverview) _then) = _$FinOverviewCopyWithImpl;
@useResult
$Res call({
 FinanceMeta meta, List<PnlValue> pnl, List<MonthPoint> months, double cashTotal, List<AmountRow> cash, double receivables, double payables, double advancesGiven, double advancesReceived, List<AmountRow> inventory, String? balancesMonth, List<AmountRow> expenseGrowth
});


$FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class _$FinOverviewCopyWithImpl<$Res>
    implements $FinOverviewCopyWith<$Res> {
  _$FinOverviewCopyWithImpl(this._self, this._then);

  final FinOverview _self;
  final $Res Function(FinOverview) _then;

/// Create a copy of FinOverview
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? meta = null,Object? pnl = null,Object? months = null,Object? cashTotal = null,Object? cash = null,Object? receivables = null,Object? payables = null,Object? advancesGiven = null,Object? advancesReceived = null,Object? inventory = null,Object? balancesMonth = freezed,Object? expenseGrowth = null,}) {
  return _then(FinOverview(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,pnl: null == pnl ? _self.pnl : pnl // ignore: cast_nullable_to_non_nullable
as List<PnlValue>,months: null == months ? _self.months : months // ignore: cast_nullable_to_non_nullable
as List<MonthPoint>,cashTotal: null == cashTotal ? _self.cashTotal : cashTotal // ignore: cast_nullable_to_non_nullable
as double,cash: null == cash ? _self.cash : cash // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,receivables: null == receivables ? _self.receivables : receivables // ignore: cast_nullable_to_non_nullable
as double,payables: null == payables ? _self.payables : payables // ignore: cast_nullable_to_non_nullable
as double,advancesGiven: null == advancesGiven ? _self.advancesGiven : advancesGiven // ignore: cast_nullable_to_non_nullable
as double,advancesReceived: null == advancesReceived ? _self.advancesReceived : advancesReceived // ignore: cast_nullable_to_non_nullable
as double,inventory: null == inventory ? _self.inventory : inventory // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,balancesMonth: freezed == balancesMonth ? _self.balancesMonth : balancesMonth // ignore: cast_nullable_to_non_nullable
as String?,expenseGrowth: null == expenseGrowth ? _self.expenseGrowth : expenseGrowth // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,
  ));
}
/// Create a copy of FinOverview
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}
}


/// Adds pattern-matching-related methods to [FinOverview].
extension FinOverviewPatterns on FinOverview {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinOverview value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinOverview() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinOverview value)  $default,){
final _that = this;
switch (_that) {
case _FinOverview():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinOverview value)?  $default,){
final _that = this;
switch (_that) {
case _FinOverview() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<PnlValue> pnl,  List<MonthPoint> months,  double cashTotal,  List<AmountRow> cash,  double receivables,  double payables,  double advancesGiven,  double advancesReceived,  List<AmountRow> inventory,  String? balancesMonth,  List<AmountRow> expenseGrowth)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinOverview() when $default != null:
return $default(_that.meta,_that.pnl,_that.months,_that.cashTotal,_that.cash,_that.receivables,_that.payables,_that.advancesGiven,_that.advancesReceived,_that.inventory,_that.balancesMonth,_that.expenseGrowth);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<PnlValue> pnl,  List<MonthPoint> months,  double cashTotal,  List<AmountRow> cash,  double receivables,  double payables,  double advancesGiven,  double advancesReceived,  List<AmountRow> inventory,  String? balancesMonth,  List<AmountRow> expenseGrowth)  $default,) {final _that = this;
switch (_that) {
case _FinOverview():
return $default(_that.meta,_that.pnl,_that.months,_that.cashTotal,_that.cash,_that.receivables,_that.payables,_that.advancesGiven,_that.advancesReceived,_that.inventory,_that.balancesMonth,_that.expenseGrowth);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FinanceMeta meta,  List<PnlValue> pnl,  List<MonthPoint> months,  double cashTotal,  List<AmountRow> cash,  double receivables,  double payables,  double advancesGiven,  double advancesReceived,  List<AmountRow> inventory,  String? balancesMonth,  List<AmountRow> expenseGrowth)?  $default,) {final _that = this;
switch (_that) {
case _FinOverview() when $default != null:
return $default(_that.meta,_that.pnl,_that.months,_that.cashTotal,_that.cash,_that.receivables,_that.payables,_that.advancesGiven,_that.advancesReceived,_that.inventory,_that.balancesMonth,_that.expenseGrowth);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinOverview extends FinOverview {
  const _FinOverview({required this.meta,  List<PnlValue> pnl = const <PnlValue>[],  List<MonthPoint> months = const <MonthPoint>[], this.cashTotal = 0,  List<AmountRow> cash = const <AmountRow>[], this.receivables = 0, this.payables = 0, this.advancesGiven = 0, this.advancesReceived = 0,  List<AmountRow> inventory = const <AmountRow>[], this.balancesMonth,  List<AmountRow> expenseGrowth = const <AmountRow>[]}): _pnl = pnl,_months = months,_cash = cash,_inventory = inventory,_expenseGrowth = expenseGrowth,super._();
  factory _FinOverview.fromJson(Map<String, dynamic> json) => _$FinOverviewFromJson(json);

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
 final  List<AmountRow> _cash;
@override@JsonKey() List<AmountRow> get cash {
  if (_cash is EqualUnmodifiableListView) return _cash;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_cash);
}

@override@JsonKey() final  double receivables;
@override@JsonKey() final  double payables;
@override@JsonKey() final  double advancesGiven;
@override@JsonKey() final  double advancesReceived;
 final  List<AmountRow> _inventory;
@override@JsonKey() List<AmountRow> get inventory {
  if (_inventory is EqualUnmodifiableListView) return _inventory;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_inventory);
}

@override final  String? balancesMonth;
 final  List<AmountRow> _expenseGrowth;
@override@JsonKey() List<AmountRow> get expenseGrowth {
  if (_expenseGrowth is EqualUnmodifiableListView) return _expenseGrowth;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_expenseGrowth);
}


/// Create a copy of FinOverview
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinOverviewCopyWith<_FinOverview> get copyWith => __$FinOverviewCopyWithImpl<_FinOverview>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinOverviewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinOverview&&(identical(other.meta, meta) || other.meta == meta)&&const DeepCollectionEquality().equals(other.pnl, _pnl)&&const DeepCollectionEquality().equals(other.months, _months)&&(identical(other.cashTotal, cashTotal) || other.cashTotal == cashTotal)&&const DeepCollectionEquality().equals(other.cash, _cash)&&(identical(other.receivables, receivables) || other.receivables == receivables)&&(identical(other.payables, payables) || other.payables == payables)&&(identical(other.advancesGiven, advancesGiven) || other.advancesGiven == advancesGiven)&&(identical(other.advancesReceived, advancesReceived) || other.advancesReceived == advancesReceived)&&const DeepCollectionEquality().equals(other.inventory, _inventory)&&(identical(other.balancesMonth, balancesMonth) || other.balancesMonth == balancesMonth)&&const DeepCollectionEquality().equals(other.expenseGrowth, _expenseGrowth));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,meta,const DeepCollectionEquality().hash(_pnl),const DeepCollectionEquality().hash(_months),cashTotal,const DeepCollectionEquality().hash(_cash),receivables,payables,advancesGiven,advancesReceived,const DeepCollectionEquality().hash(_inventory),balancesMonth,const DeepCollectionEquality().hash(_expenseGrowth));
}

@override
String toString() {
    return 'FinOverview(meta: $meta, pnl: $pnl, months: $months, cashTotal: $cashTotal, cash: $cash, receivables: $receivables, payables: $payables, advancesGiven: $advancesGiven, advancesReceived: $advancesReceived, inventory: $inventory, balancesMonth: $balancesMonth, expenseGrowth: $expenseGrowth)';
}


}

/// @nodoc
abstract mixin class _$FinOverviewCopyWith<$Res> implements $FinOverviewCopyWith<$Res> {
  factory _$FinOverviewCopyWith(_FinOverview value, $Res Function(_FinOverview) _then) = __$FinOverviewCopyWithImpl;
@override @useResult
$Res call({
 FinanceMeta meta, List<PnlValue> pnl, List<MonthPoint> months, double cashTotal, List<AmountRow> cash, double receivables, double payables, double advancesGiven, double advancesReceived, List<AmountRow> inventory, String? balancesMonth, List<AmountRow> expenseGrowth
});


@override $FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class __$FinOverviewCopyWithImpl<$Res>
    implements _$FinOverviewCopyWith<$Res> {
  __$FinOverviewCopyWithImpl(this._self, this._then);

  final _FinOverview _self;
  final $Res Function(_FinOverview) _then;

/// Create a copy of FinOverview
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? meta = null,Object? pnl = null,Object? months = null,Object? cashTotal = null,Object? cash = null,Object? receivables = null,Object? payables = null,Object? advancesGiven = null,Object? advancesReceived = null,Object? inventory = null,Object? balancesMonth = freezed,Object? expenseGrowth = null,}) {
  return _then(_FinOverview(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,pnl: null == pnl ? _self._pnl : pnl // ignore: cast_nullable_to_non_nullable
as List<PnlValue>,months: null == months ? _self._months : months // ignore: cast_nullable_to_non_nullable
as List<MonthPoint>,cashTotal: null == cashTotal ? _self.cashTotal : cashTotal // ignore: cast_nullable_to_non_nullable
as double,cash: null == cash ? _self._cash : cash // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,receivables: null == receivables ? _self.receivables : receivables // ignore: cast_nullable_to_non_nullable
as double,payables: null == payables ? _self.payables : payables // ignore: cast_nullable_to_non_nullable
as double,advancesGiven: null == advancesGiven ? _self.advancesGiven : advancesGiven // ignore: cast_nullable_to_non_nullable
as double,advancesReceived: null == advancesReceived ? _self.advancesReceived : advancesReceived // ignore: cast_nullable_to_non_nullable
as double,inventory: null == inventory ? _self._inventory : inventory // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,balancesMonth: freezed == balancesMonth ? _self.balancesMonth : balancesMonth // ignore: cast_nullable_to_non_nullable
as String?,expenseGrowth: null == expenseGrowth ? _self._expenseGrowth : expenseGrowth // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,
  ));
}

/// Create a copy of FinOverview
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
mixin _$PnlLine {

 String get code; String get title;/// income | expense | total
 String get kind; List<double> get months; double get total; double? get previous;
/// Create a copy of PnlLine
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$PnlLineCopyWith<PnlLine> get copyWith => _$PnlLineCopyWithImpl<PnlLine>(this as PnlLine, _$identity);

  /// Serializes this PnlLine to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as PnlLine;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is PnlLine&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&const DeepCollectionEquality().equals(other.months, _this.months)&&(identical(other.total, _this.total) || other.total == _this.total)&&(identical(other.previous, _this.previous) || other.previous == _this.previous));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as PnlLine;
  return Object.hash(runtimeType,_this.code,_this.title,_this.kind,const DeepCollectionEquality().hash(_this.months),_this.total,_this.previous);
}

@override
String toString() {
  final _this = this as PnlLine;
  return 'PnlLine(code: ${_this.code}, title: ${_this.title}, kind: ${_this.kind}, months: ${_this.months}, total: ${_this.total}, previous: ${_this.previous})';
}


}

/// @nodoc
abstract mixin class $PnlLineCopyWith<$Res>  {
  factory $PnlLineCopyWith(PnlLine value, $Res Function(PnlLine) _then) = _$PnlLineCopyWithImpl;
@useResult
$Res call({
 String code, String title, String kind, List<double> months, double total, double? previous
});




}
/// @nodoc
class _$PnlLineCopyWithImpl<$Res>
    implements $PnlLineCopyWith<$Res> {
  _$PnlLineCopyWithImpl(this._self, this._then);

  final PnlLine _self;
  final $Res Function(PnlLine) _then;

/// Create a copy of PnlLine
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? code = null,Object? title = null,Object? kind = null,Object? months = null,Object? total = null,Object? previous = freezed,}) {
  return _then(PnlLine(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,months: null == months ? _self.months : months // ignore: cast_nullable_to_non_nullable
as List<double>,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}

}


/// Adds pattern-matching-related methods to [PnlLine].
extension PnlLinePatterns on PnlLine {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _PnlLine value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _PnlLine() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _PnlLine value)  $default,){
final _that = this;
switch (_that) {
case _PnlLine():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _PnlLine value)?  $default,){
final _that = this;
switch (_that) {
case _PnlLine() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String code,  String title,  String kind,  List<double> months,  double total,  double? previous)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _PnlLine() when $default != null:
return $default(_that.code,_that.title,_that.kind,_that.months,_that.total,_that.previous);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String code,  String title,  String kind,  List<double> months,  double total,  double? previous)  $default,) {final _that = this;
switch (_that) {
case _PnlLine():
return $default(_that.code,_that.title,_that.kind,_that.months,_that.total,_that.previous);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String code,  String title,  String kind,  List<double> months,  double total,  double? previous)?  $default,) {final _that = this;
switch (_that) {
case _PnlLine() when $default != null:
return $default(_that.code,_that.title,_that.kind,_that.months,_that.total,_that.previous);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _PnlLine implements PnlLine {
  const _PnlLine({required this.code, required this.title, required this.kind,  List<double> months = const <double>[], this.total = 0, this.previous}): _months = months;
  factory _PnlLine.fromJson(Map<String, dynamic> json) => _$PnlLineFromJson(json);

@override final  String code;
@override final  String title;
/// income | expense | total
@override final  String kind;
 final  List<double> _months;
@override@JsonKey() List<double> get months {
  if (_months is EqualUnmodifiableListView) return _months;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_months);
}

@override@JsonKey() final  double total;
@override final  double? previous;

/// Create a copy of PnlLine
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$PnlLineCopyWith<_PnlLine> get copyWith => __$PnlLineCopyWithImpl<_PnlLine>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$PnlLineToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _PnlLine&&(identical(other.code, code) || other.code == code)&&(identical(other.title, title) || other.title == title)&&(identical(other.kind, kind) || other.kind == kind)&&const DeepCollectionEquality().equals(other.months, _months)&&(identical(other.total, total) || other.total == total)&&(identical(other.previous, previous) || other.previous == previous));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,code,title,kind,const DeepCollectionEquality().hash(_months),total,previous);
}

@override
String toString() {
    return 'PnlLine(code: $code, title: $title, kind: $kind, months: $months, total: $total, previous: $previous)';
}


}

/// @nodoc
abstract mixin class _$PnlLineCopyWith<$Res> implements $PnlLineCopyWith<$Res> {
  factory _$PnlLineCopyWith(_PnlLine value, $Res Function(_PnlLine) _then) = __$PnlLineCopyWithImpl;
@override @useResult
$Res call({
 String code, String title, String kind, List<double> months, double total, double? previous
});




}
/// @nodoc
class __$PnlLineCopyWithImpl<$Res>
    implements _$PnlLineCopyWith<$Res> {
  __$PnlLineCopyWithImpl(this._self, this._then);

  final _PnlLine _self;
  final $Res Function(_PnlLine) _then;

/// Create a copy of PnlLine
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? code = null,Object? title = null,Object? kind = null,Object? months = null,Object? total = null,Object? previous = freezed,}) {
  return _then(_PnlLine(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,months: null == months ? _self._months : months // ignore: cast_nullable_to_non_nullable
as List<double>,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,previous: freezed == previous ? _self.previous : previous // ignore: cast_nullable_to_non_nullable
as double?,
  ));
}


}


/// @nodoc
mixin _$FinPnl {

 FinanceMeta get meta; List<String> get months; List<PnlLine> get lines; List<AmountRow> get expensesByAccount; List<AmountRow> get expensesByDepartment;
/// Create a copy of FinPnl
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinPnlCopyWith<FinPnl> get copyWith => _$FinPnlCopyWithImpl<FinPnl>(this as FinPnl, _$identity);

  /// Serializes this FinPnl to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinPnl;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinPnl&&(identical(other.meta, _this.meta) || other.meta == _this.meta)&&const DeepCollectionEquality().equals(other.months, _this.months)&&const DeepCollectionEquality().equals(other.lines, _this.lines)&&const DeepCollectionEquality().equals(other.expensesByAccount, _this.expensesByAccount)&&const DeepCollectionEquality().equals(other.expensesByDepartment, _this.expensesByDepartment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinPnl;
  return Object.hash(runtimeType,_this.meta,const DeepCollectionEquality().hash(_this.months),const DeepCollectionEquality().hash(_this.lines),const DeepCollectionEquality().hash(_this.expensesByAccount),const DeepCollectionEquality().hash(_this.expensesByDepartment));
}

@override
String toString() {
  final _this = this as FinPnl;
  return 'FinPnl(meta: ${_this.meta}, months: ${_this.months}, lines: ${_this.lines}, expensesByAccount: ${_this.expensesByAccount}, expensesByDepartment: ${_this.expensesByDepartment})';
}


}

/// @nodoc
abstract mixin class $FinPnlCopyWith<$Res>  {
  factory $FinPnlCopyWith(FinPnl value, $Res Function(FinPnl) _then) = _$FinPnlCopyWithImpl;
@useResult
$Res call({
 FinanceMeta meta, List<String> months, List<PnlLine> lines, List<AmountRow> expensesByAccount, List<AmountRow> expensesByDepartment
});


$FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class _$FinPnlCopyWithImpl<$Res>
    implements $FinPnlCopyWith<$Res> {
  _$FinPnlCopyWithImpl(this._self, this._then);

  final FinPnl _self;
  final $Res Function(FinPnl) _then;

/// Create a copy of FinPnl
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? meta = null,Object? months = null,Object? lines = null,Object? expensesByAccount = null,Object? expensesByDepartment = null,}) {
  return _then(FinPnl(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,months: null == months ? _self.months : months // ignore: cast_nullable_to_non_nullable
as List<String>,lines: null == lines ? _self.lines : lines // ignore: cast_nullable_to_non_nullable
as List<PnlLine>,expensesByAccount: null == expensesByAccount ? _self.expensesByAccount : expensesByAccount // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,expensesByDepartment: null == expensesByDepartment ? _self.expensesByDepartment : expensesByDepartment // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,
  ));
}
/// Create a copy of FinPnl
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}
}


/// Adds pattern-matching-related methods to [FinPnl].
extension FinPnlPatterns on FinPnl {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinPnl value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinPnl() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinPnl value)  $default,){
final _that = this;
switch (_that) {
case _FinPnl():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinPnl value)?  $default,){
final _that = this;
switch (_that) {
case _FinPnl() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<String> months,  List<PnlLine> lines,  List<AmountRow> expensesByAccount,  List<AmountRow> expensesByDepartment)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinPnl() when $default != null:
return $default(_that.meta,_that.months,_that.lines,_that.expensesByAccount,_that.expensesByDepartment);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<String> months,  List<PnlLine> lines,  List<AmountRow> expensesByAccount,  List<AmountRow> expensesByDepartment)  $default,) {final _that = this;
switch (_that) {
case _FinPnl():
return $default(_that.meta,_that.months,_that.lines,_that.expensesByAccount,_that.expensesByDepartment);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FinanceMeta meta,  List<String> months,  List<PnlLine> lines,  List<AmountRow> expensesByAccount,  List<AmountRow> expensesByDepartment)?  $default,) {final _that = this;
switch (_that) {
case _FinPnl() when $default != null:
return $default(_that.meta,_that.months,_that.lines,_that.expensesByAccount,_that.expensesByDepartment);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinPnl implements FinPnl {
  const _FinPnl({required this.meta,  List<String> months = const <String>[],  List<PnlLine> lines = const <PnlLine>[],  List<AmountRow> expensesByAccount = const <AmountRow>[],  List<AmountRow> expensesByDepartment = const <AmountRow>[]}): _months = months,_lines = lines,_expensesByAccount = expensesByAccount,_expensesByDepartment = expensesByDepartment;
  factory _FinPnl.fromJson(Map<String, dynamic> json) => _$FinPnlFromJson(json);

@override final  FinanceMeta meta;
 final  List<String> _months;
@override@JsonKey() List<String> get months {
  if (_months is EqualUnmodifiableListView) return _months;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_months);
}

 final  List<PnlLine> _lines;
@override@JsonKey() List<PnlLine> get lines {
  if (_lines is EqualUnmodifiableListView) return _lines;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_lines);
}

 final  List<AmountRow> _expensesByAccount;
@override@JsonKey() List<AmountRow> get expensesByAccount {
  if (_expensesByAccount is EqualUnmodifiableListView) return _expensesByAccount;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_expensesByAccount);
}

 final  List<AmountRow> _expensesByDepartment;
@override@JsonKey() List<AmountRow> get expensesByDepartment {
  if (_expensesByDepartment is EqualUnmodifiableListView) return _expensesByDepartment;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_expensesByDepartment);
}


/// Create a copy of FinPnl
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinPnlCopyWith<_FinPnl> get copyWith => __$FinPnlCopyWithImpl<_FinPnl>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinPnlToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinPnl&&(identical(other.meta, meta) || other.meta == meta)&&const DeepCollectionEquality().equals(other.months, _months)&&const DeepCollectionEquality().equals(other.lines, _lines)&&const DeepCollectionEquality().equals(other.expensesByAccount, _expensesByAccount)&&const DeepCollectionEquality().equals(other.expensesByDepartment, _expensesByDepartment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,meta,const DeepCollectionEquality().hash(_months),const DeepCollectionEquality().hash(_lines),const DeepCollectionEquality().hash(_expensesByAccount),const DeepCollectionEquality().hash(_expensesByDepartment));
}

@override
String toString() {
    return 'FinPnl(meta: $meta, months: $months, lines: $lines, expensesByAccount: $expensesByAccount, expensesByDepartment: $expensesByDepartment)';
}


}

/// @nodoc
abstract mixin class _$FinPnlCopyWith<$Res> implements $FinPnlCopyWith<$Res> {
  factory _$FinPnlCopyWith(_FinPnl value, $Res Function(_FinPnl) _then) = __$FinPnlCopyWithImpl;
@override @useResult
$Res call({
 FinanceMeta meta, List<String> months, List<PnlLine> lines, List<AmountRow> expensesByAccount, List<AmountRow> expensesByDepartment
});


@override $FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class __$FinPnlCopyWithImpl<$Res>
    implements _$FinPnlCopyWith<$Res> {
  __$FinPnlCopyWithImpl(this._self, this._then);

  final _FinPnl _self;
  final $Res Function(_FinPnl) _then;

/// Create a copy of FinPnl
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? meta = null,Object? months = null,Object? lines = null,Object? expensesByAccount = null,Object? expensesByDepartment = null,}) {
  return _then(_FinPnl(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,months: null == months ? _self._months : months // ignore: cast_nullable_to_non_nullable
as List<String>,lines: null == lines ? _self._lines : lines // ignore: cast_nullable_to_non_nullable
as List<PnlLine>,expensesByAccount: null == expensesByAccount ? _self._expensesByAccount : expensesByAccount // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,expensesByDepartment: null == expensesByDepartment ? _self._expensesByDepartment : expensesByDepartment // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,
  ));
}

/// Create a copy of FinPnl
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
mixin _$CashFlow {

 String get code; String get title; bool get inflow; double get total;
/// Create a copy of CashFlow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$CashFlowCopyWith<CashFlow> get copyWith => _$CashFlowCopyWithImpl<CashFlow>(this as CashFlow, _$identity);

  /// Serializes this CashFlow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as CashFlow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is CashFlow&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.inflow, _this.inflow) || other.inflow == _this.inflow)&&(identical(other.total, _this.total) || other.total == _this.total));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as CashFlow;
  return Object.hash(runtimeType,_this.code,_this.title,_this.inflow,_this.total);
}

@override
String toString() {
  final _this = this as CashFlow;
  return 'CashFlow(code: ${_this.code}, title: ${_this.title}, inflow: ${_this.inflow}, total: ${_this.total})';
}


}

/// @nodoc
abstract mixin class $CashFlowCopyWith<$Res>  {
  factory $CashFlowCopyWith(CashFlow value, $Res Function(CashFlow) _then) = _$CashFlowCopyWithImpl;
@useResult
$Res call({
 String code, String title, bool inflow, double total
});




}
/// @nodoc
class _$CashFlowCopyWithImpl<$Res>
    implements $CashFlowCopyWith<$Res> {
  _$CashFlowCopyWithImpl(this._self, this._then);

  final CashFlow _self;
  final $Res Function(CashFlow) _then;

/// Create a copy of CashFlow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? code = null,Object? title = null,Object? inflow = null,Object? total = null,}) {
  return _then(CashFlow(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,inflow: null == inflow ? _self.inflow : inflow // ignore: cast_nullable_to_non_nullable
as bool,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [CashFlow].
extension CashFlowPatterns on CashFlow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _CashFlow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _CashFlow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _CashFlow value)  $default,){
final _that = this;
switch (_that) {
case _CashFlow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _CashFlow value)?  $default,){
final _that = this;
switch (_that) {
case _CashFlow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String code,  String title,  bool inflow,  double total)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _CashFlow() when $default != null:
return $default(_that.code,_that.title,_that.inflow,_that.total);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String code,  String title,  bool inflow,  double total)  $default,) {final _that = this;
switch (_that) {
case _CashFlow():
return $default(_that.code,_that.title,_that.inflow,_that.total);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String code,  String title,  bool inflow,  double total)?  $default,) {final _that = this;
switch (_that) {
case _CashFlow() when $default != null:
return $default(_that.code,_that.title,_that.inflow,_that.total);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _CashFlow implements CashFlow {
  const _CashFlow({required this.code, required this.title, this.inflow = false, this.total = 0});
  factory _CashFlow.fromJson(Map<String, dynamic> json) => _$CashFlowFromJson(json);

@override final  String code;
@override final  String title;
@override@JsonKey() final  bool inflow;
@override@JsonKey() final  double total;

/// Create a copy of CashFlow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$CashFlowCopyWith<_CashFlow> get copyWith => __$CashFlowCopyWithImpl<_CashFlow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$CashFlowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _CashFlow&&(identical(other.code, code) || other.code == code)&&(identical(other.title, title) || other.title == title)&&(identical(other.inflow, inflow) || other.inflow == inflow)&&(identical(other.total, total) || other.total == total));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,code,title,inflow,total);
}

@override
String toString() {
    return 'CashFlow(code: $code, title: $title, inflow: $inflow, total: $total)';
}


}

/// @nodoc
abstract mixin class _$CashFlowCopyWith<$Res> implements $CashFlowCopyWith<$Res> {
  factory _$CashFlowCopyWith(_CashFlow value, $Res Function(_CashFlow) _then) = __$CashFlowCopyWithImpl;
@override @useResult
$Res call({
 String code, String title, bool inflow, double total
});




}
/// @nodoc
class __$CashFlowCopyWithImpl<$Res>
    implements _$CashFlowCopyWith<$Res> {
  __$CashFlowCopyWithImpl(this._self, this._then);

  final _CashFlow _self;
  final $Res Function(_CashFlow) _then;

/// Create a copy of CashFlow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? code = null,Object? title = null,Object? inflow = null,Object? total = null,}) {
  return _then(_CashFlow(
code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,inflow: null == inflow ? _self.inflow : inflow // ignore: cast_nullable_to_non_nullable
as bool,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$FinCash {

 FinanceMeta get meta; List<CashFlow> get flows; double get inflow; double get outflow; List<AmountRow> get balances; String? get balancesMonth;
/// Create a copy of FinCash
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinCashCopyWith<FinCash> get copyWith => _$FinCashCopyWithImpl<FinCash>(this as FinCash, _$identity);

  /// Serializes this FinCash to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinCash;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinCash&&(identical(other.meta, _this.meta) || other.meta == _this.meta)&&const DeepCollectionEquality().equals(other.flows, _this.flows)&&(identical(other.inflow, _this.inflow) || other.inflow == _this.inflow)&&(identical(other.outflow, _this.outflow) || other.outflow == _this.outflow)&&const DeepCollectionEquality().equals(other.balances, _this.balances)&&(identical(other.balancesMonth, _this.balancesMonth) || other.balancesMonth == _this.balancesMonth));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinCash;
  return Object.hash(runtimeType,_this.meta,const DeepCollectionEquality().hash(_this.flows),_this.inflow,_this.outflow,const DeepCollectionEquality().hash(_this.balances),_this.balancesMonth);
}

@override
String toString() {
  final _this = this as FinCash;
  return 'FinCash(meta: ${_this.meta}, flows: ${_this.flows}, inflow: ${_this.inflow}, outflow: ${_this.outflow}, balances: ${_this.balances}, balancesMonth: ${_this.balancesMonth})';
}


}

/// @nodoc
abstract mixin class $FinCashCopyWith<$Res>  {
  factory $FinCashCopyWith(FinCash value, $Res Function(FinCash) _then) = _$FinCashCopyWithImpl;
@useResult
$Res call({
 FinanceMeta meta, List<CashFlow> flows, double inflow, double outflow, List<AmountRow> balances, String? balancesMonth
});


$FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class _$FinCashCopyWithImpl<$Res>
    implements $FinCashCopyWith<$Res> {
  _$FinCashCopyWithImpl(this._self, this._then);

  final FinCash _self;
  final $Res Function(FinCash) _then;

/// Create a copy of FinCash
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? meta = null,Object? flows = null,Object? inflow = null,Object? outflow = null,Object? balances = null,Object? balancesMonth = freezed,}) {
  return _then(FinCash(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,flows: null == flows ? _self.flows : flows // ignore: cast_nullable_to_non_nullable
as List<CashFlow>,inflow: null == inflow ? _self.inflow : inflow // ignore: cast_nullable_to_non_nullable
as double,outflow: null == outflow ? _self.outflow : outflow // ignore: cast_nullable_to_non_nullable
as double,balances: null == balances ? _self.balances : balances // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,balancesMonth: freezed == balancesMonth ? _self.balancesMonth : balancesMonth // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}
/// Create a copy of FinCash
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}
}


/// Adds pattern-matching-related methods to [FinCash].
extension FinCashPatterns on FinCash {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinCash value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinCash() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinCash value)  $default,){
final _that = this;
switch (_that) {
case _FinCash():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinCash value)?  $default,){
final _that = this;
switch (_that) {
case _FinCash() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<CashFlow> flows,  double inflow,  double outflow,  List<AmountRow> balances,  String? balancesMonth)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinCash() when $default != null:
return $default(_that.meta,_that.flows,_that.inflow,_that.outflow,_that.balances,_that.balancesMonth);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<CashFlow> flows,  double inflow,  double outflow,  List<AmountRow> balances,  String? balancesMonth)  $default,) {final _that = this;
switch (_that) {
case _FinCash():
return $default(_that.meta,_that.flows,_that.inflow,_that.outflow,_that.balances,_that.balancesMonth);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FinanceMeta meta,  List<CashFlow> flows,  double inflow,  double outflow,  List<AmountRow> balances,  String? balancesMonth)?  $default,) {final _that = this;
switch (_that) {
case _FinCash() when $default != null:
return $default(_that.meta,_that.flows,_that.inflow,_that.outflow,_that.balances,_that.balancesMonth);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinCash implements FinCash {
  const _FinCash({required this.meta,  List<CashFlow> flows = const <CashFlow>[], this.inflow = 0, this.outflow = 0,  List<AmountRow> balances = const <AmountRow>[], this.balancesMonth}): _flows = flows,_balances = balances;
  factory _FinCash.fromJson(Map<String, dynamic> json) => _$FinCashFromJson(json);

@override final  FinanceMeta meta;
 final  List<CashFlow> _flows;
@override@JsonKey() List<CashFlow> get flows {
  if (_flows is EqualUnmodifiableListView) return _flows;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_flows);
}

@override@JsonKey() final  double inflow;
@override@JsonKey() final  double outflow;
 final  List<AmountRow> _balances;
@override@JsonKey() List<AmountRow> get balances {
  if (_balances is EqualUnmodifiableListView) return _balances;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_balances);
}

@override final  String? balancesMonth;

/// Create a copy of FinCash
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinCashCopyWith<_FinCash> get copyWith => __$FinCashCopyWithImpl<_FinCash>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinCashToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinCash&&(identical(other.meta, meta) || other.meta == meta)&&const DeepCollectionEquality().equals(other.flows, _flows)&&(identical(other.inflow, inflow) || other.inflow == inflow)&&(identical(other.outflow, outflow) || other.outflow == outflow)&&const DeepCollectionEquality().equals(other.balances, _balances)&&(identical(other.balancesMonth, balancesMonth) || other.balancesMonth == balancesMonth));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,meta,const DeepCollectionEquality().hash(_flows),inflow,outflow,const DeepCollectionEquality().hash(_balances),balancesMonth);
}

@override
String toString() {
    return 'FinCash(meta: $meta, flows: $flows, inflow: $inflow, outflow: $outflow, balances: $balances, balancesMonth: $balancesMonth)';
}


}

/// @nodoc
abstract mixin class _$FinCashCopyWith<$Res> implements $FinCashCopyWith<$Res> {
  factory _$FinCashCopyWith(_FinCash value, $Res Function(_FinCash) _then) = __$FinCashCopyWithImpl;
@override @useResult
$Res call({
 FinanceMeta meta, List<CashFlow> flows, double inflow, double outflow, List<AmountRow> balances, String? balancesMonth
});


@override $FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class __$FinCashCopyWithImpl<$Res>
    implements _$FinCashCopyWith<$Res> {
  __$FinCashCopyWithImpl(this._self, this._then);

  final _FinCash _self;
  final $Res Function(_FinCash) _then;

/// Create a copy of FinCash
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? meta = null,Object? flows = null,Object? inflow = null,Object? outflow = null,Object? balances = null,Object? balancesMonth = freezed,}) {
  return _then(_FinCash(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,flows: null == flows ? _self._flows : flows // ignore: cast_nullable_to_non_nullable
as List<CashFlow>,inflow: null == inflow ? _self.inflow : inflow // ignore: cast_nullable_to_non_nullable
as double,outflow: null == outflow ? _self.outflow : outflow // ignore: cast_nullable_to_non_nullable
as double,balances: null == balances ? _self._balances : balances // ignore: cast_nullable_to_non_nullable
as List<AmountRow>,balancesMonth: freezed == balancesMonth ? _self.balancesMonth : balancesMonth // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

/// Create a copy of FinCash
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
mixin _$SettlementRow {

 String get counterpartyId; String get name; String? get inn; double get balance; double get overdue; int? get maxOverdueDays;
/// Create a copy of SettlementRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SettlementRowCopyWith<SettlementRow> get copyWith => _$SettlementRowCopyWithImpl<SettlementRow>(this as SettlementRow, _$identity);

  /// Serializes this SettlementRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as SettlementRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is SettlementRow&&(identical(other.counterpartyId, _this.counterpartyId) || other.counterpartyId == _this.counterpartyId)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.inn, _this.inn) || other.inn == _this.inn)&&(identical(other.balance, _this.balance) || other.balance == _this.balance)&&(identical(other.overdue, _this.overdue) || other.overdue == _this.overdue)&&(identical(other.maxOverdueDays, _this.maxOverdueDays) || other.maxOverdueDays == _this.maxOverdueDays));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as SettlementRow;
  return Object.hash(runtimeType,_this.counterpartyId,_this.name,_this.inn,_this.balance,_this.overdue,_this.maxOverdueDays);
}

@override
String toString() {
  final _this = this as SettlementRow;
  return 'SettlementRow(counterpartyId: ${_this.counterpartyId}, name: ${_this.name}, inn: ${_this.inn}, balance: ${_this.balance}, overdue: ${_this.overdue}, maxOverdueDays: ${_this.maxOverdueDays})';
}


}

/// @nodoc
abstract mixin class $SettlementRowCopyWith<$Res>  {
  factory $SettlementRowCopyWith(SettlementRow value, $Res Function(SettlementRow) _then) = _$SettlementRowCopyWithImpl;
@useResult
$Res call({
 String counterpartyId, String name, String? inn, double balance, double overdue, int? maxOverdueDays
});




}
/// @nodoc
class _$SettlementRowCopyWithImpl<$Res>
    implements $SettlementRowCopyWith<$Res> {
  _$SettlementRowCopyWithImpl(this._self, this._then);

  final SettlementRow _self;
  final $Res Function(SettlementRow) _then;

/// Create a copy of SettlementRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? counterpartyId = null,Object? name = null,Object? inn = freezed,Object? balance = null,Object? overdue = null,Object? maxOverdueDays = freezed,}) {
  return _then(SettlementRow(
counterpartyId: null == counterpartyId ? _self.counterpartyId : counterpartyId // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,inn: freezed == inn ? _self.inn : inn // ignore: cast_nullable_to_non_nullable
as String?,balance: null == balance ? _self.balance : balance // ignore: cast_nullable_to_non_nullable
as double,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as double,maxOverdueDays: freezed == maxOverdueDays ? _self.maxOverdueDays : maxOverdueDays // ignore: cast_nullable_to_non_nullable
as int?,
  ));
}

}


/// Adds pattern-matching-related methods to [SettlementRow].
extension SettlementRowPatterns on SettlementRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _SettlementRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _SettlementRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _SettlementRow value)  $default,){
final _that = this;
switch (_that) {
case _SettlementRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _SettlementRow value)?  $default,){
final _that = this;
switch (_that) {
case _SettlementRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String counterpartyId,  String name,  String? inn,  double balance,  double overdue,  int? maxOverdueDays)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _SettlementRow() when $default != null:
return $default(_that.counterpartyId,_that.name,_that.inn,_that.balance,_that.overdue,_that.maxOverdueDays);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String counterpartyId,  String name,  String? inn,  double balance,  double overdue,  int? maxOverdueDays)  $default,) {final _that = this;
switch (_that) {
case _SettlementRow():
return $default(_that.counterpartyId,_that.name,_that.inn,_that.balance,_that.overdue,_that.maxOverdueDays);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String counterpartyId,  String name,  String? inn,  double balance,  double overdue,  int? maxOverdueDays)?  $default,) {final _that = this;
switch (_that) {
case _SettlementRow() when $default != null:
return $default(_that.counterpartyId,_that.name,_that.inn,_that.balance,_that.overdue,_that.maxOverdueDays);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _SettlementRow implements SettlementRow {
  const _SettlementRow({required this.counterpartyId, required this.name, this.inn, this.balance = 0, this.overdue = 0, this.maxOverdueDays});
  factory _SettlementRow.fromJson(Map<String, dynamic> json) => _$SettlementRowFromJson(json);

@override final  String counterpartyId;
@override final  String name;
@override final  String? inn;
@override@JsonKey() final  double balance;
@override@JsonKey() final  double overdue;
@override final  int? maxOverdueDays;

/// Create a copy of SettlementRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$SettlementRowCopyWith<_SettlementRow> get copyWith => __$SettlementRowCopyWithImpl<_SettlementRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$SettlementRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _SettlementRow&&(identical(other.counterpartyId, counterpartyId) || other.counterpartyId == counterpartyId)&&(identical(other.name, name) || other.name == name)&&(identical(other.inn, inn) || other.inn == inn)&&(identical(other.balance, balance) || other.balance == balance)&&(identical(other.overdue, overdue) || other.overdue == overdue)&&(identical(other.maxOverdueDays, maxOverdueDays) || other.maxOverdueDays == maxOverdueDays));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,counterpartyId,name,inn,balance,overdue,maxOverdueDays);
}

@override
String toString() {
    return 'SettlementRow(counterpartyId: $counterpartyId, name: $name, inn: $inn, balance: $balance, overdue: $overdue, maxOverdueDays: $maxOverdueDays)';
}


}

/// @nodoc
abstract mixin class _$SettlementRowCopyWith<$Res> implements $SettlementRowCopyWith<$Res> {
  factory _$SettlementRowCopyWith(_SettlementRow value, $Res Function(_SettlementRow) _then) = __$SettlementRowCopyWithImpl;
@override @useResult
$Res call({
 String counterpartyId, String name, String? inn, double balance, double overdue, int? maxOverdueDays
});




}
/// @nodoc
class __$SettlementRowCopyWithImpl<$Res>
    implements _$SettlementRowCopyWith<$Res> {
  __$SettlementRowCopyWithImpl(this._self, this._then);

  final _SettlementRow _self;
  final $Res Function(_SettlementRow) _then;

/// Create a copy of SettlementRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? counterpartyId = null,Object? name = null,Object? inn = freezed,Object? balance = null,Object? overdue = null,Object? maxOverdueDays = freezed,}) {
  return _then(_SettlementRow(
counterpartyId: null == counterpartyId ? _self.counterpartyId : counterpartyId // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,inn: freezed == inn ? _self.inn : inn // ignore: cast_nullable_to_non_nullable
as String?,balance: null == balance ? _self.balance : balance // ignore: cast_nullable_to_non_nullable
as double,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as double,maxOverdueDays: freezed == maxOverdueDays ? _self.maxOverdueDays : maxOverdueDays // ignore: cast_nullable_to_non_nullable
as int?,
  ));
}


}


/// @nodoc
mixin _$FinSettlements {

 FinanceMeta get meta; String get kind; double get total; double get overdue;/// not_due, 1_30, 31_60, 61_90, over_90, no_term
 Map<String, double> get buckets; double get advances; List<SettlementRow> get rows; String get method;
/// Create a copy of FinSettlements
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinSettlementsCopyWith<FinSettlements> get copyWith => _$FinSettlementsCopyWithImpl<FinSettlements>(this as FinSettlements, _$identity);

  /// Serializes this FinSettlements to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinSettlements;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinSettlements&&(identical(other.meta, _this.meta) || other.meta == _this.meta)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.total, _this.total) || other.total == _this.total)&&(identical(other.overdue, _this.overdue) || other.overdue == _this.overdue)&&const DeepCollectionEquality().equals(other.buckets, _this.buckets)&&(identical(other.advances, _this.advances) || other.advances == _this.advances)&&const DeepCollectionEquality().equals(other.rows, _this.rows)&&(identical(other.method, _this.method) || other.method == _this.method));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinSettlements;
  return Object.hash(runtimeType,_this.meta,_this.kind,_this.total,_this.overdue,const DeepCollectionEquality().hash(_this.buckets),_this.advances,const DeepCollectionEquality().hash(_this.rows),_this.method);
}

@override
String toString() {
  final _this = this as FinSettlements;
  return 'FinSettlements(meta: ${_this.meta}, kind: ${_this.kind}, total: ${_this.total}, overdue: ${_this.overdue}, buckets: ${_this.buckets}, advances: ${_this.advances}, rows: ${_this.rows}, method: ${_this.method})';
}


}

/// @nodoc
abstract mixin class $FinSettlementsCopyWith<$Res>  {
  factory $FinSettlementsCopyWith(FinSettlements value, $Res Function(FinSettlements) _then) = _$FinSettlementsCopyWithImpl;
@useResult
$Res call({
 FinanceMeta meta, String kind, double total, double overdue, Map<String, double> buckets, double advances, List<SettlementRow> rows, String method
});


$FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class _$FinSettlementsCopyWithImpl<$Res>
    implements $FinSettlementsCopyWith<$Res> {
  _$FinSettlementsCopyWithImpl(this._self, this._then);

  final FinSettlements _self;
  final $Res Function(FinSettlements) _then;

/// Create a copy of FinSettlements
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? meta = null,Object? kind = null,Object? total = null,Object? overdue = null,Object? buckets = null,Object? advances = null,Object? rows = null,Object? method = null,}) {
  return _then(FinSettlements(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as double,buckets: null == buckets ? _self.buckets : buckets // ignore: cast_nullable_to_non_nullable
as Map<String, double>,advances: null == advances ? _self.advances : advances // ignore: cast_nullable_to_non_nullable
as double,rows: null == rows ? _self.rows : rows // ignore: cast_nullable_to_non_nullable
as List<SettlementRow>,method: null == method ? _self.method : method // ignore: cast_nullable_to_non_nullable
as String,
  ));
}
/// Create a copy of FinSettlements
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}
}


/// Adds pattern-matching-related methods to [FinSettlements].
extension FinSettlementsPatterns on FinSettlements {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinSettlements value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinSettlements() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinSettlements value)  $default,){
final _that = this;
switch (_that) {
case _FinSettlements():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinSettlements value)?  $default,){
final _that = this;
switch (_that) {
case _FinSettlements() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FinanceMeta meta,  String kind,  double total,  double overdue,  Map<String, double> buckets,  double advances,  List<SettlementRow> rows,  String method)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinSettlements() when $default != null:
return $default(_that.meta,_that.kind,_that.total,_that.overdue,_that.buckets,_that.advances,_that.rows,_that.method);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FinanceMeta meta,  String kind,  double total,  double overdue,  Map<String, double> buckets,  double advances,  List<SettlementRow> rows,  String method)  $default,) {final _that = this;
switch (_that) {
case _FinSettlements():
return $default(_that.meta,_that.kind,_that.total,_that.overdue,_that.buckets,_that.advances,_that.rows,_that.method);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FinanceMeta meta,  String kind,  double total,  double overdue,  Map<String, double> buckets,  double advances,  List<SettlementRow> rows,  String method)?  $default,) {final _that = this;
switch (_that) {
case _FinSettlements() when $default != null:
return $default(_that.meta,_that.kind,_that.total,_that.overdue,_that.buckets,_that.advances,_that.rows,_that.method);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinSettlements implements FinSettlements {
  const _FinSettlements({required this.meta, this.kind = 'receivables', this.total = 0, this.overdue = 0,  Map<String, double> buckets = const <String, double>{}, this.advances = 0,  List<SettlementRow> rows = const <SettlementRow>[], this.method = ''}): _buckets = buckets,_rows = rows;
  factory _FinSettlements.fromJson(Map<String, dynamic> json) => _$FinSettlementsFromJson(json);

@override final  FinanceMeta meta;
@override@JsonKey() final  String kind;
@override@JsonKey() final  double total;
@override@JsonKey() final  double overdue;
/// not_due, 1_30, 31_60, 61_90, over_90, no_term
 final  Map<String, double> _buckets;
/// not_due, 1_30, 31_60, 61_90, over_90, no_term
@override@JsonKey() Map<String, double> get buckets {
  if (_buckets is EqualUnmodifiableMapView) return _buckets;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableMapView(_buckets);
}

@override@JsonKey() final  double advances;
 final  List<SettlementRow> _rows;
@override@JsonKey() List<SettlementRow> get rows {
  if (_rows is EqualUnmodifiableListView) return _rows;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_rows);
}

@override@JsonKey() final  String method;

/// Create a copy of FinSettlements
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinSettlementsCopyWith<_FinSettlements> get copyWith => __$FinSettlementsCopyWithImpl<_FinSettlements>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinSettlementsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinSettlements&&(identical(other.meta, meta) || other.meta == meta)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.total, total) || other.total == total)&&(identical(other.overdue, overdue) || other.overdue == overdue)&&const DeepCollectionEquality().equals(other.buckets, _buckets)&&(identical(other.advances, advances) || other.advances == advances)&&const DeepCollectionEquality().equals(other.rows, _rows)&&(identical(other.method, method) || other.method == method));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,meta,kind,total,overdue,const DeepCollectionEquality().hash(_buckets),advances,const DeepCollectionEquality().hash(_rows),method);
}

@override
String toString() {
    return 'FinSettlements(meta: $meta, kind: $kind, total: $total, overdue: $overdue, buckets: $buckets, advances: $advances, rows: $rows, method: $method)';
}


}

/// @nodoc
abstract mixin class _$FinSettlementsCopyWith<$Res> implements $FinSettlementsCopyWith<$Res> {
  factory _$FinSettlementsCopyWith(_FinSettlements value, $Res Function(_FinSettlements) _then) = __$FinSettlementsCopyWithImpl;
@override @useResult
$Res call({
 FinanceMeta meta, String kind, double total, double overdue, Map<String, double> buckets, double advances, List<SettlementRow> rows, String method
});


@override $FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class __$FinSettlementsCopyWithImpl<$Res>
    implements _$FinSettlementsCopyWith<$Res> {
  __$FinSettlementsCopyWithImpl(this._self, this._then);

  final _FinSettlements _self;
  final $Res Function(_FinSettlements) _then;

/// Create a copy of FinSettlements
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? meta = null,Object? kind = null,Object? total = null,Object? overdue = null,Object? buckets = null,Object? advances = null,Object? rows = null,Object? method = null,}) {
  return _then(_FinSettlements(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,total: null == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as double,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as double,buckets: null == buckets ? _self._buckets : buckets // ignore: cast_nullable_to_non_nullable
as Map<String, double>,advances: null == advances ? _self.advances : advances // ignore: cast_nullable_to_non_nullable
as double,rows: null == rows ? _self._rows : rows // ignore: cast_nullable_to_non_nullable
as List<SettlementRow>,method: null == method ? _self.method : method // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

/// Create a copy of FinSettlements
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
mixin _$Anomaly {

 String get kind; String get title; String get detail; double? get amount; String? get date;/// Путь веба: /finance/accounts/{code}, /finance/entries?…, /finance/documents/{id}.
 String? get link;/// 3 — критично, 2 — внимание, 1 — к сведению.
 int get severity;
/// Create a copy of Anomaly
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AnomalyCopyWith<Anomaly> get copyWith => _$AnomalyCopyWithImpl<Anomaly>(this as Anomaly, _$identity);

  /// Serializes this Anomaly to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as Anomaly;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is Anomaly&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.detail, _this.detail) || other.detail == _this.detail)&&(identical(other.amount, _this.amount) || other.amount == _this.amount)&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.link, _this.link) || other.link == _this.link)&&(identical(other.severity, _this.severity) || other.severity == _this.severity));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as Anomaly;
  return Object.hash(runtimeType,_this.kind,_this.title,_this.detail,_this.amount,_this.date,_this.link,_this.severity);
}

@override
String toString() {
  final _this = this as Anomaly;
  return 'Anomaly(kind: ${_this.kind}, title: ${_this.title}, detail: ${_this.detail}, amount: ${_this.amount}, date: ${_this.date}, link: ${_this.link}, severity: ${_this.severity})';
}


}

/// @nodoc
abstract mixin class $AnomalyCopyWith<$Res>  {
  factory $AnomalyCopyWith(Anomaly value, $Res Function(Anomaly) _then) = _$AnomalyCopyWithImpl;
@useResult
$Res call({
 String kind, String title, String detail, double? amount, String? date, String? link, int severity
});




}
/// @nodoc
class _$AnomalyCopyWithImpl<$Res>
    implements $AnomalyCopyWith<$Res> {
  _$AnomalyCopyWithImpl(this._self, this._then);

  final Anomaly _self;
  final $Res Function(Anomaly) _then;

/// Create a copy of Anomaly
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? kind = null,Object? title = null,Object? detail = null,Object? amount = freezed,Object? date = freezed,Object? link = freezed,Object? severity = null,}) {
  return _then(Anomaly(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,detail: null == detail ? _self.detail : detail // ignore: cast_nullable_to_non_nullable
as String,amount: freezed == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double?,date: freezed == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String?,link: freezed == link ? _self.link : link // ignore: cast_nullable_to_non_nullable
as String?,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [Anomaly].
extension AnomalyPatterns on Anomaly {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _Anomaly value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _Anomaly() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _Anomaly value)  $default,){
final _that = this;
switch (_that) {
case _Anomaly():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _Anomaly value)?  $default,){
final _that = this;
switch (_that) {
case _Anomaly() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String kind,  String title,  String detail,  double? amount,  String? date,  String? link,  int severity)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _Anomaly() when $default != null:
return $default(_that.kind,_that.title,_that.detail,_that.amount,_that.date,_that.link,_that.severity);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String kind,  String title,  String detail,  double? amount,  String? date,  String? link,  int severity)  $default,) {final _that = this;
switch (_that) {
case _Anomaly():
return $default(_that.kind,_that.title,_that.detail,_that.amount,_that.date,_that.link,_that.severity);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String kind,  String title,  String detail,  double? amount,  String? date,  String? link,  int severity)?  $default,) {final _that = this;
switch (_that) {
case _Anomaly() when $default != null:
return $default(_that.kind,_that.title,_that.detail,_that.amount,_that.date,_that.link,_that.severity);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _Anomaly implements Anomaly {
  const _Anomaly({required this.kind, required this.title, this.detail = '', this.amount, this.date, this.link, this.severity = 1});
  factory _Anomaly.fromJson(Map<String, dynamic> json) => _$AnomalyFromJson(json);

@override final  String kind;
@override final  String title;
@override@JsonKey() final  String detail;
@override final  double? amount;
@override final  String? date;
/// Путь веба: /finance/accounts/{code}, /finance/entries?…, /finance/documents/{id}.
@override final  String? link;
/// 3 — критично, 2 — внимание, 1 — к сведению.
@override@JsonKey() final  int severity;

/// Create a copy of Anomaly
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AnomalyCopyWith<_Anomaly> get copyWith => __$AnomalyCopyWithImpl<_Anomaly>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AnomalyToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _Anomaly&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.title, title) || other.title == title)&&(identical(other.detail, detail) || other.detail == detail)&&(identical(other.amount, amount) || other.amount == amount)&&(identical(other.date, date) || other.date == date)&&(identical(other.link, link) || other.link == link)&&(identical(other.severity, severity) || other.severity == severity));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,kind,title,detail,amount,date,link,severity);
}

@override
String toString() {
    return 'Anomaly(kind: $kind, title: $title, detail: $detail, amount: $amount, date: $date, link: $link, severity: $severity)';
}


}

/// @nodoc
abstract mixin class _$AnomalyCopyWith<$Res> implements $AnomalyCopyWith<$Res> {
  factory _$AnomalyCopyWith(_Anomaly value, $Res Function(_Anomaly) _then) = __$AnomalyCopyWithImpl;
@override @useResult
$Res call({
 String kind, String title, String detail, double? amount, String? date, String? link, int severity
});




}
/// @nodoc
class __$AnomalyCopyWithImpl<$Res>
    implements _$AnomalyCopyWith<$Res> {
  __$AnomalyCopyWithImpl(this._self, this._then);

  final _Anomaly _self;
  final $Res Function(_Anomaly) _then;

/// Create a copy of Anomaly
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? kind = null,Object? title = null,Object? detail = null,Object? amount = freezed,Object? date = freezed,Object? link = freezed,Object? severity = null,}) {
  return _then(_Anomaly(
kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,detail: null == detail ? _self.detail : detail // ignore: cast_nullable_to_non_nullable
as String,amount: freezed == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double?,date: freezed == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String?,link: freezed == link ? _self.link : link // ignore: cast_nullable_to_non_nullable
as String?,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$FinAnomalies {

 FinanceMeta get meta; List<Anomaly> get items; List<String> get checks;
/// Create a copy of FinAnomalies
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinAnomaliesCopyWith<FinAnomalies> get copyWith => _$FinAnomaliesCopyWithImpl<FinAnomalies>(this as FinAnomalies, _$identity);

  /// Serializes this FinAnomalies to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinAnomalies;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinAnomalies&&(identical(other.meta, _this.meta) || other.meta == _this.meta)&&const DeepCollectionEquality().equals(other.items, _this.items)&&const DeepCollectionEquality().equals(other.checks, _this.checks));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinAnomalies;
  return Object.hash(runtimeType,_this.meta,const DeepCollectionEquality().hash(_this.items),const DeepCollectionEquality().hash(_this.checks));
}

@override
String toString() {
  final _this = this as FinAnomalies;
  return 'FinAnomalies(meta: ${_this.meta}, items: ${_this.items}, checks: ${_this.checks})';
}


}

/// @nodoc
abstract mixin class $FinAnomaliesCopyWith<$Res>  {
  factory $FinAnomaliesCopyWith(FinAnomalies value, $Res Function(FinAnomalies) _then) = _$FinAnomaliesCopyWithImpl;
@useResult
$Res call({
 FinanceMeta meta, List<Anomaly> items, List<String> checks
});


$FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class _$FinAnomaliesCopyWithImpl<$Res>
    implements $FinAnomaliesCopyWith<$Res> {
  _$FinAnomaliesCopyWithImpl(this._self, this._then);

  final FinAnomalies _self;
  final $Res Function(FinAnomalies) _then;

/// Create a copy of FinAnomalies
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? meta = null,Object? items = null,Object? checks = null,}) {
  return _then(FinAnomalies(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,items: null == items ? _self.items : items // ignore: cast_nullable_to_non_nullable
as List<Anomaly>,checks: null == checks ? _self.checks : checks // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}
/// Create a copy of FinAnomalies
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}
}


/// Adds pattern-matching-related methods to [FinAnomalies].
extension FinAnomaliesPatterns on FinAnomalies {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinAnomalies value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinAnomalies() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinAnomalies value)  $default,){
final _that = this;
switch (_that) {
case _FinAnomalies():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinAnomalies value)?  $default,){
final _that = this;
switch (_that) {
case _FinAnomalies() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<Anomaly> items,  List<String> checks)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinAnomalies() when $default != null:
return $default(_that.meta,_that.items,_that.checks);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<Anomaly> items,  List<String> checks)  $default,) {final _that = this;
switch (_that) {
case _FinAnomalies():
return $default(_that.meta,_that.items,_that.checks);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FinanceMeta meta,  List<Anomaly> items,  List<String> checks)?  $default,) {final _that = this;
switch (_that) {
case _FinAnomalies() when $default != null:
return $default(_that.meta,_that.items,_that.checks);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinAnomalies implements FinAnomalies {
  const _FinAnomalies({required this.meta,  List<Anomaly> items = const <Anomaly>[],  List<String> checks = const <String>[]}): _items = items,_checks = checks;
  factory _FinAnomalies.fromJson(Map<String, dynamic> json) => _$FinAnomaliesFromJson(json);

@override final  FinanceMeta meta;
 final  List<Anomaly> _items;
@override@JsonKey() List<Anomaly> get items {
  if (_items is EqualUnmodifiableListView) return _items;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_items);
}

 final  List<String> _checks;
@override@JsonKey() List<String> get checks {
  if (_checks is EqualUnmodifiableListView) return _checks;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_checks);
}


/// Create a copy of FinAnomalies
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinAnomaliesCopyWith<_FinAnomalies> get copyWith => __$FinAnomaliesCopyWithImpl<_FinAnomalies>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinAnomaliesToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinAnomalies&&(identical(other.meta, meta) || other.meta == meta)&&const DeepCollectionEquality().equals(other.items, _items)&&const DeepCollectionEquality().equals(other.checks, _checks));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,meta,const DeepCollectionEquality().hash(_items),const DeepCollectionEquality().hash(_checks));
}

@override
String toString() {
    return 'FinAnomalies(meta: $meta, items: $items, checks: $checks)';
}


}

/// @nodoc
abstract mixin class _$FinAnomaliesCopyWith<$Res> implements $FinAnomaliesCopyWith<$Res> {
  factory _$FinAnomaliesCopyWith(_FinAnomalies value, $Res Function(_FinAnomalies) _then) = __$FinAnomaliesCopyWithImpl;
@override @useResult
$Res call({
 FinanceMeta meta, List<Anomaly> items, List<String> checks
});


@override $FinanceMetaCopyWith<$Res> get meta;

}
/// @nodoc
class __$FinAnomaliesCopyWithImpl<$Res>
    implements _$FinAnomaliesCopyWith<$Res> {
  __$FinAnomaliesCopyWithImpl(this._self, this._then);

  final _FinAnomalies _self;
  final $Res Function(_FinAnomalies) _then;

/// Create a copy of FinAnomalies
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? meta = null,Object? items = null,Object? checks = null,}) {
  return _then(_FinAnomalies(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,items: null == items ? _self._items : items // ignore: cast_nullable_to_non_nullable
as List<Anomaly>,checks: null == checks ? _self._checks : checks // ignore: cast_nullable_to_non_nullable
as List<String>,
  ));
}

/// Create a copy of FinAnomalies
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
mixin _$EntryView {

 String get id; String get date; String get debitAccount; String? get debitName; String get creditAccount; String? get creditName; double get amount; String? get content; String? get counterparty; String? get documentId; String get registrarType; String? get registrarNumber;
/// Create a copy of EntryView
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$EntryViewCopyWith<EntryView> get copyWith => _$EntryViewCopyWithImpl<EntryView>(this as EntryView, _$identity);

  /// Serializes this EntryView to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as EntryView;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is EntryView&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.debitAccount, _this.debitAccount) || other.debitAccount == _this.debitAccount)&&(identical(other.debitName, _this.debitName) || other.debitName == _this.debitName)&&(identical(other.creditAccount, _this.creditAccount) || other.creditAccount == _this.creditAccount)&&(identical(other.creditName, _this.creditName) || other.creditName == _this.creditName)&&(identical(other.amount, _this.amount) || other.amount == _this.amount)&&(identical(other.content, _this.content) || other.content == _this.content)&&(identical(other.counterparty, _this.counterparty) || other.counterparty == _this.counterparty)&&(identical(other.documentId, _this.documentId) || other.documentId == _this.documentId)&&(identical(other.registrarType, _this.registrarType) || other.registrarType == _this.registrarType)&&(identical(other.registrarNumber, _this.registrarNumber) || other.registrarNumber == _this.registrarNumber));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as EntryView;
  return Object.hash(runtimeType,_this.id,_this.date,_this.debitAccount,_this.debitName,_this.creditAccount,_this.creditName,_this.amount,_this.content,_this.counterparty,_this.documentId,_this.registrarType,_this.registrarNumber);
}

@override
String toString() {
  final _this = this as EntryView;
  return 'EntryView(id: ${_this.id}, date: ${_this.date}, debitAccount: ${_this.debitAccount}, debitName: ${_this.debitName}, creditAccount: ${_this.creditAccount}, creditName: ${_this.creditName}, amount: ${_this.amount}, content: ${_this.content}, counterparty: ${_this.counterparty}, documentId: ${_this.documentId}, registrarType: ${_this.registrarType}, registrarNumber: ${_this.registrarNumber})';
}


}

/// @nodoc
abstract mixin class $EntryViewCopyWith<$Res>  {
  factory $EntryViewCopyWith(EntryView value, $Res Function(EntryView) _then) = _$EntryViewCopyWithImpl;
@useResult
$Res call({
 String id, String date, String debitAccount, String? debitName, String creditAccount, String? creditName, double amount, String? content, String? counterparty, String? documentId, String registrarType, String? registrarNumber
});




}
/// @nodoc
class _$EntryViewCopyWithImpl<$Res>
    implements $EntryViewCopyWith<$Res> {
  _$EntryViewCopyWithImpl(this._self, this._then);

  final EntryView _self;
  final $Res Function(EntryView) _then;

/// Create a copy of EntryView
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? date = null,Object? debitAccount = null,Object? debitName = freezed,Object? creditAccount = null,Object? creditName = freezed,Object? amount = null,Object? content = freezed,Object? counterparty = freezed,Object? documentId = freezed,Object? registrarType = null,Object? registrarNumber = freezed,}) {
  return _then(EntryView(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,debitAccount: null == debitAccount ? _self.debitAccount : debitAccount // ignore: cast_nullable_to_non_nullable
as String,debitName: freezed == debitName ? _self.debitName : debitName // ignore: cast_nullable_to_non_nullable
as String?,creditAccount: null == creditAccount ? _self.creditAccount : creditAccount // ignore: cast_nullable_to_non_nullable
as String,creditName: freezed == creditName ? _self.creditName : creditName // ignore: cast_nullable_to_non_nullable
as String?,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,content: freezed == content ? _self.content : content // ignore: cast_nullable_to_non_nullable
as String?,counterparty: freezed == counterparty ? _self.counterparty : counterparty // ignore: cast_nullable_to_non_nullable
as String?,documentId: freezed == documentId ? _self.documentId : documentId // ignore: cast_nullable_to_non_nullable
as String?,registrarType: null == registrarType ? _self.registrarType : registrarType // ignore: cast_nullable_to_non_nullable
as String,registrarNumber: freezed == registrarNumber ? _self.registrarNumber : registrarNumber // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [EntryView].
extension EntryViewPatterns on EntryView {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _EntryView value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _EntryView() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _EntryView value)  $default,){
final _that = this;
switch (_that) {
case _EntryView():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _EntryView value)?  $default,){
final _that = this;
switch (_that) {
case _EntryView() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String date,  String debitAccount,  String? debitName,  String creditAccount,  String? creditName,  double amount,  String? content,  String? counterparty,  String? documentId,  String registrarType,  String? registrarNumber)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _EntryView() when $default != null:
return $default(_that.id,_that.date,_that.debitAccount,_that.debitName,_that.creditAccount,_that.creditName,_that.amount,_that.content,_that.counterparty,_that.documentId,_that.registrarType,_that.registrarNumber);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String date,  String debitAccount,  String? debitName,  String creditAccount,  String? creditName,  double amount,  String? content,  String? counterparty,  String? documentId,  String registrarType,  String? registrarNumber)  $default,) {final _that = this;
switch (_that) {
case _EntryView():
return $default(_that.id,_that.date,_that.debitAccount,_that.debitName,_that.creditAccount,_that.creditName,_that.amount,_that.content,_that.counterparty,_that.documentId,_that.registrarType,_that.registrarNumber);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String date,  String debitAccount,  String? debitName,  String creditAccount,  String? creditName,  double amount,  String? content,  String? counterparty,  String? documentId,  String registrarType,  String? registrarNumber)?  $default,) {final _that = this;
switch (_that) {
case _EntryView() when $default != null:
return $default(_that.id,_that.date,_that.debitAccount,_that.debitName,_that.creditAccount,_that.creditName,_that.amount,_that.content,_that.counterparty,_that.documentId,_that.registrarType,_that.registrarNumber);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _EntryView implements EntryView {
  const _EntryView({required this.id, required this.date, required this.debitAccount, this.debitName, required this.creditAccount, this.creditName, this.amount = 0, this.content, this.counterparty, this.documentId, this.registrarType = '', this.registrarNumber});
  factory _EntryView.fromJson(Map<String, dynamic> json) => _$EntryViewFromJson(json);

@override final  String id;
@override final  String date;
@override final  String debitAccount;
@override final  String? debitName;
@override final  String creditAccount;
@override final  String? creditName;
@override@JsonKey() final  double amount;
@override final  String? content;
@override final  String? counterparty;
@override final  String? documentId;
@override@JsonKey() final  String registrarType;
@override final  String? registrarNumber;

/// Create a copy of EntryView
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$EntryViewCopyWith<_EntryView> get copyWith => __$EntryViewCopyWithImpl<_EntryView>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$EntryViewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _EntryView&&(identical(other.id, id) || other.id == id)&&(identical(other.date, date) || other.date == date)&&(identical(other.debitAccount, debitAccount) || other.debitAccount == debitAccount)&&(identical(other.debitName, debitName) || other.debitName == debitName)&&(identical(other.creditAccount, creditAccount) || other.creditAccount == creditAccount)&&(identical(other.creditName, creditName) || other.creditName == creditName)&&(identical(other.amount, amount) || other.amount == amount)&&(identical(other.content, content) || other.content == content)&&(identical(other.counterparty, counterparty) || other.counterparty == counterparty)&&(identical(other.documentId, documentId) || other.documentId == documentId)&&(identical(other.registrarType, registrarType) || other.registrarType == registrarType)&&(identical(other.registrarNumber, registrarNumber) || other.registrarNumber == registrarNumber));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,date,debitAccount,debitName,creditAccount,creditName,amount,content,counterparty,documentId,registrarType,registrarNumber);
}

@override
String toString() {
    return 'EntryView(id: $id, date: $date, debitAccount: $debitAccount, debitName: $debitName, creditAccount: $creditAccount, creditName: $creditName, amount: $amount, content: $content, counterparty: $counterparty, documentId: $documentId, registrarType: $registrarType, registrarNumber: $registrarNumber)';
}


}

/// @nodoc
abstract mixin class _$EntryViewCopyWith<$Res> implements $EntryViewCopyWith<$Res> {
  factory _$EntryViewCopyWith(_EntryView value, $Res Function(_EntryView) _then) = __$EntryViewCopyWithImpl;
@override @useResult
$Res call({
 String id, String date, String debitAccount, String? debitName, String creditAccount, String? creditName, double amount, String? content, String? counterparty, String? documentId, String registrarType, String? registrarNumber
});




}
/// @nodoc
class __$EntryViewCopyWithImpl<$Res>
    implements _$EntryViewCopyWith<$Res> {
  __$EntryViewCopyWithImpl(this._self, this._then);

  final _EntryView _self;
  final $Res Function(_EntryView) _then;

/// Create a copy of EntryView
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? date = null,Object? debitAccount = null,Object? debitName = freezed,Object? creditAccount = null,Object? creditName = freezed,Object? amount = null,Object? content = freezed,Object? counterparty = freezed,Object? documentId = freezed,Object? registrarType = null,Object? registrarNumber = freezed,}) {
  return _then(_EntryView(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,debitAccount: null == debitAccount ? _self.debitAccount : debitAccount // ignore: cast_nullable_to_non_nullable
as String,debitName: freezed == debitName ? _self.debitName : debitName // ignore: cast_nullable_to_non_nullable
as String?,creditAccount: null == creditAccount ? _self.creditAccount : creditAccount // ignore: cast_nullable_to_non_nullable
as String,creditName: freezed == creditName ? _self.creditName : creditName // ignore: cast_nullable_to_non_nullable
as String?,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,content: freezed == content ? _self.content : content // ignore: cast_nullable_to_non_nullable
as String?,counterparty: freezed == counterparty ? _self.counterparty : counterparty // ignore: cast_nullable_to_non_nullable
as String?,documentId: freezed == documentId ? _self.documentId : documentId // ignore: cast_nullable_to_non_nullable
as String?,registrarType: null == registrarType ? _self.registrarType : registrarType // ignore: cast_nullable_to_non_nullable
as String,registrarNumber: freezed == registrarNumber ? _self.registrarNumber : registrarNumber // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}


/// @nodoc
mixin _$DocLine {

 int get line; String? get item; double get quantity; String? get unit; double? get price; double get amount;
/// Create a copy of DocLine
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$DocLineCopyWith<DocLine> get copyWith => _$DocLineCopyWithImpl<DocLine>(this as DocLine, _$identity);

  /// Serializes this DocLine to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as DocLine;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is DocLine&&(identical(other.line, _this.line) || other.line == _this.line)&&(identical(other.item, _this.item) || other.item == _this.item)&&(identical(other.quantity, _this.quantity) || other.quantity == _this.quantity)&&(identical(other.unit, _this.unit) || other.unit == _this.unit)&&(identical(other.price, _this.price) || other.price == _this.price)&&(identical(other.amount, _this.amount) || other.amount == _this.amount));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as DocLine;
  return Object.hash(runtimeType,_this.line,_this.item,_this.quantity,_this.unit,_this.price,_this.amount);
}

@override
String toString() {
  final _this = this as DocLine;
  return 'DocLine(line: ${_this.line}, item: ${_this.item}, quantity: ${_this.quantity}, unit: ${_this.unit}, price: ${_this.price}, amount: ${_this.amount})';
}


}

/// @nodoc
abstract mixin class $DocLineCopyWith<$Res>  {
  factory $DocLineCopyWith(DocLine value, $Res Function(DocLine) _then) = _$DocLineCopyWithImpl;
@useResult
$Res call({
 int line, String? item, double quantity, String? unit, double? price, double amount
});




}
/// @nodoc
class _$DocLineCopyWithImpl<$Res>
    implements $DocLineCopyWith<$Res> {
  _$DocLineCopyWithImpl(this._self, this._then);

  final DocLine _self;
  final $Res Function(DocLine) _then;

/// Create a copy of DocLine
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? line = null,Object? item = freezed,Object? quantity = null,Object? unit = freezed,Object? price = freezed,Object? amount = null,}) {
  return _then(DocLine(
line: null == line ? _self.line : line // ignore: cast_nullable_to_non_nullable
as int,item: freezed == item ? _self.item : item // ignore: cast_nullable_to_non_nullable
as String?,quantity: null == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double,unit: freezed == unit ? _self.unit : unit // ignore: cast_nullable_to_non_nullable
as String?,price: freezed == price ? _self.price : price // ignore: cast_nullable_to_non_nullable
as double?,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [DocLine].
extension DocLinePatterns on DocLine {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _DocLine value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _DocLine() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _DocLine value)  $default,){
final _that = this;
switch (_that) {
case _DocLine():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _DocLine value)?  $default,){
final _that = this;
switch (_that) {
case _DocLine() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int line,  String? item,  double quantity,  String? unit,  double? price,  double amount)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _DocLine() when $default != null:
return $default(_that.line,_that.item,_that.quantity,_that.unit,_that.price,_that.amount);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int line,  String? item,  double quantity,  String? unit,  double? price,  double amount)  $default,) {final _that = this;
switch (_that) {
case _DocLine():
return $default(_that.line,_that.item,_that.quantity,_that.unit,_that.price,_that.amount);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int line,  String? item,  double quantity,  String? unit,  double? price,  double amount)?  $default,) {final _that = this;
switch (_that) {
case _DocLine() when $default != null:
return $default(_that.line,_that.item,_that.quantity,_that.unit,_that.price,_that.amount);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _DocLine implements DocLine {
  const _DocLine({required this.line, this.item, this.quantity = 0, this.unit, this.price, this.amount = 0});
  factory _DocLine.fromJson(Map<String, dynamic> json) => _$DocLineFromJson(json);

@override final  int line;
@override final  String? item;
@override@JsonKey() final  double quantity;
@override final  String? unit;
@override final  double? price;
@override@JsonKey() final  double amount;

/// Create a copy of DocLine
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$DocLineCopyWith<_DocLine> get copyWith => __$DocLineCopyWithImpl<_DocLine>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$DocLineToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _DocLine&&(identical(other.line, line) || other.line == line)&&(identical(other.item, item) || other.item == item)&&(identical(other.quantity, quantity) || other.quantity == quantity)&&(identical(other.unit, unit) || other.unit == unit)&&(identical(other.price, price) || other.price == price)&&(identical(other.amount, amount) || other.amount == amount));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,line,item,quantity,unit,price,amount);
}

@override
String toString() {
    return 'DocLine(line: $line, item: $item, quantity: $quantity, unit: $unit, price: $price, amount: $amount)';
}


}

/// @nodoc
abstract mixin class _$DocLineCopyWith<$Res> implements $DocLineCopyWith<$Res> {
  factory _$DocLineCopyWith(_DocLine value, $Res Function(_DocLine) _then) = __$DocLineCopyWithImpl;
@override @useResult
$Res call({
 int line, String? item, double quantity, String? unit, double? price, double amount
});




}
/// @nodoc
class __$DocLineCopyWithImpl<$Res>
    implements _$DocLineCopyWith<$Res> {
  __$DocLineCopyWithImpl(this._self, this._then);

  final _DocLine _self;
  final $Res Function(_DocLine) _then;

/// Create a copy of DocLine
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? line = null,Object? item = freezed,Object? quantity = null,Object? unit = freezed,Object? price = freezed,Object? amount = null,}) {
  return _then(_DocLine(
line: null == line ? _self.line : line // ignore: cast_nullable_to_non_nullable
as int,item: freezed == item ? _self.item : item // ignore: cast_nullable_to_non_nullable
as String?,quantity: null == quantity ? _self.quantity : quantity // ignore: cast_nullable_to_non_nullable
as double,unit: freezed == unit ? _self.unit : unit // ignore: cast_nullable_to_non_nullable
as String?,price: freezed == price ? _self.price : price // ignore: cast_nullable_to_non_nullable
as double?,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$FinDocument {

 String get id; String get type; String get sourceType; String get number; String get date; bool get posted; String? get organization; String? get counterparty; String? get contract; String? get warehouse; String get currency; double get amount; double? get vatAmount; String? get purpose; String? get comment; List<DocLine> get lines; List<EntryView> get entries;
/// Create a copy of FinDocument
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinDocumentCopyWith<FinDocument> get copyWith => _$FinDocumentCopyWithImpl<FinDocument>(this as FinDocument, _$identity);

  /// Serializes this FinDocument to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinDocument;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinDocument&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.type, _this.type) || other.type == _this.type)&&(identical(other.sourceType, _this.sourceType) || other.sourceType == _this.sourceType)&&(identical(other.number, _this.number) || other.number == _this.number)&&(identical(other.date, _this.date) || other.date == _this.date)&&(identical(other.posted, _this.posted) || other.posted == _this.posted)&&(identical(other.organization, _this.organization) || other.organization == _this.organization)&&(identical(other.counterparty, _this.counterparty) || other.counterparty == _this.counterparty)&&(identical(other.contract, _this.contract) || other.contract == _this.contract)&&(identical(other.warehouse, _this.warehouse) || other.warehouse == _this.warehouse)&&(identical(other.currency, _this.currency) || other.currency == _this.currency)&&(identical(other.amount, _this.amount) || other.amount == _this.amount)&&(identical(other.vatAmount, _this.vatAmount) || other.vatAmount == _this.vatAmount)&&(identical(other.purpose, _this.purpose) || other.purpose == _this.purpose)&&(identical(other.comment, _this.comment) || other.comment == _this.comment)&&const DeepCollectionEquality().equals(other.lines, _this.lines)&&const DeepCollectionEquality().equals(other.entries, _this.entries));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinDocument;
  return Object.hash(runtimeType,_this.id,_this.type,_this.sourceType,_this.number,_this.date,_this.posted,_this.organization,_this.counterparty,_this.contract,_this.warehouse,_this.currency,_this.amount,_this.vatAmount,_this.purpose,_this.comment,const DeepCollectionEquality().hash(_this.lines),const DeepCollectionEquality().hash(_this.entries));
}

@override
String toString() {
  final _this = this as FinDocument;
  return 'FinDocument(id: ${_this.id}, type: ${_this.type}, sourceType: ${_this.sourceType}, number: ${_this.number}, date: ${_this.date}, posted: ${_this.posted}, organization: ${_this.organization}, counterparty: ${_this.counterparty}, contract: ${_this.contract}, warehouse: ${_this.warehouse}, currency: ${_this.currency}, amount: ${_this.amount}, vatAmount: ${_this.vatAmount}, purpose: ${_this.purpose}, comment: ${_this.comment}, lines: ${_this.lines}, entries: ${_this.entries})';
}


}

/// @nodoc
abstract mixin class $FinDocumentCopyWith<$Res>  {
  factory $FinDocumentCopyWith(FinDocument value, $Res Function(FinDocument) _then) = _$FinDocumentCopyWithImpl;
@useResult
$Res call({
 String id, String type, String sourceType, String number, String date, bool posted, String? organization, String? counterparty, String? contract, String? warehouse, String currency, double amount, double? vatAmount, String? purpose, String? comment, List<DocLine> lines, List<EntryView> entries
});




}
/// @nodoc
class _$FinDocumentCopyWithImpl<$Res>
    implements $FinDocumentCopyWith<$Res> {
  _$FinDocumentCopyWithImpl(this._self, this._then);

  final FinDocument _self;
  final $Res Function(FinDocument) _then;

/// Create a copy of FinDocument
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? type = null,Object? sourceType = null,Object? number = null,Object? date = null,Object? posted = null,Object? organization = freezed,Object? counterparty = freezed,Object? contract = freezed,Object? warehouse = freezed,Object? currency = null,Object? amount = null,Object? vatAmount = freezed,Object? purpose = freezed,Object? comment = freezed,Object? lines = null,Object? entries = null,}) {
  return _then(FinDocument(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,type: null == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String,sourceType: null == sourceType ? _self.sourceType : sourceType // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,posted: null == posted ? _self.posted : posted // ignore: cast_nullable_to_non_nullable
as bool,organization: freezed == organization ? _self.organization : organization // ignore: cast_nullable_to_non_nullable
as String?,counterparty: freezed == counterparty ? _self.counterparty : counterparty // ignore: cast_nullable_to_non_nullable
as String?,contract: freezed == contract ? _self.contract : contract // ignore: cast_nullable_to_non_nullable
as String?,warehouse: freezed == warehouse ? _self.warehouse : warehouse // ignore: cast_nullable_to_non_nullable
as String?,currency: null == currency ? _self.currency : currency // ignore: cast_nullable_to_non_nullable
as String,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,vatAmount: freezed == vatAmount ? _self.vatAmount : vatAmount // ignore: cast_nullable_to_non_nullable
as double?,purpose: freezed == purpose ? _self.purpose : purpose // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,lines: null == lines ? _self.lines : lines // ignore: cast_nullable_to_non_nullable
as List<DocLine>,entries: null == entries ? _self.entries : entries // ignore: cast_nullable_to_non_nullable
as List<EntryView>,
  ));
}

}


/// Adds pattern-matching-related methods to [FinDocument].
extension FinDocumentPatterns on FinDocument {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinDocument value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinDocument() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinDocument value)  $default,){
final _that = this;
switch (_that) {
case _FinDocument():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinDocument value)?  $default,){
final _that = this;
switch (_that) {
case _FinDocument() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String type,  String sourceType,  String number,  String date,  bool posted,  String? organization,  String? counterparty,  String? contract,  String? warehouse,  String currency,  double amount,  double? vatAmount,  String? purpose,  String? comment,  List<DocLine> lines,  List<EntryView> entries)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinDocument() when $default != null:
return $default(_that.id,_that.type,_that.sourceType,_that.number,_that.date,_that.posted,_that.organization,_that.counterparty,_that.contract,_that.warehouse,_that.currency,_that.amount,_that.vatAmount,_that.purpose,_that.comment,_that.lines,_that.entries);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String type,  String sourceType,  String number,  String date,  bool posted,  String? organization,  String? counterparty,  String? contract,  String? warehouse,  String currency,  double amount,  double? vatAmount,  String? purpose,  String? comment,  List<DocLine> lines,  List<EntryView> entries)  $default,) {final _that = this;
switch (_that) {
case _FinDocument():
return $default(_that.id,_that.type,_that.sourceType,_that.number,_that.date,_that.posted,_that.organization,_that.counterparty,_that.contract,_that.warehouse,_that.currency,_that.amount,_that.vatAmount,_that.purpose,_that.comment,_that.lines,_that.entries);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String type,  String sourceType,  String number,  String date,  bool posted,  String? organization,  String? counterparty,  String? contract,  String? warehouse,  String currency,  double amount,  double? vatAmount,  String? purpose,  String? comment,  List<DocLine> lines,  List<EntryView> entries)?  $default,) {final _that = this;
switch (_that) {
case _FinDocument() when $default != null:
return $default(_that.id,_that.type,_that.sourceType,_that.number,_that.date,_that.posted,_that.organization,_that.counterparty,_that.contract,_that.warehouse,_that.currency,_that.amount,_that.vatAmount,_that.purpose,_that.comment,_that.lines,_that.entries);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinDocument implements FinDocument {
  const _FinDocument({required this.id, required this.type, this.sourceType = '', this.number = '', required this.date, this.posted = false, this.organization, this.counterparty, this.contract, this.warehouse, this.currency = 'UZS', this.amount = 0, this.vatAmount, this.purpose, this.comment,  List<DocLine> lines = const <DocLine>[],  List<EntryView> entries = const <EntryView>[]}): _lines = lines,_entries = entries;
  factory _FinDocument.fromJson(Map<String, dynamic> json) => _$FinDocumentFromJson(json);

@override final  String id;
@override final  String type;
@override@JsonKey() final  String sourceType;
@override@JsonKey() final  String number;
@override final  String date;
@override@JsonKey() final  bool posted;
@override final  String? organization;
@override final  String? counterparty;
@override final  String? contract;
@override final  String? warehouse;
@override@JsonKey() final  String currency;
@override@JsonKey() final  double amount;
@override final  double? vatAmount;
@override final  String? purpose;
@override final  String? comment;
 final  List<DocLine> _lines;
@override@JsonKey() List<DocLine> get lines {
  if (_lines is EqualUnmodifiableListView) return _lines;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_lines);
}

 final  List<EntryView> _entries;
@override@JsonKey() List<EntryView> get entries {
  if (_entries is EqualUnmodifiableListView) return _entries;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_entries);
}


/// Create a copy of FinDocument
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinDocumentCopyWith<_FinDocument> get copyWith => __$FinDocumentCopyWithImpl<_FinDocument>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinDocumentToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinDocument&&(identical(other.id, id) || other.id == id)&&(identical(other.type, type) || other.type == type)&&(identical(other.sourceType, sourceType) || other.sourceType == sourceType)&&(identical(other.number, number) || other.number == number)&&(identical(other.date, date) || other.date == date)&&(identical(other.posted, posted) || other.posted == posted)&&(identical(other.organization, organization) || other.organization == organization)&&(identical(other.counterparty, counterparty) || other.counterparty == counterparty)&&(identical(other.contract, contract) || other.contract == contract)&&(identical(other.warehouse, warehouse) || other.warehouse == warehouse)&&(identical(other.currency, currency) || other.currency == currency)&&(identical(other.amount, amount) || other.amount == amount)&&(identical(other.vatAmount, vatAmount) || other.vatAmount == vatAmount)&&(identical(other.purpose, purpose) || other.purpose == purpose)&&(identical(other.comment, comment) || other.comment == comment)&&const DeepCollectionEquality().equals(other.lines, _lines)&&const DeepCollectionEquality().equals(other.entries, _entries));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,type,sourceType,number,date,posted,organization,counterparty,contract,warehouse,currency,amount,vatAmount,purpose,comment,const DeepCollectionEquality().hash(_lines),const DeepCollectionEquality().hash(_entries));
}

@override
String toString() {
    return 'FinDocument(id: $id, type: $type, sourceType: $sourceType, number: $number, date: $date, posted: $posted, organization: $organization, counterparty: $counterparty, contract: $contract, warehouse: $warehouse, currency: $currency, amount: $amount, vatAmount: $vatAmount, purpose: $purpose, comment: $comment, lines: $lines, entries: $entries)';
}


}

/// @nodoc
abstract mixin class _$FinDocumentCopyWith<$Res> implements $FinDocumentCopyWith<$Res> {
  factory _$FinDocumentCopyWith(_FinDocument value, $Res Function(_FinDocument) _then) = __$FinDocumentCopyWithImpl;
@override @useResult
$Res call({
 String id, String type, String sourceType, String number, String date, bool posted, String? organization, String? counterparty, String? contract, String? warehouse, String currency, double amount, double? vatAmount, String? purpose, String? comment, List<DocLine> lines, List<EntryView> entries
});




}
/// @nodoc
class __$FinDocumentCopyWithImpl<$Res>
    implements _$FinDocumentCopyWith<$Res> {
  __$FinDocumentCopyWithImpl(this._self, this._then);

  final _FinDocument _self;
  final $Res Function(_FinDocument) _then;

/// Create a copy of FinDocument
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? type = null,Object? sourceType = null,Object? number = null,Object? date = null,Object? posted = null,Object? organization = freezed,Object? counterparty = freezed,Object? contract = freezed,Object? warehouse = freezed,Object? currency = null,Object? amount = null,Object? vatAmount = freezed,Object? purpose = freezed,Object? comment = freezed,Object? lines = null,Object? entries = null,}) {
  return _then(_FinDocument(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,type: null == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String,sourceType: null == sourceType ? _self.sourceType : sourceType // ignore: cast_nullable_to_non_nullable
as String,number: null == number ? _self.number : number // ignore: cast_nullable_to_non_nullable
as String,date: null == date ? _self.date : date // ignore: cast_nullable_to_non_nullable
as String,posted: null == posted ? _self.posted : posted // ignore: cast_nullable_to_non_nullable
as bool,organization: freezed == organization ? _self.organization : organization // ignore: cast_nullable_to_non_nullable
as String?,counterparty: freezed == counterparty ? _self.counterparty : counterparty // ignore: cast_nullable_to_non_nullable
as String?,contract: freezed == contract ? _self.contract : contract // ignore: cast_nullable_to_non_nullable
as String?,warehouse: freezed == warehouse ? _self.warehouse : warehouse // ignore: cast_nullable_to_non_nullable
as String?,currency: null == currency ? _self.currency : currency // ignore: cast_nullable_to_non_nullable
as String,amount: null == amount ? _self.amount : amount // ignore: cast_nullable_to_non_nullable
as double,vatAmount: freezed == vatAmount ? _self.vatAmount : vatAmount // ignore: cast_nullable_to_non_nullable
as double?,purpose: freezed == purpose ? _self.purpose : purpose // ignore: cast_nullable_to_non_nullable
as String?,comment: freezed == comment ? _self.comment : comment // ignore: cast_nullable_to_non_nullable
as String?,lines: null == lines ? _self._lines : lines // ignore: cast_nullable_to_non_nullable
as List<DocLine>,entries: null == entries ? _self._entries : entries // ignore: cast_nullable_to_non_nullable
as List<EntryView>,
  ));
}


}


/// @nodoc
mixin _$TrialRow {

 String get account; String get name; double get openingDebit; double get openingCredit; double get turnoverDebit; double get turnoverCredit; double get closingDebit; double get closingCredit;
/// Create a copy of TrialRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TrialRowCopyWith<TrialRow> get copyWith => _$TrialRowCopyWithImpl<TrialRow>(this as TrialRow, _$identity);

  /// Serializes this TrialRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TrialRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TrialRow&&(identical(other.account, _this.account) || other.account == _this.account)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.openingDebit, _this.openingDebit) || other.openingDebit == _this.openingDebit)&&(identical(other.openingCredit, _this.openingCredit) || other.openingCredit == _this.openingCredit)&&(identical(other.turnoverDebit, _this.turnoverDebit) || other.turnoverDebit == _this.turnoverDebit)&&(identical(other.turnoverCredit, _this.turnoverCredit) || other.turnoverCredit == _this.turnoverCredit)&&(identical(other.closingDebit, _this.closingDebit) || other.closingDebit == _this.closingDebit)&&(identical(other.closingCredit, _this.closingCredit) || other.closingCredit == _this.closingCredit));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TrialRow;
  return Object.hash(runtimeType,_this.account,_this.name,_this.openingDebit,_this.openingCredit,_this.turnoverDebit,_this.turnoverCredit,_this.closingDebit,_this.closingCredit);
}

@override
String toString() {
  final _this = this as TrialRow;
  return 'TrialRow(account: ${_this.account}, name: ${_this.name}, openingDebit: ${_this.openingDebit}, openingCredit: ${_this.openingCredit}, turnoverDebit: ${_this.turnoverDebit}, turnoverCredit: ${_this.turnoverCredit}, closingDebit: ${_this.closingDebit}, closingCredit: ${_this.closingCredit})';
}


}

/// @nodoc
abstract mixin class $TrialRowCopyWith<$Res>  {
  factory $TrialRowCopyWith(TrialRow value, $Res Function(TrialRow) _then) = _$TrialRowCopyWithImpl;
@useResult
$Res call({
 String account, String name, double openingDebit, double openingCredit, double turnoverDebit, double turnoverCredit, double closingDebit, double closingCredit
});




}
/// @nodoc
class _$TrialRowCopyWithImpl<$Res>
    implements $TrialRowCopyWith<$Res> {
  _$TrialRowCopyWithImpl(this._self, this._then);

  final TrialRow _self;
  final $Res Function(TrialRow) _then;

/// Create a copy of TrialRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? account = null,Object? name = null,Object? openingDebit = null,Object? openingCredit = null,Object? turnoverDebit = null,Object? turnoverCredit = null,Object? closingDebit = null,Object? closingCredit = null,}) {
  return _then(TrialRow(
account: null == account ? _self.account : account // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,openingDebit: null == openingDebit ? _self.openingDebit : openingDebit // ignore: cast_nullable_to_non_nullable
as double,openingCredit: null == openingCredit ? _self.openingCredit : openingCredit // ignore: cast_nullable_to_non_nullable
as double,turnoverDebit: null == turnoverDebit ? _self.turnoverDebit : turnoverDebit // ignore: cast_nullable_to_non_nullable
as double,turnoverCredit: null == turnoverCredit ? _self.turnoverCredit : turnoverCredit // ignore: cast_nullable_to_non_nullable
as double,closingDebit: null == closingDebit ? _self.closingDebit : closingDebit // ignore: cast_nullable_to_non_nullable
as double,closingCredit: null == closingCredit ? _self.closingCredit : closingCredit // ignore: cast_nullable_to_non_nullable
as double,
  ));
}

}


/// Adds pattern-matching-related methods to [TrialRow].
extension TrialRowPatterns on TrialRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TrialRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TrialRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TrialRow value)  $default,){
final _that = this;
switch (_that) {
case _TrialRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TrialRow value)?  $default,){
final _that = this;
switch (_that) {
case _TrialRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String account,  String name,  double openingDebit,  double openingCredit,  double turnoverDebit,  double turnoverCredit,  double closingDebit,  double closingCredit)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TrialRow() when $default != null:
return $default(_that.account,_that.name,_that.openingDebit,_that.openingCredit,_that.turnoverDebit,_that.turnoverCredit,_that.closingDebit,_that.closingCredit);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String account,  String name,  double openingDebit,  double openingCredit,  double turnoverDebit,  double turnoverCredit,  double closingDebit,  double closingCredit)  $default,) {final _that = this;
switch (_that) {
case _TrialRow():
return $default(_that.account,_that.name,_that.openingDebit,_that.openingCredit,_that.turnoverDebit,_that.turnoverCredit,_that.closingDebit,_that.closingCredit);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String account,  String name,  double openingDebit,  double openingCredit,  double turnoverDebit,  double turnoverCredit,  double closingDebit,  double closingCredit)?  $default,) {final _that = this;
switch (_that) {
case _TrialRow() when $default != null:
return $default(_that.account,_that.name,_that.openingDebit,_that.openingCredit,_that.turnoverDebit,_that.turnoverCredit,_that.closingDebit,_that.closingCredit);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TrialRow implements TrialRow {
  const _TrialRow({required this.account, required this.name, this.openingDebit = 0, this.openingCredit = 0, this.turnoverDebit = 0, this.turnoverCredit = 0, this.closingDebit = 0, this.closingCredit = 0});
  factory _TrialRow.fromJson(Map<String, dynamic> json) => _$TrialRowFromJson(json);

@override final  String account;
@override final  String name;
@override@JsonKey() final  double openingDebit;
@override@JsonKey() final  double openingCredit;
@override@JsonKey() final  double turnoverDebit;
@override@JsonKey() final  double turnoverCredit;
@override@JsonKey() final  double closingDebit;
@override@JsonKey() final  double closingCredit;

/// Create a copy of TrialRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TrialRowCopyWith<_TrialRow> get copyWith => __$TrialRowCopyWithImpl<_TrialRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TrialRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TrialRow&&(identical(other.account, account) || other.account == account)&&(identical(other.name, name) || other.name == name)&&(identical(other.openingDebit, openingDebit) || other.openingDebit == openingDebit)&&(identical(other.openingCredit, openingCredit) || other.openingCredit == openingCredit)&&(identical(other.turnoverDebit, turnoverDebit) || other.turnoverDebit == turnoverDebit)&&(identical(other.turnoverCredit, turnoverCredit) || other.turnoverCredit == turnoverCredit)&&(identical(other.closingDebit, closingDebit) || other.closingDebit == closingDebit)&&(identical(other.closingCredit, closingCredit) || other.closingCredit == closingCredit));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,account,name,openingDebit,openingCredit,turnoverDebit,turnoverCredit,closingDebit,closingCredit);
}

@override
String toString() {
    return 'TrialRow(account: $account, name: $name, openingDebit: $openingDebit, openingCredit: $openingCredit, turnoverDebit: $turnoverDebit, turnoverCredit: $turnoverCredit, closingDebit: $closingDebit, closingCredit: $closingCredit)';
}


}

/// @nodoc
abstract mixin class _$TrialRowCopyWith<$Res> implements $TrialRowCopyWith<$Res> {
  factory _$TrialRowCopyWith(_TrialRow value, $Res Function(_TrialRow) _then) = __$TrialRowCopyWithImpl;
@override @useResult
$Res call({
 String account, String name, double openingDebit, double openingCredit, double turnoverDebit, double turnoverCredit, double closingDebit, double closingCredit
});




}
/// @nodoc
class __$TrialRowCopyWithImpl<$Res>
    implements _$TrialRowCopyWith<$Res> {
  __$TrialRowCopyWithImpl(this._self, this._then);

  final _TrialRow _self;
  final $Res Function(_TrialRow) _then;

/// Create a copy of TrialRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? account = null,Object? name = null,Object? openingDebit = null,Object? openingCredit = null,Object? turnoverDebit = null,Object? turnoverCredit = null,Object? closingDebit = null,Object? closingCredit = null,}) {
  return _then(_TrialRow(
account: null == account ? _self.account : account // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,openingDebit: null == openingDebit ? _self.openingDebit : openingDebit // ignore: cast_nullable_to_non_nullable
as double,openingCredit: null == openingCredit ? _self.openingCredit : openingCredit // ignore: cast_nullable_to_non_nullable
as double,turnoverDebit: null == turnoverDebit ? _self.turnoverDebit : turnoverDebit // ignore: cast_nullable_to_non_nullable
as double,turnoverCredit: null == turnoverCredit ? _self.turnoverCredit : turnoverCredit // ignore: cast_nullable_to_non_nullable
as double,closingDebit: null == closingDebit ? _self.closingDebit : closingDebit // ignore: cast_nullable_to_non_nullable
as double,closingCredit: null == closingCredit ? _self.closingCredit : closingCredit // ignore: cast_nullable_to_non_nullable
as double,
  ));
}


}


/// @nodoc
mixin _$FinAccounts {

 FinanceMeta get meta; List<TrialRow> get rows; TrialRow? get total;
/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$FinAccountsCopyWith<FinAccounts> get copyWith => _$FinAccountsCopyWithImpl<FinAccounts>(this as FinAccounts, _$identity);

  /// Serializes this FinAccounts to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as FinAccounts;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is FinAccounts&&(identical(other.meta, _this.meta) || other.meta == _this.meta)&&const DeepCollectionEquality().equals(other.rows, _this.rows)&&(identical(other.total, _this.total) || other.total == _this.total));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as FinAccounts;
  return Object.hash(runtimeType,_this.meta,const DeepCollectionEquality().hash(_this.rows),_this.total);
}

@override
String toString() {
  final _this = this as FinAccounts;
  return 'FinAccounts(meta: ${_this.meta}, rows: ${_this.rows}, total: ${_this.total})';
}


}

/// @nodoc
abstract mixin class $FinAccountsCopyWith<$Res>  {
  factory $FinAccountsCopyWith(FinAccounts value, $Res Function(FinAccounts) _then) = _$FinAccountsCopyWithImpl;
@useResult
$Res call({
 FinanceMeta meta, List<TrialRow> rows, TrialRow? total
});


$FinanceMetaCopyWith<$Res> get meta;$TrialRowCopyWith<$Res>? get total;

}
/// @nodoc
class _$FinAccountsCopyWithImpl<$Res>
    implements $FinAccountsCopyWith<$Res> {
  _$FinAccountsCopyWithImpl(this._self, this._then);

  final FinAccounts _self;
  final $Res Function(FinAccounts) _then;

/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? meta = null,Object? rows = null,Object? total = freezed,}) {
  return _then(FinAccounts(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,rows: null == rows ? _self.rows : rows // ignore: cast_nullable_to_non_nullable
as List<TrialRow>,total: freezed == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as TrialRow?,
  ));
}
/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TrialRowCopyWith<$Res>? get total {
    if (_self.total == null) {
    return null;
  }

  return $TrialRowCopyWith<$Res>(_self.total!, (value) {
    return _then(_self.copyWith(total: value));
  });
}
}


/// Adds pattern-matching-related methods to [FinAccounts].
extension FinAccountsPatterns on FinAccounts {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _FinAccounts value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _FinAccounts() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _FinAccounts value)  $default,){
final _that = this;
switch (_that) {
case _FinAccounts():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _FinAccounts value)?  $default,){
final _that = this;
switch (_that) {
case _FinAccounts() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<TrialRow> rows,  TrialRow? total)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _FinAccounts() when $default != null:
return $default(_that.meta,_that.rows,_that.total);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( FinanceMeta meta,  List<TrialRow> rows,  TrialRow? total)  $default,) {final _that = this;
switch (_that) {
case _FinAccounts():
return $default(_that.meta,_that.rows,_that.total);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( FinanceMeta meta,  List<TrialRow> rows,  TrialRow? total)?  $default,) {final _that = this;
switch (_that) {
case _FinAccounts() when $default != null:
return $default(_that.meta,_that.rows,_that.total);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _FinAccounts implements FinAccounts {
  const _FinAccounts({required this.meta,  List<TrialRow> rows = const <TrialRow>[], this.total}): _rows = rows;
  factory _FinAccounts.fromJson(Map<String, dynamic> json) => _$FinAccountsFromJson(json);

@override final  FinanceMeta meta;
 final  List<TrialRow> _rows;
@override@JsonKey() List<TrialRow> get rows {
  if (_rows is EqualUnmodifiableListView) return _rows;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_rows);
}

@override final  TrialRow? total;

/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$FinAccountsCopyWith<_FinAccounts> get copyWith => __$FinAccountsCopyWithImpl<_FinAccounts>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$FinAccountsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _FinAccounts&&(identical(other.meta, meta) || other.meta == meta)&&const DeepCollectionEquality().equals(other.rows, _rows)&&(identical(other.total, total) || other.total == total));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,meta,const DeepCollectionEquality().hash(_rows),total);
}

@override
String toString() {
    return 'FinAccounts(meta: $meta, rows: $rows, total: $total)';
}


}

/// @nodoc
abstract mixin class _$FinAccountsCopyWith<$Res> implements $FinAccountsCopyWith<$Res> {
  factory _$FinAccountsCopyWith(_FinAccounts value, $Res Function(_FinAccounts) _then) = __$FinAccountsCopyWithImpl;
@override @useResult
$Res call({
 FinanceMeta meta, List<TrialRow> rows, TrialRow? total
});


@override $FinanceMetaCopyWith<$Res> get meta;@override $TrialRowCopyWith<$Res>? get total;

}
/// @nodoc
class __$FinAccountsCopyWithImpl<$Res>
    implements _$FinAccountsCopyWith<$Res> {
  __$FinAccountsCopyWithImpl(this._self, this._then);

  final _FinAccounts _self;
  final $Res Function(_FinAccounts) _then;

/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? meta = null,Object? rows = null,Object? total = freezed,}) {
  return _then(_FinAccounts(
meta: null == meta ? _self.meta : meta // ignore: cast_nullable_to_non_nullable
as FinanceMeta,rows: null == rows ? _self._rows : rows // ignore: cast_nullable_to_non_nullable
as List<TrialRow>,total: freezed == total ? _self.total : total // ignore: cast_nullable_to_non_nullable
as TrialRow?,
  ));
}

/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$FinanceMetaCopyWith<$Res> get meta {
  
  return $FinanceMetaCopyWith<$Res>(_self.meta, (value) {
    return _then(_self.copyWith(meta: value));
  });
}/// Create a copy of FinAccounts
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TrialRowCopyWith<$Res>? get total {
    if (_self.total == null) {
    return null;
  }

  return $TrialRowCopyWith<$Res>(_self.total!, (value) {
    return _then(_self.copyWith(total: value));
  });
}
}

// dart format on
