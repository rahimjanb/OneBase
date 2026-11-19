/// Сервер отдаёт ссылки в виде путей веба («/sales/regions/…?year=2026&month=11», «/tasks/…», «/hr/employees/…»): находки AI,
/// уведомления, источники ответов консультанта, отклонения финансов. Здесь они переводятся в экраны приложения; неизвестное —
/// в ближайший раздел, в крайнем случае на главную.
String appRouteForWeb(String? href) {
  if (href == null || href.isEmpty) return '/home';
  final uri = Uri.tryParse(href);
  if (uri == null) return '/home';
  final s = uri.pathSegments;
  final q = uri.query.isEmpty ? '' : '?${uri.query}';
  if (s.isEmpty) return '/home';
  String? id(int i) => s.length > i && s[i].isNotEmpty ? s[i] : null;
  switch (s.first) {
    case 'sales':
      return switch (id(1)) {
        'regions' when id(2) != null => '/analytics/sales/regions/${id(2)}$q',
        'agents' when id(2) != null => '/analytics/sales/agents/${id(2)}$q',
        'directions' when id(2) != null => '/analytics/sales/directions/${id(2)}$q',
        'supervisors' when id(2) != null => '/analytics/sales/supervisors/${id(2)}$q',
        'points' || 'clients' when id(2) != null => '/home/sales/points/${id(2)}',
        'points' || 'clients' => '/home/sales',
        _ => '/analytics/sales$q',
      };
    case 'tasks':
      final first = id(1);
      if (first == null) return '/tasks';
      if (first == 'all' || first == 'my' || first == 'department' || first == 'archive' || first == 'board') return '/tasks$q';
      if (RegExp(r'^[0-9a-f-]{36}$').hasMatch(first)) return '/tasks/$first';
      return '/tasks';
    case 'hr':
      if (id(1) == 'employees' && id(2) != null) return '/home/hr/employees/${id(2)}';
      return '/home/hr';
    case 'finance':
      return '/home/finance';
    case 'production':
      if (id(1) == 'orders' && id(2) != null) return '/home/production/orders/${id(2)}';
      return '/home/production';
    case 'procurement':
      if (id(1) == 'items' && id(2) != null) return '/home/warehouse/items/${id(2)}';
      return '/home/warehouse';
    case 'requests':
      if (id(1) != null && RegExp(r'^[0-9a-f-]{36}$').hasMatch(id(1)!)) return '/home/requests/${id(1)}';
      return '/home/requests';
    case 'consultant':
      return id(1) == null ? '/ai' : '/ai/c/${id(1)}';
    case 'ai':
      return '/home/insights';
    case 'notifications':
      return '/home/notifications';
    case 'departments':
      return '/home/departments';
    case 'settings' || 'me':
      return '/profile';
    default:
      return '/home';
  }
}
