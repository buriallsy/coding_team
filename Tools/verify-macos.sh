#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -P "$(dirname "$0")/.." && pwd -P)"
game_project="$repo_root/BorrowingSun2D"
logs="$repo_root/artifacts/verification"
evidence="$game_project/Docs/Evidence/round-03"
: "${BORROWINGSUN_GODOT_BIN:?Set the full path to Godot 4.7.1 .NET}"
mkdir -p "$logs"

build_args=(--nologo)
if [[ -n "${BORROWINGSUN_NUGET_SOURCE:-}" ]]; then
    build_args+=(--source "$BORROWINGSUN_NUGET_SOURCE" -p:NuGetAudit=false)
fi

dotnet build "$repo_root/Tests/DomainChecks/DomainChecks.csproj" "${build_args[@]}" 2>&1 | tee "$logs/domain-build.log"
dotnet run --no-build --project "$repo_root/Tests/DomainChecks/DomainChecks.csproj" 2>&1 | tee "$logs/domain-checks.log"
grep -q '^DOMAIN_CHECKS PASSED:' "$logs/domain-checks.log"

dotnet build "$game_project/BorrowingSun2D.csproj" "${build_args[@]}" 2>&1 | tee "$logs/game-build.log"
"$BORROWINGSUN_GODOT_BIN" --headless --path "$game_project" --fixed-fps 60 --quit-after 14000 \
    --log-file "$logs/three-shore-engine.log" -- --verify 2>&1 | tee "$logs/three-shore-route.log"
grep -q '^TOWER_ROUTE PASSED$' "$logs/three-shore-route.log"

# Godot's C# generator needs the same physical project path as the compiler's source paths.
qa_dir="$(mktemp -d)"
qa_dir="$(cd -P "$qa_dir" && pwd -P)"
trap 'rm -rf -- "$qa_dir"' EXIT
qa_project="$qa_dir/project"
mkdir -p "$qa_project/VerificationHarness"
cp "$game_project/BorrowingSun2D.csproj" "$game_project/project.godot" "$qa_project/"
cp -R "$game_project/Scenes" "$game_project/Scripts" "$qa_project/"
cp "$evidence/round03-cargo-probe.cs.txt" "$qa_project/VerificationHarness/Round03CargoProbe.cs"
cp "$evidence/round03-cargo-launcher.gd.txt" "$qa_dir/round03-cargo-launcher.gd"

dotnet build "$qa_project/BorrowingSun2D.csproj" "${build_args[@]}" 2>&1 | tee "$logs/round03-probe-build.log"
"$BORROWINGSUN_GODOT_BIN" --headless --path "$qa_project" --fixed-fps 60 --quit-after 14000 \
    --log-file "$logs/round03-cargo-engine.log" --script "$qa_dir/round03-cargo-launcher.gd" \
    2>&1 | tee "$logs/round03-cargo.log"
grep -q '^ROUND_03_CARGO_QA PASSED' "$logs/round03-cargo.log"
echo 'VERIFICATION PASSED: domain, three-shore route, and Round 03 cargo route.'
