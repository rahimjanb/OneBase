"""Общее: журнал с маскированием секретов, запуск команд, SHA-256, атомарная запись JSON."""

from __future__ import annotations

import datetime as dt
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import IO, Iterable, Sequence

CHUNK = 1024 * 1024

# Значения, похожие на ключи, маскируются, даже если их нет в .env: закрытый ключ age, JWT, токены Telegram-ботов.
_PATTERNS = [
    re.compile(r"AGE-SECRET-KEY-1[0-9A-Z]+"),
    re.compile(r"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}"),
    re.compile(r"\b\d{6,}:[A-Za-z0-9_-]{30,}\b"),
]


class DrError(Exception):
    """Ошибка шага: текст уже без секретов и понятен администратору."""


class Masker:
    """Заменяет известные секреты на *** — в журнале и в тексте ошибок."""

    def __init__(self) -> None:
        self._values: list[str] = []

    def add(self, *values: str | None) -> None:
        for value in values:
            # Короткие значения («1», «onebase») маскировать нельзя: испортится весь журнал.
            if value and len(value) >= 8 and value not in self._values:
                self._values.append(value)
        self._values.sort(key=len, reverse=True)

    def __call__(self, text: str) -> str:
        for value in self._values:
            text = text.replace(value, "***")
        for pattern in _PATTERNS:
            text = pattern.sub("***", text)
        return text


class Log:
    """Журнал: строки со временем в stdout и, если задан, в файл. Всё проходит через Masker."""

    def __init__(self, masker: Masker, file: Path | None = None) -> None:
        self.mask = masker
        self._file: IO[str] | None = open(file, "a", encoding="utf-8") if file else None

    def _write(self, level: str, message: str) -> None:
        line = f"{dt.datetime.now():%Y-%m-%d %H:%M:%S} {level:5} {self.mask(message)}"
        print(line, flush=True)
        if self._file:
            self._file.write(line + "\n")
            self._file.flush()

    def info(self, message: str) -> None:
        self._write("INFO", message)

    def warn(self, message: str) -> None:
        self._write("WARN", message)

    def error(self, message: str) -> None:
        self._write("ERROR", message)

    def close(self) -> None:
        if self._file:
            self._file.close()
            self._file = None


def run(argv: Sequence[str], log: Log, *, cwd: Path | None = None, input_text: str | None = None,
        env: dict[str, str] | None = None, check: bool = True, timeout: float | None = None) -> subprocess.CompletedProcess[str]:
    """Команда с захватом вывода. Секреты передаются только через env, в argv их не бывает — argv можно показывать."""
    try:
        result = subprocess.run(list(argv), cwd=cwd, input=input_text, capture_output=True, text=True, env=env, timeout=timeout)
    except FileNotFoundError as exc:
        raise DrError(f"Не найдена программа {argv[0]}") from exc
    except subprocess.TimeoutExpired as exc:
        raise DrError(f"Команда не ответила за {timeout:.0f} с: {' '.join(argv[:4])}…") from exc
    if check and result.returncode != 0:
        raise DrError(f"Команда завершилась с кодом {result.returncode}: {' '.join(argv[:6])}\n{tail(log.mask(result.stderr))}")
    return result


def tail(text: str, lines: int = 12) -> str:
    return "\n".join(text.strip().splitlines()[-lines:])


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(CHUNK), b""):
            digest.update(block)
    return digest.hexdigest()


class HashingWriter:
    """Пишет поток в файл и сразу считает SHA-256 и размер — файл не читается второй раз."""

    def __init__(self, path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        self._f = open(path, "wb")
        self._digest = hashlib.sha256()
        self.size = 0

    def write(self, block: bytes) -> None:
        self._f.write(block)
        self._digest.update(block)
        self.size += len(block)

    def close(self) -> tuple[int, str]:
        self._f.close()
        return self.size, self._digest.hexdigest()


def copy_stream(source: IO[bytes], path: Path) -> tuple[int, str]:
    writer = HashingWriter(path)
    try:
        for block in iter(lambda: source.read(CHUNK), b""):
            writer.write(block)
    finally:
        size, digest = writer.close()
    return size, digest


def write_json(path: Path, data: object) -> None:
    """Атомарно: во временный файл рядом и переименование — читатель не увидит половину файла."""
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")
        os.replace(tmp, path)
    except BaseException:
        Path(tmp).unlink(missing_ok=True)
        raise


def read_json(path: Path) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def human(size: float) -> str:
    for unit in ("Б", "КБ", "МБ", "ГБ", "ТБ"):
        if size < 1024 or unit == "ТБ":
            return f"{size:.0f} {unit}" if unit == "Б" else f"{size:.1f} {unit}"
        size /= 1024
    return f"{size:.1f} ТБ"


def now_iso() -> str:
    return dt.datetime.now(dt.timezone.utc).astimezone().isoformat(timespec="seconds")


def private_dir(path: Path) -> Path:
    """Каталог, доступный только владельцу: в нём бывают открытые данные копии."""
    path.mkdir(parents=True, exist_ok=True)
    os.chmod(path, 0o700)
    return path


def fail(message: str) -> None:
    print(message, file=sys.stderr)
    raise SystemExit(2)


def lines(items: Iterable[str]) -> str:
    return "\n".join(f"  - {item}" for item in items)
