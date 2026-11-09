"""Модульные тесты Disaster Recovery: cd deploy/dr && PYTHONPATH=. python3 -m unittest discover -s tests"""

from __future__ import annotations

import json
import os
import shutil
import tempfile
import unittest
from pathlib import Path

from onebase_dr import envfile, manifest, s3
from onebase_dr.backup import rotate
from onebase_dr.util import Log, Masker, sha256_file, write_json

AWS_KEY = "AKIAIOSFODNN7EXAMPLE"
AWS_SECRET = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"


class S3SignatureTests(unittest.TestCase):
    """Примеры из документации AWS «Signature Calculations for the Authorization Header»."""

    def test_get_object_example(self):
        auth = s3.authorization("GET", "/test.txt", [], {
            "Host": "examplebucket.s3.amazonaws.com", "Range": "bytes=0-9",
            "x-amz-content-sha256": s3.EMPTY_SHA256, "x-amz-date": "20130524T000000Z",
        }, s3.EMPTY_SHA256, AWS_KEY, AWS_SECRET, "us-east-1", "20130524T000000Z")
        self.assertEqual(auth, "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
                               "SignedHeaders=host;range;x-amz-content-sha256;x-amz-date, "
                               "Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41")

    def test_list_objects_example(self):
        auth = s3.authorization("GET", "/", [("max-keys", "2"), ("prefix", "J")], {
            "Host": "examplebucket.s3.amazonaws.com",
            "x-amz-content-sha256": s3.EMPTY_SHA256, "x-amz-date": "20130524T000000Z",
        }, s3.EMPTY_SHA256, AWS_KEY, AWS_SECRET, "us-east-1", "20130524T000000Z")
        self.assertTrue(auth.endswith("Signature=34b48302e7b5fa45bde8084f4b7868a86f0a534bc59db6670ed5711ef69dc6f7"))

    def test_uri_and_query_encoding(self):
        self.assertEqual(s3.object_uri("onebase-files", "files/Отдел кадров/a+b.pdf"),
                         "/onebase-files/files/%D0%9E%D1%82%D0%B4%D0%B5%D0%BB%20%D0%BA%D0%B0%D0%B4%D1%80%D0%BE%D0%B2/a%2Bb.pdf")
        self.assertEqual(s3.canonical_query([("prefix", "a/b c"), ("list-type", "2")]), "list-type=2&prefix=a%2Fb%20c")


class EnvTests(unittest.TestCase):
    def test_parse_like_compose(self):
        values = envfile.parse('# c\nA=1\nexport B="x y"\nC=\'q#z\'\nD=v # comment\nE=a#b\nbad line\n')
        self.assertEqual(values, {"A": "1", "B": "x y", "C": "q#z", "D": "v", "E": "a#b"})

    def test_secret_values(self):
        values = {"POSTGRES_PASSWORD": "p" * 10, "JWT_SIGNING_KEY": "k" * 40, "PUBLIC_HOST": "example.org", "LINKO_TOKEN": ""}
        self.assertEqual(sorted(envfile.secret_values(values)), sorted(["p" * 10, "k" * 40]))


class MaskerTests(unittest.TestCase):
    def test_masks_known_values_and_patterns(self):
        m = Masker()
        m.add("supersecretvalue", "short", None)
        text = m("pw=supersecretvalue key=AGE-SECRET-KEY-1QQQQ5555ZZZZ short")
        self.assertNotIn("supersecretvalue", text)
        self.assertNotIn("AGE-SECRET-KEY", text)
        self.assertIn("short", text)  # короткие значения не маскируются — иначе испортится журнал


class ManifestTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp())

    def tearDown(self):
        shutil.rmtree(self.root)

    def test_entries_and_corruption(self):
        (self.root / "a").mkdir()
        (self.root / "a" / "one.bin").write_bytes(b"hello")
        (self.root / "two.txt").write_text("мир", encoding="utf-8")
        entries = manifest.file_entries(self.root, {})
        self.assertEqual(set(entries), {"a/one.bin", "two.txt"})
        self.assertEqual(manifest.verify_files(self.root, entries), [])
        (self.root / "a" / "one.bin").write_bytes(b"hellO")
        (self.root / "extra").write_text("x")
        problems = manifest.verify_files(self.root, entries)
        self.assertTrue(any("SHA-256 a/one.bin" in p for p in problems))
        self.assertTrue(any("лишний файл extra" in p for p in problems))

    def _set(self, name: str) -> Path:
        d = self.root / name
        d.mkdir()
        payload = d / manifest.PAYLOAD
        payload.write_bytes(manifest.AGE_HEADER + b"-> X25519 test\nbody")
        digest = sha256_file(payload)
        (d / manifest.PAYLOAD_SHA).write_text(f"{digest}  {manifest.PAYLOAD}\n")
        write_json(d / manifest.PUBLIC, {"format": manifest.FORMAT, "version": 1,
                                          "payload": {"size": payload.stat().st_size, "sha256": digest}})
        return d

    def test_outer_check_detects_damage(self):
        d = self._set("onebase-backup-2026-11-07_0300")
        self.assertEqual(manifest.check_outer(d), [])
        with open(d / manifest.PAYLOAD, "r+b") as f:
            f.seek(-1, os.SEEK_END)
            f.write(b"Y")
        self.assertTrue(any("повреждён" in p for p in manifest.check_outer(d)))

    def test_rotation_keeps_newest_and_ignores_other_files(self):
        names = [f"onebase-backup-2026-11-0{i}_0300" for i in range(1, 6)]
        for n in names:
            self._set(n)
        (self.root / "onebase-2026-11-01_0300.dump").write_text("old")
        (self.root / "onebase-backup-2026-11-09_0300.partial").mkdir()
        rotate(self.root, 2, self.root / names[-1], Log(Masker()))
        left = sorted(p.name for p in self.root.iterdir())
        self.assertEqual(left, ["onebase-2026-11-01_0300.dump", names[3], names[4], "onebase-backup-2026-11-09_0300.partial"])


@unittest.skipUnless(shutil.which("age") and shutil.which("age-keygen"), "нет age")
class CryptoTests(unittest.TestCase):
    def test_roundtrip_and_wrong_key(self):
        from onebase_dr.crypto import decrypt_to_dir, encrypt_dir, generate_key
        from onebase_dr.util import DrError

        root = Path(tempfile.mkdtemp())
        try:
            log = Log(Masker())
            src = root / "src"
            (src / "minio" / "b" / "data").mkdir(parents=True)
            (src / "minio" / "b" / "data" / "0000000").write_bytes(os.urandom(300_000))
            (src / manifest.INNER).write_text(json.dumps({"files": {}}))
            public = generate_key(root / "key.txt", log)
            (root / "rec.txt").write_text(public + "\n")
            encrypt_dir(src, root / "p.age", root / "rec.txt", log)
            out = root / "out"
            out.mkdir()
            decrypt_to_dir(root / "p.age", root / "key.txt", out, log)
            self.assertEqual(sha256_file(out / "minio/b/data/0000000"), sha256_file(src / "minio/b/data/0000000"))
            generate_key(root / "other.txt", log)
            with self.assertRaises(DrError) as ctx:
                decrypt_to_dir(root / "p.age", root / "other.txt", root / "out2", log)
            self.assertIn("ключ не подходит", str(ctx.exception))
        finally:
            shutil.rmtree(root)


if __name__ == "__main__":
    unittest.main()
