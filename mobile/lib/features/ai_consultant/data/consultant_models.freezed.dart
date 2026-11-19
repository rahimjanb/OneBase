// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'consultant_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$ChatMessage {

 String get id;/// user | assistant
 String get role;/// completed | failed
 String get status; String get content; String? get error; String get createdAt; int get durationMs; Map<String, Object?>? get answer; Map<String, Object?>? get details;
/// Create a copy of ChatMessage
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ChatMessageCopyWith<ChatMessage> get copyWith => _$ChatMessageCopyWithImpl<ChatMessage>(this as ChatMessage, _$identity);

  /// Serializes this ChatMessage to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ChatMessage;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ChatMessage&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.role, _this.role) || other.role == _this.role)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.content, _this.content) || other.content == _this.content)&&(identical(other.error, _this.error) || other.error == _this.error)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt)&&(identical(other.durationMs, _this.durationMs) || other.durationMs == _this.durationMs)&&const DeepCollectionEquality().equals(other.answer, _this.answer)&&const DeepCollectionEquality().equals(other.details, _this.details));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ChatMessage;
  return Object.hash(runtimeType,_this.id,_this.role,_this.status,_this.content,_this.error,_this.createdAt,_this.durationMs,const DeepCollectionEquality().hash(_this.answer),const DeepCollectionEquality().hash(_this.details));
}

@override
String toString() {
  final _this = this as ChatMessage;
  return 'ChatMessage(id: ${_this.id}, role: ${_this.role}, status: ${_this.status}, content: ${_this.content}, error: ${_this.error}, createdAt: ${_this.createdAt}, durationMs: ${_this.durationMs}, answer: ${_this.answer}, details: ${_this.details})';
}


}

/// @nodoc
abstract mixin class $ChatMessageCopyWith<$Res>  {
  factory $ChatMessageCopyWith(ChatMessage value, $Res Function(ChatMessage) _then) = _$ChatMessageCopyWithImpl;
@useResult
$Res call({
 String id, String role, String status, String content, String? error, String createdAt, int durationMs, Map<String, Object?>? answer, Map<String, Object?>? details
});




}
/// @nodoc
class _$ChatMessageCopyWithImpl<$Res>
    implements $ChatMessageCopyWith<$Res> {
  _$ChatMessageCopyWithImpl(this._self, this._then);

  final ChatMessage _self;
  final $Res Function(ChatMessage) _then;

/// Create a copy of ChatMessage
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? role = null,Object? status = null,Object? content = null,Object? error = freezed,Object? createdAt = null,Object? durationMs = null,Object? answer = freezed,Object? details = freezed,}) {
  return _then(ChatMessage(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,role: null == role ? _self.role : role // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,content: null == content ? _self.content : content // ignore: cast_nullable_to_non_nullable
as String,error: freezed == error ? _self.error : error // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,durationMs: null == durationMs ? _self.durationMs : durationMs // ignore: cast_nullable_to_non_nullable
as int,answer: freezed == answer ? _self.answer : answer // ignore: cast_nullable_to_non_nullable
as Map<String, Object?>?,details: freezed == details ? _self.details : details // ignore: cast_nullable_to_non_nullable
as Map<String, Object?>?,
  ));
}

}


/// Adds pattern-matching-related methods to [ChatMessage].
extension ChatMessagePatterns on ChatMessage {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ChatMessage value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ChatMessage() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ChatMessage value)  $default,){
final _that = this;
switch (_that) {
case _ChatMessage():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ChatMessage value)?  $default,){
final _that = this;
switch (_that) {
case _ChatMessage() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String role,  String status,  String content,  String? error,  String createdAt,  int durationMs,  Map<String, Object?>? answer,  Map<String, Object?>? details)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ChatMessage() when $default != null:
return $default(_that.id,_that.role,_that.status,_that.content,_that.error,_that.createdAt,_that.durationMs,_that.answer,_that.details);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String role,  String status,  String content,  String? error,  String createdAt,  int durationMs,  Map<String, Object?>? answer,  Map<String, Object?>? details)  $default,) {final _that = this;
switch (_that) {
case _ChatMessage():
return $default(_that.id,_that.role,_that.status,_that.content,_that.error,_that.createdAt,_that.durationMs,_that.answer,_that.details);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String role,  String status,  String content,  String? error,  String createdAt,  int durationMs,  Map<String, Object?>? answer,  Map<String, Object?>? details)?  $default,) {final _that = this;
switch (_that) {
case _ChatMessage() when $default != null:
return $default(_that.id,_that.role,_that.status,_that.content,_that.error,_that.createdAt,_that.durationMs,_that.answer,_that.details);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ChatMessage implements ChatMessage {
  const _ChatMessage({required this.id, required this.role, this.status = 'completed', this.content = '', this.error, required this.createdAt, this.durationMs = 0,  Map<String, Object?>? answer,  Map<String, Object?>? details}): _answer = answer,_details = details;
  factory _ChatMessage.fromJson(Map<String, dynamic> json) => _$ChatMessageFromJson(json);

@override final  String id;
/// user | assistant
@override final  String role;
/// completed | failed
@override@JsonKey() final  String status;
@override@JsonKey() final  String content;
@override final  String? error;
@override final  String createdAt;
@override@JsonKey() final  int durationMs;
 final  Map<String, Object?>? _answer;
@override Map<String, Object?>? get answer {
  final value = _answer;
  if (value == null) return null;
  if (_answer is EqualUnmodifiableMapView) return _answer;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableMapView(value);
}

 final  Map<String, Object?>? _details;
@override Map<String, Object?>? get details {
  final value = _details;
  if (value == null) return null;
  if (_details is EqualUnmodifiableMapView) return _details;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableMapView(value);
}


/// Create a copy of ChatMessage
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ChatMessageCopyWith<_ChatMessage> get copyWith => __$ChatMessageCopyWithImpl<_ChatMessage>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ChatMessageToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ChatMessage&&(identical(other.id, id) || other.id == id)&&(identical(other.role, role) || other.role == role)&&(identical(other.status, status) || other.status == status)&&(identical(other.content, content) || other.content == content)&&(identical(other.error, error) || other.error == error)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt)&&(identical(other.durationMs, durationMs) || other.durationMs == durationMs)&&const DeepCollectionEquality().equals(other.answer, _answer)&&const DeepCollectionEquality().equals(other.details, _details));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,role,status,content,error,createdAt,durationMs,const DeepCollectionEquality().hash(_answer),const DeepCollectionEquality().hash(_details));
}

@override
String toString() {
    return 'ChatMessage(id: $id, role: $role, status: $status, content: $content, error: $error, createdAt: $createdAt, durationMs: $durationMs, answer: $answer, details: $details)';
}


}

/// @nodoc
abstract mixin class _$ChatMessageCopyWith<$Res> implements $ChatMessageCopyWith<$Res> {
  factory _$ChatMessageCopyWith(_ChatMessage value, $Res Function(_ChatMessage) _then) = __$ChatMessageCopyWithImpl;
@override @useResult
$Res call({
 String id, String role, String status, String content, String? error, String createdAt, int durationMs, Map<String, Object?>? answer, Map<String, Object?>? details
});




}
/// @nodoc
class __$ChatMessageCopyWithImpl<$Res>
    implements _$ChatMessageCopyWith<$Res> {
  __$ChatMessageCopyWithImpl(this._self, this._then);

  final _ChatMessage _self;
  final $Res Function(_ChatMessage) _then;

/// Create a copy of ChatMessage
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? role = null,Object? status = null,Object? content = null,Object? error = freezed,Object? createdAt = null,Object? durationMs = null,Object? answer = freezed,Object? details = freezed,}) {
  return _then(_ChatMessage(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,role: null == role ? _self.role : role // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,content: null == content ? _self.content : content // ignore: cast_nullable_to_non_nullable
as String,error: freezed == error ? _self.error : error // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,durationMs: null == durationMs ? _self.durationMs : durationMs // ignore: cast_nullable_to_non_nullable
as int,answer: freezed == answer ? _self._answer : answer // ignore: cast_nullable_to_non_nullable
as Map<String, Object?>?,details: freezed == details ? _self._details : details // ignore: cast_nullable_to_non_nullable
as Map<String, Object?>?,
  ));
}


}


/// @nodoc
mixin _$ConversationSummary {

 String get id; String get title; String get lastMessageAt; String get source;
/// Create a copy of ConversationSummary
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ConversationSummaryCopyWith<ConversationSummary> get copyWith => _$ConversationSummaryCopyWithImpl<ConversationSummary>(this as ConversationSummary, _$identity);

  /// Serializes this ConversationSummary to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ConversationSummary;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ConversationSummary&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.lastMessageAt, _this.lastMessageAt) || other.lastMessageAt == _this.lastMessageAt)&&(identical(other.source, _this.source) || other.source == _this.source));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ConversationSummary;
  return Object.hash(runtimeType,_this.id,_this.title,_this.lastMessageAt,_this.source);
}

@override
String toString() {
  final _this = this as ConversationSummary;
  return 'ConversationSummary(id: ${_this.id}, title: ${_this.title}, lastMessageAt: ${_this.lastMessageAt}, source: ${_this.source})';
}


}

/// @nodoc
abstract mixin class $ConversationSummaryCopyWith<$Res>  {
  factory $ConversationSummaryCopyWith(ConversationSummary value, $Res Function(ConversationSummary) _then) = _$ConversationSummaryCopyWithImpl;
@useResult
$Res call({
 String id, String title, String lastMessageAt, String source
});




}
/// @nodoc
class _$ConversationSummaryCopyWithImpl<$Res>
    implements $ConversationSummaryCopyWith<$Res> {
  _$ConversationSummaryCopyWithImpl(this._self, this._then);

  final ConversationSummary _self;
  final $Res Function(ConversationSummary) _then;

/// Create a copy of ConversationSummary
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? title = null,Object? lastMessageAt = null,Object? source = null,}) {
  return _then(ConversationSummary(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,lastMessageAt: null == lastMessageAt ? _self.lastMessageAt : lastMessageAt // ignore: cast_nullable_to_non_nullable
as String,source: null == source ? _self.source : source // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [ConversationSummary].
extension ConversationSummaryPatterns on ConversationSummary {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ConversationSummary value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ConversationSummary() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ConversationSummary value)  $default,){
final _that = this;
switch (_that) {
case _ConversationSummary():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ConversationSummary value)?  $default,){
final _that = this;
switch (_that) {
case _ConversationSummary() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String title,  String lastMessageAt,  String source)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ConversationSummary() when $default != null:
return $default(_that.id,_that.title,_that.lastMessageAt,_that.source);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String title,  String lastMessageAt,  String source)  $default,) {final _that = this;
switch (_that) {
case _ConversationSummary():
return $default(_that.id,_that.title,_that.lastMessageAt,_that.source);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String title,  String lastMessageAt,  String source)?  $default,) {final _that = this;
switch (_that) {
case _ConversationSummary() when $default != null:
return $default(_that.id,_that.title,_that.lastMessageAt,_that.source);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ConversationSummary implements ConversationSummary {
  const _ConversationSummary({required this.id, required this.title, required this.lastMessageAt, this.source = 'web'});
  factory _ConversationSummary.fromJson(Map<String, dynamic> json) => _$ConversationSummaryFromJson(json);

@override final  String id;
@override final  String title;
@override final  String lastMessageAt;
@override@JsonKey() final  String source;

/// Create a copy of ConversationSummary
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ConversationSummaryCopyWith<_ConversationSummary> get copyWith => __$ConversationSummaryCopyWithImpl<_ConversationSummary>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ConversationSummaryToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ConversationSummary&&(identical(other.id, id) || other.id == id)&&(identical(other.title, title) || other.title == title)&&(identical(other.lastMessageAt, lastMessageAt) || other.lastMessageAt == lastMessageAt)&&(identical(other.source, source) || other.source == source));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,title,lastMessageAt,source);
}

@override
String toString() {
    return 'ConversationSummary(id: $id, title: $title, lastMessageAt: $lastMessageAt, source: $source)';
}


}

/// @nodoc
abstract mixin class _$ConversationSummaryCopyWith<$Res> implements $ConversationSummaryCopyWith<$Res> {
  factory _$ConversationSummaryCopyWith(_ConversationSummary value, $Res Function(_ConversationSummary) _then) = __$ConversationSummaryCopyWithImpl;
@override @useResult
$Res call({
 String id, String title, String lastMessageAt, String source
});




}
/// @nodoc
class __$ConversationSummaryCopyWithImpl<$Res>
    implements _$ConversationSummaryCopyWith<$Res> {
  __$ConversationSummaryCopyWithImpl(this._self, this._then);

  final _ConversationSummary _self;
  final $Res Function(_ConversationSummary) _then;

/// Create a copy of ConversationSummary
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? title = null,Object? lastMessageAt = null,Object? source = null,}) {
  return _then(_ConversationSummary(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,lastMessageAt: null == lastMessageAt ? _self.lastMessageAt : lastMessageAt // ignore: cast_nullable_to_non_nullable
as String,source: null == source ? _self.source : source // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$ConversationView {

 String get id; String get title; List<ChatMessage> get messages;
/// Create a copy of ConversationView
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ConversationViewCopyWith<ConversationView> get copyWith => _$ConversationViewCopyWithImpl<ConversationView>(this as ConversationView, _$identity);

  /// Serializes this ConversationView to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ConversationView;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ConversationView&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.title, _this.title) || other.title == _this.title)&&const DeepCollectionEquality().equals(other.messages, _this.messages));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ConversationView;
  return Object.hash(runtimeType,_this.id,_this.title,const DeepCollectionEquality().hash(_this.messages));
}

@override
String toString() {
  final _this = this as ConversationView;
  return 'ConversationView(id: ${_this.id}, title: ${_this.title}, messages: ${_this.messages})';
}


}

/// @nodoc
abstract mixin class $ConversationViewCopyWith<$Res>  {
  factory $ConversationViewCopyWith(ConversationView value, $Res Function(ConversationView) _then) = _$ConversationViewCopyWithImpl;
@useResult
$Res call({
 String id, String title, List<ChatMessage> messages
});




}
/// @nodoc
class _$ConversationViewCopyWithImpl<$Res>
    implements $ConversationViewCopyWith<$Res> {
  _$ConversationViewCopyWithImpl(this._self, this._then);

  final ConversationView _self;
  final $Res Function(ConversationView) _then;

/// Create a copy of ConversationView
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? title = null,Object? messages = null,}) {
  return _then(ConversationView(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,messages: null == messages ? _self.messages : messages // ignore: cast_nullable_to_non_nullable
as List<ChatMessage>,
  ));
}

}


/// Adds pattern-matching-related methods to [ConversationView].
extension ConversationViewPatterns on ConversationView {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ConversationView value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ConversationView() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ConversationView value)  $default,){
final _that = this;
switch (_that) {
case _ConversationView():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ConversationView value)?  $default,){
final _that = this;
switch (_that) {
case _ConversationView() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String title,  List<ChatMessage> messages)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ConversationView() when $default != null:
return $default(_that.id,_that.title,_that.messages);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String title,  List<ChatMessage> messages)  $default,) {final _that = this;
switch (_that) {
case _ConversationView():
return $default(_that.id,_that.title,_that.messages);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String title,  List<ChatMessage> messages)?  $default,) {final _that = this;
switch (_that) {
case _ConversationView() when $default != null:
return $default(_that.id,_that.title,_that.messages);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ConversationView implements ConversationView {
  const _ConversationView({required this.id, required this.title,  List<ChatMessage> messages = const <ChatMessage>[]}): _messages = messages;
  factory _ConversationView.fromJson(Map<String, dynamic> json) => _$ConversationViewFromJson(json);

@override final  String id;
@override final  String title;
 final  List<ChatMessage> _messages;
@override@JsonKey() List<ChatMessage> get messages {
  if (_messages is EqualUnmodifiableListView) return _messages;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_messages);
}


/// Create a copy of ConversationView
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ConversationViewCopyWith<_ConversationView> get copyWith => __$ConversationViewCopyWithImpl<_ConversationView>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ConversationViewToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ConversationView&&(identical(other.id, id) || other.id == id)&&(identical(other.title, title) || other.title == title)&&const DeepCollectionEquality().equals(other.messages, _messages));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,title,const DeepCollectionEquality().hash(_messages));
}

@override
String toString() {
    return 'ConversationView(id: $id, title: $title, messages: $messages)';
}


}

/// @nodoc
abstract mixin class _$ConversationViewCopyWith<$Res> implements $ConversationViewCopyWith<$Res> {
  factory _$ConversationViewCopyWith(_ConversationView value, $Res Function(_ConversationView) _then) = __$ConversationViewCopyWithImpl;
@override @useResult
$Res call({
 String id, String title, List<ChatMessage> messages
});




}
/// @nodoc
class __$ConversationViewCopyWithImpl<$Res>
    implements _$ConversationViewCopyWith<$Res> {
  __$ConversationViewCopyWithImpl(this._self, this._then);

  final _ConversationView _self;
  final $Res Function(_ConversationView) _then;

/// Create a copy of ConversationView
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? title = null,Object? messages = null,}) {
  return _then(_ConversationView(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,messages: null == messages ? _self._messages : messages // ignore: cast_nullable_to_non_nullable
as List<ChatMessage>,
  ));
}


}


/// @nodoc
mixin _$AiStatus {

 bool get ready; String? get message;
/// Create a copy of AiStatus
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AiStatusCopyWith<AiStatus> get copyWith => _$AiStatusCopyWithImpl<AiStatus>(this as AiStatus, _$identity);

  /// Serializes this AiStatus to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as AiStatus;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is AiStatus&&(identical(other.ready, _this.ready) || other.ready == _this.ready)&&(identical(other.message, _this.message) || other.message == _this.message));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as AiStatus;
  return Object.hash(runtimeType,_this.ready,_this.message);
}

@override
String toString() {
  final _this = this as AiStatus;
  return 'AiStatus(ready: ${_this.ready}, message: ${_this.message})';
}


}

/// @nodoc
abstract mixin class $AiStatusCopyWith<$Res>  {
  factory $AiStatusCopyWith(AiStatus value, $Res Function(AiStatus) _then) = _$AiStatusCopyWithImpl;
@useResult
$Res call({
 bool ready, String? message
});




}
/// @nodoc
class _$AiStatusCopyWithImpl<$Res>
    implements $AiStatusCopyWith<$Res> {
  _$AiStatusCopyWithImpl(this._self, this._then);

  final AiStatus _self;
  final $Res Function(AiStatus) _then;

/// Create a copy of AiStatus
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? ready = null,Object? message = freezed,}) {
  return _then(AiStatus(
ready: null == ready ? _self.ready : ready // ignore: cast_nullable_to_non_nullable
as bool,message: freezed == message ? _self.message : message // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}

}


/// Adds pattern-matching-related methods to [AiStatus].
extension AiStatusPatterns on AiStatus {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _AiStatus value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _AiStatus() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _AiStatus value)  $default,){
final _that = this;
switch (_that) {
case _AiStatus():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _AiStatus value)?  $default,){
final _that = this;
switch (_that) {
case _AiStatus() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( bool ready,  String? message)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _AiStatus() when $default != null:
return $default(_that.ready,_that.message);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( bool ready,  String? message)  $default,) {final _that = this;
switch (_that) {
case _AiStatus():
return $default(_that.ready,_that.message);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( bool ready,  String? message)?  $default,) {final _that = this;
switch (_that) {
case _AiStatus() when $default != null:
return $default(_that.ready,_that.message);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _AiStatus implements AiStatus {
  const _AiStatus({this.ready = false, this.message});
  factory _AiStatus.fromJson(Map<String, dynamic> json) => _$AiStatusFromJson(json);

@override@JsonKey() final  bool ready;
@override final  String? message;

/// Create a copy of AiStatus
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AiStatusCopyWith<_AiStatus> get copyWith => __$AiStatusCopyWithImpl<_AiStatus>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AiStatusToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _AiStatus&&(identical(other.ready, ready) || other.ready == ready)&&(identical(other.message, message) || other.message == message));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,ready,message);
}

@override
String toString() {
    return 'AiStatus(ready: $ready, message: $message)';
}


}

/// @nodoc
abstract mixin class _$AiStatusCopyWith<$Res> implements $AiStatusCopyWith<$Res> {
  factory _$AiStatusCopyWith(_AiStatus value, $Res Function(_AiStatus) _then) = __$AiStatusCopyWithImpl;
@override @useResult
$Res call({
 bool ready, String? message
});




}
/// @nodoc
class __$AiStatusCopyWithImpl<$Res>
    implements _$AiStatusCopyWith<$Res> {
  __$AiStatusCopyWithImpl(this._self, this._then);

  final _AiStatus _self;
  final $Res Function(_AiStatus) _then;

/// Create a copy of AiStatus
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? ready = null,Object? message = freezed,}) {
  return _then(_AiStatus(
ready: null == ready ? _self.ready : ready // ignore: cast_nullable_to_non_nullable
as bool,message: freezed == message ? _self.message : message // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}

// dart format on
