"""Must-pass and must-fail cases for the release gates in scripts/.

Run: python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), SCRIPTS / f'{name}.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


docs = load('check-release-docs')
api = load('check-api-release-baseline')
assets = load('verify-release-assets')

GOOD_CHANGELOG = '# Changelog\n\n## [Unreleased]\n\n## [1.0.0] — 2026-11-14\n\n### Added\n\n- Thing.\n'
GOOD_README = 'Install:\n\n    dotnet add package Boutquin.Mediator --version 1.0.0\n'
GOOD_SECURITY = '| Version | Status |\n|---|---|\n| 1.0.x   | Supported |\n'


class ReleaseDocsTests(unittest.TestCase):
    def make(self, root, changelog=GOOD_CHANGELOG, readme=GOOD_README, security=GOOD_SECURITY):
        (root / 'CHANGELOG.md').write_text(changelog, encoding='utf-8')
        (root / 'README.md').write_text(readme, encoding='utf-8')
        (root / 'SECURITY.md').write_text(security, encoding='utf-8')

    def errors(self, **files):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.make(root, **files)
            return docs.check(root, '1.0.0')

    def test_released_docs_pass(self):
        self.assertEqual([], self.errors())

    def test_each_unreleased_state_fails(self):
        cases = {
            'prepared heading': dict(changelog=GOOD_CHANGELOG.replace('— 2026-11-14', '- prepared 2026-11-14 (not yet published)')),
            'hyphen not em dash': dict(changelog=GOOD_CHANGELOG.replace('—', '-')),
            'missing section': dict(changelog='## [Unreleased]\n'),
            'entries left in Unreleased': dict(changelog=GOOD_CHANGELOG.replace('## [Unreleased]\n', '## [Unreleased]\n\n- Pending.\n')),
            'no Unreleased section': dict(changelog=GOOD_CHANGELOG.replace('## [Unreleased]\n\n', '')),
            'README without exact version': dict(readme='dotnet add package Boutquin.Mediator\n'),
            'README links another tag': dict(readme=GOOD_README + '[x](https://github.com/o/r/blob/v0.9.0/README.md)\n'),
            'SECURITY not supported': dict(security=GOOD_SECURITY.replace('| Supported |', '| Supported upon publication |')),
        }
        for label, files in cases.items():
            with self.subTest(label):
                self.assertTrue(self.errors(**files))


class ApiBaselineTests(unittest.TestCase):
    def make(self, root, unshipped='#nullable enable\n'):
        project = root / 'src' / 'Lib'
        project.mkdir(parents=True)
        (project / 'Boutquin.Lib.csproj').write_text('<Project />')
        (project / 'PublicAPI.Shipped.txt').write_text('#nullable enable\nBoutquin.Lib.Thing\n')
        (project / 'PublicAPI.Unshipped.txt').write_text(unshipped)
        api.write(root, '1.0.0')
        return project

    def test_matching_baseline_passes_release(self):
        with tempfile.TemporaryDirectory() as directory:
            self.make(Path(directory))
            self.assertEqual(1, api.check(Path(directory), release=True))

    def test_changed_shipped_surface_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            project = self.make(Path(directory))
            (project / 'PublicAPI.Shipped.txt').write_text('#nullable enable\n')
            with self.assertRaises(ValueError):
                api.check(Path(directory))

    def test_pending_entries_fail_only_at_release(self):
        with tempfile.TemporaryDirectory() as directory:
            self.make(Path(directory), unshipped='#nullable enable\nBoutquin.Lib.New\n')
            self.assertEqual(1, api.check(Path(directory)))
            with self.assertRaises(ValueError):
                api.check(Path(directory), release=True)

    def test_unlisted_package_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.make(root)
            other = root / 'src' / 'Other'
            other.mkdir()
            (other / 'Boutquin.Other.csproj').write_text('<Project />')
            (other / 'PublicAPI.Shipped.txt').write_text('#nullable enable\n')
            with self.assertRaises(ValueError):
                api.check(root)


class ReleaseAssetTests(unittest.TestCase):
    COMMIT = 'a' * 40

    def make(self, root):
        names = ['Boutquin.Lib.1.0.0.nupkg', 'Boutquin.Lib.1.0.0.snupkg']
        for name in names:
            (root / name).write_bytes(name.encode())
        manifest = {'version': '1.0.0', 'commit': self.COMMIT,
                    'artifacts': [{'name': n, 'sha256': hashlib.sha256(n.encode()).hexdigest()} for n in names]}
        (root / 'package-hashes.json').write_text(json.dumps(manifest))
        return hashlib.sha256((root / 'package-hashes.json').read_bytes()).hexdigest()

    def test_untouched_assets_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            digest = self.make(root)
            self.assertEqual(2, assets.validate(root, 'v1.0.0', digest, self.COMMIT))

    def test_each_tampering_fails(self):
        def changed_archive(root):
            (root / 'Boutquin.Lib.1.0.0.nupkg').write_bytes(b'other')

        def extra_archive(root):
            (root / 'Boutquin.Evil.1.0.0.nupkg').write_bytes(b'evil')

        def missing_archive(root):
            (root / 'Boutquin.Lib.1.0.0.snupkg').unlink()

        cases = {
            'changed archive': (changed_archive, 'v1.0.0', None, self.COMMIT),
            'extra archive': (extra_archive, 'v1.0.0', None, self.COMMIT),
            'missing archive': (missing_archive, 'v1.0.0', None, self.COMMIT),
            'wrong pinned digest': (None, 'v1.0.0', '0' * 64, self.COMMIT),
            'wrong tag': (None, 'v1.0.1', None, self.COMMIT),
            'wrong commit': (None, 'v1.0.0', None, 'b' * 40),
            'non-release tag': (None, 'main', None, self.COMMIT),
        }
        for label, (mutate, tag, digest, commit) in cases.items():
            with self.subTest(label), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                pinned = self.make(root)
                if mutate:
                    mutate(root)
                with self.assertRaises((ValueError, OSError)):
                    assets.validate(root, tag, digest or pinned, commit)


if __name__ == '__main__':
    unittest.main()
