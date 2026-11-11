# 手动上传新版 2D 与完整交接资料

目标仓库：<https://github.com/buriallsy/coding_team>

目标分支：`feature/djwcb2333/market-loop`。文件已经在本地提交好，不需要重新逐个勾选、复制代码或创建另一个 GitHub 仓库。此次只发布这个分支，主分支不需要合并。

## 先认准文件夹

用于本机手动上传的普通 Git 副本：

```text
E:\Codex Workspace\GDAT\coding_team-market-desktop
```

实际开发目录仍是 `E:\Codex Workspace\GDAT\coding_team-market`。两者在交接准备时已对齐至同一提交，之后若继续开发，需要再次同步。`coding_team` 和 `coding_team-final-review` 是其他工作副本，不用于这次发布。

文件清单在 [Handoff.md](BorrowingSun2D/Docs/Market/Handoff.md)。想试玩就导入上传副本里的 `BorrowingSun2D/project.godot`。

## 方法一：GitHub Desktop 能正常打开时

1. 打开 GitHub Desktop，确认登录 `djwcb2333`。
2. 菜单 **File → Add local repository**，选择上面的 `coding_team-market-desktop` 文件夹，点击 **Add repository**。不要选择创建新仓库。
3. 在 **Current branch** 选中 `feature/djwcb2333/market-loop`。
4. 看 **History**，应能看到最新的“补齐交接资料：最终定案 PDF、变化图与手动上传指南”，以及之前的外卖游戏与后续计划提交。Changes 没有内容是正常的，文件已经提交。
5. 点击顶部 **Publish branch**；若分支已经存在，按钮可能显示 **Push origin**。完成 GitHub 提示的登录或确认。
6. 打开下方分支网页，核对最新提交和交接清单。如果 Desktop 提示有远端新改动，先保留提示并核对，不使用强制推送。

## 方法二：Desktop 仍然崩溃时，用 Windows PowerShell

这条路线不需要启动 Desktop。按 **Win + R**，输入 `powershell` 并回车。在你自己的 PowerShell 窗口粘贴下面这一行，再按回车：

```powershell
& 'E:\Git\cmd\git.exe' -C 'E:\Codex Workspace\GDAT\coding_team-market-desktop' push --set-upstream origin feature/djwcb2333/market-loop
```

若弹出 GitHub 登录页面，使用 `djwcb2333` 完成登录。不要把密码或访问令牌贴进聊天。上传成功通常出现分支推送信息；`Everything up-to-date` 也需要结合下方提交号检查确认。

核对线上与本地版本，分别运行：

```powershell
& 'E:\Git\cmd\git.exe' -C 'E:\Codex Workspace\GDAT\coding_team-market-desktop' rev-parse HEAD
& 'E:\Git\cmd\git.exe' -C 'E:\Codex Workspace\GDAT\coding_team-market-desktop' ls-remote origin refs/heads/feature/djwcb2333/market-loop
```

两处完整的 40 位提交号应相同。第一条只读本地，第二条读取 GitHub；第二条为空或报错不算上传成功。如果报错，保留完整错误文字交给我定位，不要使用 `--force` 或往 `main` 推送。

## 上传后让组员从这里查看

分支链接：<https://github.com/buriallsy/coding_team/tree/feature/djwcb2333/market-loop>

该链接只有分支发布成功后才可访问。组员先看根目录 README，再打开 `BorrowingSun2D/Docs/Market/Handoff.md`，其中链接了策划、数值、UML、架构、变化图、CCR 边界、未来计划和 PDF。她可以切到该分支，或在该分支网页选择 **Code → Download ZIP**，解压后用 Godot .NET 导入 2D 工程。

## 本地备份包

GDAT 文件夹还提供 `BorrowingSun-market-loop.bundle`（Git 历史与分支，可用于恢复/传给组员）和 `BorrowingSun-market-handoff.zip`（当前全部已提交文件的快照，可解压试玩）。压缩包不是上传成功的证据，也不需要把整个 ZIP 当成一个文件塞进源码仓库。优先用上面的分支推送保留课程所需的提交历史。
