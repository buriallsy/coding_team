import contextlib
import importlib.util
import io
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('ccr', PROJECT / 'Tools/ccr.py')
ccr = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ccr)

class CcrEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='ccr-test-')
        self.repo = Path(self.temp.name)
        self.original_root = ccr.ROOT
        ccr.ROOT = self.repo
        self.git('init', '-q')
        self.git('config', 'user.name', 'Fixture')
        self.git('config', 'user.email', 'fixture@example.invalid')
        for name in ('Alpha', 'Beta'):
            (self.repo / (name + '.cs')).write_text('public class ' + name + ' {}', encoding='utf-8')
        self.git('add', '.')
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'fixture baseline')
        self.git('tag', '2d-baseline')
        self.data = {'baseline': None, 'final_ref': None, 'reviews': [], 'new_core_classes': [],
            'candidates': [{'id': n, 'path': n + '.cs', 'reason': 'fixture rule'} for n in ('Alpha', 'Beta')]}
        self.analysis = self.repo / 'analysis.json'

    def tearDown(self):
        ccr.ROOT = self.original_root
        self.temp.cleanup()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.repo, stderr=subprocess.PIPE, text=True).strip()

    def run_tool(self, *extra):
        self.analysis.write_text(json.dumps(self.data), encoding='utf-8')
        output = io.StringIO()
        with patch.object(sys, 'argv', ['ccr', '--analysis', str(self.analysis), *extra]), contextlib.redirect_stdout(output):
            ccr.main()
        return output.getvalue()

    def ready(self):
        frozen = self.repo / 'frozen.json'
        self.run_tool('--freeze', '2d-baseline', '--output', str(frozen))
        self.data = json.loads(frozen.read_text(encoding='utf-8'))
        (self.repo / 'Alpha.cs').write_text('public class Alpha { public int Value = 2; }', encoding='utf-8')
        self.git('add', 'Alpha.cs')
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'fixture final')
        self.git('tag', '3d-final')
        self.data['final_ref'] = 'refs/tags/3d-final'
        self.data['reviews'] = [
            {'id': n, 'status': status, 'reason': 'fixture review', 'evidence': 'fixture assertion', 'final_paths': [n+'.cs']}
            for n,status in [('Alpha','modified'),('Beta','shared')]]

    def test_pending_never_reports_zero(self):
        result = self.run_tool()
        self.assertIn('PENDING', result)
        self.assertNotIn('%', result)

    def test_frozen_git_evidence_and_ratio(self):
        self.ready()
        self.assertIn('1/2 = 50.00%', self.run_tool())

    def test_duplicate_reviews_are_rejected(self):
        self.ready()
        self.data['reviews'][1] = self.data['reviews'][0]
        with self.assertRaises(ValueError): self.run_tool()

    def test_missing_final_source_rejected(self):
        self.ready()
        self.data['reviews'][0]['final_paths'] = ['missing.cs']
        with self.assertRaises(subprocess.CalledProcessError): self.run_tool()

    def test_moved_baseline_rejected(self):
        self.ready()
        self.git('tag', '-f', '2d-baseline', '3d-final')
        with self.assertRaises(ValueError): self.run_tool()

    def test_missing_reason_rejected(self):
        self.ready()
        self.data['reviews'][0]['reason'] = ''
        with self.assertRaises(ValueError): self.run_tool()

    def test_empty_inventory_rejected(self):
        self.data['candidates'] = []
        with self.assertRaises(ValueError): self.run_tool('--freeze','2d-baseline','--output',str(self.repo/'empty.json'))

if __name__ == '__main__': unittest.main()
