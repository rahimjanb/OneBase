"""PostgreSQL в контейнере compose: дамп в экспортированном снимке и точный счёт строк каждой таблицы в том же снимке.

Команды идут через `docker compose exec postgres` под пользователем и базой из окружения контейнера (POSTGRES_USER, POSTGRES_DB),
поэтому пароль никуда не передаётся.
"""

from __future__ import annotations

import re
import secrets
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path

from .compose import Compose
from .util import CHUNK, DrError, HashingWriter, Log, tail

SEP = "\x1f"
_SNAPSHOT = re.compile(r"^[0-9A-F]+-[0-9A-F]+-[0-9]+$")
PSQL = 'exec psql -X -q -At -v ON_ERROR_STOP=1 -F "$(printf \'\\037\')" -U "$POSTGRES_USER" -d "$POSTGRES_DB"'


def ident(name: str) -> str:
    return '"' + name.replace('"', '""') + '"'


def literal(text: str) -> str:
    return "'" + text.replace("'", "''") + "'"


@dataclass
class Table:
    schema: str
    name: str

    @property
    def full(self) -> str:
        return f"{self.schema}.{self.name}"


class Postgres:
    def __init__(self, compose: Compose, log: Log, service: str = "postgres") -> None:
        self.compose = compose
        self.log = log
        self.service = service

    def query(self, sql: str, *, timeout: float = 600) -> list[list[str]]:
        result = self.compose.run("exec", "-T", self.service, "sh", "-c", PSQL, input_text=sql, timeout=timeout)
        return [row.split(SEP) for row in result.stdout.splitlines() if row != ""]

    def scalar(self, sql: str) -> str:
        rows = self.query(sql)
        return rows[0][0] if rows and rows[0] else ""

    def ready(self) -> bool:
        result = self.compose.run("exec", "-T", self.service, "sh", "-c", 'pg_isready -q -U "$POSTGRES_USER" -d "$POSTGRES_DB"', check=False)
        return result.returncode == 0

    def wait_ready(self, seconds: int = 120) -> None:
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if self.ready():
                return
            time.sleep(2)
        raise DrError(f"PostgreSQL не готов за {seconds} с")

    def server_version(self) -> str:
        return self.scalar("SHOW server_version;")

    def database_size(self) -> int:
        return int(self.scalar("SELECT pg_database_size(current_database());") or 0)

    def extensions(self) -> dict[str, str]:
        return {r[0]: r[1] for r in self.query("SELECT extname, extversion FROM pg_extension ORDER BY 1;")}

    @staticmethod
    def _in_snapshot(snapshot: str | None, sql: str) -> str:
        if snapshot is None:
            return sql
        return ("BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;\n"
                f"SET TRANSACTION SNAPSHOT {literal(snapshot)};\n{sql}\nCOMMIT;\n")

    def tables(self, snapshot: str | None = None) -> list[Table]:
        sql = ("SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace "
               "WHERE c.relkind = 'r' AND n.nspname NOT IN ('pg_catalog', 'information_schema') "
               "AND n.nspname NOT LIKE 'pg\\_toast%' AND n.nspname NOT LIKE 'pg\\_temp%' ORDER BY 1, 2;")
        return [Table(r[0], r[1]) for r in self.query(self._in_snapshot(snapshot, sql))]

    def counts(self, tables: list[Table], snapshot: str | None = None) -> dict[str, int]:
        if not tables:
            return {}
        parts = [f"SELECT {literal(t.full)}, count(*) FROM {ident(t.schema)}.{ident(t.name)}" for t in tables]
        rows = self.query(self._in_snapshot(snapshot, "\nUNION ALL\n".join(parts) + ";"), timeout=3600)
        return {r[0]: int(r[1]) for r in rows}

    def migrations(self, snapshot: str | None = None) -> list[str]:
        exists = self.scalar("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;")
        if exists != "t":
            return []
        return [r[0] for r in self.query(self._in_snapshot(snapshot, 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;'))]

    def snapshot(self) -> "ExportedSnapshot":
        return ExportedSnapshot(self)

    def dump(self, snapshot: str, dest: Path) -> tuple[int, str]:
        """pg_dump -Fc в снимке; поток пишется в файл с подсчётом SHA-256."""
        if not _SNAPSHOT.match(snapshot):
            raise DrError("Неожиданный идентификатор снимка PostgreSQL")
        command = ["sh", "-c", f'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc --snapshot={snapshot}']
        proc = self.compose.popen(self.service, command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        writer = HashingWriter(dest)
        try:
            assert proc.stdout is not None
            for block in iter(lambda: proc.stdout.read(CHUNK), b""):
                writer.write(block)
        finally:
            size, digest = writer.close()
        err = proc.stderr.read().decode("utf-8", "replace") if proc.stderr else ""
        if proc.wait() != 0:
            raise DrError("pg_dump завершился с ошибкой:\n" + tail(self.log.mask(err)))
        return size, digest

    def dump_globals(self) -> str:
        result = self.compose.run("exec", "-T", self.service, "sh", "-c",
                                  'exec pg_dumpall -U "$POSTGRES_USER" --globals-only --no-role-passwords', timeout=300)
        return result.stdout


class ExportedSnapshot:
    """Транзакция, которая держит снимок открытым, пока идут счёт строк и pg_dump.

    Идентификатор снимка psql пишет в файл внутри контейнера (\\g файл закрывает файл сразу) — так не зависим от буферизации
    вывода psql в канал.
    """

    def __init__(self, pg: Postgres) -> None:
        self.pg = pg
        self.id = ""
        self._proc: subprocess.Popen | None = None
        self._file = f"/tmp/onebase-snapshot-{secrets.token_hex(6)}"

    def __enter__(self) -> "ExportedSnapshot":
        self._proc = self.pg.compose.popen(self.pg.service, ["sh", "-c", PSQL], stdin=subprocess.PIPE,
                                           stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True)
        assert self._proc.stdin is not None
        self._proc.stdin.write("BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;\n"
                               f"SELECT pg_export_snapshot() \\g {self._file}\n")
        self._proc.stdin.flush()
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            if self._proc.poll() is not None:
                err = self._proc.stderr.read() if self._proc.stderr else ""
                raise DrError("Не удалось открыть снимок PostgreSQL:\n" + tail(self.pg.log.mask(err)))
            read = self.pg.compose.run("exec", "-T", self.pg.service, "cat", self._file, check=False)
            value = read.stdout.strip()
            if read.returncode == 0 and _SNAPSHOT.match(value):
                self.id = value
                return self
            time.sleep(0.5)
        self._close()
        raise DrError("PostgreSQL не выдал снимок за 60 с")

    def _close(self) -> None:
        if self._proc and self._proc.poll() is None:
            try:
                assert self._proc.stdin is not None
                self._proc.stdin.write("COMMIT;\n\\q\n")
                self._proc.stdin.close()
                self._proc.wait(timeout=60)
            except (OSError, subprocess.TimeoutExpired):
                self._proc.kill()
        self.pg.compose.run("exec", "-T", self.pg.service, "rm", "-f", self._file, check=False)

    def __exit__(self, *exc) -> None:
        self._close()
