import 'package:go_router/go_router.dart';

import 'presentation/ai_screen.dart';

/// Вкладка «Консультант».
final aiRoute = GoRoute(path: '/ai', builder: (context, state) => const AiScreen());
