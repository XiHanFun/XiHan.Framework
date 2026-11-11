# 安全策略

## 报告漏洞

请不要在公开的 Issue、讨论区或 Pull Request 里报告安全漏洞。

用 GitHub 的私密漏洞报告提交：[Report a vulnerability](https://github.com/XiHanFun/XiHan.Framework/security/advisories/new)。报告只有你和维护者看得到，修复与公告都在同一个私密草稿里协作完成。

报告里请尽量写清：

- 受影响的包与版本（`XiHan.Framework.*` 的包 ID 与版本号）。
- 问题是什么、攻击者需要什么前提（匿名调用方、某个租户的已认证用户、能发布流程定义的用户、插件作者等）。
- 复现步骤或最小复现代码，以及你观察到的影响。
- 你建议的修法（可选）。

## 处理流程

1. 维护者确认收到，评估影响范围与严重程度。
2. 确认是漏洞的，在私密草稿里修复，补复现测试，随下一个版本发布；严重的单独发补丁版本。
3. 发布后公开安全公告（GitHub Security Advisory），注明受影响版本、修复版本与升级方式；报告人愿意的话在公告里致谢。

修复发布之前，请不要公开漏洞细节。

## 支持的版本

| 版本 | 状态 |
| --- | --- |
| 5.x | 当前版本线，安全修复进入这条线 |
| 4.x | 维护期，单独评估 |

5.0 正式发布之后按文档站「路线图」里的版本策略：LTS 大版本三年活跃支持，之后一年只做安全修复。

## 范围

本仓库发布的 `XiHan.Framework.*` NuGet 包、`xihan` 命令行工具与 `framework/spec` 规范。基于框架的应用（例如 BasicApp）请到各自的仓库报告。

---

## Reporting a vulnerability (English)

Please do not report security vulnerabilities through public issues, discussions or pull requests. Use GitHub private vulnerability reporting instead: [Report a vulnerability](https://github.com/XiHanFun/XiHan.Framework/security/advisories/new). Include the affected packages and versions, the attacker's preconditions, reproduction steps and the impact you observed. Fixes are developed in a private advisory, released with a regression test, and announced through a GitHub Security Advisory. Please keep details private until a fix is released.
