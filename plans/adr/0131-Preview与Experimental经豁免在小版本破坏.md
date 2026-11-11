# ADR-0131 Preview 与 Experimental 经豁免在小版本破坏

## 状态

已采纳（规范 `rfcs/0001-primitives.md` 增「稳定性级别」一节）。

## 日期

2026-11-11

## 背景

P5-05 的 API 审查（2026-11-11）统计：83 个包共 996 个公开类型，其中 `Stable` 252 个、`Preview` 718 个、`Experimental` 21 个；`PublicAPI.Shipped.txt` 全部为空。

稳定性级别的含义只写在 `01-框架定位.md` §3.3 与文档站，`Preview` 一行是「可能变更」，没有说在哪类版本里变、怎么变。工具链也没有区分：

- 包验证（`EnablePackageValidation`）按整个程序集比较，不认 `[ApiLevel]`。
- 仓库没有 `PackageValidationBaselineVersion`，也没有 `CompatibilitySuppressions.xml`。
- `Sync-PublicApi.ps1` 遇到指向已发布符号的 RS0017 就失败。

所以 5.0.0 一旦作为基线，718 个 `Preview` 类型实际上和 `Stable` 一样冻结，标记失去意义。审查里列出的 `Preview` 改形要么全部挤到 GA 前做完，要么 GA 后再也不能做。

审查同时发现，`Stable` 的签名引用了 `Preview` 类型：`IUnitOfWork` 引用 `DomainEventTracker`，`EntityQuery<T>` 引用 `TimeRange`，`RepositoryRegistrations` 引用 `RegisteredEntity`；`FullProfile`（`Stable`）组装的特性几乎都是 `Preview`。一旦允许 `Preview` 破坏，这些 `Stable` 成员会被连带破坏。

## 备选

1. **全部冻结**：GA 后任何公开面都不破坏，`Preview` 只作提示。否决：审查中属于 `Preview` 的改形（存储接口、AI、协议适配器等）都得挤在 GA 前，GA 推迟；对使用者来说 `Preview` 与 `Stable` 也就没有区别。
2. **`Preview` 不进包验证**：按命名空间或特性把 `Preview` 类型排除在比较之外。否决：包验证不支持按特性过滤；排除之后破坏不留痕迹，使用者无从得知。
3. **`Preview` 可以在小版本破坏，每一处破坏单独登记豁免**：采纳。

## 决策

1. **级别语义写进 RFC-0001「稳定性级别」**：
   - `Stable`：同一大版本内不破坏。
   - `Preview` / `Experimental`：小版本可以破坏（`Experimental` 还可以移除），补丁版本不破坏；每一处破坏单独登记豁免，并在更新日志写迁移方式。
   - `Internal`：不承诺，只供声明它的包自己使用。
   - `Deprecated`：照弃用流程。
   - 级别只升不降；`Stable` 的签名不得引用 `Preview` / `Experimental` 类型；元包的级别不高于组件；原语层整体冻结，不适用豁免。
2. **基线**：5.0.0 发版时，在 `framework/src/Directory.Build.props` 设 `PackageValidationBaselineVersion`。之后每个小版本以同一大版本最近一次发布的版本为基线；换大版本时清空豁免，另列破坏清单。
3. **登记豁免**：
   - `Preview` / `Experimental` 的破坏由 `dotnet pack -p:ApiCompatGenerateSuppressionFile=true` 生成或更新包目录下的 `CompatibilitySuppressions.xml`，与改动放在同一个提交里。
   - 提交说明引用决定这次破坏的 ADR。
   - 更新日志在「调整」或「移除」下写迁移方式。
4. **PublicAPI 文件**：
   - 已发布的 `Preview` / `Experimental` 行，在 `PublicAPI.Unshipped.txt` 里写 `*REMOVED*` 行撤下，`PublicAPI.Shipped.txt` 仍不手改。
   - `Stable` 的已发布行不改。
   - `Sync-PublicApi.ps1` 改为：RS0017 指向非 `Stable` 符号时写 `*REMOVED*` 行，指向 `Stable` 符号时照旧失败。
5. **门禁**：
   - 架构测试核对每个 `CompatibilitySuppressions.xml` 的 `Target` 所指成员按生效级别不是 `Stable`，并且原语层包没有豁免文件。
   - `Kernel.Analyzers` 新增诊断：`Stable` 成员的签名引用 `Preview` / `Experimental` 类型即报错；成员单独标注的级别高于所在类型也报错。
6. **实施时间**：
   - 规则即日生效。
   - 分析器诊断与审查中列出的 `Stable` → `Preview` 依赖修正，放进 GA 前「级别调整与冻结」那一批。
   - 豁免门禁、`Sync-PublicApi.ps1` 改造与基线设置，在 5.0.0 发版提交里落地；在此之前没有基线，豁免文件也就无从生成。

## 理由

- 与 .NET 的 `[Experimental]` 和 Kubernetes 的 alpha / beta / GA 分级是同一思路：不稳定的面允许变，但变化必须看得见。
- 逐条豁免让每一次破坏都出现在代码评审里，使用者也能从更新日志拿到迁移方式。
- 「只升不降」堵住「先降级再破坏」的绕法；「`Stable` 不依赖不稳定类型」保证 `Stable` 的承诺不会被连带打破。

## 影响

- GA 前必须完成的改动收窄为四类：`Stable` 层、生成器写进下游程序集的形状、原语层、安全缺陷。`Preview` 层的改形可以放到 5.x 的小版本，每次登记豁免；在 GA 前做则不需要登记。
- 下游（含 BasicApp）用到的 `Preview` API 可能在小版本升级时需要改代码，迁移方式见更新日志。
- 文档同步：总体方案 §11.2、`01-框架定位.md` §3.3、文档站「核心概念 · 稳定性分级」「定位 · 版本与兼容」「贡献指南」、`AGENTS.md` 与开发技能的门禁说明。
