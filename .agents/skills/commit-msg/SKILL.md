---
name: xihan-framework-commit-msg
description: 根据 XiHan.Framework 当前暂存区生成一行提交信息时使用。适用于“提交信息”“commit msg”“msg”或提交前概括 staged changes；只生成消息，不负责暂存、解决冲突或执行提交。
---

# XiHan.Framework 提交信息

1. 检查 `git status --short --branch`；存在冲突、merge 或 rebase 时先报告，不生成完成态消息。
2. 只读取 `git diff --cached --stat`、完整 staged diff 和最近 20 条提交。暂存区为空时不从未暂存内容猜测。
3. 将全部暂存变化归纳成一个意图，输出一行 Conventional Commit。

格式：`<type>(<scope>): <中文说明>`。type 使用 `feat`、`fix`、`refactor`、`perf`、`docs`、`style`、`test`、`build`、`ci`、`chore`、`revert`。

scope 优先使用受影响模块的简短名称，如 `core`、`data`、`caching`、`authentication`、`eventbus`、`web-api`、`workflow`、`tooling`、`docs`；跨模块基础能力可省略 scope，不逐项目罗列。

删除、改名或改变公共 API、配置键、默认实现和持久化语义时使用 `!`。只要求消息时最终只输出一行标题，不附解释或候选列表。
