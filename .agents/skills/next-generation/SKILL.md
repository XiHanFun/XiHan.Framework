---
name: xihan-framework-next-generation
description: 在 next/ 与 nextdocs/ 中设计、实现、审查下一代框架（XiHan Framework 5）时使用：原语层、内核、子系统、Provider、宿主、协议、插件、Spec、ADR、门禁与阶段推进。旧框架 framework/ 的任务不使用本技能。
---

# XiHan Framework 5 开发

下一代在 `next` 分支、`next/` 与 `nextdocs/` 中从零重写，与 `framework/`（4.x）互不引用。命名由术语表与文法推出时读 `references/naming.md`；构建、测试、门禁与阶段退出条件读 `references/gates.md`。

## 先查事实

```bash
git branch --show-current          # 应为 next 或其 worktree 分支
git status --short --branch
cat next/README.md                 # 当前阶段
ls next/src next/spec/rfcs nextdocs/adr
```

按顺序读：`nextdocs/下一代框架总体方案.md` → `nextdocs/architecture/01–05` → 相关 RFC 与 ADR → 目标包的 README 与 PublicAPI 文件。只读与任务相关的部分。

## 架构边界

- 环层只能向下依赖：Primitives(0) → Kernel(1) → Domain 与 `.Abstractions`(2) → 实现(3) → Hosting 与 Protocols(4) → Plugins(5) → Profiles(6)；工具链（Analyzers、Generators、Conformance、Testing、Cli、Compat）可引用任何环。同环之间只经契约包（`.Abstractions`，以及纯契约的 `Domain`）。
- Ring 0/1 与抽象包零第三方依赖，只允许 `Microsoft.Extensions.*` 抽象包；原语层无任何引用。
- Ring 0/1 零反射；反射只允许在 `Plugins.InProcess` 并标注 `RequiresDynamicCode`。
- 原语层只增不改；破坏即新包 `Primitives.V2` 并存。
- 每个抽象配一个 `role: default` 的默认实现，包名按机制命名（`InMemory`、`FileSystem`），零外部基础设施、容量有界、通过一致性套件。
- 依赖按能力 ID 声明在清单里，不按包名；管道按固定阶段插入中间件。
- 规范先于实现：公开契约先进 `next/spec/rfcs`，再写 ADR（`nextdocs/adr`），再写代码与一致性向量。
- 借鉴只能落成规则与约束，不落成类名；标识符里不出现哲学词汇。
- 不把 4.x 的 `XiHanModule`、生命周期钩子、`[DependsOn]`、Castle 代理、`DefaultXxx`、`Utils` 带进 `next/`。

## 完成条件

1. 包名、命名空间、能力 ID、配置节、错误码域由术语表推出，`test/XiHan.Framework.Architecture.Tests` 通过。
2. 公开类型有 `[ApiLevel]` 与 XML 文档，`PublicAPI.Unshipped.txt` 已更新。
3. `dotnet build -c Release` 零警告；`dotnet test --solution` 通过；`dotnet pack` 通过包验证。
4. 新公开契约有 RFC、ADR 与一致性向量；新错误码与术语已登记。
5. 一项能力一个提交：`<type>(<scope>): <中文说明>`，scope 用术语表领域词小写或 `spec`、`docs`、`build`。
