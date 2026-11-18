import 'package:flutter/material.dart';

import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/coming_soon.dart';

/// Аналитика: продажи, финансы, производство, отделы — этап 5.
class AnalyticsScreen extends StatelessWidget {
  const AnalyticsScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      ComingSoonScreen(title: context.l10n.navAnalytics, stage: 5, icon: Icons.insights_outlined);
}
