import '../auth/perm.dart';
import '../auth/user_profile.dart';

/// Какие разделы кому видны — те же правила, что у веба (`frontend/src/lib/navigation.ts`). Приложение по ним прячет недоступное;
/// доступ к данным всё равно проверяет сервер.
abstract final class Access {
  /// Общие разделы компании (Обзор, Отделы, Задачи, Отчёты).
  static const workspace = [Perm.agentsRun, Perm.salesRead, Perm.financeRead, Perm.hrRead, Perm.workTasks, Perm.fieldUse, 'field.plan'];

  static const departments = ['departments.open'];
  static const sales = [Perm.salesRead];
  static const tasks = [Perm.workTasks];
  static const consultant = [Perm.agentsRun];
  static const finance = [Perm.financeRead];
  static const analytik = [Perm.analytikUse];
  static const requests = ['procurement.request', Perm.procurementRead, 'procurement.manage', 'procurement.settings', Perm.procurementWarehouse];
  static const warehouse = [Perm.procurementWarehouse, Perm.procurementRead, 'procurement.manage', Perm.qualityCheck];
  static const production = [
    Perm.productionRead,
    'production.manage',
    'production.recipes',
    'production.operate',
    Perm.qualityCheck,
    Perm.procurementWarehouse,
    Perm.procurementRead,
    'procurement.manage',
  ];
  static const hr = [Perm.hrRead, 'integrations.manage', 'hr.department', 'hr.requests', 'hr.recruitment.read', 'hr.recruitment.manage'];

  /// «Аналитика» приложения: хотя бы один аналитический раздел.
  static const analytics = Perm.analytics;

  /// Отделы раздела «Отделы» — в порядке карточек веба.
  static const departmentCodes = ['production', 'finance', 'sales', 'marketing', 'supply', 'hr'];

  /// Свой раздел отдела: продажи, HR, снабжение, производство, финансы.
  static const departmentSections = {
    'sales': sales,
    'hr': hr,
    'supply': warehouse,
    'production': production,
    'finance': finance,
  };

  /// Права экрана: права пользователя и признаки из `/api/auth/me` — `hr.department` (руководитель с подразделениями Verifix видит HR),
  /// `hr.requests` (заявки на сотрудника), `departments.open` (открывается хотя бы один отдел).
  static List<String> screenPermissions(UserProfile? me) {
    if (me == null) return const [];
    final base = [...me.permissions, if (me.hrAccess) 'hr.department', if (me.hiringRequests) 'hr.requests'];
    final anyDepartment = departmentCodes.any((code) => departmentOpen(code, base, me.departments));
    return anyDepartment ? [...base, 'departments.open'] : base;
  }

  /// Раздел открыт, если есть хотя бы одно из его прав; пустой список — всем.
  static bool canOpen(List<String> need, List<String> permissions) => need.isEmpty || need.any(permissions.contains);

  /// Отдел доступен: открывается свой раздел отдела или задачи отдела (свой, руководимый; у руководства — все).
  static bool departmentOpen(String code, List<String> permissions, List<String> departments) {
    final own = departmentSections[code];
    return (own != null && canOpen(own, permissions)) || departments.contains(code);
  }
}
