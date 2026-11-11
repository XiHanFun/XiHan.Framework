---
name: xihan-framework-version-release
description: 为 XiHan.Framework 准备版本、更新 framework/Directory.Build.props 的版本、整理发布日志、生成和校验 NuGet 包、打标签或触发发布时使用。只有用户明确要求实际发布时才允许推送标签或运行发布工作流。
---

# XiHan.Framework 版本发布

## 前置条件

- 工作区干净，无 merge/rebase、冲突或 detached HEAD；目标版本、通道和目标分支明确。
- `framework/Directory.Build.props` 的 `<Version>` 是所有包的版本真源；tag 使用 `vX.Y.Z[-tag.N]`，且必须与 `<Version>` 完全相等。
- `docs/changelog.md` 记录用户可感知变化；`docs/package.json` 的站点版本必须与产品版本同步；其余文档不写版本，发版不用改。
- 发布工作流会打包并推送全部 NuGet 包，不能用它试跑版本选择。
- 发布分支：预览版从 `preview` 发，正式版从 `main` 发，都由 `dev` 快进；标签打在对应分支的历史上。

## 准备版本

每一步都在用户要求之后才做：

1. 核对上一 tag 到当前提交的公共 API、包、破坏性变更和 `docs/changelog.md` 未发布区。
2. 在 `dev` 上改 `<Version>` 与 `docs/package.json`，在 `docs/changelog.md` 补这一版；不替用户预选版本级别或发布通道。
3. 执行 Release 还原、构建、MTP 测试与打包，检查 `framework/artifacts/packages/` 的包数量、版本与内容。
4. 版本提交使用 `build: vX.Y.Z`；推 `dev`，等 `ci.yml` 全绿。
5. 正式版（不带预发布后缀）的版本提交里执行 `framework/scripts/Ship-PublicApi.ps1`，把各包 `PublicAPI.Unshipped.txt` 移进 `Shipped`（先 `-Check` 预览）。

## 包验证基线

- 基线是同一大版本最近一次发布的正式版，写在 `framework/Directory.Build.props` 的 `<XiHanPackageBaselineVersion>`；空表示不比对（ADR-0131）。
- 正式版在 NuGet 上可以还原之后，下一个提交把它填成刚发布的版本号；在那之前填会因还原不到基线包而打包失败。
- 基线发布之后才新增的包，在项目文件里写 `<XiHanPackageBaselineVersion />` 清空；`Cli` 不启用包验证，不受影响。
- `Preview` / `Experimental` 的破坏用 `dotnet pack <项目> -p:ApiCompatGenerateSuppressionFile=true` 生成包目录下的 `CompatibilitySuppressions.xml`，与改动同一提交；`Stable` 的破坏不登记豁免，改回去。
- 换大版本时清空基线与全部豁免文件，另列破坏清单。

## 发布

1. 快进发布分支：预览版 `git fetch . dev:preview`，正式版 `git fetch . dev:main`（不是快进就停下查），推送并等 CI 全绿。`ci.yml` 按路径触发，只改文档的提交不会自动跑，这时用 `gh workflow run ci.yml --ref preview` 手动触发；文档会嵌进 CLI 包，不能跳过。
2. 打标签并推送：`git tag -a v<版本> -m "v<版本>" <发布分支>`、`git push origin v<版本>`。
3. 发布前确认 nuget.org 的可信发布策略覆盖 `release.yml`，新包 ID 也能发。NuGet 上的版本删不掉，只能取消列出，所以发错了只能用下一个号。

只有用户明确要求发布，才可创建并推送版本标签或手动触发 `.github/workflows/release.yml`（手动运行须选中版本标签，选分支会被拒）。

`release.yml` 的 guard 查两条：带预发布后缀的标签要在 `preview` 的历史里，不带的要在 `main` 的历史里；标签号等于 `<Version>`。任一不满足拒发。

工作流先由 guard 校验标签，再通过 OIDC 登录 NuGet、打包并逐包推送；测试门禁归 `ci.yml`，发布链不重跑。发布后核对工作流、NuGet 版本和文档；任何包、测试或登录失败都应停止并报告。不得移动已发布标签、手改 nupkg、跳过测试或用 `--skip-duplicate` 掩盖版本不一致。
