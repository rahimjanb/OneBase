import 'package:flutter/material.dart';

import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/coming_soon.dart';

/// AI-консультант OneBase — этап 6.
class AiScreen extends StatelessWidget {
  const AiScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      ComingSoonScreen(title: context.l10n.navAi, stage: 6, icon: Icons.auto_awesome_outlined);
}
