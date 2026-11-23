"""Isolated package/tool tests; all credentials and paths below are synthetic."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('kit', Path(__file__).with_name('build_tester_kit.py'))
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)

class KitTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.dll = self.root / 'TownOfHost-Shell.dll'
        self.dll.write_bytes(b'MZ-synthetic-PE')
        self.record = dict(Configuration='ShellDevelopment', Version='2.2.4.0',
                           SHA256=hashlib.sha256(self.dll.read_bytes()).hexdigest().upper(),
                           DevelopmentMarker=True, DebugRuntimeIncluded=True,
                           SourceChecks=[dict(Matches=True)], DistributionSymbolsSanitized=True)
        self.verification = self.root / 'verification.json'
        for path in kit.FILES.values():
            f = self.root / path
            f.parent.mkdir(parents=True, exist_ok=True)
            f.write_text('synthetic safe content', encoding='utf-8')

    def tearDown(self):
        self.temp.cleanup()

    def build(self):
        self.verification.write_text(json.dumps([self.record]), encoding='utf-8')
        return kit.build(self.dll, self.verification, self.root / 'kit.zip', self.root)

    def test_review_kit_is_allowlisted_and_remains_blocked(self):
        manifest = self.build()
        self.assertFalse(manifest['credentialsIncluded'])
        self.assertIn('credential-provisioning-permission', manifest['blockers'])
        self.assertEqual(len(manifest['files']), 7)

    def test_refuse_public_release(self):
        self.record['Configuration'] = 'Release'
        with self.assertRaises(kit.PackageError): self.build()

    def test_refuse_unverified_symbols(self):
        self.record['DistributionSymbolsSanitized'] = False
        with self.assertRaises(kit.PackageError): self.build()

    def test_refuse_stale_hash(self):
        self.dll.write_bytes(b'MZ-older-build')
        with self.assertRaises(kit.PackageError): self.build()

    def test_refuse_source_mismatch(self):
        self.record['SourceChecks'][0]['Matches'] = False
        with self.assertRaises(kit.PackageError): self.build()

    def test_refuse_overwrite(self):
        self.build()
        with self.assertRaises(kit.PackageError): self.build()

    def test_detect_identifier_ascii_and_utf16(self):
        for encoding in ('utf-8','utf-16-le','utf-16-be'):
            with self.assertRaises(kit.PackageError): kit.inspect_bytes('synthetic-client.apps.googleusercontent.com'.encode(encoding))

    def test_detect_secret_and_token(self):
        for value in ('GOCSPX-synthetic-private-value','ya29.synthetic-private-value','1//synthetic-private-value'):
            with self.assertRaises(kit.PackageError): kit.inspect_bytes(value.encode())

    def test_detect_personal_path(self):
        with self.assertRaises(kit.PackageError): kit.inspect_bytes(b'C:\\Users\\synthetic\\source')

    def test_unlisted_files_are_never_copied(self):
        (self.root / 'oauth.dpapi').write_bytes(b'synthetic-private')
        (self.root / 'private.log').write_text('synthetic-private')
        self.assertNotIn('oauth.dpapi', self.build()['files'])

    def test_concurrent_output_creation_is_not_deleted(self):
        target = self.root / 'kit.zip'
        def collision(*args, **kwargs):
            target.write_bytes(b'synthetic concurrent output')
            raise FileExistsError()
        with patch.object(kit.zipfile, 'ZipFile', side_effect=collision):
            with self.assertRaises(FileExistsError): self.build()
        self.assertEqual(target.read_bytes(), b'synthetic concurrent output')

    def test_setup_without_approval_is_fail_closed(self):
        script = Path(__file__).with_name('Setup-YouTubeTester.ps1')
        result = subprocess.run(['powershell.exe','-NoProfile','-File',str(script),
                                 '-ApprovalRecord',str(self.root/'absent.json')], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('stage=Approval', result.stdout)
        self.assertEqual(set(p.name for p in self.root.iterdir()), {'TownOfHost-Shell.dll','tools','docs'})

    def test_diagnostic_export_discards_raw_private_data(self):
        source, target = self.root/'raw.log', self.root/'safe.txt'
        source.write_text('synthetic-private YOUTUBE_OAUTH stage=TokenExchange code=AccessDenied http=400 trailing-private\n'
                          'YOUTUBE_OAUTH stage=synthetic_private code=AccessDenied http=400\n'
                          'YOUTUBE_CHAT stage=GrpcStatus code=GrpcOk http=200 grpc=0 httpMajor=2 exception=None cause=None elapsedMs=12 totalMs=20 attempt=0 retryDelaySec=0 reason=None socket=None private-trailing\n', encoding='utf-8')
        result = subprocess.run(['powershell.exe','-NoProfile','-File',str(Path(__file__).with_name('Export-YouTubeDiagnostics.ps1')),
                                 '-LogPath',str(source),'-OutputPath',str(target)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout)
        output = target.read_text(encoding='utf-8-sig')
        self.assertNotIn('private', output)
        self.assertEqual(len(output.splitlines()), 2)

if __name__ == '__main__':
    unittest.main(verbosity=2)
