---
name: xihan-framework-commit-msg
description: 根据 XiHan.Framework 当前暂存区生成一行提交信息时使用。适用于“提交信息”“commit msg”“msg”或提交前概括 staged changes；只生成消息，不负责暂存、解决冲突或执行提交。
---

# XiHan.Framework 提交信息

1. 检查 `git status --short --branch`；存在冲突、merge 或 rebase 时先报告，不生成完成态消息。
2. 只读取 `git diff --cached --stat`、完整 staged diff 和最近 20 条提交。暂存区为空时不从未暂存内容猜测。
3. 将全部暂存变化归纳成一个意图，输出一行 Conventional Commit。

格式：`<type>(<scope>): <中文说明>`。type 使用 `feat`、`fix`、`refactor`、`perf`、`docs`、`style`、`test`、`build`、`ci`、`chore`、`revert`。

scope 优先使用术语表领域词小写，如 `kernel`、`persistence`、`caching`、`security`、`messaging`、`workflow`、`ai`、`cli`，或 `spec`、`docs`、`build`；跨子系统的基础能力可省略 scope，不逐项目罗列。

删除、改名或改变公共 API、配置键、默认实现和持久化语义时使用 `!`，迁移方式写进更新日志与文档，不写进提交信息。

## 写短

- 只写一行标题，不写正文。
- 说明一句话讲这次改动的结果，不超过 40 个字：汉字、字母、数字、标点各算一个，末尾的 `（ADR-NNNN）` 不计。
- 说明里不再出现「：」「；」，除末尾的 ADR 注外不用括号或破折号补充；不罗列文件、成员、取值与改前改后，不写向量与测试的数量、门禁结果，这些写进 ADR、更新日志或 PR 描述。
- 40 个字写不下时，多半是暂存区混了几件事（先建议拆成几笔），或细节写多了（删到只剩结果），不要加长标题。

不要：`test(testing): SpecVectors 的严格读取按可空性拒绝 null：选项加上 RespectNullableAnnotations，反射与源生成两条路都按属性、字段与构造参数的可空性注解拦下……`

要：`test(testing): SpecVectors 严格读取时拒绝落到不可空成员的 null（ADR-0300）`

只要求消息时最终只输出一行标题，不附解释或候选列表。
