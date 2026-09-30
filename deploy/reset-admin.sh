#!/usr/bin/env bash
# Аварийный вход администратора: новый пароль для логина (пользователь включается и получает роль «Администратор»;
# если такого логина нет — создаётся администратор). Пароль вводится скрыто и передаётся через stdin —
# не попадает в историю команд, список процессов и журналы.
# Запуск из корня репозитория: bash deploy/reset-admin.sh ЛОГИН
set -euo pipefail
cd "$(dirname "$0")/.."

login="${1:-}"
if [[ -z "$login" ]]; then
  echo "Укажите логин: bash deploy/reset-admin.sh ЛОГИН" >&2
  exit 1
fi

if ! docker compose ps --status running --services | grep -qx api; then
  echo "API не запущен: docker compose up -d" >&2
  exit 1
fi

read -rsp "Новый пароль (от 8 символов, ввод не отображается): " password
echo
read -rsp "Ещё раз: " again
echo
if [[ "$password" != "$again" ]]; then
  echo "Пароли не совпадают — ничего не изменено." >&2
  exit 1
fi

# Второй процесс API в том же контейнере: веб-сервер не поднимает, только меняет пользователя и завершается.
printf '%s\n' "$password" | docker compose exec -T api dotnet OneBase.Api.dll reset-admin "$login" 2>&1 \
  | grep -v -E '^(info|warn|dbug): |^      ' || true
