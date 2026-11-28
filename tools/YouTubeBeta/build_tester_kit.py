"""Build a credential-free review kit. No installation, provisioning or network.

--verification is the local Mono.Cecil/PDB verification record for this DLL.
The output remains REVIEW ONLY: it cannot grant permission to distribute credentials.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[2]
FILES = {
    'Setup-YouTubeTester.ps1': 'tools/YouTubeBeta/Setup-YouTubeTester.ps1',
    'START-SETUP.cmd': 'tools/YouTubeBeta/START-SETUP.cmd',
    'Export-YouTubeDiagnostics.ps1': 'tools/YouTubeBeta/Export-YouTubeDiagnostics.ps1',
    'Import-YouTubeDesktopClient.ps1': 'tools/Import-YouTubeDesktopClient.ps1',
    'README-ja.md': 'docs/development/youtube-external-tester-guide.md',
    'KNOWN-ISSUES-ja.md': 'docs/development/youtube-beta-known-issues.md',
}

class PackageError(Exception):
    pass

def inspect_bytes(data):
    # Inspect printable ASCII and UTF-16 as well as ordinary text. Embedded PDB
    # paths are checked separately using the symbol reader before this builder.
    samples = [data, data[::2], data[1::2]]
    for sample in samples:
        if re.search(rb'[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*\.apps\.googleusercontent\.com', sample):
            raise PackageError('ClientIdentifierDetected')
        if re.search(rb'(GOCSPX-[A-Za-z0-9_-]{8,}|ya29\.[A-Za-z0-9._-]{8,}|1//[A-Za-z0-9_-]{12,})', sample):
            raise PackageError('CredentialLikeValueDetected')
        if re.search(rb'(?:[A-Za-z]:[\\/](?:Users|Documents and Settings)[\\/]|/Users/|/home/)', sample, re.I):
            raise PackageError('PersonalPathDetected')

def build(dll, verification, output, root=ROOT):
    dll, verification, output = Path(dll), Path(verification), Path(output)
    if output.exists() or not output.parent.is_dir():
        raise PackageError('OutputMustBeNewInExistingDirectory')
    records = json.loads(verification.read_text(encoding='utf-8-sig'))
    record = next((r for r in records if r.get('Configuration') == 'ShellDevelopment'), None)
    data = dll.read_bytes()
    digest = hashlib.sha256(data).hexdigest().upper()
    if not record or record.get('Version') != '2.2.6.0' or record.get('SHA256') != digest or not record.get('DevelopmentMarker') or not record.get('DebugRuntimeIncluded'):
        raise PackageError('DevelopmentBuildVerificationFailed')
    if not record.get('SourceChecks') or any(not s.get('Matches') for s in record['SourceChecks']):
        raise PackageError('SourceVerificationFailed')
    if not record.get('DistributionSymbolsSanitized'):
        raise PackageError('SymbolPathsNotVerified')
    if not data.startswith(b'MZ'):
        raise PackageError('NotPortableExecutable')
    payload = {'TownOfHost-Shell.dll': data}
    for name, path in FILES.items():
        payload[name] = (root / path).read_bytes()
    for data in payload.values():
        inspect_bytes(data)
    manifest = dict(version='2.2.6', configuration='ShellDevelopment',
                    status='REVIEW_ONLY_NOT_READY_FOR_DISTRIBUTION',
                    blockers=['credential-provisioning-permission', 'individual-Google-test-user-approval',
                              'external-PC-validation', 'known-lobby-exit-crash'],
                    credentialsIncluded=False, automaticallyConnects=False,
                    files={name: hashlib.sha256(data).hexdigest().upper() for name, data in payload.items()})
    payload['manifest.json'] = json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8')
    created = False
    try:
        with zipfile.ZipFile(output, 'x', compression=zipfile.ZIP_DEFLATED) as archive:
            created = True
            for name, data in payload.items():
                archive.writestr(name, data)
        with zipfile.ZipFile(output) as archive:
            if set(archive.namelist()) != set(payload) or archive.testzip() is not None:
                raise PackageError('ArchiveVerificationFailed')
            for name, data in payload.items():
                if archive.read(name) != data:
                    raise PackageError('ArchiveVerificationFailed')
    except Exception:
        if created and output.exists():
            output.unlink()
        raise
    return manifest

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--dll', required=True)
    parser.add_argument('--verification', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        manifest = build(args.dll, args.verification, args.output)
        print('TESTER_KIT status=REVIEW_ONLY files=' + str(len(manifest['files']) + 1))
    except (PackageError, OSError, ValueError, KeyError):
        print('TESTER_KIT result=Stopped (verification or privacy check failed)')
        raise SystemExit(1)
