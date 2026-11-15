#!/usr/bin/env python3
"""Validate downloaded release assets against an operator-pinned manifest hash before publication.

Usage: verify-release-assets.py <assets-dir> <tag> <manifest-sha256>

The release workflow freezes the verified packages and their package-hashes.json as GitHub
Release assets. Before anything is pushed to nuget.org this check proves that:
  * package-hashes.json is byte-identical to the manifest the operator pinned (its SHA-256);
  * the manifest names the tag's version and the checked-out source commit;
  * the asset directory holds exactly the listed .nupkg/.snupkg files and nothing else;
  * every archive still has the SHA-256 the manifest recorded.
tools/Boutquin.PackageVerification then re-checks package contents (version, commit, exact
sibling dependencies, README, assembly metadata, PDB) against the same manifest.

Ported from Boutquin.MarketData's publication gate.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
TAG = re.compile(r'v(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-rc\.[0-9]+(?:\.[0-9]+)*)?')


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate(assets, tag, digest, commit):
    if not TAG.fullmatch(tag):
        raise ValueError('Only exact stable or numeric RC tags can be published')
    if not re.fullmatch(r'[0-9a-f]{64}', digest):
        raise ValueError('Expected the independently frozen manifest SHA-256')
    manifest = assets / 'package-hashes.json'
    if sha256(manifest) != digest:
        raise ValueError('Release manifest differs from the pinned manifest')
    identity = json.loads(manifest.read_text())
    if identity['version'] != tag[1:] or identity['commit'] != commit:
        raise ValueError('Release tag, manifest and checked-out source differ')
    listed = {}
    for artifact in identity['artifacts']:
        name = artifact['name']
        if Path(name).name != name or not name.endswith(('.nupkg', '.snupkg')):
            raise ValueError('Unsafe release asset path: ' + name)
        listed[name] = artifact['sha256']
    present = {path.name for path in assets.iterdir() if path.name != 'package-hashes.json'}
    if present != listed.keys():
        raise ValueError(f'Release assets differ from manifest (missing {sorted(listed.keys() - present)}, '
                         f'unlisted {sorted(present - listed.keys())})')
    for name, expected in listed.items():
        if sha256(assets / name) != expected:
            raise ValueError('Changed release archive: ' + name)
    return len(listed)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('assets', type=Path)
    parser.add_argument('tag')
    parser.add_argument('manifest_sha256')
    args = parser.parse_args(argv)
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    try:
        count = validate(args.assets, args.tag, args.manifest_sha256, commit)
    except (ValueError, KeyError, OSError) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        return 1
    print(f'PASS: {count} frozen release archives match tag {args.tag}, commit {commit} and the pinned manifest')
    return 0


if __name__ == '__main__':
    sys.exit(main())
