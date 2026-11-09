"""docker compose в каталоге проекта: как администратор, `cd ~/onebase && docker compose …` (подхватываются те же файлы и .env)."""

from __future__ import annotations

import json
import subprocess
from pathlib import Path

from .util import DrError, Log, run


class Compose:
    def __init__(self, project_dir: Path, log: Log) -> None:
        self.dir = project_dir
        self.log = log

    def argv(self, *args: str) -> list[str]:
        return ["docker", "compose", *args]

    def run(self, *args: str, check: bool = True, timeout: float | None = None, input_text: str | None = None) -> subprocess.CompletedProcess[str]:
        return run(self.argv(*args), self.log, cwd=self.dir, check=check, timeout=timeout, input_text=input_text)

    def exec_argv(self, service: str, command: list[str]) -> list[str]:
        return self.argv("exec", "-T", service, *command)

    def popen(self, service: str, command: list[str], **kwargs) -> subprocess.Popen:
        return subprocess.Popen(self.exec_argv(service, command), cwd=self.dir, **kwargs)

    def ps(self) -> list[dict]:
        out = self.run("ps", "-a", "--format", "json").stdout.strip()
        if not out:
            return []
        # Старые версии Compose отдают массив, новые — по объекту в строке.
        if out.startswith("["):
            return json.loads(out)
        return [json.loads(line) for line in out.splitlines() if line.strip()]

    def running(self) -> set[str]:
        return {c.get("Service", "") for c in self.ps() if str(c.get("State", "")).lower() == "running"}

    def images(self) -> dict[str, dict]:
        """Сервис → образ: имя, Id и digest. По digest при восстановлении берётся тот же образ."""
        result: dict[str, dict] = {}
        for container in self.ps():
            service, cid = container.get("Service"), container.get("ID")
            if not service or not cid:
                continue
            # Имя образа — из настроек контейнера: с хранилищем containerd (Docker 29+) поле Image у ps — digest, по
            # которому `docker image inspect` образ не находит.
            meta = run(["docker", "inspect", "--format", "{{.Config.Image}}\t{{.Image}}", cid], self.log, check=False)
            name, _, container_image = meta.stdout.strip().partition("\t")
            name = name or container.get("Image", "")
            entry = {"image": name, "container_image": container_image or None}
            info = run(["docker", "image", "inspect", "--format", "{{json .}}", name], self.log, check=False)
            if info.returncode == 0:
                data = json.loads(info.stdout)
                entry.update(id=data.get("Id"), repo_digests=data.get("RepoDigests") or [], created=data.get("Created"))
            result[service] = entry
        return result

    def service_ip(self, service: str) -> str:
        """Адрес контейнера сервиса в сети проекта. По нему, а не по 127.0.0.1, инструмент попадает ровно в свой проект —
        даже если на машине опубликованы порты другого Docker (например, Docker Desktop рядом с учебным сервером)."""
        cid = self.run("ps", "-q", service).stdout.strip().splitlines()
        if not cid:
            raise DrError(f"Нет контейнера сервиса {service}")
        out = run(["docker", "inspect", "--format", "{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}", cid[0]], self.log).stdout
        ips = [ip for ip in out.split() if ip]
        if not ips:
            raise DrError(f"У контейнера {service} нет адреса в сети Docker")
        return ips[0]

    def require(self, services: list[str]) -> None:
        missing = [s for s in services if s not in self.running()]
        if missing:
            raise DrError("Не запущены сервисы: " + ", ".join(missing) + f". Проверьте: cd {self.dir} && docker compose ps")
