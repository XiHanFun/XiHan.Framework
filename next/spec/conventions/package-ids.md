# 包 ID 文法

包名由"领域 + 角色"唯一推出。命名空间等于包 ID，包内子命名空间最多一层。架构测试 `PackageIds_FollowGrammar` 执行本文法。

```text
PackageId  = "XiHan.Framework." Segment
Segment    = "Primitives" | "Kernel" [ "." KernelTool ] | "Domain" | "Testing" | "Cli"
           | Subsystem "." Role
           | "Hosting." Host | "Protocols." Protocol | "Plugins." ( Isolation | "Protocol" | "Conformance" ) | "Profiles." Profile
           | "Compat." Major
Subsystem  = 术语表中除结构词以外的领域词
Role       = "Abstractions" | "Conformance" | "Generators" | Mechanism | Technology
KernelTool = "Analyzers" | "Generators"
Mechanism  = "InMemory" | "FileSystem" | "Database" | "Embedded" | ...
Technology = 官方拼写的技术名（EntityFrameworkCore、RabbitMQ、Redis、S3、AliyunOss）
Host       = "AspNetCore" | "CommandLine"
Protocol   = "Http" | "Grpc" | "Mcp" | "CloudEvents"
Isolation  = "InProcess" | "OutOfProcess" | "Wasm"
Profile    = "Minimal" | "Standard" | "Full"
Major      = "V" 数字
```

结构词：`Primitives`、`Kernel`、`Domain`、`Hosting`、`Protocols`、`Plugins`、`Profiles`、`Testing`、`Cli`、`Compat`。

## 规则

1. 角色段只表角色，领域永远在前：`Persistence.Conformance`，不是 `Conformance.Persistence`。
2. 实现包按机制或技术命名；"默认"是清单里的 `role: default`，不进包名。
3. 角色段禁用词：`Default`、`Core`、`Common`、`Shared`、`Base`、`Impl`、`Util`、`Utils`、`Helper`、`Manager`、`Bus`、`Engine`。
4. 技术名用官方拼写；非品牌缩写两字母全大写（`AI`），更长用 Pascal（`Jwt`、`Mcp`、`Oss`）。
5. 版本不进包名；只有原语层破坏性并存才出现 `Primitives.V2`。

## 环层

| 环 | 包 |
| --- | --- |
| 0 | `Primitives` |
| 1 | `Kernel` |
| 2 | `Domain`、`<领域>.Abstractions` |
| 3 | `<领域>.<机制或技术>`、`Plugins.Protocol`（插件协议的 .NET 类型与映射，各隔离级别的宿主共用） |
| 4 | `Hosting.*`、`Protocols.*` |
| 5 | `Plugins.*` |
| 6 | `Profiles.*` |
| 工具链 | `Kernel.Analyzers`、`Kernel.Generators`、`<领域>.Generators`、`<领域>.Conformance`、`Plugins.Conformance`（插件协议黑盒套件）、`Testing`、`Cli`、`Compat.*` |

只能依赖更低的环；同环之间只能引用对方的 `.Abstractions`；工具链与 Profile 可引用任何环。
