#!/usr/bin/env bash
# Создаёт .env для сервера: случайные пароли PostgreSQL, Redis, MinIO и ключ JWT, токен Cloudflare Tunnel.
# Запуск из корня репозитория: bash deploy/setup-env.sh
# Существующий .env не трогает. Секреты не выводятся на экран.
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ -e .env ]]; then
  echo "Файл .env уже есть — не перезаписываю. Удалите его вручную, если действительно нужен новый." >&2
  exit 1
fi

command -v openssl >/dev/null || { echo "Нужен openssl: sudo apt install -y openssl" >&2; exit 1; }
secret() { openssl rand -hex "$1"; }

read -rsp "Токен Cloudflare Tunnel (ввод не отображается): " tunnel_token
echo
if [[ -z "$tunnel_token" ]]; then
  echo "Токен пустой — отмена." >&2
  exit 1
fi

umask 077
cat > .env <<EOF
# Создано deploy/setup-env.sh $(date +%F). Не коммитить.
COMPOSE_PROFILES=tunnel

POSTGRES_DB=onebase
POSTGRES_USER=onebase
POSTGRES_PASSWORD=$(secret 24)

REDIS_PASSWORD=$(secret 24)

MINIO_ROOT_USER=onebase
MINIO_ROOT_PASSWORD=$(secret 24)
MINIO_BUCKET=onebase-files

JWT_ISSUER=onebase
JWT_AUDIENCE=onebase
JWT_SIGNING_KEY=$(secret 48)

# База переносится дампом — первый администратор уже есть, заново не создаётся.
SEED_ADMIN_EMAIL=
SEED_ADMIN_PASSWORD=

# Ключи AI и токены Linko хранятся в базе (зашифрованы) и переезжают вместе с ней — здесь пусто.
LLM_PROVIDER=
LLM_API_KEY=
LLM_MODEL=
EMBEDDING_MODEL=
OPENAI_API_KEY=
ANTHROPIC_API_KEY=
LINKO_BASE_URL=
LINKO_TOKEN=
LINKO_PLAN_TOKEN=

CLOUDFLARE_TUNNEL_TOKEN=${tunnel_token}
EOF

chmod 600 .env
echo "Готово: .env создан (доступ только владельцу)."
