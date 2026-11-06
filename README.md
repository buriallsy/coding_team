# Borrowing Sun (借晴)

**AP6412 · Route B · Milestone 02 · Borrowing Light**

Borrowing Sun is a weather puzzle set around a mountain tower. Rain lifts the tower and wind connects its bridges. At the Sun Court, aim the first mirror and use the Sunny token to send light to the tower mirror. Turn the tower east to reflect the beam to the receiver across the valley.

## Run and play

1. Clone or download this repository. Install **Godot 4.7.1 .NET** and the **.NET 8 SDK**.
2. In the Godot Project Manager, import `BorrowingSun2D/project.godot`. Open the project and press **F5** to run the main scene. [Project file](BorrowingSun2D/project.godot)
3. Press `D` or `→` to cross the low bridge and approach the device on the tower. Press `1` to select rain, then `E` to apply it. Wait for the lift to reach the upper level and lock.
4. Press `2` then `E` to turn north and walk left to the Sun Court mirror. Press `2` then `E` beside the mirror to aim it, then `3` and `E` to open local sunlight. The tower mirror is now hit while the east receiver remains dark.
5. Walk back to the tower device, press `2` then `E` to turn east, and watch the receiver light when the reflected beam reaches it. Press `R` to restart. [Full controls and scene entry](BorrowingSun2D/README.md)

## Verification

The macOS arm64 build passed with zero warnings and zero errors. The two-mirror route passed 57 headless checks and 59 windowed checks, including a real physics obstruction, a misaligned mirror, and walking the low bridge again after `R`. The previous three-shore route also passed 112 built-in and 71 independent keyboard checks. [Milestone 02 design, logs and reproduction steps](BorrowingSun2D/Docs/2D-to-3D/ROUND_02.md)

![Godot 2D in the east-facing position. Sunlight reflects from the Sun Court mirror to the tower mirror and lights the east receiver.](BorrowingSun2D/Docs/Evidence/round-02/east-receiver.png)

## Design and development

`TowerMechanism` owns the lift and docking rules. `SunCourtOptics2D` owns local sunshine and mirror aim, and calls the pure C# `Optics2D` solver with actual scene geometry and an obstruction query. The view draws the same segments that determine whether the receiver is lit. The next milestone adds cargo delivery powered by the water collected for the lift.

| Project documents | What to look for |
| --- | --- |
| [Game concept / 项目构思](BorrowingSun2D/Docs/2D-to-3D/DESIGN.md) | The full gameplay loop and what carries from 2D to 3D |
| [Production plan / 制作计划](BorrowingSun2D/Docs/2D-to-3D/PRODUCTION_PLAN.md) | Milestones, acceptance checks and evidence for the report |
| [Project conventions / 项目规范](BorrowingSun2D/Docs/2D-to-3D/PROJECT_CONVENTIONS.md) | Code responsibilities, scene editing, testing and Git workflow |
| [Current status / 当前版本](BorrowingSun2D/Docs/2D-to-3D/CURRENT_STATUS.md) | What is playable now, the reset regression, and the next milestone |
| [Milestone 01 / 三岸接驳](BorrowingSun2D/Docs/2D-to-3D/ROUND_01.md) | Docking design decisions, the three-shore route, and reset checks |
| [Milestone 02 / 借光](BorrowingSun2D/Docs/2D-to-3D/ROUND_02.md) | Two reflections, the north and east outcomes, and replayable tests |
| [SOLID refactor / 框架整理](BorrowingSun2D/Docs/2D-to-3D/ARCHITECTURE_REFACTOR.md) | Why the rules and Godot adapter were separated, with regression evidence |
| [Relationship Map / 对象关系图](BorrowingSun2D/Docs/2D-to-3D/RELATIONSHIP_MAP.svg) | Implemented class relationships and the physics frame order |
| [Iteration record / 制作记录](BorrowingSun2D/Docs/ITERATIONS.md) | The early reset failure, its retest, and the three-shore extension |
