"""Шифрование набора копии программой age: каталог → поток tar → age (открытые ключи); обратно — age -d с ключом → tar.

На сервере лежат только открытые ключи (recipients.txt): сервер шифрует копии, но открыть их не может. Закрытый ключ
восстановления хранит администратор вне сервера.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import tarfile
from pathlib import Path, PurePosixPath

from .manifest import INNER
from .util import DrError, Log, run, tail

INSTALL_HINT = "Нужна программа age: sudo apt install -y age"


def require_age() -> None:
    for tool in ("age", "age-keygen"):
        if not shutil.which(tool):
            raise DrError(INSTALL_HINT)


def _reset(info: tarfile.TarInfo) -> tarfile.TarInfo:
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    info.mode = 0o700 if info.isdir() else 0o600
    return info


def encrypt_dir(source: Path, out: Path, recipients_file: Path, log: Log) -> None:
    """manifest.json — первым: при чтении потока его видно раньше данных."""
    with subprocess.Popen(["age", "-R", str(recipients_file), "-o", str(out)], stdin=subprocess.PIPE, stderr=subprocess.PIPE) as proc:
        assert proc.stdin is not None and proc.stderr is not None
        try:
            with tarfile.open(fileobj=proc.stdin, mode="w|", format=tarfile.PAX_FORMAT) as tar:
                tar.add(source / INNER, arcname=INNER, filter=_reset)
                for dirpath, dirnames, names in os.walk(source):
                    dirnames.sort()
                    for name in sorted(names):
                        path = Path(dirpath) / name
                        rel = path.relative_to(source).as_posix()
                        if rel != INNER:
                            tar.add(path, arcname=rel, recursive=False, filter=_reset)
            proc.stdin.close()
        except BaseException:
            proc.kill()
            raise
        err = proc.stderr.read().decode("utf-8", "replace")
        if proc.wait() != 0:
            raise _age_error("age не зашифровал копию", err, log)


def _age_error(prefix: str, err: str, log: Log, exc: BaseException | None = None) -> DrError:
    hint = " — ключ не подходит к этой копии" if "no identity matched" in err else ""
    return DrError(prefix + hint + ":\n" + (tail(log.mask(err)) or str(exc or "")))


def _safe(member: tarfile.TarInfo) -> bool:
    path = PurePosixPath(member.name)
    return (member.isfile() or member.isdir()) and not path.is_absolute() and ".." not in path.parts


def decrypt_to_dir(payload: Path, key_file: Path, dest: Path, log: Log) -> None:
    """Расшифровка и распаковка потоком. Неверный ключ, повреждение или подмена — ошибка age (он проверяет целостность)."""
    if not key_file.is_file():
        raise DrError(f"Нет файла ключа восстановления: {key_file}")
    with subprocess.Popen(["age", "-d", "-i", str(key_file), str(payload)], stdout=subprocess.PIPE, stderr=subprocess.PIPE) as proc:
        assert proc.stdout is not None and proc.stderr is not None
        try:
            with tarfile.open(fileobj=proc.stdout, mode="r|") as tar:
                for member in tar:
                    if not _safe(member):
                        raise DrError(f"В копии недопустимый элемент: {member.name}")
                    try:
                        tar.extract(member, dest, filter="data")
                    except TypeError:
                        # Python без фильтров распаковки: путь уже проверен в _safe.
                        tar.extract(member, dest)
        except tarfile.TarError as exc:
            # Неверный ключ: age завершается сразу, tar видит пустой поток — причина в stderr age.
            proc.kill()
            proc.wait()
            raise _age_error("Копия не расшифрована", proc.stderr.read().decode("utf-8", "replace"), log, exc) from exc
        except BaseException:
            proc.kill()
            raise
        err = proc.stderr.read().decode("utf-8", "replace")
        if proc.wait() != 0:
            raise _age_error("Копия не расшифрована", err, log)


def generate_key(out: Path, log: Log) -> str:
    """Новая пара ключей: закрытый — в out (0600), возвращается открытый (age1…)."""
    require_age()
    if out.exists():
        raise DrError(f"{out} уже есть — не перезаписываю")
    old = os.umask(0o077)
    try:
        run(["age-keygen", "-o", str(out)], log)
    finally:
        os.umask(old)
    for line in out.read_text(encoding="utf-8").splitlines():
        if line.startswith("# public key: "):
            return line.split(": ", 1)[1].strip()
    raise DrError("age-keygen не записал открытый ключ")
