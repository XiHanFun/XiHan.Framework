# 能力包与清单

本章说明 5.0 的装配单元：特性（`IFeature`）、清单（`Manifest`）、能力 ID 与能力图谱的求解，以及同一能力有多个实现时如何选择 Provider。适用于所有在 5.0 上写应用或扩展的开发者。

完整 API 见 [Kernel 包](../packages/kernel)与 [Primitives 包](../packages/primitives)。

## 特性与能力包

特性是实现 `IFeature` 的类，是内核装配的最小单元。它只有两个成员：

| 成员 | 作用 |
| --- | --- |
| `Manifest Manifest { get; }` | 清单：身份、提供与依赖的能力、资源绑定、贡献点 |
| `void Configure(FeatureContext context)` | 注册服务、中间件、权限与操作；按能力图谱的拓扑顺序调用一次 |

需要在应用启动与停止时做事的特性再实现 `IHostedFeature`（它继承 `IFeature`），多出 `StartAsync(IServiceProvider, CancellationToken)` 与 `StopAsync(CancellationToken)`，详见[生命周期与健康](./lifecycle)。

能力包是包含一个或多个特性的 NuGet 包。框架里每个子系统的默认实现与 Provider 都是能力包，应用自己的业务模块也写成能力包：例如示例应用的 `OrdersFeature` 声明依赖持久化、消息、授权与多租户四个能力，并登记订单仓储、消息处理器、权限与操作。

特性没有基类、没有生命周期钩子，内核也不扫描程序集。应用用 `UseFeature<T>()` 显式加入每一个特性（要求公开的无参构造函数），或用 `UseFeature(instance)` 加入已创建的实例：

```csharp
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()                // 组合包：一组 UseFeature 的简写
    .UseFeature<RedisCachingFeature>()   // 显式 Provider：caching.distributed 改由 Redis 提供
    .UseFeature<HttpProtocolFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

组合包（`Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full`）只是预先写好的一串 `UseFeature`，例如 `UseStandardProfile()` 等于 Minimal 再加持久化、缓存、多租户、安全、消息五个子系统的内存默认实现。

## 清单

清单是装配的事实源，特性以 `Manifest` 属性给出，内核在构建期校验格式（`Manifest.Validate()`）。字段与 XiHan Spec 的 `manifest.schema.json` 一一对应：

| 字段 | 必填 | 含义 |
| --- | --- | --- |
| `Id` | 是 | 清单 ID，`xihan.<领域>.<名称>`，应用内唯一，重复报 `KERNEL_CAPABILITY_005` |
| `Package` | 否 | 所属包 ID |
| `Version` | 是 | 特性版本，SemVer |
| `Primitives` | 是 | 依赖的原语层大版本，当前为 `1` |
| `Stability` | 是 | `Stable` / `Preview` / `Experimental` / `Internal` / `Deprecated` |
| `Kind` | 是 | `Capability`、`Provider`、`Plugin`、`Host`、`Protocol` |
| `Role` | 否 | `Provider`（缺省）或 `Default`：没有显式 Provider 时才被选中 |
| `Provides` | 否 | 提供的能力与接口版本，`CapabilityDeclaration` |
| `Requires` | 否 | 依赖的能力、接口版本与是否可选，`CapabilityRequirement` |
| `Bindings` | 否 | 需要宿主注入的资源（连接串、端点），`Binding` |
| `Contributes` | 否 | 贡献点：管道中间件、操作、配置节、权限码、健康项 |
| `Activation` | 否 | 激活事件，插件按需激活时使用 |
| `Isolation` / `Permissions` | 插件 | 插件的隔离级别与所需权限 |
| `Entry` | 否 | 入口类型全名 |

一个应用能力包的清单：

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace Shop.Orders;

public sealed class OrdersFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.shop.orders",
        Package = "Shop.Orders",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
            // 可选依赖：没有消息能力时特性进入 Degraded，应用照常启动
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version, optional: true),
        ],
        Contributes = new Contributions { Permissions = ["orders.read", "orders.write"] },
        Entry = typeof(OrdersFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 在这里显式注册服务、权限与操作
    }
}
```

每个子系统的抽象包都有一个 `<领域>Capabilities` 静态类，给出能力 ID 常量与接口版本（`PersistenceCapabilities.Relational`、`CachingCapabilities.Distributed`、`SecurityCapabilities.Authorization` 等），清单里引用常量，不手写字符串。

`Contributes` 是声明：命令行的 `graph` 命令、插件宿主与架构测试读取它，内核不据此注册任何东西。中间件、权限、操作仍要在 `Configure` 里经 `FeatureContext` 登记。`Contributes.Health` 例外地有强约束：声明的每个健康条目都必须由同一个包实现并登记为 `IHealthContributor`，架构测试核对两边一致。

## 能力 ID 与接口版本

能力 ID 的文法是 `<领域>.<能力>`，领域是术语表领域词的小写形态，能力用小写字母、数字与连字符：

| 能力 ID | 含义 | 默认实现 |
| --- | --- | --- |
| `kernel.hosting` 等 `kernel.*` | 宿主、能力图谱、管道、诊断、安全原语、插件抽象 | 内核自身 |
| `persistence.relational` | 仓储与工作单元 | `Persistence.InMemory` |
| `caching.local` / `caching.distributed` | 进程内缓存 / 共享缓存与分布式锁 | `Caching.InMemory` |
| `messaging.queue` / `messaging.stream` | 队列 / 有序流与重放 | `Messaging.InMemory`（只提供队列） |
| `security.authentication` / `security.authorization` | 认证 / 授权 | `Security.InMemory` |
| `multitenancy.context` / `multitenancy.sharding` | 租户上下文 / 租户分片 | `MultiTenancy.InMemory` |
| `localization.resources` | 本地化资源 | `Localization.InMemory` |
| `objectstorage.blob` | 对象存储 | `ObjectStorage.FileSystem` |
| `scheduling.jobs` / `scheduling.leader` | 作业调度 / 领导选举 | `Scheduling.InMemory` |
| `auditing.trail` | 审计写入与查询 | `Auditing.InMemory` |
| `workflow.execution` | 工作流 | `Workflow.InMemory` |
| `ai.chat` / `ai.embedding` / `ai.tools` / `ai.knowledge` | AI 端口 | `ai.tools` 由 `AIToolsFeature` 提供 |
| `protocols.http` 等 `protocols.*` | 协议适配器 | 对应的 `Protocols.*` 包 |

完整的已登记能力见[包总览](../packages/)。引用能力时带整数接口版本，写作 `persistence.relational@1`；求解时接口版本必须相等。接口变更只增加新版本并存（`@2`），不改旧版本的语义，因此按 `@1` 写的 Provider 在 5.x 全程可用。

依赖标为可选（`optional: true`）时，缺失不阻止启动，声明它的特性进入 `Degraded`，在健康端点报告。

## 能力图谱的求解

`AddXiHan` 的回调结束后，`AppBuilder.Build()` 依次执行：

1. 收集清单：内核清单（`xihan.kernel.core`，提供全部 `kernel.*` 能力）加上所有经 `UseFeature` 加入的特性，校验每份清单的格式与 ID 唯一。
2. 为每条 `Requires` 选出唯一的提供者（规则见下一节）；没有被任何特性依赖的能力也按同一规则选出提供者。
3. 把选择结果回填到图谱节点：每个节点知道自己被选中提供哪些能力（`ResolvedNode.SelectedCapabilities`）。声明了能力却一个都没被选中的节点标为 shadowed，不装配、不启动。
4. 拓扑排序：被依赖者在前，`UseFeature` 的顺序只决定同一层次内的次序。
5. 解析资源绑定，校验每个特性的必需绑定（缺失报 `KERNEL_HOSTING_001`）。
6. 按拓扑顺序调用各特性的 `Configure`，再把 `App`、内置中间件、权限表、操作登记表与宿主服务注册进容器。

求解失败时 `Build()` 抛出 `FrameworkException`，错误信息列出冲突双方：

| 错误码 | 情形 |
| --- | --- |
| `KERNEL_CAPABILITY_001` | 必需依赖没有任何清单提供 |
| `KERNEL_CAPABILITY_002` | 依赖成环 |
| `KERNEL_CAPABILITY_003` | 有清单提供同名能力，但接口版本不符 |
| `KERNEL_CAPABILITY_004` | 同一能力有多个候选且没有指定 |
| `KERNEL_CAPABILITY_005` | 清单 ID 重复 |

已解析图在启动日志（Information 级别）里打印，命令行宿主的 `graph` 命令输出同一份内容，`(default)` 标出以默认角色被选中的节点：

```text
xihan.kernel.core 5.0.0-preview.1
  provides kernel.hosting@1
  ...
xihan.persistence.inmemory 5.0.0-preview.1 (default)
  provides persistence.relational@1
  requires kernel.hosting@1 -> xihan.kernel.core
xihan.caching.inmemory 5.0.0-preview.1 (default)
  provides caching.local@1
  provides caching.distributed@1
  requires kernel.hosting@1 -> xihan.kernel.core
```

代码里可以从容器取 `App`，用 `App.Graph`（`ResolvedGraph`）或 `App.DescribeGraph()` 读同样的信息。

## 选择 Provider

同一能力有多个候选时，按以下顺序选出一个：

1. 显式指定：代码里的 `AppBuilder.UseProvider(capabilityId, manifestId)`，其次配置 `XiHan:Providers:<能力 ID>`；两者都有时代码优先。指定的清单不存在或版本不符时，必需依赖直接失败。
2. 唯一的 `Role = Provider` 候选。
3. 唯一的 `Role = Default` 候选。默认实现只在没有任何显式 Provider 时参与竞争。
4. 以上都不成立：报 `KERNEL_CAPABILITY_004` 并列出候选。

因此加入一个 Provider 就会顶替同一能力的默认实现，不需要移除组合包里的 `UseFeature`。默认实现若还提供别的、未被顶替的能力，仍然装配，但只为被选中的能力注册服务：`Caching.InMemory` 同时提供 `caching.local` 与 `caching.distributed`，加入 `Caching.Redis` 后它只负责 `caching.local`。特性在 `Configure` 里用 `context.Node.IsSelectedFor(capabilityId)` 判断自己被选中提供哪些能力。

两个显式 Provider 提供同一能力时必须指定：

```json
{
  "XiHan": {
    "Providers": {
      "caching.distributed": "xihan.caching.redis"
    }
  }
}
```

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RedisCachingFeature>()    // 提供 caching.distributed@1
    .UseFeature<RemoteCachingFeature>()   // 同样提供 caching.distributed@1
    // 与上面的配置等价，代码优先
    .UseProvider("caching.distributed", "xihan.caching.redis"));
```

没有被选中的 `xihan.caching.remote` 一个能力都不提供，在图谱里标为 shadowed，不装配、不启动。

各 Provider 的清单 ID 写在对应包页的信息表里。

## FeatureContext

`Configure` 收到的 `FeatureContext` 给出本特性在图谱里的节点与登记入口：

| 成员 | 用途 |
| --- | --- |
| `Services` | 宿主的 `IServiceCollection`，显式注册服务 |
| `Configuration` | 宿主配置 |
| `Manifest` / `Node` | 本特性的清单与图谱节点（`Node.IsSelectedFor`、`Node.IsDegraded`、`Node.MissingOptional`） |
| `AddMiddleware<T>(stage, placement)` | 把中间件挂到某个阶段，归属本特性的清单 ID，并以单例注册中间件类型 |
| `AddPermission(definition)` | 登记权限码；操作契约与端点引用的权限码要先登记，未登记的在调用时被 Authorize 阶段拒绝 |
| `AddOperation<TOperation, TRequest, TResponse>(...)` | 登记操作契约，通常由生成的 `AddGeneratedOperations()` 代劳 |
| `GetBinding(name)` / `TryGetBinding(name)` | 读取宿主解析出的资源绑定 |
| `Pipeline`、`Permissions`、`Operations`、`Bindings` | 底层的管道构建器、权限表、操作登记表与绑定表 |

资源绑定的来源（配置节 `XiHan:Bindings` 与 Service Binding 目录 `SERVICE_BINDING_ROOT`）见[配置与选项](./configuration)。

## 相关

- [生命周期与健康](./lifecycle)
- [依赖注入](./dependency-injection)
- [执行管道](./pipeline)
- [扩展与二次开发](./extending)：编写 Provider 与能力包
- 规范：RFC-0002《清单》、`conventions/capability-ids.md`
