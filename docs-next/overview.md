# 框架概述

本页说明 XiHan Framework 5 的设计原则、环层与依赖规则、一次调用的执行路径、命名约定、稳定性分级与环境要求，供做技术评估与写扩展的读者参考。

## 三层承诺

框架按变化速度把代码分成三层，每层给出不同的兼容承诺：

| 层 | 范围 | 承诺 |
| --- | --- | --- |
| 原语层 | `XiHan.Framework.Primitives`（Ring 0） | 只增不改，跨大版本不破坏；必须破坏时发布新包 ID `Primitives.V2` 并存 |
| 内核 | `XiHan.Framework.Kernel`（Ring 1） | 大版本内不破坏 |
| 子系统与其上 | 抽象、实现、宿主、协议、插件、组合包（Ring 2–6） | 各自演进，通过同一套一致性套件即视为兼容 |

## 设计原则

| 原则 | 落在代码里的规则 |
| --- | --- |
| 内核克制 | 内核只定义宿主、能力图谱、管道、诊断、安全原语、操作契约与插件抽象，不含任何子系统实现 |
| 装配显式 | 服务在特性的 `Configure` 里显式注册；源生成器产出的登记代码也要显式调用（`AddGeneratedOperations`、`AddGeneratedRepositories`），不扫描程序集 |
| 依赖按能力声明 | 清单按能力 ID 与接口版本声明依赖，内核求解后打印已解析图，缺失、循环、版本冲突、多候选都在启动期报错 |
| 边界刚、内部柔 | 依赖方向、同环只经抽象包往来、抽象包不暴露第三方类型，全部由架构测试执行；包内实现可以随时重构 |
| 宿主落地 | 管道不绑定 HTTP；ASP.NET Core、命令行、消息消费、作业与插件回调走同一条管道 |
| 规范先于实现 | 公开契约先进 XiHan Spec 的 RFC，再写 ADR、代码与一致性向量 |
| 默认实现必须可用 | 每个抽象配一个零外部依赖、容量有界、通过一致性套件的默认实现，只在没有显式 Provider 时被选中 |

## 环层与依赖规则

```text
Ring 6  组合包          Profiles.Minimal / Standard / Full
Ring 5  协议与插件      Protocols.* / Plugins.InProcess / OutOfProcess / Wasm
Ring 4  宿主            Hosting.AspNetCore / Hosting.CommandLine
Ring 3  实现            <领域>.InMemory、<领域>.Database、Redis、EntityFrameworkCore 等
Ring 2  抽象            Domain、<领域>.Abstractions
Ring 1  内核            Kernel
Ring 0  原语层          Primitives
工具链                  Kernel.Analyzers、Kernel.Generators、Persistence.Generators、
                        <领域>.Conformance、Testing、Cli
```

1. 只能依赖更低的环；同环之间只能引用对方的抽象包（`.Abstractions`，以及纯契约的 `Domain`）。
2. 原语层不引用任何包；内核与全部抽象包只允许 `Microsoft.Extensions.*` 抽象包，内核另加 `System.Threading.RateLimiting`。
3. 抽象包不暴露第三方类型，Provider 不被任何抽象包引用。
4. 宿主与协议包不含业务规则；插件不被任何正式包引用。
5. 原语层与内核零反射；反射只允许出现在 `Plugins.InProcess`，并标注 `RequiresDynamicCode`。
6. 工具链可以引用任何环。

规则由 `next/test/XiHan.Framework.Architecture.Tests` 执行，违反即 CI 失败。

## 一次调用的路径

```text
HTTP 请求 / gRPC 调用 / MCP tools/call / 命令行 / 消息投递 / 作业触发 / 插件回调
   │  协议适配器或派发器构造 InvocationContext，把线上消息翻译成操作请求
   ▼
Ingress → Observe → Resilience → Authenticate → Tenant → Authorize
        → Validate → Idempotency → Transaction → Handle → Egress
   │  Handle 阶段执行操作，操作经子系统抽象访问 Provider
   ▼
Egress：Result 与 Error 翻译回协议（HTTP 为 RFC 9457 Problem Details，gRPC 为状态码与尾部元数据）
```

调用上下文 `InvocationContext` 携带取消令牌、截止时间、W3C Trace、已登记的 Baggage 键（租户、主体、关联 ID、幂等键、区域）、作用域对象袋与作用域服务。安全上下文（主体、租户）由 Authenticate 与 Tenant 阶段写入，之后不可变。

## 命名约定

命名由 XiHan Spec 的术语表推出，架构测试校验：

| 对象 | 规则 | 示例 |
| --- | --- | --- |
| 包 ID | `XiHan.Framework.<领域>[.<角色>]`，角色是 `Abstractions`、`Conformance`、`Generators` 或机制、技术名 | `XiHan.Framework.Caching.Redis` |
| 命名空间 | 等于包 ID，子命名空间最多一层 | `XiHan.Framework.Caching.Abstractions.Locking` |
| 能力 ID | `<领域小写>.<能力>`，引用时带接口版本 | `caching.distributed@1` |
| 清单 ID | `xihan.<领域>.<名称>` | `xihan.caching.redis` |
| 配置节 | `XiHan:<领域>` | `XiHan:Caching` |
| 错误码 | `<领域大写>_<KIND>_<三位序号>` | `PERSISTENCE_CONFLICT_001` |
| 操作 ID | `<资源复数>.<动词>` | `orders.create` |
| 消息键 | `<领域>.<资源>.<键>` | `security.token.subject_missing` |

类型名不带 `XiHan` 前缀；`Default`、`Core`、`Common`、`Manager`、`Helper` 等词不进包名与领域名；默认实现按机制命名（`InMemory`、`FileSystem`、`Database`），默认是清单里的 `role`，不是包名。

## 核心机制速览

| 机制 | 一句话 | 详见 |
| --- | --- | --- |
| 特性与清单 | `IFeature` + `Manifest`，`UseFeature<T>()` 显式组合 | [能力包与清单](./guide/modularity) |
| 生命周期 | 九态状态机，探针直接映射应用状态 | [生命周期与健康](./guide/lifecycle) |
| 执行管道 | 十一个固定阶段，中间件按阶段插入 | [执行管道](./guide/pipeline) |
| 操作契约 | `[Operation]` + `IOperation<TRequest, TResponse>`，一份契约多协议 | [操作契约](./guide/operations) |
| 结果与错误 | `Result` / `Error` 代替异常表达可恢复失败，错误只带消息键 | [结果与错误](./guide/errors) |
| 本地化 | 每种语言一个 JSON 文件，宿主按调用方区域翻译 | [国际化](./guide/localization) |
| 默认实现与 Provider | 抽象包 + 默认实现 + Provider，按能力选择 | [包总览](./packages/) |
| 一致性套件 | 默认实现与 Provider 通过同一套行为测试 | [测试与一致性套件](./guide/testing) |

## 稳定性分级

公开类型都带 `[ApiLevel(Stability, "5.0")]`，公开面以各包的 `PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt` 为准：

| 级别 | 含义 |
| --- | --- |
| `Stable` | 大版本内不破坏 |
| `Preview` | 可能变更 |
| `Experimental` | 可能移除，用于收集反馈（如 `Plugins.Wasm`、`Caching.Remote`） |
| `Internal` | 不对外承诺，分析器阻止外部引用 |
| `Deprecated` | 将在下一个大版本移除 |

弃用按规范的流程进行：先标 `Deprecated` 并以 `[Obsolete]` 给出警告与迁移说明，至少保留两个小版本；下一个大版本前升级为编译错误，大版本时移除。

## 版本与兼容

- 全部包共享一条版本线，从 `5.0.0` 起，当前为 `5.0.0-preview.1`。
- 目标框架 `net10.0`；版本号遵循 SemVer 2.0。
- 每个子系统的 Provider 接口单独版本化（`persistence.relational@1`），接口变更只加新版本并存。
- 4.x 在 `framework/` 单独维护，与 5.0 互不引用。

## 环境要求

| 项 | 要求 |
| --- | --- |
| SDK | .NET 10 SDK；构建框架源码时使用仓库 `global.json` 锁定的 10.0.1xx 功能带 |
| 测试运行器 | Microsoft.Testing.Platform（由 `global.json` 启用，`dotnet test` 用 `--solution` 或 `--project`） |
| 操作系统 | Windows、Linux、macOS；Wasm 插件宿主在 Windows 上要求宿主可执行项目设置 `CETCompat=false` |
| 外部设施 | 默认实现不需要；Provider 按各自包页的说明 |

## 下一步

- [快速上手](./quickstart)：取得预览包，写第一个操作
- [为什么选择曦寒](./why)：适用场景与代价
- [开发指南](./guide/modularity)：从能力包与清单开始
