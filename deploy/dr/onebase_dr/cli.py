"""Командная строка: backup, init-key, verify."""

from __future__ import annotations

import argparse
import os
import sys
import traceback
from pathlib import Path

from . import __version__
from .util import DrError, Log, Masker

KEY_ADVICE = """\
Закрытый ключ восстановления: {key}
  1. Скачайте его на свой компьютер:  scp {host}:{key} .
  2. Сохраните в менеджере паролей и на флешке/бумаге в надёжном месте. Без него копии не открыть — это сделано намеренно.
  3. Удалите с сервера:  shred -u {key}
Открытый ключ записан в {recipients} — им шифруются копии."""


def _backup(args: argparse.Namespace, log: Log) -> int:
    from .backup import BackupOptions, run_backup
    from .util import private_dir

    backup_dir = private_dir(Path(args.dir).expanduser().resolve())
    file_log = Log(log.mask, backup_dir / "backup.log")
    try:
        run_backup(BackupOptions(project=Path(args.project), dir=backup_dir,
                                 recipients=Path(args.recipients) if args.recipients else None, keep=args.keep,
                                 minio_endpoint=args.minio_endpoint, qdrant_url=args.qdrant_url), file_log)
    except Exception as exc:
        # Ошибка — и в backup.log: по нему администратор видит, почему ночью не было копии.
        file_log.error(str(exc) if isinstance(exc, DrError) else f"{type(exc).__name__}: {exc}")
        if not isinstance(exc, DrError):
            traceback.print_exc()
        return 1
    finally:
        file_log.close()
    return 0


def _init_key(args: argparse.Namespace, log: Log) -> int:
    from .crypto import generate_key
    from .util import private_dir

    backup_dir = private_dir(Path(args.dir).expanduser().resolve())
    recipients = backup_dir / "recipients.txt"
    key = Path(args.out).expanduser().resolve()
    public = generate_key(key, log)
    old = os.umask(0o077)
    try:
        with open(recipients, "a", encoding="utf-8") as f:
            f.write(public + "\n")
    finally:
        os.umask(old)
    print(KEY_ADVICE.format(key=key, recipients=recipients, host=os.environ.get("USER", "user") + "@сервер"))
    return 0


def _verify(args: argparse.Namespace, log: Log) -> int:
    from .verify import run_verify

    return run_verify(Path(args.backup), Path(args.key) if args.key else None, Path(args.work) if args.work else None, log)


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="onebase-dr", description="OneBase Disaster Recovery: полная резервная копия и проверка наборов.")
    p.add_argument("--version", action="version", version=__version__)
    sub = p.add_subparsers(dest="command", required=True)

    b = sub.add_parser("backup", help="снять полную копию (база, файлы, векторы, .env, код)")
    b.add_argument("--project", default=".", help="каталог OneBase с docker-compose.yml и .env (по умолчанию текущий)")
    b.add_argument("--dir", default="~/onebase-backups", help="каталог наборов (по умолчанию ~/onebase-backups)")
    b.add_argument("--recipients", help="файл открытых ключей age (по умолчанию <dir>/recipients.txt)")
    b.add_argument("--keep", type=int, help="хранить N последних наборов; без флага ничего не удаляется")
    b.add_argument("--minio-endpoint", help="host:port MinIO (по умолчанию — адрес контейнера minio в сети проекта)")
    b.add_argument("--qdrant-url", help="адрес Qdrant (по умолчанию — контейнер qdrant в сети проекта, порт 6333)")
    b.set_defaults(func=_backup)

    k = sub.add_parser("init-key", help="создать пару ключей восстановления (закрытый — забрать с сервера)")
    k.add_argument("--out", default="~/onebase-recovery-key.txt", help="куда записать закрытый ключ")
    k.add_argument("--dir", default="~/onebase-backups", help="каталог наборов: туда добавится открытый ключ")
    k.set_defaults(func=_init_key)

    v = sub.add_parser("verify", help="проверить набор: без ключа — целостность, с --key — ещё и содержимое")
    v.add_argument("--backup", required=True, help="каталог набора onebase-backup-…")
    v.add_argument("--key", help="файл закрытого ключа восстановления")
    v.add_argument("--work", help="где расшифровать для проверки (по умолчанию рядом с набором)")
    v.set_defaults(func=_verify)
    return p


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    log = Log(Masker())
    try:
        return args.func(args, log)
    except DrError as exc:
        log.error(str(exc))
        return 1
    except KeyboardInterrupt:
        log.error("Прервано")
        return 130


if __name__ == "__main__":
    sys.exit(main())
