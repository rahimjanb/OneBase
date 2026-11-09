#!/usr/bin/env bash
# Полная резервная копия OneBase: база, файлы MinIO, векторы Qdrant, .env, compose и код — один зашифрованный набор
# в ~/onebase-backups/onebase-backup-ГГГГ-ММ-ДД_ЧЧММ/. Подробно — docs/disaster-recovery.md.
#
# Один раз — ключ восстановления (закрытый ключ потом забрать с сервера):
#   bash deploy/onebase-backup.sh init-key
# Копия сейчас:
#   bash deploy/onebase-backup.sh backup --keep 14
# Проверка набора (без ключа — целостность; с ключом — ещё и содержимое):
#   bash deploy/onebase-backup.sh verify --backup ~/onebase-backups/onebase-backup-… [--key ключ]
# Каждую ночь в 03:00 (crontab -e):
#   0 3 * * * cd ~/onebase && bash deploy/onebase-backup.sh backup --keep 14 >> ~/onebase-backups/cron.log 2>&1
set -euo pipefail
cd "$(dirname "$0")/.."

command -v python3 >/dev/null || { echo "Нужен python3: sudo apt install -y python3" >&2; exit 1; }
exec env PYTHONPATH="$PWD/deploy/dr${PYTHONPATH:+:$PYTHONPATH}" python3 -m onebase_dr "$@"
