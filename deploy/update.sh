#!/usr/bin/env bash
# Обновление сервера до последней версии из GitHub: сборка образов и перезапуск. Данные (тома docker) не трогаются.
# Запуск из корня репозитория: bash deploy/update.sh
set -euo pipefail
cd "$(dirname "$0")/.."

git pull --ff-only
docker compose build api web
docker compose up -d
docker image prune -f >/dev/null

echo "Жду, пока API поднимется (миграции базы применяются при старте)…"
for _ in $(seq 1 60); do
  if curl -fs http://127.0.0.1:8080/health >/dev/null; then
    echo "OneBase работает: $(git log -1 --format='%h %s')"
    docker compose ps --format 'table {{.Service}}\t{{.Status}}'
    exit 0
  fi
  sleep 3
done

echo "API не ответил за 3 минуты. Журнал: docker compose logs --tail 100 api" >&2
exit 1
