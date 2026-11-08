# Borrowing Sun (借晴)

**AP6412 · Route B · 2D baseline and 3D graybox 05D candidate**

Borrowing Sun is a weather puzzle set around a mountain tower. Rain lifts the tower and wind connects its bridges. Aim the Sun Court mirror, borrow sunlight, and turn the tower east to light the receiver. Sustained light earns one fruit crate. The same rainwater fills the cargo counterweight so you can ride across the valley and deliver the crate.

## 3D graybox

Round 05D brings the frozen 2D introduction and delivery screen to the complete 3D route. Start with `Enter`, `Space`, or the Start button. Borrow rain to raise the tower, use wind to connect the north and east bridges, and reflect sunlight to the east receiver. Board the loaded car and press `G` to cross the valley. At Flower Court, press `G` at the marker to confirm `1/1`, then press `R` to replay from the low bridge. [3D project](BorrowingSun3D/project.godot) · [Controls](BorrowingSun3D/README.md) · [Acceptance and Windows steps](BorrowingSun3D/Docs/ROUND_05D.md) · [Change map and CCR](BorrowingSun3D/Docs/CHANGE_MAP.md)

The complete 2D route and its verification remain documented below. The Round 03 integration adds automated domain checks, a Windows and macOS CI matrix, and CCR tooling linked to the current game rules. [Architecture and verification index](START_HERE.md)

## Run and play

1. Clone or download this repository. Install **Godot 4.7.1 .NET** and the **.NET 8 SDK**.
2. In the Godot Project Manager, import `BorrowingSun2D/project.godot`. Press **F5**, then press **Enter** or click Start on the opening card. [Project file](BorrowingSun2D/project.godot)
3. Press `D` or `→` to cross the low bridge and approach the device on the tower. Press `1` to select rain, then `E` to apply it. Wait for the lift to reach the upper level and lock.
4. Press `2` then `E` to turn north and walk left to the Sun Court mirror. Press `2` then `E` beside the mirror to aim it, then `3` and `E` to open local sunlight. The tower mirror is now hit while the east receiver remains dark.
5. Walk back to the tower device and press `2` then `E` to turn east. Cross the east bridge and walk onto the cargo car. Wait for the crate and the `4/4` counterweight, then press `G` to depart while the receiver is lit.
6. When the car stops, walk right to the delivery flag and press `G` to record `1/1`. The delivery card confirms `1/1`. Press `R` to replay from the west low bridge. [Full controls and scene entry](BorrowingSun2D/README.md)

## Verification

For the 2D baseline, follow the Windows and macOS instructions and report the commit you tested. [Round 04 acceptance and replay](BorrowingSun2D/Docs/2D-to-3D/ROUND_04.md)

The macOS arm64 acceptance run passed 154 checks, including opening the game, both launch refusals, delivery, and a full replay after `R`. The earlier shore and optics checks also passed. The windowed run passed 46 checks and captured the opening, ready-to-launch and delivery screens. Windows acceptance is pending.

![Round 04 delivery screen. The player has signed for one crate and the replay button is focused.](BorrowingSun2D/Docs/Evidence/round-04/finished.png)

From the repository root, run the standalone rule checks and the CCR tool tests.

```sh
dotnet run --project Tests/DomainChecks --configuration Release
python3 -m unittest discover -s Tests -p test_ccr.py
python3 Tools/ccr.py
```

The CCR command reports the current baseline status. The CI matrix runs the domain and CCR checks. Godot route verification is a separate step. [Integration run logs](BorrowingSun2D/Docs/Evidence/2026-11-08-round03-integration/RESULTS.md) [Run the Godot route and review the acceptance gates](BorrowingSun2D/Docs/2D-to-3D/ACCEPTANCE.md)

## Design and development

`TowerMechanism` owns the lift, docking and shared rainwater. `SunCourtOptics2D` uses the pure C# `Optics2D` solver to determine live receiver light. `CargoJourney` uses that light to earn the crate and fill the counterweight. Once the car departs, `CargoTrack2D` moves its physical deck according to journey progress until arrival.

| Project documents | What to look for |
| --- | --- |
| [Game concept / 项目构思](BorrowingSun2D/Docs/2D-to-3D/DESIGN.md) | The full gameplay loop and what carries from 2D to 3D |
| [Scope and architecture / 范围与架构](BorrowingSun2D/Docs/2D-to-3D/FRAMEWORK.md) | Current rules, class maps and the CCR workflow |
| [Production plan / 制作计划](BorrowingSun2D/Docs/2D-to-3D/PRODUCTION_PLAN.md) | Milestones, acceptance checks and evidence for the report |
| [Project conventions / 项目规范](BorrowingSun2D/Docs/2D-to-3D/PROJECT_CONVENTIONS.md) | Code responsibilities, scene editing, testing and Git workflow |
| [Current status / 当前版本](BorrowingSun2D/Docs/2D-to-3D/CURRENT_STATUS.md) | What is playable now, the reset regression, and the next milestone |
| [Milestone 01 / 三岸接驳](BorrowingSun2D/Docs/2D-to-3D/ROUND_01.md) | Docking design decisions, the three-shore route, and reset checks |
| [Milestone 02 / 借光](BorrowingSun2D/Docs/2D-to-3D/ROUND_02.md) | Two reflections, the north and east outcomes, and replayable tests |
| [Milestone 04 / 首玩与验收](BorrowingSun2D/Docs/2D-to-3D/ROUND_04.md) | Opening, delivery and replay checks, including the Windows handoff |
| [Milestone 03 / 一箱水果](BorrowingSun2D/Docs/2D-to-3D/ROUND_03.md) | Shared rainwater, departure rules, physical ride, and delivery |
| [SOLID refactor / 框架整理](BorrowingSun2D/Docs/2D-to-3D/ARCHITECTURE_REFACTOR.md) | Why the rules and Godot adapter were separated, with regression evidence |
| [Relationship Map / 对象关系图](BorrowingSun2D/Docs/2D-to-3D/RELATIONSHIP_MAP.svg) | Implemented class relationships and the physics frame order |
| [Iteration record / 制作记录](BorrowingSun2D/Docs/ITERATIONS.md) | The early reset failure, its retest, and the three-shore extension |
