// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'notification_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$NotifAction {

 String get id; String get label; String get kind; String get action;
/// Create a copy of NotifAction
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$NotifActionCopyWith<NotifAction> get copyWith => _$NotifActionCopyWithImpl<NotifAction>(this as NotifAction, _$identity);

  /// Serializes this NotifAction to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as NotifAction;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is NotifAction&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.label, _this.label) || other.label == _this.label)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.action, _this.action) || other.action == _this.action));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as NotifAction;
  return Object.hash(runtimeType,_this.id,_this.label,_this.kind,_this.action);
}

@override
String toString() {
  final _this = this as NotifAction;
  return 'NotifAction(id: ${_this.id}, label: ${_this.label}, kind: ${_this.kind}, action: ${_this.action})';
}


}

/// @nodoc
abstract mixin class $NotifActionCopyWith<$Res>  {
  factory $NotifActionCopyWith(NotifAction value, $Res Function(NotifAction) _then) = _$NotifActionCopyWithImpl;
@useResult
$Res call({
 String id, String label, String kind, String action
});




}
/// @nodoc
class _$NotifActionCopyWithImpl<$Res>
    implements $NotifActionCopyWith<$Res> {
  _$NotifActionCopyWithImpl(this._self, this._then);

  final NotifAction _self;
  final $Res Function(NotifAction) _then;

/// Create a copy of NotifAction
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? label = null,Object? kind = null,Object? action = null,}) {
  return _then(NotifAction(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,label: null == label ? _self.label : label // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,action: null == action ? _self.action : action // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [NotifAction].
extension NotifActionPatterns on NotifAction {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _NotifAction value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _NotifAction() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _NotifAction value)  $default,){
final _that = this;
switch (_that) {
case _NotifAction():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _NotifAction value)?  $default,){
final _that = this;
switch (_that) {
case _NotifAction() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String label,  String kind,  String action)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _NotifAction() when $default != null:
return $default(_that.id,_that.label,_that.kind,_that.action);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String label,  String kind,  String action)  $default,) {final _that = this;
switch (_that) {
case _NotifAction():
return $default(_that.id,_that.label,_that.kind,_that.action);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String label,  String kind,  String action)?  $default,) {final _that = this;
switch (_that) {
case _NotifAction() when $default != null:
return $default(_that.id,_that.label,_that.kind,_that.action);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _NotifAction implements NotifAction {
  const _NotifAction({required this.id, required this.label, this.kind = 'secondary', required this.action});
  factory _NotifAction.fromJson(Map<String, dynamic> json) => _$NotifActionFromJson(json);

@override final  String id;
@override final  String label;
@override@JsonKey() final  String kind;
@override final  String action;

/// Create a copy of NotifAction
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$NotifActionCopyWith<_NotifAction> get copyWith => __$NotifActionCopyWithImpl<_NotifAction>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$NotifActionToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _NotifAction&&(identical(other.id, id) || other.id == id)&&(identical(other.label, label) || other.label == label)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.action, action) || other.action == action));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,label,kind,action);
}

@override
String toString() {
    return 'NotifAction(id: $id, label: $label, kind: $kind, action: $action)';
}


}

/// @nodoc
abstract mixin class _$NotifActionCopyWith<$Res> implements $NotifActionCopyWith<$Res> {
  factory _$NotifActionCopyWith(_NotifAction value, $Res Function(_NotifAction) _then) = __$NotifActionCopyWithImpl;
@override @useResult
$Res call({
 String id, String label, String kind, String action
});




}
/// @nodoc
class __$NotifActionCopyWithImpl<$Res>
    implements _$NotifActionCopyWith<$Res> {
  __$NotifActionCopyWithImpl(this._self, this._then);

  final _NotifAction _self;
  final $Res Function(_NotifAction) _then;

/// Create a copy of NotifAction
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? label = null,Object? kind = null,Object? action = null,}) {
  return _then(_NotifAction(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,label: null == label ? _self.label : label // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,action: null == action ? _self.action : action // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$Notif {

 String get id; String get type; String get category; String? get kind; String get title; String? get body; String? get href; String get createdAt; bool get read;/// critical | warning | info
 String get severity; List<NotifAction> get actions;
/// Create a copy of Notif
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$NotifCopyWith<Notif> get copyWith => _$NotifCopyWithImpl<Notif>(this as Notif, _$identity);

  /// Serializes this Notif to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as Notif;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is Notif&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.type, _this.type) || other.type == _this.type)&&(identical(other.category, _this.category) || other.category == _this.category)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.body, _this.body) || other.body == _this.body)&&(identical(other.href, _this.href) || other.href == _this.href)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt)&&(identical(other.read, _this.read) || other.read == _this.read)&&(identical(other.severity, _this.severity) || other.severity == _this.severity)&&const DeepCollectionEquality().equals(other.actions, _this.actions));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as Notif;
  return Object.hash(runtimeType,_this.id,_this.type,_this.category,_this.kind,_this.title,_this.body,_this.href,_this.createdAt,_this.read,_this.severity,const DeepCollectionEquality().hash(_this.actions));
}

@override
String toString() {
  final _this = this as Notif;
  return 'Notif(id: ${_this.id}, type: ${_this.type}, category: ${_this.category}, kind: ${_this.kind}, title: ${_this.title}, body: ${_this.body}, href: ${_this.href}, createdAt: ${_this.createdAt}, read: ${_this.read}, severity: ${_this.severity}, actions: ${_this.actions})';
}


}

/// @nodoc
abstract mixin class $NotifCopyWith<$Res>  {
  factory $NotifCopyWith(Notif value, $Res Function(Notif) _then) = _$NotifCopyWithImpl;
@useResult
$Res call({
 String id, String type, String category, String? kind, String title, String? body, String? href, String createdAt, bool read, String severity, List<NotifAction> actions
});




}
/// @nodoc
class _$NotifCopyWithImpl<$Res>
    implements $NotifCopyWith<$Res> {
  _$NotifCopyWithImpl(this._self, this._then);

  final Notif _self;
  final $Res Function(Notif) _then;

/// Create a copy of Notif
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? type = null,Object? category = null,Object? kind = freezed,Object? title = null,Object? body = freezed,Object? href = freezed,Object? createdAt = null,Object? read = null,Object? severity = null,Object? actions = null,}) {
  return _then(Notif(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,type: null == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String,category: null == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String,kind: freezed == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String?,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,body: freezed == body ? _self.body : body // ignore: cast_nullable_to_non_nullable
as String?,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,read: null == read ? _self.read : read // ignore: cast_nullable_to_non_nullable
as bool,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String,actions: null == actions ? _self.actions : actions // ignore: cast_nullable_to_non_nullable
as List<NotifAction>,
  ));
}

}


/// Adds pattern-matching-related methods to [Notif].
extension NotifPatterns on Notif {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _Notif value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _Notif() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _Notif value)  $default,){
final _that = this;
switch (_that) {
case _Notif():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _Notif value)?  $default,){
final _that = this;
switch (_that) {
case _Notif() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String type,  String category,  String? kind,  String title,  String? body,  String? href,  String createdAt,  bool read,  String severity,  List<NotifAction> actions)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _Notif() when $default != null:
return $default(_that.id,_that.type,_that.category,_that.kind,_that.title,_that.body,_that.href,_that.createdAt,_that.read,_that.severity,_that.actions);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String type,  String category,  String? kind,  String title,  String? body,  String? href,  String createdAt,  bool read,  String severity,  List<NotifAction> actions)  $default,) {final _that = this;
switch (_that) {
case _Notif():
return $default(_that.id,_that.type,_that.category,_that.kind,_that.title,_that.body,_that.href,_that.createdAt,_that.read,_that.severity,_that.actions);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String type,  String category,  String? kind,  String title,  String? body,  String? href,  String createdAt,  bool read,  String severity,  List<NotifAction> actions)?  $default,) {final _that = this;
switch (_that) {
case _Notif() when $default != null:
return $default(_that.id,_that.type,_that.category,_that.kind,_that.title,_that.body,_that.href,_that.createdAt,_that.read,_that.severity,_that.actions);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _Notif implements Notif {
  const _Notif({required this.id, this.type = 'system', this.category = 'system', this.kind, required this.title, this.body, this.href, required this.createdAt, this.read = false, this.severity = 'info',  List<NotifAction> actions = const <NotifAction>[]}): _actions = actions;
  factory _Notif.fromJson(Map<String, dynamic> json) => _$NotifFromJson(json);

@override final  String id;
@override@JsonKey() final  String type;
@override@JsonKey() final  String category;
@override final  String? kind;
@override final  String title;
@override final  String? body;
@override final  String? href;
@override final  String createdAt;
@override@JsonKey() final  bool read;
/// critical | warning | info
@override@JsonKey() final  String severity;
 final  List<NotifAction> _actions;
@override@JsonKey() List<NotifAction> get actions {
  if (_actions is EqualUnmodifiableListView) return _actions;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_actions);
}


/// Create a copy of Notif
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$NotifCopyWith<_Notif> get copyWith => __$NotifCopyWithImpl<_Notif>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$NotifToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _Notif&&(identical(other.id, id) || other.id == id)&&(identical(other.type, type) || other.type == type)&&(identical(other.category, category) || other.category == category)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.title, title) || other.title == title)&&(identical(other.body, body) || other.body == body)&&(identical(other.href, href) || other.href == href)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt)&&(identical(other.read, read) || other.read == read)&&(identical(other.severity, severity) || other.severity == severity)&&const DeepCollectionEquality().equals(other.actions, _actions));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,type,category,kind,title,body,href,createdAt,read,severity,const DeepCollectionEquality().hash(_actions));
}

@override
String toString() {
    return 'Notif(id: $id, type: $type, category: $category, kind: $kind, title: $title, body: $body, href: $href, createdAt: $createdAt, read: $read, severity: $severity, actions: $actions)';
}


}

/// @nodoc
abstract mixin class _$NotifCopyWith<$Res> implements $NotifCopyWith<$Res> {
  factory _$NotifCopyWith(_Notif value, $Res Function(_Notif) _then) = __$NotifCopyWithImpl;
@override @useResult
$Res call({
 String id, String type, String category, String? kind, String title, String? body, String? href, String createdAt, bool read, String severity, List<NotifAction> actions
});




}
/// @nodoc
class __$NotifCopyWithImpl<$Res>
    implements _$NotifCopyWith<$Res> {
  __$NotifCopyWithImpl(this._self, this._then);

  final _Notif _self;
  final $Res Function(_Notif) _then;

/// Create a copy of Notif
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? type = null,Object? category = null,Object? kind = freezed,Object? title = null,Object? body = freezed,Object? href = freezed,Object? createdAt = null,Object? read = null,Object? severity = null,Object? actions = null,}) {
  return _then(_Notif(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,type: null == type ? _self.type : type // ignore: cast_nullable_to_non_nullable
as String,category: null == category ? _self.category : category // ignore: cast_nullable_to_non_nullable
as String,kind: freezed == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String?,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,body: freezed == body ? _self.body : body // ignore: cast_nullable_to_non_nullable
as String?,href: freezed == href ? _self.href : href // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,read: null == read ? _self.read : read // ignore: cast_nullable_to_non_nullable
as bool,severity: null == severity ? _self.severity : severity // ignore: cast_nullable_to_non_nullable
as String,actions: null == actions ? _self._actions : actions // ignore: cast_nullable_to_non_nullable
as List<NotifAction>,
  ));
}


}


/// @nodoc
mixin _$NotifCounts {

 int get all; int get unread; int get tasks; int get ai; int get hr;
/// Create a copy of NotifCounts
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$NotifCountsCopyWith<NotifCounts> get copyWith => _$NotifCountsCopyWithImpl<NotifCounts>(this as NotifCounts, _$identity);

  /// Serializes this NotifCounts to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as NotifCounts;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is NotifCounts&&(identical(other.all, _this.all) || other.all == _this.all)&&(identical(other.unread, _this.unread) || other.unread == _this.unread)&&(identical(other.tasks, _this.tasks) || other.tasks == _this.tasks)&&(identical(other.ai, _this.ai) || other.ai == _this.ai)&&(identical(other.hr, _this.hr) || other.hr == _this.hr));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as NotifCounts;
  return Object.hash(runtimeType,_this.all,_this.unread,_this.tasks,_this.ai,_this.hr);
}

@override
String toString() {
  final _this = this as NotifCounts;
  return 'NotifCounts(all: ${_this.all}, unread: ${_this.unread}, tasks: ${_this.tasks}, ai: ${_this.ai}, hr: ${_this.hr})';
}


}

/// @nodoc
abstract mixin class $NotifCountsCopyWith<$Res>  {
  factory $NotifCountsCopyWith(NotifCounts value, $Res Function(NotifCounts) _then) = _$NotifCountsCopyWithImpl;
@useResult
$Res call({
 int all, int unread, int tasks, int ai, int hr
});




}
/// @nodoc
class _$NotifCountsCopyWithImpl<$Res>
    implements $NotifCountsCopyWith<$Res> {
  _$NotifCountsCopyWithImpl(this._self, this._then);

  final NotifCounts _self;
  final $Res Function(NotifCounts) _then;

/// Create a copy of NotifCounts
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? all = null,Object? unread = null,Object? tasks = null,Object? ai = null,Object? hr = null,}) {
  return _then(NotifCounts(
all: null == all ? _self.all : all // ignore: cast_nullable_to_non_nullable
as int,unread: null == unread ? _self.unread : unread // ignore: cast_nullable_to_non_nullable
as int,tasks: null == tasks ? _self.tasks : tasks // ignore: cast_nullable_to_non_nullable
as int,ai: null == ai ? _self.ai : ai // ignore: cast_nullable_to_non_nullable
as int,hr: null == hr ? _self.hr : hr // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [NotifCounts].
extension NotifCountsPatterns on NotifCounts {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _NotifCounts value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _NotifCounts() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _NotifCounts value)  $default,){
final _that = this;
switch (_that) {
case _NotifCounts():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _NotifCounts value)?  $default,){
final _that = this;
switch (_that) {
case _NotifCounts() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( int all,  int unread,  int tasks,  int ai,  int hr)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _NotifCounts() when $default != null:
return $default(_that.all,_that.unread,_that.tasks,_that.ai,_that.hr);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( int all,  int unread,  int tasks,  int ai,  int hr)  $default,) {final _that = this;
switch (_that) {
case _NotifCounts():
return $default(_that.all,_that.unread,_that.tasks,_that.ai,_that.hr);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( int all,  int unread,  int tasks,  int ai,  int hr)?  $default,) {final _that = this;
switch (_that) {
case _NotifCounts() when $default != null:
return $default(_that.all,_that.unread,_that.tasks,_that.ai,_that.hr);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _NotifCounts implements NotifCounts {
  const _NotifCounts({this.all = 0, this.unread = 0, this.tasks = 0, this.ai = 0, this.hr = 0});
  factory _NotifCounts.fromJson(Map<String, dynamic> json) => _$NotifCountsFromJson(json);

@override@JsonKey() final  int all;
@override@JsonKey() final  int unread;
@override@JsonKey() final  int tasks;
@override@JsonKey() final  int ai;
@override@JsonKey() final  int hr;

/// Create a copy of NotifCounts
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$NotifCountsCopyWith<_NotifCounts> get copyWith => __$NotifCountsCopyWithImpl<_NotifCounts>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$NotifCountsToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _NotifCounts&&(identical(other.all, all) || other.all == all)&&(identical(other.unread, unread) || other.unread == unread)&&(identical(other.tasks, tasks) || other.tasks == tasks)&&(identical(other.ai, ai) || other.ai == ai)&&(identical(other.hr, hr) || other.hr == hr));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,all,unread,tasks,ai,hr);
}

@override
String toString() {
    return 'NotifCounts(all: $all, unread: $unread, tasks: $tasks, ai: $ai, hr: $hr)';
}


}

/// @nodoc
abstract mixin class _$NotifCountsCopyWith<$Res> implements $NotifCountsCopyWith<$Res> {
  factory _$NotifCountsCopyWith(_NotifCounts value, $Res Function(_NotifCounts) _then) = __$NotifCountsCopyWithImpl;
@override @useResult
$Res call({
 int all, int unread, int tasks, int ai, int hr
});




}
/// @nodoc
class __$NotifCountsCopyWithImpl<$Res>
    implements _$NotifCountsCopyWith<$Res> {
  __$NotifCountsCopyWithImpl(this._self, this._then);

  final _NotifCounts _self;
  final $Res Function(_NotifCounts) _then;

/// Create a copy of NotifCounts
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? all = null,Object? unread = null,Object? tasks = null,Object? ai = null,Object? hr = null,}) {
  return _then(_NotifCounts(
all: null == all ? _self.all : all // ignore: cast_nullable_to_non_nullable
as int,unread: null == unread ? _self.unread : unread // ignore: cast_nullable_to_non_nullable
as int,tasks: null == tasks ? _self.tasks : tasks // ignore: cast_nullable_to_non_nullable
as int,ai: null == ai ? _self.ai : ai // ignore: cast_nullable_to_non_nullable
as int,hr: null == hr ? _self.hr : hr // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}

// dart format on
