# RFC-0002 清单

> 状态：草案 | 日期：2026-10-21 | 对应 ADR：0002、0006

## 摘要

每个能力包、Provider、插件、宿主、协议适配器带一份 `xihan.manifest.json`，声明身份、提供与依赖的能力、资源绑定、贡献点、激活事件与权限。清单是装配的事实源；.NET 实现由源生成器从 `[Capability]` 生成并嵌入程序集。Schema 见 `spec/manifest/manifest.schema.json`。

## 字段

| 字段 | 必填 | 含义 |
| --- | --- | --- |
| `id` | 是 | `xihan.<领域>.<名称>` |
| `package` | 否 | 所属包 ID |
| `version` | 是 | SemVer |
| `primitives` | 是 | 依赖的原语层大版本 |
| `stability` | 是 | 五级稳定性 |
| `kind` | 是 | `capability` / `provider` / `plugin` / `host` / `protocol` |
| `role` | 否 | `provider`（默认）或 `default`：无显式 Provider 时被选中 |
| `isolation` | 插件必填 | `in-process` / `wasm` / `out-of-process` |
| `provides` | 否 | 提供的能力及接口版本 |
| `requires` | 否 | 依赖的能力、版本范围、是否可选 |
| `bindings` | 否 | 需要宿主注入的资源 |
| `contributes` | 否 | 管道中间件、操作、配置节与 Schema、权限码、健康项 |
| `activation` | 否 | 激活事件，默认 `onStartup` |
| `permissions` | 插件 | 所需权限，宿主配置授予 |
| `entry` | 否 | 入口类型 |

## 解析规则

1. 收集：引用的程序集内嵌清单 + 插件目录中的清单文件。
2. 校验：Schema、`primitives` 与当前原语层大版本相同、`id` 唯一。
3. 求解：按能力 ID 与版本范围求解；同一能力多个候选时，`XiHan:Providers:<能力ID>` 指定 → 唯一 `role: default` → 否则失败并列出候选（`KERNEL_CAPABILITY_004`）。
4. 排序：拓扑排序；循环报 `KERNEL_CAPABILITY_002`，缺失报 `KERNEL_CAPABILITY_001`，版本冲突报 `KERNEL_CAPABILITY_003`，报错必须列出冲突双方。
5. 可选依赖缺失不阻止启动，对应特性进入 `Degraded`。
6. 输出已解析图；`xihan graph` 与启动日志打印同一份。

## 与 .NET 的映射

```csharp
[Capability("persistence.efcore",
    Provides = ["persistence.relational@1"],
    Requires = ["kernel.hosting@^1", "multitenancy.context@^1?"],
    Role = CapabilityRole.Provider)]
public sealed class EntityFrameworkCoreFeature : IFeature, IHostedFeature { … }
```

生成器从特性产出清单并作为嵌入资源 `xihan.manifest.json` 写入程序集；`bindings`、`contributes` 由同一特性的可选参数或伴随特性声明。

## 一致性向量

`spec/conformance/vectors/manifest/*.json`：合法清单、每种非法清单的期望错误、求解用例（缺失、循环、冲突、多候选、默认角色、可选依赖）。
