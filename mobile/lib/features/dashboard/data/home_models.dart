import 'package:freezed_annotation/freezed_annotation.dart';

part 'home_models.freezed.dart';
part 'home_models.g.dart';

// «Ждут вас» на главной — `GET /api/procurement/home` (ProcurementHomeView): согласования, «можно получить на складе»,
// принято на склад за неделю (директору и финансисту). Только нужные главной поля.

@freezed
abstract class ProcurementRoles with _$ProcurementRoles {
  const factory ProcurementRoles({@Default(false) bool director, @Default(false) bool financier}) = _ProcurementRoles;

  factory ProcurementRoles.fromJson(Map<String, Object?> json) => _$ProcurementRolesFromJson(json);
}

@freezed
abstract class ApprovalBrief with _$ApprovalBrief {
  const factory ApprovalBrief({
    required String id,
    required String number,
    required String title,
    @Default(0) double amount,
    String? department,
  }) = _ApprovalBrief;

  factory ApprovalBrief.fromJson(Map<String, Object?> json) => _$ApprovalBriefFromJson(json);
}

@freezed
abstract class HomeRequests with _$HomeRequests {
  const factory HomeRequests({
    @Default(0) int awaitingMe,
    @Default(<ApprovalBrief>[]) List<ApprovalBrief> approvals,
    @Default(0) int departmentToPickUp,
    @Default(0) int mineToPickUp,
  }) = _HomeRequests;

  factory HomeRequests.fromJson(Map<String, Object?> json) => _$HomeRequestsFromJson(json);
}

@freezed
abstract class AcceptedArrival with _$AcceptedArrival {
  const factory AcceptedArrival({
    required String id,
    required String number,
    required String supplierName,
    String? summary,
    String? acceptedAt,
  }) = _AcceptedArrival;

  factory AcceptedArrival.fromJson(Map<String, Object?> json) => _$AcceptedArrivalFromJson(json);
}

@freezed
abstract class ProcurementHome with _$ProcurementHome {
  const factory ProcurementHome({
    @Default(ProcurementRoles()) ProcurementRoles roles,
    @Default(HomeRequests()) HomeRequests requests,
    @Default(<AcceptedArrival>[]) List<AcceptedArrival> accepted,
    @Default(0) int acceptedCount,
    @Default(0) double acceptedAmount,
  }) = _ProcurementHome;

  const ProcurementHome._();

  factory ProcurementHome.fromJson(Map<String, Object?> json) => _$ProcurementHomeFromJson(json);

  int get pickUp => requests.departmentToPickUp > requests.mineToPickUp ? requests.departmentToPickUp : requests.mineToPickUp;

  bool get showAccepted => (roles.director || roles.financier) && acceptedCount > 0;

  /// Есть ли дела: согласования, «можно получить», принятое за неделю.
  bool get hasAttention => requests.awaitingMe > 0 || pickUp > 0 || showAccepted;
}

/// Явка за период из `/api/hr/reports` — только доля; функция верхнего уровня (ответ большой, разбирается в фоновом изоляте).
double? attendanceRateOf(Object? json) => ((json! as Map<String, Object?>)['attendanceRate'] as num?)?.toDouble();
