// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'task_models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_TaskRow _$TaskRowFromJson(Map<String, dynamic> json) => _TaskRow(
  id: json['id'] as String,
  title: json['title'] as String,
  description: json['description'] as String?,
  priority: json['priority'] as String? ?? 'Medium',
  status: json['status'] as String,
  dueDate: json['dueDate'] as String?,
  dueTime: json['dueTime'] as String?,
  overdue: json['overdue'] as bool? ?? false,
  assigneeId: json['assigneeId'] as String?,
  assigneeName: json['assigneeName'] as String?,
  createdByName: json['createdByName'] as String?,
  createdAt: json['createdAt'] as String,
  result: json['result'] as String?,
  nextStatuses:
      (json['nextStatuses'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList() ??
      const <String>[],
  departmentCode: json['departmentCode'] as String? ?? '',
  departmentName: json['departmentName'] as String? ?? '',
  projectName: json['projectName'] as String?,
  progress: (json['progress'] as num?)?.toInt() ?? 0,
  isBlocked: json['isBlocked'] as bool? ?? false,
  blockedReason: json['blockedReason'] as String?,
  archivedAt: json['archivedAt'] as String?,
  checklistDone: (json['checklistDone'] as num?)?.toInt() ?? 0,
  checklistTotal: (json['checklistTotal'] as num?)?.toInt() ?? 0,
  canManage: json['canManage'] as bool? ?? false,
  canWork: json['canWork'] as bool? ?? false,
);

Map<String, dynamic> _$TaskRowToJson(_TaskRow instance) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'description': ?instance.description,
  'priority': instance.priority,
  'status': instance.status,
  'dueDate': ?instance.dueDate,
  'dueTime': ?instance.dueTime,
  'overdue': instance.overdue,
  'assigneeId': ?instance.assigneeId,
  'assigneeName': ?instance.assigneeName,
  'createdByName': ?instance.createdByName,
  'createdAt': instance.createdAt,
  'result': ?instance.result,
  'nextStatuses': instance.nextStatuses,
  'departmentCode': instance.departmentCode,
  'departmentName': instance.departmentName,
  'projectName': ?instance.projectName,
  'progress': instance.progress,
  'isBlocked': instance.isBlocked,
  'blockedReason': ?instance.blockedReason,
  'archivedAt': ?instance.archivedAt,
  'checklistDone': instance.checklistDone,
  'checklistTotal': instance.checklistTotal,
  'canManage': instance.canManage,
  'canWork': instance.canWork,
};

_ChecklistItem _$ChecklistItemFromJson(Map<String, dynamic> json) =>
    _ChecklistItem(
      id: json['id'] as String,
      title: json['title'] as String,
      isDone: json['isDone'] as bool? ?? false,
      sortOrder: (json['sortOrder'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$ChecklistItemToJson(_ChecklistItem instance) =>
    <String, dynamic>{
      'id': instance.id,
      'title': instance.title,
      'isDone': instance.isDone,
      'sortOrder': instance.sortOrder,
    };

_TaskComment _$TaskCommentFromJson(Map<String, dynamic> json) => _TaskComment(
  id: json['id'] as String,
  authorName: json['authorName'] as String? ?? '',
  text: json['text'] as String,
  createdAt: json['createdAt'] as String,
);

Map<String, dynamic> _$TaskCommentToJson(_TaskComment instance) =>
    <String, dynamic>{
      'id': instance.id,
      'authorName': instance.authorName,
      'text': instance.text,
      'createdAt': instance.createdAt,
    };

_TaskAttachment _$TaskAttachmentFromJson(Map<String, dynamic> json) =>
    _TaskAttachment(
      id: json['id'] as String,
      fileName: json['fileName'] as String,
      contentType: json['contentType'] as String? ?? 'application/octet-stream',
      sizeBytes: (json['sizeBytes'] as num?)?.toInt() ?? 0,
      uploadedByName: json['uploadedByName'] as String?,
      createdAt: json['createdAt'] as String,
      canDelete: json['canDelete'] as bool? ?? false,
    );

Map<String, dynamic> _$TaskAttachmentToJson(_TaskAttachment instance) =>
    <String, dynamic>{
      'id': instance.id,
      'fileName': instance.fileName,
      'contentType': instance.contentType,
      'sizeBytes': instance.sizeBytes,
      'uploadedByName': ?instance.uploadedByName,
      'createdAt': instance.createdAt,
      'canDelete': instance.canDelete,
    };

_TaskEvent _$TaskEventFromJson(Map<String, dynamic> json) => _TaskEvent(
  id: json['id'] as String,
  kind: json['kind'] as String? ?? '',
  text: json['text'] as String,
  actorName: json['actorName'] as String? ?? '',
  createdAt: json['createdAt'] as String,
);

Map<String, dynamic> _$TaskEventToJson(_TaskEvent instance) =>
    <String, dynamic>{
      'id': instance.id,
      'kind': instance.kind,
      'text': instance.text,
      'actorName': instance.actorName,
      'createdAt': instance.createdAt,
    };

_TaskDetail _$TaskDetailFromJson(Map<String, dynamic> json) => _TaskDetail(
  task: TaskRow.fromJson(json['task'] as Map<String, dynamic>),
  checklist:
      (json['checklist'] as List<dynamic>?)
          ?.map((e) => ChecklistItem.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <ChecklistItem>[],
  comments:
      (json['comments'] as List<dynamic>?)
          ?.map((e) => TaskComment.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <TaskComment>[],
  attachments:
      (json['attachments'] as List<dynamic>?)
          ?.map((e) => TaskAttachment.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <TaskAttachment>[],
  history:
      (json['history'] as List<dynamic>?)
          ?.map((e) => TaskEvent.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <TaskEvent>[],
  canComment: json['canComment'] as bool? ?? true,
  canAttach: json['canAttach'] as bool? ?? false,
);

Map<String, dynamic> _$TaskDetailToJson(_TaskDetail instance) =>
    <String, dynamic>{
      'task': instance.task.toJson(),
      'checklist': instance.checklist.map((e) => e.toJson()).toList(),
      'comments': instance.comments.map((e) => e.toJson()).toList(),
      'attachments': instance.attachments.map((e) => e.toJson()).toList(),
      'history': instance.history.map((e) => e.toJson()).toList(),
      'canComment': instance.canComment,
      'canAttach': instance.canAttach,
    };

_WorkDepartmentRef _$WorkDepartmentRefFromJson(Map<String, dynamic> json) =>
    _WorkDepartmentRef(
      id: json['id'] as String,
      code: json['code'] as String,
      name: json['name'] as String,
      hasTasks: json['hasTasks'] as bool? ?? false,
      isMine: json['isMine'] as bool? ?? false,
    );

Map<String, dynamic> _$WorkDepartmentRefToJson(_WorkDepartmentRef instance) =>
    <String, dynamic>{
      'id': instance.id,
      'code': instance.code,
      'name': instance.name,
      'hasTasks': instance.hasTasks,
      'isMine': instance.isMine,
    };

_Assignee _$AssigneeFromJson(Map<String, dynamic> json) => _Assignee(
  id: json['id'] as String,
  name: json['name'] as String,
  position: json['position'] as String?,
  department: json['department'] as String?,
  inDepartment: json['inDepartment'] as bool? ?? false,
);

Map<String, dynamic> _$AssigneeToJson(_Assignee instance) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'position': ?instance.position,
  'department': ?instance.department,
  'inDepartment': instance.inDepartment,
};
