// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'task_models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$TaskRow {

 String get id; String get title; String? get description;/// Low | Medium | High | Urgent
 String get priority;/// New | InProgress | Review | Done | Cancelled
 String get status; String? get dueDate; String? get dueTime; bool get overdue; String? get assigneeId; String? get assigneeName; String? get createdByName; String get createdAt; String? get result; List<String> get nextStatuses; String get departmentCode; String get departmentName; String? get projectName; int get progress; bool get isBlocked; String? get blockedReason; String? get archivedAt; int get checklistDone; int get checklistTotal; bool get canManage; bool get canWork;
/// Create a copy of TaskRow
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TaskRowCopyWith<TaskRow> get copyWith => _$TaskRowCopyWithImpl<TaskRow>(this as TaskRow, _$identity);

  /// Serializes this TaskRow to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TaskRow;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TaskRow&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.description, _this.description) || other.description == _this.description)&&(identical(other.priority, _this.priority) || other.priority == _this.priority)&&(identical(other.status, _this.status) || other.status == _this.status)&&(identical(other.dueDate, _this.dueDate) || other.dueDate == _this.dueDate)&&(identical(other.dueTime, _this.dueTime) || other.dueTime == _this.dueTime)&&(identical(other.overdue, _this.overdue) || other.overdue == _this.overdue)&&(identical(other.assigneeId, _this.assigneeId) || other.assigneeId == _this.assigneeId)&&(identical(other.assigneeName, _this.assigneeName) || other.assigneeName == _this.assigneeName)&&(identical(other.createdByName, _this.createdByName) || other.createdByName == _this.createdByName)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt)&&(identical(other.result, _this.result) || other.result == _this.result)&&const DeepCollectionEquality().equals(other.nextStatuses, _this.nextStatuses)&&(identical(other.departmentCode, _this.departmentCode) || other.departmentCode == _this.departmentCode)&&(identical(other.departmentName, _this.departmentName) || other.departmentName == _this.departmentName)&&(identical(other.projectName, _this.projectName) || other.projectName == _this.projectName)&&(identical(other.progress, _this.progress) || other.progress == _this.progress)&&(identical(other.isBlocked, _this.isBlocked) || other.isBlocked == _this.isBlocked)&&(identical(other.blockedReason, _this.blockedReason) || other.blockedReason == _this.blockedReason)&&(identical(other.archivedAt, _this.archivedAt) || other.archivedAt == _this.archivedAt)&&(identical(other.checklistDone, _this.checklistDone) || other.checklistDone == _this.checklistDone)&&(identical(other.checklistTotal, _this.checklistTotal) || other.checklistTotal == _this.checklistTotal)&&(identical(other.canManage, _this.canManage) || other.canManage == _this.canManage)&&(identical(other.canWork, _this.canWork) || other.canWork == _this.canWork));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TaskRow;
  return Object.hashAll([runtimeType,_this.id,_this.title,_this.description,_this.priority,_this.status,_this.dueDate,_this.dueTime,_this.overdue,_this.assigneeId,_this.assigneeName,_this.createdByName,_this.createdAt,_this.result,const DeepCollectionEquality().hash(_this.nextStatuses),_this.departmentCode,_this.departmentName,_this.projectName,_this.progress,_this.isBlocked,_this.blockedReason,_this.archivedAt,_this.checklistDone,_this.checklistTotal,_this.canManage,_this.canWork]);
}

@override
String toString() {
  final _this = this as TaskRow;
  return 'TaskRow(id: ${_this.id}, title: ${_this.title}, description: ${_this.description}, priority: ${_this.priority}, status: ${_this.status}, dueDate: ${_this.dueDate}, dueTime: ${_this.dueTime}, overdue: ${_this.overdue}, assigneeId: ${_this.assigneeId}, assigneeName: ${_this.assigneeName}, createdByName: ${_this.createdByName}, createdAt: ${_this.createdAt}, result: ${_this.result}, nextStatuses: ${_this.nextStatuses}, departmentCode: ${_this.departmentCode}, departmentName: ${_this.departmentName}, projectName: ${_this.projectName}, progress: ${_this.progress}, isBlocked: ${_this.isBlocked}, blockedReason: ${_this.blockedReason}, archivedAt: ${_this.archivedAt}, checklistDone: ${_this.checklistDone}, checklistTotal: ${_this.checklistTotal}, canManage: ${_this.canManage}, canWork: ${_this.canWork})';
}


}

/// @nodoc
abstract mixin class $TaskRowCopyWith<$Res>  {
  factory $TaskRowCopyWith(TaskRow value, $Res Function(TaskRow) _then) = _$TaskRowCopyWithImpl;
@useResult
$Res call({
 String id, String title, String? description, String priority, String status, String? dueDate, String? dueTime, bool overdue, String? assigneeId, String? assigneeName, String? createdByName, String createdAt, String? result, List<String> nextStatuses, String departmentCode, String departmentName, String? projectName, int progress, bool isBlocked, String? blockedReason, String? archivedAt, int checklistDone, int checklistTotal, bool canManage, bool canWork
});




}
/// @nodoc
class _$TaskRowCopyWithImpl<$Res>
    implements $TaskRowCopyWith<$Res> {
  _$TaskRowCopyWithImpl(this._self, this._then);

  final TaskRow _self;
  final $Res Function(TaskRow) _then;

/// Create a copy of TaskRow
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? title = null,Object? description = freezed,Object? priority = null,Object? status = null,Object? dueDate = freezed,Object? dueTime = freezed,Object? overdue = null,Object? assigneeId = freezed,Object? assigneeName = freezed,Object? createdByName = freezed,Object? createdAt = null,Object? result = freezed,Object? nextStatuses = null,Object? departmentCode = null,Object? departmentName = null,Object? projectName = freezed,Object? progress = null,Object? isBlocked = null,Object? blockedReason = freezed,Object? archivedAt = freezed,Object? checklistDone = null,Object? checklistTotal = null,Object? canManage = null,Object? canWork = null,}) {
  return _then(TaskRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,description: freezed == description ? _self.description : description // ignore: cast_nullable_to_non_nullable
as String?,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,dueDate: freezed == dueDate ? _self.dueDate : dueDate // ignore: cast_nullable_to_non_nullable
as String?,dueTime: freezed == dueTime ? _self.dueTime : dueTime // ignore: cast_nullable_to_non_nullable
as String?,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as bool,assigneeId: freezed == assigneeId ? _self.assigneeId : assigneeId // ignore: cast_nullable_to_non_nullable
as String?,assigneeName: freezed == assigneeName ? _self.assigneeName : assigneeName // ignore: cast_nullable_to_non_nullable
as String?,createdByName: freezed == createdByName ? _self.createdByName : createdByName // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,result: freezed == result ? _self.result : result // ignore: cast_nullable_to_non_nullable
as String?,nextStatuses: null == nextStatuses ? _self.nextStatuses : nextStatuses // ignore: cast_nullable_to_non_nullable
as List<String>,departmentCode: null == departmentCode ? _self.departmentCode : departmentCode // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,projectName: freezed == projectName ? _self.projectName : projectName // ignore: cast_nullable_to_non_nullable
as String?,progress: null == progress ? _self.progress : progress // ignore: cast_nullable_to_non_nullable
as int,isBlocked: null == isBlocked ? _self.isBlocked : isBlocked // ignore: cast_nullable_to_non_nullable
as bool,blockedReason: freezed == blockedReason ? _self.blockedReason : blockedReason // ignore: cast_nullable_to_non_nullable
as String?,archivedAt: freezed == archivedAt ? _self.archivedAt : archivedAt // ignore: cast_nullable_to_non_nullable
as String?,checklistDone: null == checklistDone ? _self.checklistDone : checklistDone // ignore: cast_nullable_to_non_nullable
as int,checklistTotal: null == checklistTotal ? _self.checklistTotal : checklistTotal // ignore: cast_nullable_to_non_nullable
as int,canManage: null == canManage ? _self.canManage : canManage // ignore: cast_nullable_to_non_nullable
as bool,canWork: null == canWork ? _self.canWork : canWork // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [TaskRow].
extension TaskRowPatterns on TaskRow {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TaskRow value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TaskRow() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TaskRow value)  $default,){
final _that = this;
switch (_that) {
case _TaskRow():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TaskRow value)?  $default,){
final _that = this;
switch (_that) {
case _TaskRow() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String title,  String? description,  String priority,  String status,  String? dueDate,  String? dueTime,  bool overdue,  String? assigneeId,  String? assigneeName,  String? createdByName,  String createdAt,  String? result,  List<String> nextStatuses,  String departmentCode,  String departmentName,  String? projectName,  int progress,  bool isBlocked,  String? blockedReason,  String? archivedAt,  int checklistDone,  int checklistTotal,  bool canManage,  bool canWork)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TaskRow() when $default != null:
return $default(_that.id,_that.title,_that.description,_that.priority,_that.status,_that.dueDate,_that.dueTime,_that.overdue,_that.assigneeId,_that.assigneeName,_that.createdByName,_that.createdAt,_that.result,_that.nextStatuses,_that.departmentCode,_that.departmentName,_that.projectName,_that.progress,_that.isBlocked,_that.blockedReason,_that.archivedAt,_that.checklistDone,_that.checklistTotal,_that.canManage,_that.canWork);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String title,  String? description,  String priority,  String status,  String? dueDate,  String? dueTime,  bool overdue,  String? assigneeId,  String? assigneeName,  String? createdByName,  String createdAt,  String? result,  List<String> nextStatuses,  String departmentCode,  String departmentName,  String? projectName,  int progress,  bool isBlocked,  String? blockedReason,  String? archivedAt,  int checklistDone,  int checklistTotal,  bool canManage,  bool canWork)  $default,) {final _that = this;
switch (_that) {
case _TaskRow():
return $default(_that.id,_that.title,_that.description,_that.priority,_that.status,_that.dueDate,_that.dueTime,_that.overdue,_that.assigneeId,_that.assigneeName,_that.createdByName,_that.createdAt,_that.result,_that.nextStatuses,_that.departmentCode,_that.departmentName,_that.projectName,_that.progress,_that.isBlocked,_that.blockedReason,_that.archivedAt,_that.checklistDone,_that.checklistTotal,_that.canManage,_that.canWork);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String title,  String? description,  String priority,  String status,  String? dueDate,  String? dueTime,  bool overdue,  String? assigneeId,  String? assigneeName,  String? createdByName,  String createdAt,  String? result,  List<String> nextStatuses,  String departmentCode,  String departmentName,  String? projectName,  int progress,  bool isBlocked,  String? blockedReason,  String? archivedAt,  int checklistDone,  int checklistTotal,  bool canManage,  bool canWork)?  $default,) {final _that = this;
switch (_that) {
case _TaskRow() when $default != null:
return $default(_that.id,_that.title,_that.description,_that.priority,_that.status,_that.dueDate,_that.dueTime,_that.overdue,_that.assigneeId,_that.assigneeName,_that.createdByName,_that.createdAt,_that.result,_that.nextStatuses,_that.departmentCode,_that.departmentName,_that.projectName,_that.progress,_that.isBlocked,_that.blockedReason,_that.archivedAt,_that.checklistDone,_that.checklistTotal,_that.canManage,_that.canWork);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TaskRow implements TaskRow {
  const _TaskRow({required this.id, required this.title, this.description, this.priority = 'Medium', required this.status, this.dueDate, this.dueTime, this.overdue = false, this.assigneeId, this.assigneeName, this.createdByName, required this.createdAt, this.result,  List<String> nextStatuses = const <String>[], this.departmentCode = '', this.departmentName = '', this.projectName, this.progress = 0, this.isBlocked = false, this.blockedReason, this.archivedAt, this.checklistDone = 0, this.checklistTotal = 0, this.canManage = false, this.canWork = false}): _nextStatuses = nextStatuses;
  factory _TaskRow.fromJson(Map<String, dynamic> json) => _$TaskRowFromJson(json);

@override final  String id;
@override final  String title;
@override final  String? description;
/// Low | Medium | High | Urgent
@override@JsonKey() final  String priority;
/// New | InProgress | Review | Done | Cancelled
@override final  String status;
@override final  String? dueDate;
@override final  String? dueTime;
@override@JsonKey() final  bool overdue;
@override final  String? assigneeId;
@override final  String? assigneeName;
@override final  String? createdByName;
@override final  String createdAt;
@override final  String? result;
 final  List<String> _nextStatuses;
@override@JsonKey() List<String> get nextStatuses {
  if (_nextStatuses is EqualUnmodifiableListView) return _nextStatuses;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_nextStatuses);
}

@override@JsonKey() final  String departmentCode;
@override@JsonKey() final  String departmentName;
@override final  String? projectName;
@override@JsonKey() final  int progress;
@override@JsonKey() final  bool isBlocked;
@override final  String? blockedReason;
@override final  String? archivedAt;
@override@JsonKey() final  int checklistDone;
@override@JsonKey() final  int checklistTotal;
@override@JsonKey() final  bool canManage;
@override@JsonKey() final  bool canWork;

/// Create a copy of TaskRow
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TaskRowCopyWith<_TaskRow> get copyWith => __$TaskRowCopyWithImpl<_TaskRow>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TaskRowToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TaskRow&&(identical(other.id, id) || other.id == id)&&(identical(other.title, title) || other.title == title)&&(identical(other.description, description) || other.description == description)&&(identical(other.priority, priority) || other.priority == priority)&&(identical(other.status, status) || other.status == status)&&(identical(other.dueDate, dueDate) || other.dueDate == dueDate)&&(identical(other.dueTime, dueTime) || other.dueTime == dueTime)&&(identical(other.overdue, overdue) || other.overdue == overdue)&&(identical(other.assigneeId, assigneeId) || other.assigneeId == assigneeId)&&(identical(other.assigneeName, assigneeName) || other.assigneeName == assigneeName)&&(identical(other.createdByName, createdByName) || other.createdByName == createdByName)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt)&&(identical(other.result, result) || other.result == result)&&const DeepCollectionEquality().equals(other.nextStatuses, _nextStatuses)&&(identical(other.departmentCode, departmentCode) || other.departmentCode == departmentCode)&&(identical(other.departmentName, departmentName) || other.departmentName == departmentName)&&(identical(other.projectName, projectName) || other.projectName == projectName)&&(identical(other.progress, progress) || other.progress == progress)&&(identical(other.isBlocked, isBlocked) || other.isBlocked == isBlocked)&&(identical(other.blockedReason, blockedReason) || other.blockedReason == blockedReason)&&(identical(other.archivedAt, archivedAt) || other.archivedAt == archivedAt)&&(identical(other.checklistDone, checklistDone) || other.checklistDone == checklistDone)&&(identical(other.checklistTotal, checklistTotal) || other.checklistTotal == checklistTotal)&&(identical(other.canManage, canManage) || other.canManage == canManage)&&(identical(other.canWork, canWork) || other.canWork == canWork));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hashAll([runtimeType,id,title,description,priority,status,dueDate,dueTime,overdue,assigneeId,assigneeName,createdByName,createdAt,result,const DeepCollectionEquality().hash(_nextStatuses),departmentCode,departmentName,projectName,progress,isBlocked,blockedReason,archivedAt,checklistDone,checklistTotal,canManage,canWork]);
}

@override
String toString() {
    return 'TaskRow(id: $id, title: $title, description: $description, priority: $priority, status: $status, dueDate: $dueDate, dueTime: $dueTime, overdue: $overdue, assigneeId: $assigneeId, assigneeName: $assigneeName, createdByName: $createdByName, createdAt: $createdAt, result: $result, nextStatuses: $nextStatuses, departmentCode: $departmentCode, departmentName: $departmentName, projectName: $projectName, progress: $progress, isBlocked: $isBlocked, blockedReason: $blockedReason, archivedAt: $archivedAt, checklistDone: $checklistDone, checklistTotal: $checklistTotal, canManage: $canManage, canWork: $canWork)';
}


}

/// @nodoc
abstract mixin class _$TaskRowCopyWith<$Res> implements $TaskRowCopyWith<$Res> {
  factory _$TaskRowCopyWith(_TaskRow value, $Res Function(_TaskRow) _then) = __$TaskRowCopyWithImpl;
@override @useResult
$Res call({
 String id, String title, String? description, String priority, String status, String? dueDate, String? dueTime, bool overdue, String? assigneeId, String? assigneeName, String? createdByName, String createdAt, String? result, List<String> nextStatuses, String departmentCode, String departmentName, String? projectName, int progress, bool isBlocked, String? blockedReason, String? archivedAt, int checklistDone, int checklistTotal, bool canManage, bool canWork
});




}
/// @nodoc
class __$TaskRowCopyWithImpl<$Res>
    implements _$TaskRowCopyWith<$Res> {
  __$TaskRowCopyWithImpl(this._self, this._then);

  final _TaskRow _self;
  final $Res Function(_TaskRow) _then;

/// Create a copy of TaskRow
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? title = null,Object? description = freezed,Object? priority = null,Object? status = null,Object? dueDate = freezed,Object? dueTime = freezed,Object? overdue = null,Object? assigneeId = freezed,Object? assigneeName = freezed,Object? createdByName = freezed,Object? createdAt = null,Object? result = freezed,Object? nextStatuses = null,Object? departmentCode = null,Object? departmentName = null,Object? projectName = freezed,Object? progress = null,Object? isBlocked = null,Object? blockedReason = freezed,Object? archivedAt = freezed,Object? checklistDone = null,Object? checklistTotal = null,Object? canManage = null,Object? canWork = null,}) {
  return _then(_TaskRow(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,description: freezed == description ? _self.description : description // ignore: cast_nullable_to_non_nullable
as String?,priority: null == priority ? _self.priority : priority // ignore: cast_nullable_to_non_nullable
as String,status: null == status ? _self.status : status // ignore: cast_nullable_to_non_nullable
as String,dueDate: freezed == dueDate ? _self.dueDate : dueDate // ignore: cast_nullable_to_non_nullable
as String?,dueTime: freezed == dueTime ? _self.dueTime : dueTime // ignore: cast_nullable_to_non_nullable
as String?,overdue: null == overdue ? _self.overdue : overdue // ignore: cast_nullable_to_non_nullable
as bool,assigneeId: freezed == assigneeId ? _self.assigneeId : assigneeId // ignore: cast_nullable_to_non_nullable
as String?,assigneeName: freezed == assigneeName ? _self.assigneeName : assigneeName // ignore: cast_nullable_to_non_nullable
as String?,createdByName: freezed == createdByName ? _self.createdByName : createdByName // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,result: freezed == result ? _self.result : result // ignore: cast_nullable_to_non_nullable
as String?,nextStatuses: null == nextStatuses ? _self._nextStatuses : nextStatuses // ignore: cast_nullable_to_non_nullable
as List<String>,departmentCode: null == departmentCode ? _self.departmentCode : departmentCode // ignore: cast_nullable_to_non_nullable
as String,departmentName: null == departmentName ? _self.departmentName : departmentName // ignore: cast_nullable_to_non_nullable
as String,projectName: freezed == projectName ? _self.projectName : projectName // ignore: cast_nullable_to_non_nullable
as String?,progress: null == progress ? _self.progress : progress // ignore: cast_nullable_to_non_nullable
as int,isBlocked: null == isBlocked ? _self.isBlocked : isBlocked // ignore: cast_nullable_to_non_nullable
as bool,blockedReason: freezed == blockedReason ? _self.blockedReason : blockedReason // ignore: cast_nullable_to_non_nullable
as String?,archivedAt: freezed == archivedAt ? _self.archivedAt : archivedAt // ignore: cast_nullable_to_non_nullable
as String?,checklistDone: null == checklistDone ? _self.checklistDone : checklistDone // ignore: cast_nullable_to_non_nullable
as int,checklistTotal: null == checklistTotal ? _self.checklistTotal : checklistTotal // ignore: cast_nullable_to_non_nullable
as int,canManage: null == canManage ? _self.canManage : canManage // ignore: cast_nullable_to_non_nullable
as bool,canWork: null == canWork ? _self.canWork : canWork // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$ChecklistItem {

 String get id; String get title; bool get isDone; int get sortOrder;
/// Create a copy of ChecklistItem
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ChecklistItemCopyWith<ChecklistItem> get copyWith => _$ChecklistItemCopyWithImpl<ChecklistItem>(this as ChecklistItem, _$identity);

  /// Serializes this ChecklistItem to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as ChecklistItem;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is ChecklistItem&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.title, _this.title) || other.title == _this.title)&&(identical(other.isDone, _this.isDone) || other.isDone == _this.isDone)&&(identical(other.sortOrder, _this.sortOrder) || other.sortOrder == _this.sortOrder));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as ChecklistItem;
  return Object.hash(runtimeType,_this.id,_this.title,_this.isDone,_this.sortOrder);
}

@override
String toString() {
  final _this = this as ChecklistItem;
  return 'ChecklistItem(id: ${_this.id}, title: ${_this.title}, isDone: ${_this.isDone}, sortOrder: ${_this.sortOrder})';
}


}

/// @nodoc
abstract mixin class $ChecklistItemCopyWith<$Res>  {
  factory $ChecklistItemCopyWith(ChecklistItem value, $Res Function(ChecklistItem) _then) = _$ChecklistItemCopyWithImpl;
@useResult
$Res call({
 String id, String title, bool isDone, int sortOrder
});




}
/// @nodoc
class _$ChecklistItemCopyWithImpl<$Res>
    implements $ChecklistItemCopyWith<$Res> {
  _$ChecklistItemCopyWithImpl(this._self, this._then);

  final ChecklistItem _self;
  final $Res Function(ChecklistItem) _then;

/// Create a copy of ChecklistItem
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? title = null,Object? isDone = null,Object? sortOrder = null,}) {
  return _then(ChecklistItem(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,isDone: null == isDone ? _self.isDone : isDone // ignore: cast_nullable_to_non_nullable
as bool,sortOrder: null == sortOrder ? _self.sortOrder : sortOrder // ignore: cast_nullable_to_non_nullable
as int,
  ));
}

}


/// Adds pattern-matching-related methods to [ChecklistItem].
extension ChecklistItemPatterns on ChecklistItem {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _ChecklistItem value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _ChecklistItem() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _ChecklistItem value)  $default,){
final _that = this;
switch (_that) {
case _ChecklistItem():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _ChecklistItem value)?  $default,){
final _that = this;
switch (_that) {
case _ChecklistItem() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String title,  bool isDone,  int sortOrder)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _ChecklistItem() when $default != null:
return $default(_that.id,_that.title,_that.isDone,_that.sortOrder);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String title,  bool isDone,  int sortOrder)  $default,) {final _that = this;
switch (_that) {
case _ChecklistItem():
return $default(_that.id,_that.title,_that.isDone,_that.sortOrder);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String title,  bool isDone,  int sortOrder)?  $default,) {final _that = this;
switch (_that) {
case _ChecklistItem() when $default != null:
return $default(_that.id,_that.title,_that.isDone,_that.sortOrder);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _ChecklistItem implements ChecklistItem {
  const _ChecklistItem({required this.id, required this.title, this.isDone = false, this.sortOrder = 0});
  factory _ChecklistItem.fromJson(Map<String, dynamic> json) => _$ChecklistItemFromJson(json);

@override final  String id;
@override final  String title;
@override@JsonKey() final  bool isDone;
@override@JsonKey() final  int sortOrder;

/// Create a copy of ChecklistItem
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$ChecklistItemCopyWith<_ChecklistItem> get copyWith => __$ChecklistItemCopyWithImpl<_ChecklistItem>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$ChecklistItemToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _ChecklistItem&&(identical(other.id, id) || other.id == id)&&(identical(other.title, title) || other.title == title)&&(identical(other.isDone, isDone) || other.isDone == isDone)&&(identical(other.sortOrder, sortOrder) || other.sortOrder == sortOrder));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,title,isDone,sortOrder);
}

@override
String toString() {
    return 'ChecklistItem(id: $id, title: $title, isDone: $isDone, sortOrder: $sortOrder)';
}


}

/// @nodoc
abstract mixin class _$ChecklistItemCopyWith<$Res> implements $ChecklistItemCopyWith<$Res> {
  factory _$ChecklistItemCopyWith(_ChecklistItem value, $Res Function(_ChecklistItem) _then) = __$ChecklistItemCopyWithImpl;
@override @useResult
$Res call({
 String id, String title, bool isDone, int sortOrder
});




}
/// @nodoc
class __$ChecklistItemCopyWithImpl<$Res>
    implements _$ChecklistItemCopyWith<$Res> {
  __$ChecklistItemCopyWithImpl(this._self, this._then);

  final _ChecklistItem _self;
  final $Res Function(_ChecklistItem) _then;

/// Create a copy of ChecklistItem
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? title = null,Object? isDone = null,Object? sortOrder = null,}) {
  return _then(_ChecklistItem(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,title: null == title ? _self.title : title // ignore: cast_nullable_to_non_nullable
as String,isDone: null == isDone ? _self.isDone : isDone // ignore: cast_nullable_to_non_nullable
as bool,sortOrder: null == sortOrder ? _self.sortOrder : sortOrder // ignore: cast_nullable_to_non_nullable
as int,
  ));
}


}


/// @nodoc
mixin _$TaskComment {

 String get id; String get authorName; String get text; String get createdAt;
/// Create a copy of TaskComment
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TaskCommentCopyWith<TaskComment> get copyWith => _$TaskCommentCopyWithImpl<TaskComment>(this as TaskComment, _$identity);

  /// Serializes this TaskComment to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TaskComment;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TaskComment&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.authorName, _this.authorName) || other.authorName == _this.authorName)&&(identical(other.text, _this.text) || other.text == _this.text)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TaskComment;
  return Object.hash(runtimeType,_this.id,_this.authorName,_this.text,_this.createdAt);
}

@override
String toString() {
  final _this = this as TaskComment;
  return 'TaskComment(id: ${_this.id}, authorName: ${_this.authorName}, text: ${_this.text}, createdAt: ${_this.createdAt})';
}


}

/// @nodoc
abstract mixin class $TaskCommentCopyWith<$Res>  {
  factory $TaskCommentCopyWith(TaskComment value, $Res Function(TaskComment) _then) = _$TaskCommentCopyWithImpl;
@useResult
$Res call({
 String id, String authorName, String text, String createdAt
});




}
/// @nodoc
class _$TaskCommentCopyWithImpl<$Res>
    implements $TaskCommentCopyWith<$Res> {
  _$TaskCommentCopyWithImpl(this._self, this._then);

  final TaskComment _self;
  final $Res Function(TaskComment) _then;

/// Create a copy of TaskComment
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? authorName = null,Object? text = null,Object? createdAt = null,}) {
  return _then(TaskComment(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,authorName: null == authorName ? _self.authorName : authorName // ignore: cast_nullable_to_non_nullable
as String,text: null == text ? _self.text : text // ignore: cast_nullable_to_non_nullable
as String,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [TaskComment].
extension TaskCommentPatterns on TaskComment {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TaskComment value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TaskComment() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TaskComment value)  $default,){
final _that = this;
switch (_that) {
case _TaskComment():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TaskComment value)?  $default,){
final _that = this;
switch (_that) {
case _TaskComment() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String authorName,  String text,  String createdAt)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TaskComment() when $default != null:
return $default(_that.id,_that.authorName,_that.text,_that.createdAt);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String authorName,  String text,  String createdAt)  $default,) {final _that = this;
switch (_that) {
case _TaskComment():
return $default(_that.id,_that.authorName,_that.text,_that.createdAt);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String authorName,  String text,  String createdAt)?  $default,) {final _that = this;
switch (_that) {
case _TaskComment() when $default != null:
return $default(_that.id,_that.authorName,_that.text,_that.createdAt);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TaskComment implements TaskComment {
  const _TaskComment({required this.id, this.authorName = '', required this.text, required this.createdAt});
  factory _TaskComment.fromJson(Map<String, dynamic> json) => _$TaskCommentFromJson(json);

@override final  String id;
@override@JsonKey() final  String authorName;
@override final  String text;
@override final  String createdAt;

/// Create a copy of TaskComment
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TaskCommentCopyWith<_TaskComment> get copyWith => __$TaskCommentCopyWithImpl<_TaskComment>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TaskCommentToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TaskComment&&(identical(other.id, id) || other.id == id)&&(identical(other.authorName, authorName) || other.authorName == authorName)&&(identical(other.text, text) || other.text == text)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,authorName,text,createdAt);
}

@override
String toString() {
    return 'TaskComment(id: $id, authorName: $authorName, text: $text, createdAt: $createdAt)';
}


}

/// @nodoc
abstract mixin class _$TaskCommentCopyWith<$Res> implements $TaskCommentCopyWith<$Res> {
  factory _$TaskCommentCopyWith(_TaskComment value, $Res Function(_TaskComment) _then) = __$TaskCommentCopyWithImpl;
@override @useResult
$Res call({
 String id, String authorName, String text, String createdAt
});




}
/// @nodoc
class __$TaskCommentCopyWithImpl<$Res>
    implements _$TaskCommentCopyWith<$Res> {
  __$TaskCommentCopyWithImpl(this._self, this._then);

  final _TaskComment _self;
  final $Res Function(_TaskComment) _then;

/// Create a copy of TaskComment
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? authorName = null,Object? text = null,Object? createdAt = null,}) {
  return _then(_TaskComment(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,authorName: null == authorName ? _self.authorName : authorName // ignore: cast_nullable_to_non_nullable
as String,text: null == text ? _self.text : text // ignore: cast_nullable_to_non_nullable
as String,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$TaskAttachment {

 String get id; String get fileName; String get contentType; int get sizeBytes; String? get uploadedByName; String get createdAt; bool get canDelete;
/// Create a copy of TaskAttachment
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TaskAttachmentCopyWith<TaskAttachment> get copyWith => _$TaskAttachmentCopyWithImpl<TaskAttachment>(this as TaskAttachment, _$identity);

  /// Serializes this TaskAttachment to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TaskAttachment;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TaskAttachment&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.fileName, _this.fileName) || other.fileName == _this.fileName)&&(identical(other.contentType, _this.contentType) || other.contentType == _this.contentType)&&(identical(other.sizeBytes, _this.sizeBytes) || other.sizeBytes == _this.sizeBytes)&&(identical(other.uploadedByName, _this.uploadedByName) || other.uploadedByName == _this.uploadedByName)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt)&&(identical(other.canDelete, _this.canDelete) || other.canDelete == _this.canDelete));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TaskAttachment;
  return Object.hash(runtimeType,_this.id,_this.fileName,_this.contentType,_this.sizeBytes,_this.uploadedByName,_this.createdAt,_this.canDelete);
}

@override
String toString() {
  final _this = this as TaskAttachment;
  return 'TaskAttachment(id: ${_this.id}, fileName: ${_this.fileName}, contentType: ${_this.contentType}, sizeBytes: ${_this.sizeBytes}, uploadedByName: ${_this.uploadedByName}, createdAt: ${_this.createdAt}, canDelete: ${_this.canDelete})';
}


}

/// @nodoc
abstract mixin class $TaskAttachmentCopyWith<$Res>  {
  factory $TaskAttachmentCopyWith(TaskAttachment value, $Res Function(TaskAttachment) _then) = _$TaskAttachmentCopyWithImpl;
@useResult
$Res call({
 String id, String fileName, String contentType, int sizeBytes, String? uploadedByName, String createdAt, bool canDelete
});




}
/// @nodoc
class _$TaskAttachmentCopyWithImpl<$Res>
    implements $TaskAttachmentCopyWith<$Res> {
  _$TaskAttachmentCopyWithImpl(this._self, this._then);

  final TaskAttachment _self;
  final $Res Function(TaskAttachment) _then;

/// Create a copy of TaskAttachment
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? fileName = null,Object? contentType = null,Object? sizeBytes = null,Object? uploadedByName = freezed,Object? createdAt = null,Object? canDelete = null,}) {
  return _then(TaskAttachment(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,fileName: null == fileName ? _self.fileName : fileName // ignore: cast_nullable_to_non_nullable
as String,contentType: null == contentType ? _self.contentType : contentType // ignore: cast_nullable_to_non_nullable
as String,sizeBytes: null == sizeBytes ? _self.sizeBytes : sizeBytes // ignore: cast_nullable_to_non_nullable
as int,uploadedByName: freezed == uploadedByName ? _self.uploadedByName : uploadedByName // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,canDelete: null == canDelete ? _self.canDelete : canDelete // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [TaskAttachment].
extension TaskAttachmentPatterns on TaskAttachment {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TaskAttachment value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TaskAttachment() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TaskAttachment value)  $default,){
final _that = this;
switch (_that) {
case _TaskAttachment():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TaskAttachment value)?  $default,){
final _that = this;
switch (_that) {
case _TaskAttachment() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String fileName,  String contentType,  int sizeBytes,  String? uploadedByName,  String createdAt,  bool canDelete)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TaskAttachment() when $default != null:
return $default(_that.id,_that.fileName,_that.contentType,_that.sizeBytes,_that.uploadedByName,_that.createdAt,_that.canDelete);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String fileName,  String contentType,  int sizeBytes,  String? uploadedByName,  String createdAt,  bool canDelete)  $default,) {final _that = this;
switch (_that) {
case _TaskAttachment():
return $default(_that.id,_that.fileName,_that.contentType,_that.sizeBytes,_that.uploadedByName,_that.createdAt,_that.canDelete);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String fileName,  String contentType,  int sizeBytes,  String? uploadedByName,  String createdAt,  bool canDelete)?  $default,) {final _that = this;
switch (_that) {
case _TaskAttachment() when $default != null:
return $default(_that.id,_that.fileName,_that.contentType,_that.sizeBytes,_that.uploadedByName,_that.createdAt,_that.canDelete);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TaskAttachment extends TaskAttachment {
  const _TaskAttachment({required this.id, required this.fileName, this.contentType = 'application/octet-stream', this.sizeBytes = 0, this.uploadedByName, required this.createdAt, this.canDelete = false}): super._();
  factory _TaskAttachment.fromJson(Map<String, dynamic> json) => _$TaskAttachmentFromJson(json);

@override final  String id;
@override final  String fileName;
@override@JsonKey() final  String contentType;
@override@JsonKey() final  int sizeBytes;
@override final  String? uploadedByName;
@override final  String createdAt;
@override@JsonKey() final  bool canDelete;

/// Create a copy of TaskAttachment
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TaskAttachmentCopyWith<_TaskAttachment> get copyWith => __$TaskAttachmentCopyWithImpl<_TaskAttachment>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TaskAttachmentToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TaskAttachment&&(identical(other.id, id) || other.id == id)&&(identical(other.fileName, fileName) || other.fileName == fileName)&&(identical(other.contentType, contentType) || other.contentType == contentType)&&(identical(other.sizeBytes, sizeBytes) || other.sizeBytes == sizeBytes)&&(identical(other.uploadedByName, uploadedByName) || other.uploadedByName == uploadedByName)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt)&&(identical(other.canDelete, canDelete) || other.canDelete == canDelete));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,fileName,contentType,sizeBytes,uploadedByName,createdAt,canDelete);
}

@override
String toString() {
    return 'TaskAttachment(id: $id, fileName: $fileName, contentType: $contentType, sizeBytes: $sizeBytes, uploadedByName: $uploadedByName, createdAt: $createdAt, canDelete: $canDelete)';
}


}

/// @nodoc
abstract mixin class _$TaskAttachmentCopyWith<$Res> implements $TaskAttachmentCopyWith<$Res> {
  factory _$TaskAttachmentCopyWith(_TaskAttachment value, $Res Function(_TaskAttachment) _then) = __$TaskAttachmentCopyWithImpl;
@override @useResult
$Res call({
 String id, String fileName, String contentType, int sizeBytes, String? uploadedByName, String createdAt, bool canDelete
});




}
/// @nodoc
class __$TaskAttachmentCopyWithImpl<$Res>
    implements _$TaskAttachmentCopyWith<$Res> {
  __$TaskAttachmentCopyWithImpl(this._self, this._then);

  final _TaskAttachment _self;
  final $Res Function(_TaskAttachment) _then;

/// Create a copy of TaskAttachment
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? fileName = null,Object? contentType = null,Object? sizeBytes = null,Object? uploadedByName = freezed,Object? createdAt = null,Object? canDelete = null,}) {
  return _then(_TaskAttachment(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,fileName: null == fileName ? _self.fileName : fileName // ignore: cast_nullable_to_non_nullable
as String,contentType: null == contentType ? _self.contentType : contentType // ignore: cast_nullable_to_non_nullable
as String,sizeBytes: null == sizeBytes ? _self.sizeBytes : sizeBytes // ignore: cast_nullable_to_non_nullable
as int,uploadedByName: freezed == uploadedByName ? _self.uploadedByName : uploadedByName // ignore: cast_nullable_to_non_nullable
as String?,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,canDelete: null == canDelete ? _self.canDelete : canDelete // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$TaskEvent {

 String get id; String get kind; String get text; String get actorName; String get createdAt;
/// Create a copy of TaskEvent
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TaskEventCopyWith<TaskEvent> get copyWith => _$TaskEventCopyWithImpl<TaskEvent>(this as TaskEvent, _$identity);

  /// Serializes this TaskEvent to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TaskEvent;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TaskEvent&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.kind, _this.kind) || other.kind == _this.kind)&&(identical(other.text, _this.text) || other.text == _this.text)&&(identical(other.actorName, _this.actorName) || other.actorName == _this.actorName)&&(identical(other.createdAt, _this.createdAt) || other.createdAt == _this.createdAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TaskEvent;
  return Object.hash(runtimeType,_this.id,_this.kind,_this.text,_this.actorName,_this.createdAt);
}

@override
String toString() {
  final _this = this as TaskEvent;
  return 'TaskEvent(id: ${_this.id}, kind: ${_this.kind}, text: ${_this.text}, actorName: ${_this.actorName}, createdAt: ${_this.createdAt})';
}


}

/// @nodoc
abstract mixin class $TaskEventCopyWith<$Res>  {
  factory $TaskEventCopyWith(TaskEvent value, $Res Function(TaskEvent) _then) = _$TaskEventCopyWithImpl;
@useResult
$Res call({
 String id, String kind, String text, String actorName, String createdAt
});




}
/// @nodoc
class _$TaskEventCopyWithImpl<$Res>
    implements $TaskEventCopyWith<$Res> {
  _$TaskEventCopyWithImpl(this._self, this._then);

  final TaskEvent _self;
  final $Res Function(TaskEvent) _then;

/// Create a copy of TaskEvent
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? kind = null,Object? text = null,Object? actorName = null,Object? createdAt = null,}) {
  return _then(TaskEvent(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,text: null == text ? _self.text : text // ignore: cast_nullable_to_non_nullable
as String,actorName: null == actorName ? _self.actorName : actorName // ignore: cast_nullable_to_non_nullable
as String,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [TaskEvent].
extension TaskEventPatterns on TaskEvent {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TaskEvent value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TaskEvent() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TaskEvent value)  $default,){
final _that = this;
switch (_that) {
case _TaskEvent():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TaskEvent value)?  $default,){
final _that = this;
switch (_that) {
case _TaskEvent() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String kind,  String text,  String actorName,  String createdAt)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TaskEvent() when $default != null:
return $default(_that.id,_that.kind,_that.text,_that.actorName,_that.createdAt);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String kind,  String text,  String actorName,  String createdAt)  $default,) {final _that = this;
switch (_that) {
case _TaskEvent():
return $default(_that.id,_that.kind,_that.text,_that.actorName,_that.createdAt);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String kind,  String text,  String actorName,  String createdAt)?  $default,) {final _that = this;
switch (_that) {
case _TaskEvent() when $default != null:
return $default(_that.id,_that.kind,_that.text,_that.actorName,_that.createdAt);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TaskEvent implements TaskEvent {
  const _TaskEvent({required this.id, this.kind = '', required this.text, this.actorName = '', required this.createdAt});
  factory _TaskEvent.fromJson(Map<String, dynamic> json) => _$TaskEventFromJson(json);

@override final  String id;
@override@JsonKey() final  String kind;
@override final  String text;
@override@JsonKey() final  String actorName;
@override final  String createdAt;

/// Create a copy of TaskEvent
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TaskEventCopyWith<_TaskEvent> get copyWith => __$TaskEventCopyWithImpl<_TaskEvent>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TaskEventToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TaskEvent&&(identical(other.id, id) || other.id == id)&&(identical(other.kind, kind) || other.kind == kind)&&(identical(other.text, text) || other.text == text)&&(identical(other.actorName, actorName) || other.actorName == actorName)&&(identical(other.createdAt, createdAt) || other.createdAt == createdAt));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,kind,text,actorName,createdAt);
}

@override
String toString() {
    return 'TaskEvent(id: $id, kind: $kind, text: $text, actorName: $actorName, createdAt: $createdAt)';
}


}

/// @nodoc
abstract mixin class _$TaskEventCopyWith<$Res> implements $TaskEventCopyWith<$Res> {
  factory _$TaskEventCopyWith(_TaskEvent value, $Res Function(_TaskEvent) _then) = __$TaskEventCopyWithImpl;
@override @useResult
$Res call({
 String id, String kind, String text, String actorName, String createdAt
});




}
/// @nodoc
class __$TaskEventCopyWithImpl<$Res>
    implements _$TaskEventCopyWith<$Res> {
  __$TaskEventCopyWithImpl(this._self, this._then);

  final _TaskEvent _self;
  final $Res Function(_TaskEvent) _then;

/// Create a copy of TaskEvent
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? kind = null,Object? text = null,Object? actorName = null,Object? createdAt = null,}) {
  return _then(_TaskEvent(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,kind: null == kind ? _self.kind : kind // ignore: cast_nullable_to_non_nullable
as String,text: null == text ? _self.text : text // ignore: cast_nullable_to_non_nullable
as String,actorName: null == actorName ? _self.actorName : actorName // ignore: cast_nullable_to_non_nullable
as String,createdAt: null == createdAt ? _self.createdAt : createdAt // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}


/// @nodoc
mixin _$TaskDetail {

 TaskRow get task; List<ChecklistItem> get checklist; List<TaskComment> get comments; List<TaskAttachment> get attachments; List<TaskEvent> get history; bool get canComment; bool get canAttach;
/// Create a copy of TaskDetail
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$TaskDetailCopyWith<TaskDetail> get copyWith => _$TaskDetailCopyWithImpl<TaskDetail>(this as TaskDetail, _$identity);

  /// Serializes this TaskDetail to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as TaskDetail;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is TaskDetail&&(identical(other.task, _this.task) || other.task == _this.task)&&const DeepCollectionEquality().equals(other.checklist, _this.checklist)&&const DeepCollectionEquality().equals(other.comments, _this.comments)&&const DeepCollectionEquality().equals(other.attachments, _this.attachments)&&const DeepCollectionEquality().equals(other.history, _this.history)&&(identical(other.canComment, _this.canComment) || other.canComment == _this.canComment)&&(identical(other.canAttach, _this.canAttach) || other.canAttach == _this.canAttach));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as TaskDetail;
  return Object.hash(runtimeType,_this.task,const DeepCollectionEquality().hash(_this.checklist),const DeepCollectionEquality().hash(_this.comments),const DeepCollectionEquality().hash(_this.attachments),const DeepCollectionEquality().hash(_this.history),_this.canComment,_this.canAttach);
}

@override
String toString() {
  final _this = this as TaskDetail;
  return 'TaskDetail(task: ${_this.task}, checklist: ${_this.checklist}, comments: ${_this.comments}, attachments: ${_this.attachments}, history: ${_this.history}, canComment: ${_this.canComment}, canAttach: ${_this.canAttach})';
}


}

/// @nodoc
abstract mixin class $TaskDetailCopyWith<$Res>  {
  factory $TaskDetailCopyWith(TaskDetail value, $Res Function(TaskDetail) _then) = _$TaskDetailCopyWithImpl;
@useResult
$Res call({
 TaskRow task, List<ChecklistItem> checklist, List<TaskComment> comments, List<TaskAttachment> attachments, List<TaskEvent> history, bool canComment, bool canAttach
});


$TaskRowCopyWith<$Res> get task;

}
/// @nodoc
class _$TaskDetailCopyWithImpl<$Res>
    implements $TaskDetailCopyWith<$Res> {
  _$TaskDetailCopyWithImpl(this._self, this._then);

  final TaskDetail _self;
  final $Res Function(TaskDetail) _then;

/// Create a copy of TaskDetail
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? task = null,Object? checklist = null,Object? comments = null,Object? attachments = null,Object? history = null,Object? canComment = null,Object? canAttach = null,}) {
  return _then(TaskDetail(
task: null == task ? _self.task : task // ignore: cast_nullable_to_non_nullable
as TaskRow,checklist: null == checklist ? _self.checklist : checklist // ignore: cast_nullable_to_non_nullable
as List<ChecklistItem>,comments: null == comments ? _self.comments : comments // ignore: cast_nullable_to_non_nullable
as List<TaskComment>,attachments: null == attachments ? _self.attachments : attachments // ignore: cast_nullable_to_non_nullable
as List<TaskAttachment>,history: null == history ? _self.history : history // ignore: cast_nullable_to_non_nullable
as List<TaskEvent>,canComment: null == canComment ? _self.canComment : canComment // ignore: cast_nullable_to_non_nullable
as bool,canAttach: null == canAttach ? _self.canAttach : canAttach // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}
/// Create a copy of TaskDetail
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TaskRowCopyWith<$Res> get task {
  
  return $TaskRowCopyWith<$Res>(_self.task, (value) {
    return _then(_self.copyWith(task: value));
  });
}
}


/// Adds pattern-matching-related methods to [TaskDetail].
extension TaskDetailPatterns on TaskDetail {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _TaskDetail value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _TaskDetail() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _TaskDetail value)  $default,){
final _that = this;
switch (_that) {
case _TaskDetail():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _TaskDetail value)?  $default,){
final _that = this;
switch (_that) {
case _TaskDetail() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( TaskRow task,  List<ChecklistItem> checklist,  List<TaskComment> comments,  List<TaskAttachment> attachments,  List<TaskEvent> history,  bool canComment,  bool canAttach)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _TaskDetail() when $default != null:
return $default(_that.task,_that.checklist,_that.comments,_that.attachments,_that.history,_that.canComment,_that.canAttach);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( TaskRow task,  List<ChecklistItem> checklist,  List<TaskComment> comments,  List<TaskAttachment> attachments,  List<TaskEvent> history,  bool canComment,  bool canAttach)  $default,) {final _that = this;
switch (_that) {
case _TaskDetail():
return $default(_that.task,_that.checklist,_that.comments,_that.attachments,_that.history,_that.canComment,_that.canAttach);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( TaskRow task,  List<ChecklistItem> checklist,  List<TaskComment> comments,  List<TaskAttachment> attachments,  List<TaskEvent> history,  bool canComment,  bool canAttach)?  $default,) {final _that = this;
switch (_that) {
case _TaskDetail() when $default != null:
return $default(_that.task,_that.checklist,_that.comments,_that.attachments,_that.history,_that.canComment,_that.canAttach);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _TaskDetail implements TaskDetail {
  const _TaskDetail({required this.task,  List<ChecklistItem> checklist = const <ChecklistItem>[],  List<TaskComment> comments = const <TaskComment>[],  List<TaskAttachment> attachments = const <TaskAttachment>[],  List<TaskEvent> history = const <TaskEvent>[], this.canComment = true, this.canAttach = false}): _checklist = checklist,_comments = comments,_attachments = attachments,_history = history;
  factory _TaskDetail.fromJson(Map<String, dynamic> json) => _$TaskDetailFromJson(json);

@override final  TaskRow task;
 final  List<ChecklistItem> _checklist;
@override@JsonKey() List<ChecklistItem> get checklist {
  if (_checklist is EqualUnmodifiableListView) return _checklist;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_checklist);
}

 final  List<TaskComment> _comments;
@override@JsonKey() List<TaskComment> get comments {
  if (_comments is EqualUnmodifiableListView) return _comments;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_comments);
}

 final  List<TaskAttachment> _attachments;
@override@JsonKey() List<TaskAttachment> get attachments {
  if (_attachments is EqualUnmodifiableListView) return _attachments;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_attachments);
}

 final  List<TaskEvent> _history;
@override@JsonKey() List<TaskEvent> get history {
  if (_history is EqualUnmodifiableListView) return _history;
  // ignore: implicit_dynamic_type
  return EqualUnmodifiableListView(_history);
}

@override@JsonKey() final  bool canComment;
@override@JsonKey() final  bool canAttach;

/// Create a copy of TaskDetail
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$TaskDetailCopyWith<_TaskDetail> get copyWith => __$TaskDetailCopyWithImpl<_TaskDetail>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$TaskDetailToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _TaskDetail&&(identical(other.task, task) || other.task == task)&&const DeepCollectionEquality().equals(other.checklist, _checklist)&&const DeepCollectionEquality().equals(other.comments, _comments)&&const DeepCollectionEquality().equals(other.attachments, _attachments)&&const DeepCollectionEquality().equals(other.history, _history)&&(identical(other.canComment, canComment) || other.canComment == canComment)&&(identical(other.canAttach, canAttach) || other.canAttach == canAttach));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,task,const DeepCollectionEquality().hash(_checklist),const DeepCollectionEquality().hash(_comments),const DeepCollectionEquality().hash(_attachments),const DeepCollectionEquality().hash(_history),canComment,canAttach);
}

@override
String toString() {
    return 'TaskDetail(task: $task, checklist: $checklist, comments: $comments, attachments: $attachments, history: $history, canComment: $canComment, canAttach: $canAttach)';
}


}

/// @nodoc
abstract mixin class _$TaskDetailCopyWith<$Res> implements $TaskDetailCopyWith<$Res> {
  factory _$TaskDetailCopyWith(_TaskDetail value, $Res Function(_TaskDetail) _then) = __$TaskDetailCopyWithImpl;
@override @useResult
$Res call({
 TaskRow task, List<ChecklistItem> checklist, List<TaskComment> comments, List<TaskAttachment> attachments, List<TaskEvent> history, bool canComment, bool canAttach
});


@override $TaskRowCopyWith<$Res> get task;

}
/// @nodoc
class __$TaskDetailCopyWithImpl<$Res>
    implements _$TaskDetailCopyWith<$Res> {
  __$TaskDetailCopyWithImpl(this._self, this._then);

  final _TaskDetail _self;
  final $Res Function(_TaskDetail) _then;

/// Create a copy of TaskDetail
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? task = null,Object? checklist = null,Object? comments = null,Object? attachments = null,Object? history = null,Object? canComment = null,Object? canAttach = null,}) {
  return _then(_TaskDetail(
task: null == task ? _self.task : task // ignore: cast_nullable_to_non_nullable
as TaskRow,checklist: null == checklist ? _self._checklist : checklist // ignore: cast_nullable_to_non_nullable
as List<ChecklistItem>,comments: null == comments ? _self._comments : comments // ignore: cast_nullable_to_non_nullable
as List<TaskComment>,attachments: null == attachments ? _self._attachments : attachments // ignore: cast_nullable_to_non_nullable
as List<TaskAttachment>,history: null == history ? _self._history : history // ignore: cast_nullable_to_non_nullable
as List<TaskEvent>,canComment: null == canComment ? _self.canComment : canComment // ignore: cast_nullable_to_non_nullable
as bool,canAttach: null == canAttach ? _self.canAttach : canAttach // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

/// Create a copy of TaskDetail
/// with the given fields replaced by the non-null parameter values.
@override
@pragma('vm:prefer-inline')
$TaskRowCopyWith<$Res> get task {
  
  return $TaskRowCopyWith<$Res>(_self.task, (value) {
    return _then(_self.copyWith(task: value));
  });
}
}


/// @nodoc
mixin _$WorkDepartmentRef {

 String get id; String get code; String get name; bool get hasTasks; bool get isMine;
/// Create a copy of WorkDepartmentRef
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$WorkDepartmentRefCopyWith<WorkDepartmentRef> get copyWith => _$WorkDepartmentRefCopyWithImpl<WorkDepartmentRef>(this as WorkDepartmentRef, _$identity);

  /// Serializes this WorkDepartmentRef to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as WorkDepartmentRef;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is WorkDepartmentRef&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.code, _this.code) || other.code == _this.code)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.hasTasks, _this.hasTasks) || other.hasTasks == _this.hasTasks)&&(identical(other.isMine, _this.isMine) || other.isMine == _this.isMine));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as WorkDepartmentRef;
  return Object.hash(runtimeType,_this.id,_this.code,_this.name,_this.hasTasks,_this.isMine);
}

@override
String toString() {
  final _this = this as WorkDepartmentRef;
  return 'WorkDepartmentRef(id: ${_this.id}, code: ${_this.code}, name: ${_this.name}, hasTasks: ${_this.hasTasks}, isMine: ${_this.isMine})';
}


}

/// @nodoc
abstract mixin class $WorkDepartmentRefCopyWith<$Res>  {
  factory $WorkDepartmentRefCopyWith(WorkDepartmentRef value, $Res Function(WorkDepartmentRef) _then) = _$WorkDepartmentRefCopyWithImpl;
@useResult
$Res call({
 String id, String code, String name, bool hasTasks, bool isMine
});




}
/// @nodoc
class _$WorkDepartmentRefCopyWithImpl<$Res>
    implements $WorkDepartmentRefCopyWith<$Res> {
  _$WorkDepartmentRefCopyWithImpl(this._self, this._then);

  final WorkDepartmentRef _self;
  final $Res Function(WorkDepartmentRef) _then;

/// Create a copy of WorkDepartmentRef
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? code = null,Object? name = null,Object? hasTasks = null,Object? isMine = null,}) {
  return _then(WorkDepartmentRef(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,hasTasks: null == hasTasks ? _self.hasTasks : hasTasks // ignore: cast_nullable_to_non_nullable
as bool,isMine: null == isMine ? _self.isMine : isMine // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [WorkDepartmentRef].
extension WorkDepartmentRefPatterns on WorkDepartmentRef {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _WorkDepartmentRef value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _WorkDepartmentRef() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _WorkDepartmentRef value)  $default,){
final _that = this;
switch (_that) {
case _WorkDepartmentRef():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _WorkDepartmentRef value)?  $default,){
final _that = this;
switch (_that) {
case _WorkDepartmentRef() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String code,  String name,  bool hasTasks,  bool isMine)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _WorkDepartmentRef() when $default != null:
return $default(_that.id,_that.code,_that.name,_that.hasTasks,_that.isMine);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String code,  String name,  bool hasTasks,  bool isMine)  $default,) {final _that = this;
switch (_that) {
case _WorkDepartmentRef():
return $default(_that.id,_that.code,_that.name,_that.hasTasks,_that.isMine);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String code,  String name,  bool hasTasks,  bool isMine)?  $default,) {final _that = this;
switch (_that) {
case _WorkDepartmentRef() when $default != null:
return $default(_that.id,_that.code,_that.name,_that.hasTasks,_that.isMine);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _WorkDepartmentRef implements WorkDepartmentRef {
  const _WorkDepartmentRef({required this.id, required this.code, required this.name, this.hasTasks = false, this.isMine = false});
  factory _WorkDepartmentRef.fromJson(Map<String, dynamic> json) => _$WorkDepartmentRefFromJson(json);

@override final  String id;
@override final  String code;
@override final  String name;
@override@JsonKey() final  bool hasTasks;
@override@JsonKey() final  bool isMine;

/// Create a copy of WorkDepartmentRef
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$WorkDepartmentRefCopyWith<_WorkDepartmentRef> get copyWith => __$WorkDepartmentRefCopyWithImpl<_WorkDepartmentRef>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$WorkDepartmentRefToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _WorkDepartmentRef&&(identical(other.id, id) || other.id == id)&&(identical(other.code, code) || other.code == code)&&(identical(other.name, name) || other.name == name)&&(identical(other.hasTasks, hasTasks) || other.hasTasks == hasTasks)&&(identical(other.isMine, isMine) || other.isMine == isMine));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,code,name,hasTasks,isMine);
}

@override
String toString() {
    return 'WorkDepartmentRef(id: $id, code: $code, name: $name, hasTasks: $hasTasks, isMine: $isMine)';
}


}

/// @nodoc
abstract mixin class _$WorkDepartmentRefCopyWith<$Res> implements $WorkDepartmentRefCopyWith<$Res> {
  factory _$WorkDepartmentRefCopyWith(_WorkDepartmentRef value, $Res Function(_WorkDepartmentRef) _then) = __$WorkDepartmentRefCopyWithImpl;
@override @useResult
$Res call({
 String id, String code, String name, bool hasTasks, bool isMine
});




}
/// @nodoc
class __$WorkDepartmentRefCopyWithImpl<$Res>
    implements _$WorkDepartmentRefCopyWith<$Res> {
  __$WorkDepartmentRefCopyWithImpl(this._self, this._then);

  final _WorkDepartmentRef _self;
  final $Res Function(_WorkDepartmentRef) _then;

/// Create a copy of WorkDepartmentRef
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? code = null,Object? name = null,Object? hasTasks = null,Object? isMine = null,}) {
  return _then(_WorkDepartmentRef(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,code: null == code ? _self.code : code // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,hasTasks: null == hasTasks ? _self.hasTasks : hasTasks // ignore: cast_nullable_to_non_nullable
as bool,isMine: null == isMine ? _self.isMine : isMine // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}


/// @nodoc
mixin _$Assignee {

 String get id; String get name; String? get position; String? get department; bool get inDepartment;
/// Create a copy of Assignee
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$AssigneeCopyWith<Assignee> get copyWith => _$AssigneeCopyWithImpl<Assignee>(this as Assignee, _$identity);

  /// Serializes this Assignee to a JSON map.
  Map<String, dynamic> toJson();


@override
bool operator ==(Object other) {
  final _this = this as Assignee;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is Assignee&&(identical(other.id, _this.id) || other.id == _this.id)&&(identical(other.name, _this.name) || other.name == _this.name)&&(identical(other.position, _this.position) || other.position == _this.position)&&(identical(other.department, _this.department) || other.department == _this.department)&&(identical(other.inDepartment, _this.inDepartment) || other.inDepartment == _this.inDepartment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
  final _this = this as Assignee;
  return Object.hash(runtimeType,_this.id,_this.name,_this.position,_this.department,_this.inDepartment);
}

@override
String toString() {
  final _this = this as Assignee;
  return 'Assignee(id: ${_this.id}, name: ${_this.name}, position: ${_this.position}, department: ${_this.department}, inDepartment: ${_this.inDepartment})';
}


}

/// @nodoc
abstract mixin class $AssigneeCopyWith<$Res>  {
  factory $AssigneeCopyWith(Assignee value, $Res Function(Assignee) _then) = _$AssigneeCopyWithImpl;
@useResult
$Res call({
 String id, String name, String? position, String? department, bool inDepartment
});




}
/// @nodoc
class _$AssigneeCopyWithImpl<$Res>
    implements $AssigneeCopyWith<$Res> {
  _$AssigneeCopyWithImpl(this._self, this._then);

  final Assignee _self;
  final $Res Function(Assignee) _then;

/// Create a copy of Assignee
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? id = null,Object? name = null,Object? position = freezed,Object? department = freezed,Object? inDepartment = null,}) {
  return _then(Assignee(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,position: freezed == position ? _self.position : position // ignore: cast_nullable_to_non_nullable
as String?,department: freezed == department ? _self.department : department // ignore: cast_nullable_to_non_nullable
as String?,inDepartment: null == inDepartment ? _self.inDepartment : inDepartment // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}

}


/// Adds pattern-matching-related methods to [Assignee].
extension AssigneePatterns on Assignee {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>(TResult Function( _Assignee value)?  $default,{required TResult orElse(),}){
final _that = this;
switch (_that) {
case _Assignee() when $default != null:
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

@optionalTypeArgs TResult map<TResult extends Object?>(TResult Function( _Assignee value)  $default,){
final _that = this;
switch (_that) {
case _Assignee():
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>(TResult? Function( _Assignee value)?  $default,){
final _that = this;
switch (_that) {
case _Assignee() when $default != null:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>(TResult Function( String id,  String name,  String? position,  String? department,  bool inDepartment)?  $default,{required TResult orElse(),}) {final _that = this;
switch (_that) {
case _Assignee() when $default != null:
return $default(_that.id,_that.name,_that.position,_that.department,_that.inDepartment);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>(TResult Function( String id,  String name,  String? position,  String? department,  bool inDepartment)  $default,) {final _that = this;
switch (_that) {
case _Assignee():
return $default(_that.id,_that.name,_that.position,_that.department,_that.inDepartment);case _:
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>(TResult? Function( String id,  String name,  String? position,  String? department,  bool inDepartment)?  $default,) {final _that = this;
switch (_that) {
case _Assignee() when $default != null:
return $default(_that.id,_that.name,_that.position,_that.department,_that.inDepartment);case _:
  return null;

}
}

}

/// @nodoc
@JsonSerializable()

class _Assignee implements Assignee {
  const _Assignee({required this.id, required this.name, this.position, this.department, this.inDepartment = false});
  factory _Assignee.fromJson(Map<String, dynamic> json) => _$AssigneeFromJson(json);

@override final  String id;
@override final  String name;
@override final  String? position;
@override final  String? department;
@override@JsonKey() final  bool inDepartment;

/// Create a copy of Assignee
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
_$AssigneeCopyWith<_Assignee> get copyWith => __$AssigneeCopyWithImpl<_Assignee>(this, _$identity);

@override
Map<String, dynamic> toJson() {
  return _$AssigneeToJson(this, );
}

@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is _Assignee&&(identical(other.id, id) || other.id == id)&&(identical(other.name, name) || other.name == name)&&(identical(other.position, position) || other.position == position)&&(identical(other.department, department) || other.department == department)&&(identical(other.inDepartment, inDepartment) || other.inDepartment == inDepartment));
}

@JsonKey(includeFromJson: false, includeToJson: false)
@override
int get hashCode {
    return Object.hash(runtimeType,id,name,position,department,inDepartment);
}

@override
String toString() {
    return 'Assignee(id: $id, name: $name, position: $position, department: $department, inDepartment: $inDepartment)';
}


}

/// @nodoc
abstract mixin class _$AssigneeCopyWith<$Res> implements $AssigneeCopyWith<$Res> {
  factory _$AssigneeCopyWith(_Assignee value, $Res Function(_Assignee) _then) = __$AssigneeCopyWithImpl;
@override @useResult
$Res call({
 String id, String name, String? position, String? department, bool inDepartment
});




}
/// @nodoc
class __$AssigneeCopyWithImpl<$Res>
    implements _$AssigneeCopyWith<$Res> {
  __$AssigneeCopyWithImpl(this._self, this._then);

  final _Assignee _self;
  final $Res Function(_Assignee) _then;

/// Create a copy of Assignee
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? id = null,Object? name = null,Object? position = freezed,Object? department = freezed,Object? inDepartment = null,}) {
  return _then(_Assignee(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as String,name: null == name ? _self.name : name // ignore: cast_nullable_to_non_nullable
as String,position: freezed == position ? _self.position : position // ignore: cast_nullable_to_non_nullable
as String?,department: freezed == department ? _self.department : department // ignore: cast_nullable_to_non_nullable
as String?,inDepartment: null == inDepartment ? _self.inDepartment : inDepartment // ignore: cast_nullable_to_non_nullable
as bool,
  ));
}


}

// dart format on
