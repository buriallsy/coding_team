#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -P "$(dirname "$0")/.." && pwd -P)"
source_game="$repo_root/BorrowingSun2D"
logs="$repo_root/artifacts/verification"
evidence="$source_game/Docs/Evidence/round-04"
: "${BORROWINGSUN_GODOT_BIN:?Set the full path to Godot 4.7.1 .NET}"
mkdir -p "$logs"

# Build copies on physical paths so Godot and the C# source generator agree.
qa_dir="$(mktemp -d)"
qa_dir="$(cd -P "$qa_dir" && pwd -P)"
trap 'rm -rf -- "$qa_dir"' EXIT
game_project="$qa_dir/production"
qa_project="$qa_dir/project"
mkdir -p "$game_project"
cp "$source_game/BorrowingSun2D.csproj" "$source_game/project.godot" "$game_project/"
cp -R "$source_game/Scenes" "$source_game/Scripts" "$game_project/"
cp -R "$game_project" "$qa_project"
mkdir -p "$qa_project/VerificationHarness"
cp "$evidence/round04-acceptance-probe.cs.txt" "$qa_project/VerificationHarness/Round04AcceptanceProbe.cs"
cp "$evidence/round04-acceptance-launcher.gd.txt" "$qa_dir/round04-acceptance-launcher.gd"

build_args=(--nologo --disable-build-servers)
if [[ -n "${BORROWINGSUN_NUGET_SOURCE:-}" ]]; then
    # The SDK resolver reads NuGet.Config before the normal project restore starts.
    feed="$(cd -P "$BORROWINGSUN_NUGET_SOURCE" && pwd -P)"
    feed="$(printf '%s' "$feed" | sed 's/\&/\&amp;/g; s/</\&lt;/g; s/>/\&gt;/g; s/"/\&quot;/g')"
    printf '<configuration><packageSources><clear/><add key="GodotSDK-local" value="%s"/></packageSources></configuration>\n' "$feed" > "$qa_dir/NuGet.Config"
    build_args+=(-p:NuGetAudit=false)
fi

dotnet build "$repo_root/Tests/DomainChecks/DomainChecks.csproj" "${build_args[@]}" 2>&1 | tee "$logs/domain-build.log"
dotnet run --no-build --project "$repo_root/Tests/DomainChecks/DomainChecks.csproj" 2>&1 | tee "$logs/domain-checks.log"
grep -q '^DOMAIN_CHECKS PASSED:' "$logs/domain-checks.log"

dotnet build "$game_project/BorrowingSun2D.csproj" "${build_args[@]}" 2>&1 | tee "$logs/game-build.log"
"$BORROWINGSUN_GODOT_BIN" --headless --path "$game_project" --fixed-fps 60 --quit-after 14000 \
    --log-file "$logs/three-shore-engine.log" -- --verify --skip-intro 2>&1 | tee "$logs/three-shore-route.log"
grep -q '^TOWER_ROUTE PASSED$' "$logs/three-shore-route.log"

dotnet build "$qa_project/BorrowingSun2D.csproj" "${build_args[@]}" 2>&1 | tee "$logs/round04-probe-build.log"
"$BORROWINGSUN_GODOT_BIN" --headless --path "$qa_project" --fixed-fps 60 --quit-after 14000 \
    --log-file "$logs/round04-acceptance-engine.log" --script "$qa_dir/round04-acceptance-launcher.gd" \
    2>&1 | tee "$logs/round04-acceptance.log"
grep -q '^ROUND_04_ACCEPTANCE PASSED' "$logs/round04-acceptance.log"
echo 'VERIFICATION PASSED: domain, three-shore route, and Round 04 first-run and replay.'
