#!/usr/bin/env bash
# Резервная копия базы: ~/onebase-backups/onebase-ГГГГ-ММ-ДД_ЧЧММ.dump, хранятся последние 14.
# Запуск из корня репозитория: bash deploy/backup.sh
# Каждую ночь в 03:00 (crontab -e):  0 3 * * * cd ~/onebase && bash deploy/backup.sh >> ~/onebase-backups/backup.log 2>&1
set -euo pipefail
cd "$(dirname "$0")/.."

dir="$HOME/onebase-backups"
mkdir -p "$dir"
chmod 700 "$dir"
file="$dir/onebase-$(date +%F_%H%M).dump"

umask 077
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$file.part"
mv "$file.part" "$file"
echo "$(date '+%F %T') копия: $file ($(du -h "$file" | cut -f1))"

ls -1t "$dir"/onebase-*.dump | tail -n +15 | xargs -r rm --
