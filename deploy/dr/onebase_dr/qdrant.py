"""REST-клиент Qdrant: коллекции, точный счёт точек, снимки коллекций (создать, скачать, удалить, загрузить при восстановлении)."""

from __future__ import annotations

import http.client
import json
import os
import uuid
from pathlib import Path
from urllib.parse import quote, urlsplit

from .util import CHUNK, HashingWriter


class QdrantError(Exception):
    pass


class Qdrant:
    def __init__(self, url: str = "http://127.0.0.1:6333", *, api_key: str | None = None, timeout: float = 600) -> None:
        parts = urlsplit(url)
        self.host = parts.netloc
        self.secure = parts.scheme == "https"
        self.api_key = api_key
        self.timeout = timeout

    def _connection(self) -> http.client.HTTPConnection:
        cls = http.client.HTTPSConnection if self.secure else http.client.HTTPConnection
        return cls(self.host, timeout=self.timeout)

    def _headers(self, extra: dict[str, str] | None = None) -> dict[str, str]:
        headers = dict(extra or {})
        if self.api_key:
            headers["api-key"] = self.api_key
        return headers

    def _json(self, method: str, path: str, body: object | None = None) -> object:
        conn = self._connection()
        try:
            payload = json.dumps(body).encode() if body is not None else None
            conn.request(method, path, body=payload, headers=self._headers({"Content-Type": "application/json"} if payload else None))
            response = conn.getresponse()
            data = response.read()
            if response.status >= 300:
                raise QdrantError(f"Qdrant {method} {path}: {response.status} {data[:300].decode('utf-8', 'replace')}")
            parsed = json.loads(data or b"{}")
            return parsed.get("result", parsed) if isinstance(parsed, dict) else parsed
        finally:
            conn.close()

    @staticmethod
    def _c(name: str) -> str:
        return quote(name, safe="")

    def version(self) -> str:
        result = self._json("GET", "/")
        return str(result.get("version", "")) if isinstance(result, dict) else ""

    def collections(self) -> list[str]:
        result = self._json("GET", "/collections")
        return sorted(c["name"] for c in result.get("collections", []))

    def collection_info(self, name: str) -> dict:
        return self._json("GET", f"/collections/{self._c(name)}")

    def count(self, name: str) -> int:
        result = self._json("POST", f"/collections/{self._c(name)}/points/count", {"exact": True})
        return int(result.get("count", 0))

    def create_snapshot(self, name: str) -> str:
        result = self._json("POST", f"/collections/{self._c(name)}/snapshots?wait=true")
        return str(result["name"])

    def delete_snapshot(self, name: str, snapshot: str) -> None:
        self._json("DELETE", f"/collections/{self._c(name)}/snapshots/{self._c(snapshot)}?wait=true")

    def download_snapshot(self, name: str, snapshot: str, dest: Path) -> tuple[int, str]:
        conn = self._connection()
        try:
            conn.request("GET", f"/collections/{self._c(name)}/snapshots/{self._c(snapshot)}", headers=self._headers())
            response = conn.getresponse()
            if response.status != 200:
                raise QdrantError(f"Qdrant: снимок {snapshot} не скачан: {response.status} {response.read()[:200]!r}")
            writer = HashingWriter(dest)
            try:
                for block in iter(lambda: response.read(CHUNK), b""):
                    writer.write(block)
            finally:
                size, digest = writer.close()
            return size, digest
        finally:
            conn.close()

    def upload_snapshot(self, name: str, path: Path) -> None:
        """Восстановление коллекции из файла снимка (коллекция создаётся или заменяется данными снимка)."""
        boundary = uuid.uuid4().hex
        head = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"snapshot\"; filename=\"{path.name}\"\r\n"
                "Content-Type: application/octet-stream\r\n\r\n").encode()
        tail = f"\r\n--{boundary}--\r\n".encode()
        size = len(head) + os.path.getsize(path) + len(tail)
        conn = self._connection()
        try:
            conn.putrequest("POST", f"/collections/{self._c(name)}/snapshots/upload?wait=true&priority=snapshot")
            for key, value in self._headers({"Content-Type": f"multipart/form-data; boundary={boundary}", "Content-Length": str(size)}).items():
                conn.putheader(key, value)
            conn.endheaders()
            conn.send(head)
            with open(path, "rb") as f:
                for block in iter(lambda: f.read(CHUNK), b""):
                    conn.send(block)
            conn.send(tail)
            response = conn.getresponse()
            data = response.read()
            if response.status >= 300:
                raise QdrantError(f"Qdrant: снимок {name} не загружен: {response.status} {data[:300].decode('utf-8', 'replace')}")
        finally:
            conn.close()
