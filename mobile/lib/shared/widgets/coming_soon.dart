import 'package:flutter/material.dart';

import '../extensions/context_ext.dart';
import 'ob_app_bar.dart';
import 'states.dart';

/// Раздел, который появится на следующих этапах плана: честное состояние вместо пустого экрана или выдуманных данных.
class ComingSoonScreen extends StatelessWidget {
  const ComingSoonScreen({super.key, required this.title, required this.stage, this.icon = Icons.construction_rounded});

  final String title;
  final int stage;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Scaffold(
        body: CustomScrollView(
          slivers: [
            ObSliverAppBar(title: title),
            SliverFillRemaining(
              hasScrollBody: false,
              child: EmptyView(
                icon: icon,
                title: context.l10n.comingSoonTitle,
                message: context.l10n.comingSoonMessage(stage),
              ),
            ),
          ],
        ),
      );
}
