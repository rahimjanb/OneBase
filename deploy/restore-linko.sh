#!/usr/bin/env bash
# Переносит на сервер данные Linko и продаж (схемы linko и sales) из дампа рабочего компьютера:
#   pg_dump -Fc --data-only --schema=linko --schema=sales
# Пользователи, роли, настройки AI, токены Linko и чаты сервера не трогаются.
# Вместе с данными переезжает состояние синхронизации — дальше Linko догружается с того же места, а не с нуля.
# Всё одной транзакцией: при ошибке данные сервера остаются как были.
# Запуск из корня репозитория: bash deploy/restore-linko.sh ~/onebase-linko.dump
set -euo pipefail
cd "$(dirname "$0")/.."

dump="${1:-}"
if [[ -z "$dump" || ! -f "$dump" ]]; then
  echo "Укажите файл дампа: bash deploy/restore-linko.sh ~/onebase-linko.dump" >&2
  exit 1
fi
[[ -f .env ]] || { echo "Нет .env — это точно папка OneBase на сервере?" >&2; exit 1; }

api_running=0
if docker compose ps --status running --services | grep -qx api; then
  api_running=1
  echo "Останавливаю API — синхронизация Linko не должна писать в базу во время переноса…"
  docker compose stop api
  # При любом исходе (и при ошибке) API запускается снова.
  trap 'docker compose start api >/dev/null && echo "API запущен."' EXIT
fi

echo "Заменяю данные Linko и продаж ($(du -h "$dump" | cut -f1)) — несколько минут…"
# Очистка схем linko и sales и загрузка дампа — одна транзакция (ON_ERROR_STOP: при ошибке — откат).
# session_replication_role = replica — порядок таблиц в дампе не упирается во внешние ключи.
prelude=$(cat <<'SQL'
BEGIN;
SET session_replication_role = replica;
DO $$
BEGIN
  EXECUTE (SELECT 'TRUNCATE TABLE ' || string_agg(format('%I.%I', schemaname, tablename), ', ')
           FROM pg_tables WHERE schemaname IN ('linko', 'sales'));
END
$$;
SQL
)

if ! docker compose exec -T -e PRELUDE="$prelude" postgres sh -c '
  set -eo pipefail
  { printf "%s\n" "$PRELUDE"; pg_restore --data-only --no-owner --no-privileges -f -; printf "COMMIT;\n"; } \
    | psql -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" >/dev/null
' < "$dump"; then
  echo "Перенос не удался (ошибка выше) — изменения отменены, данные сервера остались как были." >&2
  exit 1
fi

echo "Проверка:"
docker compose exec -T postgres sh -c 'psql -tA -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
select 'заказов: ' || count(*) from linko."Orders";
select 'визитов: ' || count(*) from linko."Visits";
select 'планов ТП: ' || count(*) from sales."StaffPlans";
select 'пользователей OneBase (не тронуты): ' || count(*) from "Users";
SQL

if [[ $api_running == 1 ]]; then
  echo "Готово. Синхронизация продолжит с состояния, перенесённого с компьютера."
else
  echo "Готово. Запустите API: docker compose up -d"
fi
