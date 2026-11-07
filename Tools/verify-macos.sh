#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
: "${BORROWINGSUN_GODOT_BIN:?Set the full path to Godot 4.7.1 .NET}"
mkdir -p artifacts/verification
dotnet run --project Tests/DomainChecks | tee artifacts/verification/domain-checks.log
dotnet build BorrowingSun2D/BorrowingSun2D.csproj | tee artifacts/verification/game-build.log
"$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD/BorrowingSun2D" --fixed-fps 60 -- --verify 2>&1 | tee artifacts/verification/mvp-route.log
cp BorrowingSun2D/Docs/Evidence/independent-route.gd.txt artifacts/verification/independent-route.gd
"$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD/BorrowingSun2D" --fixed-fps 60 --script "$PWD/artifacts/verification/independent-route.gd" 2>&1 | tee artifacts/verification/independent-route.log
echo 'Executed checks passed. This is not the complete-level acceptance test.'
