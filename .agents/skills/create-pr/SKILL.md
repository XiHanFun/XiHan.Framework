---
name: xihan-framework-create-pr
description: 为 XiHan.Framework 准备、审查或创建 Pull Request 时使用。适用于生成 PR 标题与正文、比较分支、填写仓库模板或执行 gh pr create；普通模块代码审查不使用。
---

# XiHan.Framework 创建 PR

## 前置检查

- 检查分支、上游、工作区、merge/rebase 和冲突；非正常分支状态不创建 PR。
- 根据用户意图和远端分支确定 base；普通开发通常面向 `dev`，release/hotfix 以现有分支策略为准，不默认改成 `main`。
- 用 `git log <base>..HEAD`、`git diff --stat <base>...HEAD` 和完整 diff 审查整条分支。
- 核对模块依赖、公共 API、XML 文档、包元数据、测试和迁移说明。

## PR 内容

严格使用 `.github/PULL_REQUEST_TEMPLATE.md`，保留关联 Issue、变更类型、变更说明、影响范围、自测清单、破坏性变更和补充说明。

- 标题使用 Conventional Commit 形态，说明框架使用者可感知的结果。
- 影响范围列出模块和 NuGet 包，不堆文件名。
- 只有实际执行的 Release 构建、MTP 测试和文档构建才能勾选。
- 公共 API、配置、默认实现、数据库/缓存键和序列化变化必须写迁移影响。
- 新 ProjectReference 或 `[DependsOn]` 变化说明依赖方向和无环证据。

只要草稿时不写远程。明确要求创建 PR 时才允许推送当前分支并执行 `gh pr create`；禁止 force push、合并或改写其他远端分支。创建后返回 PR 链接。
