---
name: xihan-framework-version-release
description: 为 XiHan.Framework 准备版本、更新 version.props、整理发布日志、生成和校验 NuGet 包、打标签或触发发布时使用。只有用户明确要求实际发布时才允许推送标签或运行发布工作流。
---

# XiHan.Framework 版本发布

## 前置条件

- 工作区干净，无 merge/rebase、冲突或 detached HEAD；目标版本、通道和目标分支明确。
- `framework/props/version.props` 是程序集与 NuGet 版本真源；tag 使用 `vX.Y.Z[-tag.N]`，且必须与 `<Version>` 完全相等。
- `docs/changelog.md` 记录用户可感知变化；`docs/package.json` 的站点版本必须与正式产品版本同步。
- 发布工作流会构建并推送全部 NuGet 包，不能用它试跑版本选择。

## 准备版本

1. 核对上一 tag 到当前提交的公共 API、包、迁移影响和 `docs/changelog.md` 未发布区。
2. 运行交互脚本 `pwsh -File framework/scripts/nuget/VersionUpgrade.ps1`；不替用户预选版本级别或发布通道。
3. 核对 `AssemblyVersion`、`FileVersion`、`Version`、文档版本和 changelog 标题。
4. 执行 Release 还原、构建和 MTP 测试，检查 `framework/nupkgs` 包数量、版本与内容。
5. 版本提交使用 `build: vX.Y.Z`，并确认提交进入预期发布分支。

## 发布

只有用户明确要求发布，才可创建并推送版本标签或手动触发 `.github/workflows/publish.yml`（手动运行须选中版本标签，选分支会被拒）。推标签前先确认标签提交属于预期 `main` 历史且版本与 `version.props` 完全一致——工作流的 guard 两条都查，任一不满足拒发。

工作流先由 guard 校验标签，再通过 OIDC 登录 NuGet、构建并逐包推送；测试与覆盖率门禁归 `ci.yml`，发布链不重跑。发布后核对工作流、NuGet 版本和文档；任何包、测试或登录失败都应停止并报告。不得移动已发布标签、手改 nupkg、跳过测试或用 `--skip-duplicate` 掩盖版本不一致。
