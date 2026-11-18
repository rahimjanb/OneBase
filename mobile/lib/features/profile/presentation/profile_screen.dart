import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/core_providers.dart';
import '../../../core/storage/preferences.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_app_bar.dart';
import '../../../shared/widgets/ob_button.dart';
import '../../../shared/widgets/ob_card.dart';
import '../../../shared/widgets/section_header.dart';

/// Профиль: кто вошёл (имя, должность, отдел, роли), оформление, язык, о приложении, выход. Остальные настройки — этап 12.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    return Scaffold(
      body: CustomScrollView(
        slivers: [
          ObSliverAppBar(title: l10n.navProfile),
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(ObSpace.page, 0, ObSpace.page, ObSpace.xxl),
            sliver: SliverList.list(
              children: const [
                UserSection(),
                AppearanceSection(),
                LanguageSection(),
                AboutSection(),
                LogoutSection(),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Карточка пользователя: инициалы, имя, должность и отдел, роли. Без сети — пометка «профиль сохранён на телефоне».
class UserSection extends ConsumerWidget {
  const UserSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    if (auth is! AuthSignedIn) return const SizedBox.shrink();
    final p = auth.profile;
    final c = context.ob;
    final subtitle = [p.position, p.departmentName].whereType<String>().where((s) => s.trim().isNotEmpty).join(' · ');
    return Padding(
      padding: const EdgeInsets.only(top: ObSpace.sm),
      child: ObCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 28,
                  backgroundColor: c.accentSoft,
                  child: Text(p.initials, style: context.text.titleMedium!.copyWith(color: c.accentStrong)),
                ),
                const SizedBox(width: ObSpace.lg),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(p.name, style: context.text.titleLarge),
                      if (subtitle.isNotEmpty) Text(subtitle, style: context.text.bodyMedium),
                      Text(p.email ?? p.login, style: context.text.bodySmall),
                    ],
                  ),
                ),
              ],
            ),
            if (p.roles.isNotEmpty) ...[
              const SizedBox(height: ObSpace.md),
              Wrap(
                spacing: ObSpace.sm,
                runSpacing: ObSpace.sm,
                children: [
                  for (final r in p.roles)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                      decoration: BoxDecoration(color: c.muted, borderRadius: BorderRadius.circular(ObRadius.sm)),
                      child: Text(r, style: context.text.labelMedium),
                    ),
                ],
              ),
            ],
            if (auth.offline) ...[
              const SizedBox(height: ObSpace.md),
              Row(
                children: [
                  Icon(Icons.cloud_off_rounded, size: 16, color: c.warn),
                  const SizedBox(width: 6),
                  Expanded(child: Text(context.l10n.offlineProfile, style: context.text.bodySmall)),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Выход: подтверждение, затем сеанс закрывается на сервере, токен и кэш удаляются с телефона.
class LogoutSection extends ConsumerWidget {
  const LogoutSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (ref.watch(authControllerProvider) is! AuthSignedIn) return const SizedBox.shrink();
    final l10n = context.l10n;
    return Padding(
      padding: const EdgeInsets.only(top: ObSpace.xxl),
      child: ObButton.secondary(
        label: l10n.logout,
        icon: Icons.logout_rounded,
        expanded: true,
        onPressed: () async {
          final ok = await showAdaptiveDialog<bool>(
            context: context,
            builder: (context) => AlertDialog.adaptive(
              title: Text(l10n.logoutConfirmTitle),
              content: Text(l10n.logoutConfirmMessage),
              actions: [
                TextButton(onPressed: () => Navigator.of(context).pop(false), child: Text(l10n.actionCancel)),
                TextButton(
                  onPressed: () => Navigator.of(context).pop(true),
                  style: TextButton.styleFrom(foregroundColor: context.ob.bad),
                  child: Text(l10n.logout),
                ),
              ],
            ),
          );
          if (ok ?? false) await ref.read(authControllerProvider.notifier).logout();
        },
      ),
    );
  }
}

/// Тема: как в системе / светлая / тёмная.
class AppearanceSection extends ConsumerWidget {
  const AppearanceSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = context.l10n;
    final mode = ref.watch(themeModeProvider);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.profileAppearance),
        SegmentedButton<ThemeMode>(
          showSelectedIcon: false,
          segments: [
            ButtonSegment(value: ThemeMode.system, label: Text(l10n.themeSystem)),
            ButtonSegment(value: ThemeMode.light, label: Text(l10n.themeLight)),
            ButtonSegment(value: ThemeMode.dark, label: Text(l10n.themeDark)),
          ],
          selected: {mode},
          onSelectionChanged: (s) => ref.read(themeModeProvider.notifier).set(s.first),
        ),
      ],
    );
  }
}

/// Язык интерфейса: русский или узбекский; сервер отвечает на нём же.
class LanguageSection extends ConsumerWidget {
  const LanguageSection({super.key});

  static const _names = {'ru': 'Русский', 'uz': "O'zbekcha"};

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final current = effectiveLanguage(ref.watch(localeProvider));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: context.l10n.language),
        ObCard(
          padding: const EdgeInsets.symmetric(vertical: ObSpace.xs),
          child: RadioGroup<String>(
            groupValue: current,
            onChanged: (code) {
              if (code != null) ref.read(localeProvider.notifier).set(Locale(code));
            },
            child: Column(
              children: [
                for (final l in supportedLocales)
                  RadioListTile<String>(
                    value: l.languageCode,
                    title: Text(_names[l.languageCode] ?? l.languageCode),
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// Версия приложения и сервер, к которому оно подключено.
class AboutSection extends ConsumerWidget {
  const AboutSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final config = ref.watch(appConfigProvider);
    final l10n = context.l10n;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: l10n.profileAbout),
        ObCard(
          padding: const EdgeInsets.symmetric(vertical: ObSpace.xs),
          child: Column(
            children: [
              ListTile(
                leading: const Icon(Icons.info_outline_rounded),
                title: Text(l10n.profileVersion(config.appVersion)),
                subtitle: Text(config.env.name),
              ),
              ListTile(
                leading: const Icon(Icons.dns_outlined),
                title: Text(l10n.profileServer),
                subtitle: Text(config.apiBaseUrl.isEmpty ? l10n.errorNotConfigured : config.apiBaseUrl),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
