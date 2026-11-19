import 'package:go_router/go_router.dart';

import 'presentation/profile_screen.dart';

/// Вкладка «Профиль».
final profileRoute = GoRoute(path: '/profile', builder: (context, state) => const ProfileScreen());
