"""Чтение .env так же, как его читает docker compose: KEY=VALUE, кавычки, комментарии после « #» у значения без кавычек."""

from __future__ import annotations

import re
from pathlib import Path

_KEY = re.compile(r"^[A-Za-z_][A-Za-z0-9_.]*$")
# Переменные с такими словами в имени — секреты: их значения маскируются в журнале.
SECRET_HINTS = ("PASSWORD", "SECRET", "TOKEN", "KEY")


def parse(text: str) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw in text.splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("export "):
            line = line[7:].lstrip()
        key, sep, value = line.partition("=")
        key = key.strip()
        if not sep or not _KEY.match(key):
            continue
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        else:
            hash_at = value.find(" #")
            if hash_at >= 0:
                value = value[:hash_at].rstrip()
        values[key] = value
    return values


def load(path: Path) -> dict[str, str]:
    return parse(path.read_text(encoding="utf-8"))


def secret_values(values: dict[str, str]) -> list[str]:
    return [v for k, v in values.items() if v and any(hint in k.upper() for hint in SECRET_HINTS)]
