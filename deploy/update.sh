#!/usr/bin/env bash
# Обновление сервера до последней версии из GitHub: сборка образов и перезапуск. Данные (тома docker) не трогаются.
# Запуск из корня репозитория: bash deploy/update.sh
set -euo pipefail
cd "$(dirname "$0")/.."

before=$(git rev-parse HEAD)
git pull --ff-only
docker compose build api web
docker compose up -d
# nginx читает шаблон только при старте: если конфигурация изменилась (например, добавился сайт Sales Base на :81),
# пересоздаём контейнер, иначе новые настройки не применятся.
if ! git diff --quiet "$before" HEAD -- infra/nginx docker-compose.yml; then
  echo "Конфигурация nginx или docker-compose изменилась — пересоздаю nginx"
  docker compose up -d --force-recreate --no-deps nginx
fi
docker image prune -f >/dev/null

echo "Жду, пока API поднимется (миграции базы применяются при старте)…"
for _ in $(seq 1 60); do
  if curl -fs http://127.0.0.1:8080/health >/dev/null; then
    echo "OneBase работает: $(git log -1 --format='%h %s')"
    # Sales Base (порт 81 внутри nginx): основной сайт уже работает, поэтому только предупреждение.
    if docker compose exec -T nginx wget -qO- http://127.0.0.1:81/health >/dev/null 2>&1; then
      echo "Sales Base отвечает (nginx :81)"
    else
      echo "Внимание: Sales Base (nginx :81) не ответил. Журнал: docker compose logs --tail 50 nginx" >&2
    fi
    docker compose ps --format 'table {{.Service}}\t{{.Status}}'
    exit 0
  fi
  sleep 3
done

echo "API не ответил за 3 минуты. Журнал: docker compose logs --tail 100 api" >&2
exit 1
