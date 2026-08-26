# 贡献指南

感谢你为 XiHan.Framework 贡献代码。这是一份极简指南，目标是让你第一次 PR 就能顺利合入。

## 环境准备

- **.NET SDK 10.0.302**（`global.json` 锁定 10.0.3xx 功能带，仓库内 `dotnet --version` 应解析为 10.0.302）
- 拉取代码后在 `framework/` 下执行：

```bash
dotnet restore XiHan.Framework.slnx
dotnet build XiHan.Framework.slnx --configuration Release -p:GeneratePackageOnBuild=false
dotnet test XiHan.Framework.slnx --configuration Release --no-build
```

> 测试由 global.json 的 `test.runner` 以 Microsoft.Testing.Platform（MTP）模式驱动，不要用 VSTest 时代的 `--logger trx` 等参数。

## 提交约定

- 消息格式：`type(scope): 中文描述`，type 取 `feat / fix / docs / chore / build / ci / style / refactor / test`；
- 涉及行为变更的提交，正文写清**根因与取舍**（可参考近期提交历史）；
- 一个提交只做一件事，构建脚本、测试栈、文档等不同性质的改动分开提交。

## 代码约定

- 所有公开成员补 XML 文档注释（`GenerateDocumentationFile` 已开启）；
- 文件头版权注释以仓库现有格式为准（分析器 XHFH001 会检查）；
- 新增公共 API 时同步更新 `docs/packages/` 下对应包页；
- 新增测试请放在对应 `.Tests` 工程，避免在空壳工程里堆积示例代码。

## PR 流程

1. 在 `dev` 分支拉出特性分支；
2. 本地跑通构建与测试（命令见上）；
3. 提交 PR 到 `dev`，CI（构建 + MTP 测试 + 覆盖率门禁）通过后由维护者合并。

有任何疑问欢迎在 Issue 或 QQ 群 462371834 讨论。
