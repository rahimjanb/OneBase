import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../extensions/context_ext.dart';
import 'ob_app_bar.dart';
import 'states.dart';

/// Раздел, который ещё в работе: честное состояние вместо пустого экрана или выдуманных данных.
class ComingSoonScreen extends StatelessWidget {
  const ComingSoonScreen({super.key, required this.title, required this.stage, this.icon = LucideIcons.construction});

  final String title;
  final int stage;
  final IconData icon;

  @override
  Widget build(BuildContext context) => ObPage(
        title: title,
        slivers: [
          SliverFillRemaining(
            hasScrollBody: false,
            child: EmptyView(icon: icon, title: context.l10n.comingSoonTitle, message: context.l10n.comingSoonMessage(stage)),
          ),
        ],
      );
}
