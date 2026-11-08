"""Auditable class-based CCR. Git objects prove identity; humans review behaviour."""
import argparse
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT = ROOT / 'BorrowingSun2D/Docs/2D-to-3D/ccr-analysis.json'

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True, stderr=subprocess.PIPE).strip()

def validate_inventory(items):
    if not items or len({i['id'] for i in items}) != len(items):
        raise ValueError('Empty or duplicate core class IDs')
    for item in items:
        p = Path(item['path'])
        if p.is_absolute() or '..' in p.parts or not item.get('reason'):
            raise ValueError('Invalid path or missing inclusion reason')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--analysis', type=Path, default=DEFAULT)
    parser.add_argument('--freeze', metavar='TAG')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    data = json.loads(args.analysis.read_text(encoding='utf-8-sig'))
    if args.freeze:
        if not args.output or args.output.exists():
            raise ValueError('--output must name a new file')
        tag = 'refs/tags/' + args.freeze
        commit = git('rev-parse', '--verify', tag + '^{commit}')
        items = data['candidates']
        validate_inventory(items)
        frozen = []
        for item in items:
            source = git('show', commit + ':' + item['path'])
            if 'class ' + item['id'] not in source:
                raise ValueError('Candidate class absent at tag: ' + item['id'])
            frozen.append(dict(item, blob=git('rev-parse', commit + ':' + item['path'])))
        data['baseline'] = {'tag': args.freeze, 'commit': commit, 'classes': frozen}
        data['final_ref'] = None
        data['reviews'] = []
        args.output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print('FROZEN INVENTORY:', commit, 'classes=', len(frozen))
        return
    if data.get('baseline') is None:
        print('CCR PENDING: complete 2D baseline is not frozen; no percentage is justified.')
        return
    baseline = data['baseline']
    classes = baseline['classes']
    validate_inventory(classes)
    if git('rev-parse', '--verify', 'refs/tags/' + baseline['tag'] + '^{commit}') != baseline['commit']:
        raise ValueError('Baseline tag moved')
    for item in classes:
        if git('rev-parse', baseline['commit'] + ':' + item['path']) != item['blob']:
            raise ValueError('Baseline blob mismatch')
    if not data.get('final_ref'):
        print('CCR PENDING: baseline exists but 3D final reference is missing.')
        return
    final = git('rev-parse', '--verify', data['final_ref'] + '^{commit}')
    reviews = data['reviews']
    if len(reviews) != len(classes) or {r['id'] for r in reviews} != {i['id'] for i in classes}:
        raise ValueError('Every original core class needs exactly one review')
    for review in reviews:
        if review['status'] not in ('shared', 'modified', 'removed') or not review.get('reason') or not review.get('evidence'):
            raise ValueError('Missing reviewed status/reason/evidence')
        paths = review.get('final_paths', [])
        if review['status'] != 'removed' and not paths:
            raise ValueError('Shared/modified classes require corresponding 3D paths')
        for path in paths:
            git('cat-file', '-e', final + ':' + path)
    additions = data.get('new_core_classes', [])
    if len({i['id'] for i in additions}) != len(additions):
        raise ValueError('Duplicate new class ID')
    for item in additions:
        if not item.get('reason') or not item.get('evidence'):
            raise ValueError('New core class needs reason and evidence')
        git('cat-file', '-e', final + ':' + item['path'])
    changed = sum(r['status'] != 'shared' for r in reviews)
    print(f'CCR = {changed}/{len(classes)} = {100 * changed / len(classes):.2f}%')
    print('3D-only additions (outside denominator):', len(additions))
    print('Behaviour classifications are reviewer supplied, not inferred from text differences.')

if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as exc:
        raise SystemExit('CCR ERROR: ' + str(exc))
