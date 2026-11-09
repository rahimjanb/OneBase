"""Минимальный S3-клиент для MinIO (AWS Signature V4, path-style): список, скачивание и загрузка объектов потоком.

Свой, а не mc/rclone: на чистом сервере не нужно ничего ставить, а SHA-256 каждого объекта считается при скачивании и
проверяется самим MinIO при загрузке (x-amz-content-sha256).
"""

from __future__ import annotations

import datetime as dt
import hashlib
import hmac
import http.client
import os
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator
from urllib.parse import quote

from .util import CHUNK, HashingWriter

EMPTY_SHA256 = hashlib.sha256(b"").hexdigest()
_NS = "{http://s3.amazonaws.com/doc/2006-03-01/}"


class S3Error(Exception):
    def __init__(self, status: int, code: str, message: str) -> None:
        super().__init__(f"S3 {status} {code}: {message}")
        self.status = status
        self.code = code


@dataclass
class S3Object:
    key: str
    size: int
    etag: str
    last_modified: str


def _hmac(key: bytes, text: str) -> bytes:
    return hmac.new(key, text.encode("utf-8"), hashlib.sha256).digest()


def signing_key(secret_key: str, date: str, region: str, service: str = "s3") -> bytes:
    k = _hmac(("AWS4" + secret_key).encode("utf-8"), date)
    k = _hmac(k, region)
    k = _hmac(k, service)
    return _hmac(k, "aws4_request")


def canonical_query(pairs: list[tuple[str, str]]) -> str:
    encoded = sorted((quote(k, safe="-_.~"), quote(v, safe="-_.~")) for k, v in pairs)
    return "&".join(f"{k}={v}" for k, v in encoded)


def authorization(method: str, canonical_uri: str, query: list[tuple[str, str]], headers: dict[str, str],
                  payload_hash: str, access_key: str, secret_key: str, region: str, amz_date: str) -> str:
    """Заголовок Authorization по AWS SigV4. headers — все отправляемые заголовки (подписываются все), имена любые по регистру."""
    lowered = {k.lower(): " ".join(str(v).split()) for k, v in headers.items()}
    names = sorted(lowered)
    canonical_headers = "".join(f"{n}:{lowered[n]}\n" for n in names)
    signed = ";".join(names)
    request = "\n".join([method, canonical_uri, canonical_query(query), canonical_headers, signed, payload_hash])
    date = amz_date[:8]
    scope = f"{date}/{region}/s3/aws4_request"
    to_sign = "\n".join(["AWS4-HMAC-SHA256", amz_date, scope, hashlib.sha256(request.encode("utf-8")).hexdigest()])
    signature = hmac.new(signing_key(secret_key, date, region), to_sign.encode("utf-8"), hashlib.sha256).hexdigest()
    return f"AWS4-HMAC-SHA256 Credential={access_key}/{scope}, SignedHeaders={signed}, Signature={signature}"


def object_uri(bucket: str, key: str | None = None) -> str:
    uri = "/" + quote(bucket, safe="-_.~")
    if key is not None:
        uri += "/" + quote(key, safe="/-_.~")
    return uri


class S3:
    def __init__(self, endpoint: str, access_key: str, secret_key: str, *, region: str = "us-east-1",
                 secure: bool = False, timeout: float = 120) -> None:
        self.host = endpoint.removeprefix("http://").removeprefix("https://").rstrip("/")
        self.access_key = access_key
        self.secret_key = secret_key
        self.region = region
        self.secure = secure
        self.timeout = timeout

    def _connection(self) -> http.client.HTTPConnection:
        cls = http.client.HTTPSConnection if self.secure else http.client.HTTPConnection
        return cls(self.host, timeout=self.timeout)

    def _send(self, method: str, uri: str, query: list[tuple[str, str]] | None = None, headers: dict[str, str] | None = None,
              body=None, payload_hash: str = EMPTY_SHA256) -> tuple[http.client.HTTPConnection, http.client.HTTPResponse]:
        query = query or []
        amz_date = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        all_headers = {"Host": self.host, "x-amz-date": amz_date, "x-amz-content-sha256": payload_hash, **(headers or {})}
        all_headers["Authorization"] = authorization(method, uri, query, all_headers, payload_hash,
                                                     self.access_key, self.secret_key, self.region, amz_date)
        target = uri + ("?" + canonical_query(query) if query else "")
        conn = self._connection()
        conn.request(method, target, body=body, headers=all_headers)
        return conn, conn.getresponse()

    @staticmethod
    def _raise(response: http.client.HTTPResponse) -> None:
        data = response.read()
        code, message = "", data[:200].decode("utf-8", "replace")
        try:
            root = ET.fromstring(data)
            code = root.findtext("Code") or ""
            message = root.findtext("Message") or message
        except ET.ParseError:
            pass
        raise S3Error(response.status, code, message)

    def _simple(self, method: str, uri: str, query: list[tuple[str, str]] | None = None, ok: tuple[int, ...] = (200,)) -> tuple[int, bytes]:
        conn, response = self._send(method, uri, query)
        try:
            if response.status not in ok:
                self._raise(response)
            return response.status, response.read()
        finally:
            conn.close()

    def bucket_exists(self, bucket: str) -> bool:
        status, _ = self._simple("HEAD", object_uri(bucket), ok=(200, 404))
        return status == 200

    def make_bucket(self, bucket: str) -> None:
        self._simple("PUT", object_uri(bucket))

    def list_objects(self, bucket: str) -> Iterator[S3Object]:
        token: str | None = None
        while True:
            query = [("list-type", "2"), ("max-keys", "1000")]
            if token:
                query.append(("continuation-token", token))
            _, data = self._simple("GET", object_uri(bucket), query)
            root = ET.fromstring(data)
            for item in root.iter(_NS + "Contents"):
                yield S3Object(
                    key=item.findtext(_NS + "Key") or "",
                    size=int(item.findtext(_NS + "Size") or 0),
                    etag=(item.findtext(_NS + "ETag") or "").strip('"'),
                    last_modified=item.findtext(_NS + "LastModified") or "",
                )
            if (root.findtext(_NS + "IsTruncated") or "").lower() != "true":
                return
            token = root.findtext(_NS + "NextContinuationToken")
            if not token:
                return

    def get_object(self, bucket: str, key: str, dest: Path) -> tuple[int, str, str]:
        """Скачивает в файл. Возвращает (размер, SHA-256, Content-Type). Нет объекта — S3Error 404."""
        conn, response = self._send("GET", object_uri(bucket, key))
        try:
            if response.status != 200:
                self._raise(response)
            content_type = response.getheader("Content-Type") or "application/octet-stream"
            writer = HashingWriter(dest)
            try:
                for block in iter(lambda: response.read(CHUNK), b""):
                    writer.write(block)
            finally:
                size, digest = writer.close()
            expected = response.getheader("Content-Length")
            if expected is not None and int(expected) != size:
                raise S3Error(200, "ShortRead", f"получено {size} из {expected} байт")
            return size, digest, content_type
        finally:
            conn.close()

    def put_object(self, bucket: str, key: str, src: Path, sha256: str, content_type: str) -> None:
        """Загрузка с известным SHA-256: MinIO сам сверит содержимое и откажет при расхождении."""
        size = os.path.getsize(src)
        with open(src, "rb") as body:
            conn, response = self._send("PUT", object_uri(bucket, key), headers={
                "Content-Length": str(size), "Content-Type": content_type or "application/octet-stream",
            }, body=body, payload_hash=sha256)
            try:
                if response.status != 200:
                    self._raise(response)
                response.read()
            finally:
                conn.close()

    def head_object(self, bucket: str, key: str) -> dict[str, str] | None:
        conn, response = self._send("HEAD", object_uri(bucket, key))
        try:
            response.read()
            if response.status == 404:
                return None
            if response.status != 200:
                raise S3Error(response.status, "", "HEAD")
            return {k.lower(): v for k, v in response.getheaders()}
        finally:
            conn.close()
