#!/usr/bin/env python3
"""Check shipped API fingerprints; reject pending declarations at release boundaries.

Usage: check-api-release-baseline.py [--release] [--write] [--root PATH]

api-release-baseline.json pins the SHA-256 of every src/*/PublicAPI.Shipped.txt for the last
released version. A shipped surface may only change together with that file, so a release cannot
silently alter or drop API that consumers already depend on. With --release, any entry left in a
PublicAPI.Unshipped.txt fails: a stable release must promote its pending API first.
--write regenerates the fingerprints (use only when promoting API at a release boundary).

Ported from Boutquin.MarketData's release gate.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
BASELINE = 'api-release-baseline.json'


def shipped_files(root):
    files = {}
    for shipped in sorted((root / 'src').glob('*/PublicAPI.Shipped.txt')):
        projects = sorted(shipped.parent.glob('*.csproj'))
        if len(projects) != 1:
            raise ValueError(f'{shipped.parent}: expected exactly one project file')
        files[projects[0].stem] = shipped
    return files


def check(root, release=False):
    manifest = json.loads((root / BASELINE).read_text())
    expected = manifest['packages']
    files = shipped_files(root)
    if files.keys() != expected.keys():
        missing = sorted(expected.keys() - files.keys())
        extra = sorted(files.keys() - expected.keys())
        raise ValueError(f'API baseline package set differs (missing {missing}, unlisted {extra})')
    for name, shipped in files.items():
        raw = shipped.read_bytes()
        if not raw.startswith(b'#nullable enable\n'):
            raise ValueError(f'{shipped}: expected LF nullable header')
        if hashlib.sha256(raw).hexdigest() != expected[name]:
            raise ValueError(f'{shipped}: shipped API differs from the {manifest["version"]} fingerprint')
        pending = [line for line in shipped.with_name('PublicAPI.Unshipped.txt').read_text().splitlines()
                   if line.strip() and not line.startswith('#')]
        if release and pending:
            raise ValueError(f'{name}: pending API entries at release boundary ({len(pending)})')
    return len(files)


def write(root, version):
    packages = {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in shipped_files(root).items()}
    payload = {'version': version, 'packages': packages}
    (root / BASELINE).write_text(json.dumps(payload, indent=2) + '\n')
    return len(packages)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release', action='store_true')
    parser.add_argument('--write', metavar='VERSION')
    parser.add_argument('--root', type=Path, default=ROOT)
    args = parser.parse_args(argv)
    try:
        if args.write:
            print(f'WROTE: {write(args.root, args.write)} API fingerprints for {args.write}')
            return 0
        count = check(args.root, args.release)
    except (ValueError, KeyError, OSError) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        return 1
    print(f'PASS: {count} released API baselines' + ('; no pending entries' if args.release else ''))
    return 0


if __name__ == '__main__':
    sys.exit(main())
