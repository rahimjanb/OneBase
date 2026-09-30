#!/usr/bin/env bash
# Восстанавливает дамп PostgreSQL (pg_dump -Fc) в ПУСТУЮ базу сервера — до первого запуска API.
# Запуск из корня репозитория: bash deploy/restore-db.sh ~/onebase.dump
# Если в базе уже есть таблицы, ничего не делает: перезаписывать данные скрипт не будет.
set -euo pipefail
cd "$(dirname "$0")/.."

dump="${1:-}"
if [[ -z "$dump" || ! -f "$dump" ]]; then
  echo "Укажите файл дампа: bash deploy/restore-db.sh ~/onebase.dump" >&2
  exit 1
fi
[[ -f .env ]] || { echo "Нет .env — сначала bash deploy/setup-env.sh" >&2; exit 1; }

if docker compose ps --status running --services | grep -qx api; then
  echo "API уже запущен — он создал бы в базе свою схему. Остановите его: docker compose stop api" >&2
  exit 1
fi

echo "Запускаю PostgreSQL…"
docker compose up -d postgres
for _ in $(seq 1 60); do
  if docker compose exec -T postgres sh -c 'pg_isready -q -U "$POSTGRES_USER" -d "$POSTGRES_DB"'; then break; fi
  sleep 2
done

tables=$(docker compose exec -T postgres sh -c \
  'psql -tA -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select count(*) from information_schema.tables where table_schema not in ('"'"'pg_catalog'"'"','"'"'information_schema'"'"')"')
if [[ "${tables//[[:space:]]/}" != "0" ]]; then
  echo "В базе уже есть таблицы ($tables) — восстановление отменено, данные не тронуты." >&2
  exit 1
fi

echo "Восстанавливаю $(du -h "$dump" | cut -f1) — это займёт несколько минут…"
docker compose exec -T postgres sh -c 'pg_restore --no-owner --no-privileges --exit-on-error -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < "$dump"

echo "Проверка:"
docker compose exec -T postgres sh -c 'psql -tA -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select '"'"'пользователей: '"'"' || count(*) from \"Users\"" -c "select '"'"'заказов Linko: '"'"' || count(*) from linko.\"Orders\"" -c "select '"'"'миграций: '"'"' || count(*) from \"__EFMigrationsHistory\""'
echo "Готово. Дальше: docker compose up -d --build"
