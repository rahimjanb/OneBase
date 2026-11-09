"""Проверка набора копии: без ключа — состав, формат и SHA-256 зашифрованного файла; с ключом — расшифровка и SHA-256 каждого файла."""

from __future__ import annotations

import shutil
from pathlib import Path

from .crypto import decrypt_to_dir, require_age
from .manifest import INNER, PAYLOAD, PUBLIC, check_outer, verify_files
from .util import DrError, Log, human, private_dir, read_json


def open_set(set_dir: Path, key: Path, work: Path, log: Log) -> tuple[Path, dict]:
    """Расшифровывает набор в work/<имя>.open (0700) и сверяет файлы с манифестом. Возвращает (каталог, манифест)."""
    problems = check_outer(set_dir)
    if problems:
        raise DrError("Набор повреждён: " + "; ".join(problems))
    require_age()
    target = work / f".{set_dir.name}.open"
    if target.exists():
        shutil.rmtree(target)
    private_dir(target)
    payload = set_dir / PAYLOAD
    need = payload.stat().st_size * 1.1
    free = shutil.disk_usage(work).free
    if free < need:
        raise DrError(f"Мало места для расшифровки в {work}: нужно ≈{human(need)}, свободно {human(free)}")
    log.info(f"Расшифровка {payload.name} ({human(payload.stat().st_size)})…")
    decrypt_to_dir(payload, key, target, log)
    if not (target / INNER).is_file():
        raise DrError("В копии нет manifest.json")
    manifest = read_json(target / INNER)
    problems = verify_files(target, manifest.get("files", {}))
    if problems:
        raise DrError(f"Содержимое не совпадает с манифестом ({len(problems)}): " + "; ".join(problems[:10]))
    return target, manifest


def run_verify(set_dir: Path, key: Path | None, work: Path | None, log: Log) -> int:
    set_dir = set_dir.expanduser().resolve()
    if not set_dir.is_dir():
        raise DrError(f"Нет каталога набора {set_dir}")
    problems = check_outer(set_dir)
    if problems:
        for p in problems:
            log.error(p)
        return 1
    public = read_json(set_dir / PUBLIC)
    c = public["components"]
    log.info(f"Набор {public['name']} от {public['created_at']}, OneBase {public['onebase']['commit'][:9]}")
    log.info(f"  PostgreSQL {c['postgres']['version']}: {c['postgres']['tables']} таблиц, {c['postgres']['rows_total']} строк")
    log.info(f"  MinIO {c['minio']['bucket']}: {c['minio']['objects']} объектов, {human(c['minio']['bytes'])}")
    log.info("  Qdrant " + c["qdrant"]["version"] + ": " + (", ".join(f"{q['name']} — {q['points']} точек" for q in c["qdrant"]["collections"]) or "коллекций нет"))
    log.info(f"  Зашифрованный файл: {human(public['payload']['size'])}, SHA-256 совпадает; ключ получателя {public['recipients']}")
    if key is None:
        log.info("Без ключа проверено всё, что можно проверить. С ключом (--key) — ещё расшифровка и SHA-256 каждого файла.")
        return 0
    work_dir = (work or set_dir.parent).expanduser().resolve()
    target = None
    try:
        target, manifest = open_set(set_dir, key.expanduser(), work_dir, log)
        log.info(f"Расшифровано и сверено: {len(manifest['files'])} файлов, все SHA-256 совпадают")
        return 0
    finally:
        if target is not None:
            shutil.rmtree(target, ignore_errors=True)
        else:
            shutil.rmtree(work_dir / f".{set_dir.name}.open", ignore_errors=True)
