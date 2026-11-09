"""Полная резервная копия OneBase одним зашифрованным набором: PostgreSQL, файлы MinIO, коллекции Qdrant, .env, compose, код.

Порядок съёмки: база → MinIO → Qdrant. Объекты MinIO перечисляются после снимка базы, поэтому всё, на что ссылается дамп,
уже есть в списке; удалённый между дампом и копированием объект — предупреждение в манифесте. Redis не копируется: в нём
только кэш ответов консультанта.
"""

from __future__ import annotations

import fcntl
import json
import os
import platform
import shutil
import time
import zipapp
from dataclasses import dataclass, field
from pathlib import Path

from . import __version__, envfile
from .compose import Compose
from .crypto import encrypt_dir, require_age
from .manifest import (ENGINE, FORMAT, INNER, PAYLOAD, PAYLOAD_SHA, PUBLIC, SET_NAME, VERSION, check_outer,
                       file_entries, read_recipients, recipients_fingerprint)
from .postgres import Postgres
from .qdrant import Qdrant
from .s3 import S3, S3Error
from .util import DrError, Log, human, now_iso, private_dir, run, sha256_file, write_json

SERVICES = ["postgres", "minio", "qdrant"]
COMPOSE_FILES = ("docker-compose*.yml", "docker-compose*.yaml", "compose*.yml", "compose*.yaml")
MB = 1024 * 1024


@dataclass
class BackupOptions:
    project: Path
    dir: Path
    recipients: Path | None = None
    keep: int | None = None
    # Пусто — адрес контейнера сервиса в сети проекта (см. Compose.service_ip).
    minio_endpoint: str | None = None
    qdrant_url: str | None = None


@dataclass
class Steps:
    items: list[dict] = field(default_factory=list)

    def timed(self, name: str):
        steps = self

        class _Timer:
            def __enter__(self):
                self.start = time.monotonic()

            def __exit__(self, *exc):
                steps.items.append({"step": name, "seconds": round(time.monotonic() - self.start, 1), "ok": exc[0] is None})

        return _Timer()


def set_name(backup_dir: Path) -> str:
    base = "onebase-backup-" + time.strftime("%Y-%m-%d_%H%M")
    name, n = base, 2
    while (backup_dir / name).exists() or (backup_dir / f"{name}.partial").exists():
        name, n = f"{base}-{n}", n + 1
    return name


def build_engine(target: Path) -> None:
    """Restore Engine той же версии — в набор: восстановление не зависит от GitHub."""
    source = Path(__file__).resolve().parent.parent
    zipapp.create_archive(source, target, interpreter="/usr/bin/env python3", main="onebase_dr.cli:main",
                          filter=lambda p: bool(p.parts) and p.parts[0] == "onebase_dr" and "__pycache__" not in p.parts)


def host_info(log: Log) -> dict:
    os_release = {}
    try:
        for line in Path("/etc/os-release").read_text(encoding="utf-8").splitlines():
            key, _, value = line.partition("=")
            os_release[key] = value.strip('"')
    except OSError:
        pass
    docker = run(["docker", "version", "--format", "{{.Server.Version}}"], log, check=False).stdout.strip()
    compose = run(["docker", "compose", "version", "--short"], log, check=False).stdout.strip()
    return {
        "os": os_release.get("PRETTY_NAME", platform.platform()),
        "os_id": os_release.get("ID", ""),
        "os_version": os_release.get("VERSION_ID", ""),
        "arch": platform.machine(),
        "kernel": platform.release(),
        "docker": docker,
        "compose": compose,
        "python": platform.python_version(),
    }


def rotate(backup_dir: Path, keep: int, current: Path, log: Log) -> None:
    """Только наборы этого инструмента и только после успешного нового. Старые *.dump от backup.sh не трогаются."""
    sets = sorted(p for p in backup_dir.iterdir()
                  if p.is_dir() and SET_NAME.match(p.name) and (p / PUBLIC).is_file() and (p / PAYLOAD_SHA).is_file())
    extra = len(sets) - keep
    for old in [p for p in sets if p != current][:max(0, extra)]:
        shutil.rmtree(old)
        log.info(f"Ротация: удалён старый набор {old.name} (хранятся последние {keep})")


def status(backup_dir: Path, data: dict) -> None:
    write_json(backup_dir / "last-backup.json", data)


def run_backup(opts: BackupOptions, log: Log) -> Path:
    project = opts.project.resolve()
    if not (project / "docker-compose.yml").is_file():
        raise DrError(f"В {project} нет docker-compose.yml — запускайте из каталога OneBase или укажите --project")
    env_path = project / ".env"
    if not env_path.is_file():
        raise DrError(f"Нет {env_path}: без него копия неполная (ключ JWT, токен туннеля)")
    env = envfile.load(env_path)
    log.mask.add(*envfile.secret_values(env))

    backup_dir = private_dir(opts.dir.expanduser().resolve())
    recipients_path = (opts.recipients or backup_dir / "recipients.txt").expanduser()
    if not recipients_path.is_file():
        raise DrError(f"Нет открытого ключа восстановления {recipients_path}. Создайте пару ключей: "
                      "bash deploy/onebase-backup.sh init-key (подробно — docs/disaster-recovery.md)")
    recipients = read_recipients(recipients_path)
    if not recipients or not all(r.startswith(("age1", "ssh-ed25519 ", "ssh-rsa ")) for r in recipients):
        raise DrError(f"{recipients_path}: ожидаются открытые ключи age (age1…), по одному в строке")
    require_age()
    for tool in ("docker", "git"):
        if not shutil.which(tool):
            raise DrError(f"Не найдена программа {tool}")
    if opts.keep is not None and opts.keep < 1:
        raise DrError("--keep: от 1")

    lock = open(backup_dir / ".lock", "w")
    try:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError as exc:
        raise DrError("Копия уже снимается другим запуском") from exc

    started = now_iso()
    t0 = time.monotonic()
    name = set_name(backup_dir)
    staging = private_dir(backup_dir / f".staging-{name}")
    partial = private_dir(backup_dir / f"{name}.partial")
    final = backup_dir / name
    status(backup_dir, {"status": "running", "set": name, "started_at": started})
    log.info(f"Копия {name}: проект {project}, каталог {backup_dir}")
    try:
        compose = Compose(project, log)
        compose.require(SERVICES)
        pg = Postgres(compose, log)
        bucket = env.get("MINIO_BUCKET") or "onebase-files"
        minio_endpoint = opts.minio_endpoint or f"{compose.service_ip('minio')}:9000"
        qdrant_url = opts.qdrant_url or f"http://{compose.service_ip('qdrant')}:6333"
        log.info(f"MinIO {minio_endpoint}, Qdrant {qdrant_url}")
        s3 = S3(minio_endpoint, env.get("MINIO_ROOT_USER", ""), env.get("MINIO_ROOT_PASSWORD", ""))
        qd = Qdrant(qdrant_url)
        steps = Steps()
        known: dict[str, tuple[int, str]] = {}

        with steps.timed("preflight"):
            db_size = pg.database_size()
            bucket_exists = s3.bucket_exists(bucket)
            minio_bytes = sum(o.size for o in s3.list_objects(bucket)) if bucket_exists else 0
            collections = qd.collections()
            qdrant_est = 0
            for c in collections:
                vectors = qd.collection_info(c).get("config", {}).get("params", {}).get("vectors", {})
                dims = vectors.get("size", 1536) if isinstance(vectors, dict) and "size" in vectors else 1536
                qdrant_est += qd.count(c) * (dims * 4 + 4096)
            need = 2 * (int(db_size * 0.7) + minio_bytes + qdrant_est + 64 * MB) + 1024 * MB
            free = shutil.disk_usage(backup_dir).free
            log.info(f"База {human(db_size)}, файлы {human(minio_bytes)}, векторы ≈{human(qdrant_est)}; "
                     f"нужно ≈{human(need)}, свободно {human(free)}")
            if free < need:
                raise DrError(f"Мало места в {backup_dir}: нужно ≈{human(need)}, свободно {human(free)}")

        with steps.timed("postgres"):
            with pg.snapshot() as snap:
                tables = pg.tables(snap.id)
                counts = pg.counts(tables, snap.id)
                migrations = pg.migrations(snap.id)
                known["postgres/onebase.dump"] = pg.dump(snap.id, staging / "postgres" / "onebase.dump")
            (staging / "postgres" / "globals.sql").write_text(pg.dump_globals(), encoding="utf-8")
            pg_info = {
                "version": pg.server_version(),
                "database_size": db_size,
                "extensions": pg.extensions(),
                "tables": len(tables),
                "rows_total": sum(counts.values()),
                "row_counts": counts,
                "migrations": migrations,
            }
            log.info(f"PostgreSQL {pg_info['version']}: {len(tables)} таблиц, {pg_info['rows_total']} строк, "
                     f"{len(migrations)} миграций, дамп {human(known['postgres/onebase.dump'][0])}")

        with steps.timed("minio"):
            objects, vanished = [], 0
            if bucket_exists:
                for i, obj in enumerate(s3.list_objects(bucket)):
                    rel = f"minio/{bucket}/data/{i:07d}"
                    try:
                        size, digest, content_type = s3.get_object(bucket, obj.key, staging / rel)
                    except S3Error as exc:
                        if exc.status == 404:
                            vanished += 1
                            (staging / rel).unlink(missing_ok=True)
                            continue
                        raise
                    known[rel] = (size, digest)
                    objects.append({"file": rel, "key": obj.key, "size": size, "sha256": digest,
                                    "content_type": content_type, "etag": obj.etag, "last_modified": obj.last_modified})
            index = staging / "minio" / f"{bucket}.jsonl"
            index.parent.mkdir(parents=True, exist_ok=True)
            with open(index, "w", encoding="utf-8") as f:
                for entry in objects:
                    f.write(json.dumps(entry, ensure_ascii=False) + "\n")
            minio_info = {"bucket": bucket, "bucket_existed": bucket_exists, "objects": len(objects),
                          "bytes": sum(o["size"] for o in objects), "index": index.relative_to(staging).as_posix(),
                          "vanished_during_backup": vanished}
            if vanished:
                log.warn(f"MinIO: {vanished} объектов удалены во время копии — в наборе их нет")
            log.info(f"MinIO {bucket}: {len(objects)} объектов, {human(minio_info['bytes'])}")

        with steps.timed("qdrant"):
            qdrant_info = {"version": qd.version(), "collections": []}
            for c in collections:
                info = qd.collection_info(c)
                points = qd.count(c)
                snapshot = qd.create_snapshot(c)
                rel = f"qdrant/{c}.snapshot"
                try:
                    known[rel] = qd.download_snapshot(c, snapshot, staging / rel)
                finally:
                    qd.delete_snapshot(c, snapshot)
                qdrant_info["collections"].append({"name": c, "points": points, "file": rel, "config": info.get("config", {})})
                log.info(f"Qdrant {c}: {points} точек, снимок {human(known[rel][0])}")

        with steps.timed("config"):
            config = staging / "config"
            config.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(env_path, config / "env")
            os.chmod(config / "env", 0o600)
            compose_files = []
            for pattern in COMPOSE_FILES:
                for path in sorted(project.glob(pattern)):
                    (config / "compose").mkdir(exist_ok=True)
                    shutil.copyfile(path, config / "compose" / path.name)
                    compose_files.append(path.name)
            cron = run(["crontab", "-l"], log, check=False)
            if cron.returncode == 0 and cron.stdout.strip():
                (config / "crontab").write_text(cron.stdout, encoding="utf-8")
            host = host_info(log)
            images = compose.images()

        with steps.timed("source"):
            git = lambda *a: run(["git", *a], log, cwd=project).stdout.strip()  # noqa: E731
            commit = git("rev-parse", "HEAD")
            branch = git("rev-parse", "--abbrev-ref", "HEAD")
            changed = [line for line in git("status", "--porcelain").splitlines() if line.strip()]
            (staging / "source").mkdir(exist_ok=True)
            run(["git", "bundle", "create", str(staging / "source" / "onebase.bundle"), "HEAD"], log, cwd=project)
            tracked_changes = [c for c in changed if not c.startswith("??")]
            if tracked_changes:
                patch = run(["git", "diff", "HEAD", "--binary"], log, cwd=project).stdout
                (staging / "source" / "worktree.patch").write_text(patch, encoding="utf-8")
                log.warn(f"В рабочем каталоге {len(tracked_changes)} изменённых файлов — сохранены в source/worktree.patch")
            source_info = {"commit": commit, "branch": branch, "bundle": "source/onebase.bundle",
                           "patch": "source/worktree.patch" if tracked_changes else None,
                           "untracked": [c[3:] for c in changed if c.startswith("??")]}

        manifest = {
            "format": FORMAT, "version": VERSION, "name": name, "created_at": started, "tool": __version__,
            "project_name": project.name, "source": source_info, "host": host, "images": images,
            "compose_files": compose_files,
            "postgres": pg_info, "minio": minio_info, "qdrant": qdrant_info,
            "redis": {"included": False, "reason": "только кэш ответов консультанта — наполнится сам"},
            "secrets": {"env": "config/env", "dataprotection": "в базе: таблица DataProtectionKeys"},
            "steps": steps.items,
        }
        with steps.timed("manifest"):
            manifest["files"] = file_entries(staging, known)
            write_json(staging / INNER, manifest)

        with steps.timed("encrypt"):
            payload = partial / PAYLOAD
            encrypt_dir(staging, payload, recipients_path, log)
            payload_sha = sha256_file(payload)
            (partial / PAYLOAD_SHA).write_text(f"{payload_sha}  {PAYLOAD}\n", encoding="utf-8")
            build_engine(partial / ENGINE)

        public = {
            "format": FORMAT, "version": VERSION, "name": name, "created_at": started, "tool": __version__,
            "onebase": {"commit": source_info["commit"], "branch": source_info["branch"], "dirty": bool(source_info["patch"])},
            "components": {
                "postgres": {"version": pg_info["version"], "tables": pg_info["tables"], "rows_total": pg_info["rows_total"],
                             "migrations": len(pg_info["migrations"]), "last_migration": (pg_info["migrations"] or [None])[-1]},
                "minio": {"bucket": bucket, "objects": minio_info["objects"], "bytes": minio_info["bytes"]},
                "qdrant": {"version": qdrant_info["version"],
                           "collections": [{"name": c["name"], "points": c["points"]} for c in qdrant_info["collections"]]},
                "redis": {"included": False},
            },
            "payload": {"file": PAYLOAD, "size": payload.stat().st_size, "sha256": payload_sha},
            "recipients": recipients_fingerprint(recipients),
            "engine": {"file": ENGINE, "version": __version__},
            "host": {"os": host["os"], "arch": host["arch"], "docker": host["docker"], "compose": host["compose"]},
        }
        write_json(partial / PUBLIC, public)

        problems = check_outer(partial)
        if problems:
            raise DrError("Готовый набор не прошёл проверку: " + "; ".join(problems))
        os.rename(partial, final)
        seconds = round(time.monotonic() - t0, 1)
        size = sum(p.stat().st_size for p in final.iterdir())
        status(backup_dir, {"status": "success", "set": name, "started_at": started, "finished_at": now_iso(),
                            "seconds": seconds, "size": size, "payload_sha256": payload_sha})
        log.info(f"Готово: {final} ({human(size)}, {seconds} с)")
        if opts.keep:
            rotate(backup_dir, opts.keep, final, log)
        return final
    except BaseException as exc:
        shutil.rmtree(partial, ignore_errors=True)
        status(backup_dir, {"status": "failed", "set": name, "started_at": started, "finished_at": now_iso(),
                            "error": log.mask(str(exc)) or type(exc).__name__})
        raise
    finally:
        shutil.rmtree(staging, ignore_errors=True)
        fcntl.flock(lock, fcntl.LOCK_UN)
        lock.close()
