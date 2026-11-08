#!/usr/bin/env python3
"""Dispatch one fixed-action request to the current Unity Editor project."""
import argparse
import json
from pathlib import Path
import time

parser = argparse.ArgumentParser()
parser.add_argument("action", choices=['skywell-ladder-height-setup', 'skywell-ladder-height-audit', 'skywell-ladder-height-checks', 'skywell-climb-animation-setup', 'skywell-direct-boarding-setup', 'skywell-direct-boarding-audit', 'skywell-remote-return', 'skywell-traversal-setup', 'skywell-traversal-audit', 'skywell-cave-setup', 'skywell-cave-audit', 'skywell-surface-boundary', 'skywell-surface-setup', 'skywell-surface-audit', 'skywell-surface-checks', 'skywell-deer-path-seam', 'skywell-deer-feeding-refine', 'skywell-deer-route', 'skywell-deer-shoulder-route', 'skywell-deer-grove-setup', 'skywell-deer-grove-audit', 'skywell-deer-grove-checks', 'skywell-camera-setup', 'skywell-camera-checks', 'skywell-shoulder-route', 'skywell-targeting-colors', 'skywell-targeting-setup', 'skywell-targeting-audit', 'skywell-targeting-checks', 'skywell-npc-audit', 'skywell-npc-appearance', 'skywell-story-refine', 'skywell-story-checks', 'skywell-story-setup', 'skywell-boat-dock-seam', 'skywell-boat-setup', 'skywell-background-setup', 'skywell-collector-control', 'skywell-collector-aim', 'skywell-collector-shape', 'skywell-collector-views', 'skywell-courtyard-audit', 'skywell-courtyard-apply', 'skywell-observation-setup', 'skywell-observation-checks', 'skywell-weather-views', 'skywell-weather-material-fix', 'project-refresh', 'skywell-redesign-apply', 'skywell-underwater-checks', 'project-references', 'project-tests', 'cargo-capture', 'cargo-checks', 'cargo-drop-checks', 'cargo-export', 'cargo-push-checks', 'cargo-push-visual', 'cargo-reset', 'enter-play', 'exit-play', 'l1-regression', 'skywell-capture', 'skywell-checks', 'skywell-cleanup-checks', 'skywell-cleanup-views', 'skywell-compact-checks', 'skywell-export', 'skywell-lift-views', 'skywell-portfolio-build', 'skywell-portfolio-checks', 'skywell-portfolio-views', 'skywell-preview', 'skywell-rain-checks', 'skywell-refuge-checks', 'skywell-status', 'skywell-sun-checks', 'skywell-sun-views', 'skywell-tuning-checks', 'skywell-tuning-open', 'skywell-tuning-save'])
parser.add_argument("--timeout", type=float, default=25)
args = parser.parse_args()
folder = Path(__file__).resolve().parents[1] / "validation" / "editor-bridge"
folder.mkdir(parents=True, exist_ok=True)
request = folder / "editor-request.json"
if request.exists():
    raise SystemExit("A previous request is still pending. Inspect it before dispatching another.")
request_id = args.action + "-" + str(time.time_ns())
temp = folder / "editor-request.writing"
temp.write_text(json.dumps({"id": request_id, "action": args.action}), encoding="utf-8")
temp.replace(request)
deadline = time.monotonic() + args.timeout
while time.monotonic() < deadline:
    response = folder / "editor-response.json"
    try:
        data = json.loads(response.read_text(encoding="utf-8"))
        if data.get("id") == request_id:
            print(json.dumps(data, ensure_ascii=False))
            raise SystemExit(0 if data.get("status") == "dispatched" else 1)
    except (FileNotFoundError, json.JSONDecodeError):
        pass
    time.sleep(.2)
raise SystemExit("Editor has not acknowledged this request yet; refresh/import scripts in the open project.")
