import 'package:freezed_annotation/freezed_annotation.dart';

part 'task_models.freezed.dart';
part 'task_models.g.dart';

// Задачи (`/api/work/*`, право `work.tasks`). Кто видит, ведёт и управляет — решает сервер; разрешённые переходы статуса приходят
// готовым списком `nextStatuses`. Тексты истории — с сервера (по-русски).

@freezed
abstract class TaskRow with _$TaskRow {
  const factory TaskRow({
    required String id,
    required String title,
    String? description,

    /// Low | Medium | High | Urgent
    @Default('Medium') String priority,

    /// New | InProgress | Review | Done | Cancelled
    required String status,
    String? dueDate,
    String? dueTime,
    @Default(false) bool overdue,
    String? assigneeId,
    String? assigneeName,
    String? createdByName,
    required String createdAt,
    String? result,
    @Default(<String>[]) List<String> nextStatuses,
    @Default('') String departmentCode,
    @Default('') String departmentName,
    String? projectName,
    @Default(0) int progress,
    @Default(false) bool isBlocked,
    String? blockedReason,
    String? archivedAt,
    @Default(0) int checklistDone,
    @Default(0) int checklistTotal,
    @Default(false) bool canManage,
    @Default(false) bool canWork,
  }) = _TaskRow;

  factory TaskRow.fromJson(Map<String, Object?> json) => _$TaskRowFromJson(json);
}

@freezed
abstract class ChecklistItem with _$ChecklistItem {
  const factory ChecklistItem({required String id, required String title, @Default(false) bool isDone, @Default(0) int sortOrder}) = _ChecklistItem;

  factory ChecklistItem.fromJson(Map<String, Object?> json) => _$ChecklistItemFromJson(json);
}

@freezed
abstract class TaskComment with _$TaskComment {
  const factory TaskComment({required String id, @Default('') String authorName, required String text, required String createdAt}) = _TaskComment;

  factory TaskComment.fromJson(Map<String, Object?> json) => _$TaskCommentFromJson(json);
}

@freezed
abstract class TaskAttachment with _$TaskAttachment {
  const factory TaskAttachment({
    required String id,
    required String fileName,
    @Default('application/octet-stream') String contentType,
    @Default(0) int sizeBytes,
    String? uploadedByName,
    required String createdAt,
    @Default(false) bool canDelete,
  }) = _TaskAttachment;

  const TaskAttachment._();

  factory TaskAttachment.fromJson(Map<String, Object?> json) => _$TaskAttachmentFromJson(json);

  bool get isImage => contentType.startsWith('image/');
}

@freezed
abstract class TaskEvent with _$TaskEvent {
  const factory TaskEvent({required String id, @Default('') String kind, required String text, @Default('') String actorName, required String createdAt}) =
      _TaskEvent;

  factory TaskEvent.fromJson(Map<String, Object?> json) => _$TaskEventFromJson(json);
}

@freezed
abstract class TaskDetail with _$TaskDetail {
  const factory TaskDetail({
    required TaskRow task,
    @Default(<ChecklistItem>[]) List<ChecklistItem> checklist,
    @Default(<TaskComment>[]) List<TaskComment> comments,
    @Default(<TaskAttachment>[]) List<TaskAttachment> attachments,
    @Default(<TaskEvent>[]) List<TaskEvent> history,
    @Default(true) bool canComment,
    @Default(false) bool canAttach,
  }) = _TaskDetail;

  factory TaskDetail.fromJson(Map<String, Object?> json) => _$TaskDetailFromJson(json);
}

@freezed
abstract class WorkDepartmentRef with _$WorkDepartmentRef {
  const factory WorkDepartmentRef({
    required String id,
    required String code,
    required String name,
    @Default(false) bool hasTasks,
    @Default(false) bool isMine,
  }) = _WorkDepartmentRef;

  factory WorkDepartmentRef.fromJson(Map<String, Object?> json) => _$WorkDepartmentRefFromJson(json);
}

@freezed
abstract class Assignee with _$Assignee {
  const factory Assignee({required String id, required String name, String? position, String? department, @Default(false) bool inDepartment}) = _Assignee;

  factory Assignee.fromJson(Map<String, Object?> json) => _$AssigneeFromJson(json);
}
