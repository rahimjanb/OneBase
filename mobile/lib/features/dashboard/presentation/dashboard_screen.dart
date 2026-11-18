import 'package:flutter/material.dart';

import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/coming_soon.dart';

/// Главная. Dashboard на реальных данных — этап 3.
class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      ComingSoonScreen(title: context.l10n.navHome, stage: 3, icon: Icons.space_dashboard_outlined);
}
