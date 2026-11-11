# ADR-0134 共享 internal 的包按精确版本依赖

## 状态

已采纳（不改规范：NuGet 依赖范围是 .NET 实现的打包细节，不进跨语言契约）。

## 日期

2026-11-11

## 背景

P5-05 的 API 审查发现，插件的几个包之间经 `InternalsVisibleTo` 共享 internal：

| 提供 internal 的包 | 使用它的包 |
| --- | --- |
| `Plugins.Protocol`（由 `.proto` 生成、`Access="Internal"` 的协议消息与桩，以及握手核对与错误映射） | `Plugins.OutOfProcess`、`Plugins.Wasm`、`Plugins.Conformance` |
| `Plugins.OutOfProcess` | `Plugins.Conformance` |
| `Plugins.Wasm` | `Plugins.Conformance` |

NuGet 把项目引用打成「≥ 当前版本」的依赖，包验证（APICompat）又只比较公开面、不比较 internal。于是会出现这样的情况：应用引用 `Plugins.OutOfProcess` 5.0.0，另一个依赖把 `Plugins.Protocol` 抬到 5.1.0。还原能成功，构建也能通过，到运行时 OutOfProcess 调用 Protocol 里改过的 internal 成员才报 `MissingMethodException` 或 `TypeLoadException`。ADR-0131 允许 `Preview` 在小版本破坏，但这件事与它无关：internal 不在任何稳定性承诺之内，哪个版本都可能改。

## 备选

1. **把协议消息与映射改为公开**：生成的消息类型派生自 `Google.Protobuf` 的 `IMessage<T>`，公开签名会泄漏第三方类型（`DependencyTests.PublicSignatures_DoNotLeakThirdPartyTypes`），还会把几十个生成类型冻进公开面。否决。
2. **把生成代码与映射以源码链接编进各宿主包，撤掉 `Plugins.Protocol`**：同一批类型在每个宿主里各有一份，Conformance 同时引用两个宿主，会出现同名的 internal 类型；改动面大，也收不回 Conformance 对宿主 internal 的依赖。否决。
3. **共享 internal 的项目引用打成精确版本 `[版本]`**：采纳。所有包本来就共享一条版本线（ADR-0001），精确依赖只是把这条约定落实到这几对包上。

## 决策

- 项目引用上加元数据 `XiHanExactVersion="true"`，表示「我用了它的 internal」。`framework/src/Directory.Build.targets` 的 `_XiHanPinExactProjectReferenceVersions` 目标接在 NuGet 的 `_GetProjectReferenceVersions` 之后运行：NuGet 按被引用项目的完整路径收集版本，项目引用上的元数据带不过去，所以这个目标按完整路径挑出带标记的项，把它们的 `ProjectVersion` 改写成 `[版本]`。其余项目引用仍是「≥ 版本」。
- 当前打上标记的有五条引用：
  - `Plugins.OutOfProcess` → `Plugins.Protocol`
  - `Plugins.Wasm` → `Plugins.Protocol`
  - `Plugins.Conformance` → `Plugins.Protocol`
  - `Plugins.Conformance` → `Plugins.OutOfProcess`
  - `Plugins.Conformance` → `Plugins.Wasm`
- 门禁：架构测试 `DependencyTests.InternalsSharedAcrossPackages_ArePinnedToTheExactVersion` 核对两件事。一是正式包经 `InternalsVisibleTo` 授权给另一个正式包时，对方必须引用它；二是这条引用必须带 `XiHanExactVersion="true"`。给 `<程序集>.Tests` 的授权不在此列。

## 影响

- `Plugins.OutOfProcess`、`Plugins.Wasm`、`Plugins.Conformance` 的 NuGet 依赖从 `≥ 版本` 变为 `[版本]`。应用必须让这几个包与 `Plugins.Protocol` 保持同一版本。单独升级其中一个时，还原阶段就会报出来：直接引用了别的版本报 NU1608 警告，两个依赖要求不同版本报 NU1107 错误；不再等到运行时才失败。
- 以后新增跨包共享 internal，照同样的方式标记，否则架构测试不通过。
