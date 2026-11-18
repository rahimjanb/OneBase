/// Коды прав OneBase (`OneBase.Application.Security.Permissions`). Приложение по ним прячет недоступное; проверяет доступ сервер.
abstract final class Perm {
  static const salesRead = 'sales.read';
  static const financeRead = 'finance.read';
  static const hrRead = 'hr.read';
  static const workTasks = 'work.tasks';
  static const agentsRun = 'ai.agents.run';
  static const approvalsDecide = 'ai.approvals.decide';
  static const analytikUse = 'analytik.use';
  static const productionRead = 'production.read';
  static const procurementRead = 'procurement.read';
  static const procurementWarehouse = 'procurement.warehouse';
  static const qualityCheck = 'quality.check';
  static const fieldUse = 'field.use';

  /// Вкладка «Аналитика»: хотя бы один аналитический раздел.
  static const analytics = [salesRead, financeRead, analytikUse, productionRead];
}
