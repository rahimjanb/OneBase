import 'package:flutter/foundation.dart';
import 'package:go_router/go_router.dart';

import 'presentation/ai_screen.dart';

/// Вкладка «Консультант»: `/ai` — новый чат (`?ask=` подставляет вопрос в поле, отправляет пользователь), `/ai/c/:id` — сохранённый чат.
final aiRoute = GoRoute(
  path: '/ai',
  builder: (context, state) => AiScreen(key: const ValueKey('ai-new'), ask: state.uri.queryParameters['ask']),
  routes: [
    GoRoute(
      path: 'c/:id',
      builder: (context, state) {
        final id = state.pathParameters['id']!;
        return AiScreen(key: ValueKey('ai-$id'), conversationId: id);
      },
    ),
  ],
);
