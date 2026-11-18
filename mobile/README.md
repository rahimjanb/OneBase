# OneBase Mobile

Мобильный клиент OneBase для iOS и Android на Flutter. Бэкенд, бизнес-логика и база — те же, что у веба: приложение ходит в
`OneBase.Api` (`/api/*`) с токеном входа. Архитектура — [docs/mobile/ARCHITECTURE.md](../docs/mobile/ARCHITECTURE.md), прогресс —
[docs/mobile/PROGRESS.md](../docs/mobile/PROGRESS.md).

## Запуск

```bash
cd mobile
flutter pub get
dart run build_runner build          # модели (freezed/json) и база (drift)
flutter run --dart-define-from-file=env/development.json
```

- **development** — локальный API на `:5081`:
  - iOS-симулятор — `http://localhost:5081`;
  - Android-эмулятор — `http://10.0.2.2:5081`.
- **staging / production** — заполнить `API_BASE_URL` в `env/staging.json` / `env/production.json`, адрес только HTTPS.
  Без адреса приложение показывает «Адрес сервера не задан» и в сеть не ходит.

## Проверки

```bash
flutter analyze
flutter test
flutter build ios --simulator --debug --dart-define-from-file=env/development.json
flutter build apk --debug --dart-define-from-file=env/development.json
```

## Где что

| Путь | Что |
|---|---|
| `lib/app/` | запуск, роутер, оболочка с нижней панелью, тема OneBase |
| `lib/core/` | API (Dio), сеанс, кэш (Drift), сеть, локализация, журнал |
| `lib/shared/` | общие виджеты дизайн-системы |
| `lib/features/<модуль>/` | `data/` (DTO, API, репозиторий) → `domain/` → `presentation/` (провайдеры, экраны) |
| `lib/l10n/` | строки `app_ru.arb` (основа) и `app_uz.arb`; код — `flutter gen-l10n` |
| `env/` | окружения сборки |

Сгенерированный код (`*.g.dart`, `*.freezed.dart`, `lib/l10n/gen/`) лежит в репозитории, чтобы проект собирался сразу после
клонирования. После правки моделей или ARB перегенерируйте: `dart run build_runner build` / `flutter gen-l10n`.
