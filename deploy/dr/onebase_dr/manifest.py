"""Манифест копии и проверка набора.

Набор копии — каталог onebase-backup-ГГГГ-ММ-ДД_ЧЧММ/:
  payload.tar.age          всё содержимое, зашифровано age
  payload.tar.age.sha256   SHA-256 зашифрованного файла (формат sha256sum -c)
  manifest.public.json     без секретов и персональных данных: версии, размеры, SHA-256, отпечаток ключа получателя
  onebase-restore.pyz      Restore Engine той же версии
Внутри payload.tar — manifest.json (полный) и файлы, перечисленные в нём с SHA-256.
"""

from __future__ import annotations

import hashlib
import os
import re
from pathlib import Path

from .util import read_json, sha256_file

FORMAT = "onebase-backup"
VERSION = 1
PAYLOAD = "payload.tar.age"
PAYLOAD_SHA = PAYLOAD + ".sha256"
PUBLIC = "manifest.public.json"
ENGINE = "onebase-restore.pyz"
INNER = "manifest.json"
SET_NAME = re.compile(r"^onebase-backup-\d{4}-\d{2}-\d{2}_\d{4}(-\d+)?$")
AGE_HEADER = b"age-encryption.org/v1\n"


def recipients_fingerprint(recipients: list[str]) -> str:
    """Отпечаток открытых ключей: по нему видно, каким ключом восстановления открывается набор, без самого ключа."""
    joined = "\n".join(sorted(recipients)).encode()
    return "sha256:" + hashlib.sha256(joined).hexdigest()[:16]


def read_recipients(path: Path) -> list[str]:
    keys = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            keys.append(line)
    return keys


def file_entries(root: Path, known: dict[str, tuple[int, str]]) -> dict[str, dict]:
    """Все файлы каталога копии (кроме самого manifest.json) с размером и SHA-256; уже посчитанные не читаются заново."""
    entries: dict[str, dict] = {}
    for dirpath, _, names in os.walk(root):
        for name in names:
            path = Path(dirpath) / name
            rel = path.relative_to(root).as_posix()
            if rel == INNER:
                continue
            size, digest = known.get(rel) or (path.stat().st_size, sha256_file(path))
            entries[rel] = {"size": size, "sha256": digest}
    return dict(sorted(entries.items()))


def verify_files(root: Path, entries: dict[str, dict]) -> list[str]:
    """Расхождения содержимого с манифестом: нет файла, другой размер, другой SHA-256, лишний файл."""
    problems = []
    for rel, entry in entries.items():
        path = root / rel
        if not path.is_file():
            problems.append(f"нет файла {rel}")
        elif path.stat().st_size != entry["size"]:
            problems.append(f"другой размер {rel}")
        elif sha256_file(path) != entry["sha256"]:
            problems.append(f"не совпадает SHA-256 {rel}")
    for dirpath, _, names in os.walk(root):
        for name in names:
            rel = (Path(dirpath) / name).relative_to(root).as_posix()
            if rel != INNER and rel not in entries:
                problems.append(f"лишний файл {rel}")
    return problems


def read_payload_sha(set_dir: Path) -> str:
    text = (set_dir / PAYLOAD_SHA).read_text(encoding="utf-8").split()
    if not text or not re.fullmatch(r"[0-9a-f]{64}", text[0]):
        raise ValueError(f"{PAYLOAD_SHA}: нет SHA-256")
    return text[0]


def check_outer(set_dir: Path) -> list[str]:
    """Проверка без ключа: состав набора, формат, SHA-256 зашифрованного файла и заголовок age."""
    problems = []
    for name in (PAYLOAD, PAYLOAD_SHA, PUBLIC):
        if not (set_dir / name).is_file():
            problems.append(f"нет {name}")
    if problems:
        return problems
    public = read_json(set_dir / PUBLIC)
    if public.get("format") != FORMAT:
        problems.append("это не набор копии OneBase")
    if int(public.get("version", 0)) > VERSION:
        problems.append(f"набор версии {public.get('version')}, этот инструмент знает до {VERSION} — нужен новый onebase-restore")
    expected = read_payload_sha(set_dir)
    if public.get("payload", {}).get("sha256") not in (None, expected):
        problems.append("SHA-256 в manifest.public.json и payload.tar.age.sha256 различаются")
    payload = set_dir / PAYLOAD
    if public.get("payload", {}).get("size") not in (None, payload.stat().st_size):
        problems.append("размер payload.tar.age не совпадает с манифестом")
    with open(payload, "rb") as f:
        if f.read(len(AGE_HEADER)) != AGE_HEADER:
            problems.append("payload.tar.age не похож на файл age")
    if not problems and sha256_file(payload) != expected:
        problems.append("SHA-256 payload.tar.age не совпадает: файл повреждён или изменён")
    return problems
