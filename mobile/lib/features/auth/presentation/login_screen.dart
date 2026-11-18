import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/auth/auth_controller.dart';
import '../../../core/core_providers.dart';
import '../../../core/errors/app_failure.dart';
import '../../../shared/extensions/context_ext.dart';
import '../../../shared/widgets/ob_button.dart';
import '../../../shared/widgets/ob_text_field.dart';
import 'brand_mark.dart';

/// Вход под существующей учётной записью OneBase (логин или почта и пароль — как на сайте). Поля поддерживают автозаполнение
/// паролей iOS и Android. Кнопка — в нижней части экрана, удобно одной рукой.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _login = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _login.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final l10n = context.l10n;
    if (_login.text.trim().isEmpty || _password.text.isEmpty) {
      setState(() => _error = l10n.loginRequired);
      return;
    }
    FocusScope.of(context).unfocus();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).login(_login.text, _password.text);
      TextInput.finishAutofillContext();
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _error = e.kind == ApiErrorKind.unauthorized ? l10n.loginInvalid : AppFailure.from(e).describe(l10n));
    } on Object {
      if (!mounted) return;
      setState(() => _error = l10n.errorUnknown);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final c = context.ob;
    final auth = ref.watch(authControllerProvider);
    final expired = auth is AuthSignedOut && auth.reason == SignOutReason.expired;
    final server = Uri.tryParse(ref.watch(appConfigProvider).apiBaseUrl)?.host ?? '';

    return Scaffold(
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, box) => SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: ObSpace.xxl),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: box.maxHeight),
              child: Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 420),
                  child: AutofillGroup(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const SizedBox(height: 48),
                            const BrandMark(),
                            const SizedBox(height: ObSpace.xxl),
                            Text(l10n.loginTitle, style: context.text.headlineMedium),
                            const SizedBox(height: ObSpace.sm),
                            Text(l10n.loginSubtitle, style: context.text.bodyMedium),
                            if (expired) ...[
                              const SizedBox(height: ObSpace.xl),
                              _Notice(text: l10n.sessionExpired, color: c.warn, background: c.warnSoft),
                            ],
                            const SizedBox(height: 32),
                            ObTextField(
                              label: l10n.loginField,
                              controller: _login,
                              keyboardType: TextInputType.emailAddress,
                              textInputAction: TextInputAction.next,
                              autofillHints: const [AutofillHints.username, AutofillHints.email],
                              enabled: !_busy,
                              prefixIcon: Icons.person_outline_rounded,
                            ),
                            const SizedBox(height: ObSpace.md),
                            ObTextField(
                              label: l10n.passwordField,
                              controller: _password,
                              obscure: true,
                              showLabel: l10n.showPassword,
                              hideLabel: l10n.hidePassword,
                              textInputAction: TextInputAction.done,
                              autofillHints: const [AutofillHints.password],
                              onSubmitted: (_) => _submit(),
                              enabled: !_busy,
                              prefixIcon: Icons.lock_outline_rounded,
                            ),
                            if (_error != null) ...[
                              const SizedBox(height: ObSpace.md),
                              Semantics(
                                liveRegion: true,
                                child: _Notice(text: _error!, color: c.bad, background: c.badSoft),
                              ),
                            ],
                          ],
                        ),
                        Padding(
                          padding: const EdgeInsets.only(top: 32, bottom: ObSpace.lg),
                          child: Column(
                            children: [
                              ObButton(label: l10n.loginButton, loading: _busy, expanded: true, onPressed: _submit),
                              const SizedBox(height: ObSpace.md),
                              Text(server, style: context.text.bodySmall),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Notice extends StatelessWidget {
  const _Notice({required this.text, required this.color, required this.background});

  final String text;
  final Color color;
  final Color background;

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: ObSpace.md, vertical: 10),
        decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(ObRadius.md)),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: 1),
              child: Icon(Icons.info_outline_rounded, size: 18, color: color),
            ),
            const SizedBox(width: ObSpace.sm),
            Expanded(child: Text(text, style: context.text.bodyMedium!.copyWith(color: context.ob.ink))),
          ],
        ),
      );
}
