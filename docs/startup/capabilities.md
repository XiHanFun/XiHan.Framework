# 能力图谱与 Provider

这一页讲内核怎样把一堆清单变成一张图：能力 ID 与接口版本的文法、求解的六个步骤、可选依赖缺失时的 `Degraded`、同一能力有多个候选时怎样选出 Provider，以及求解失败时的五个错误码。读完这页，你能看懂 `graph` 命令的每一行，知道加入一个 Provider 包为什么会自动顶替默认实现，也知道两个 Provider 冲突时该在哪里指定。前置知识是[特性与清单](/startup/features)。

## 为什么需要

应用里的依赖如果按包名写死，换一个实现就要改所有引用方；如果完全靠容器的「后注册覆盖先注册」，谁最终生效取决于注册顺序，不可读也不可验证。框架让依赖按能力 ID 声明：业务包只说「我要 `persistence.relational@1`」，不关心谁提供；内核在构建期为每条依赖选出唯一的提供者，默认实现自动让位给显式 Provider，有歧义就在启动前报错并列出候选。结果是一张可打印的图，部署前就能核对。

## 能力 ID 与接口版本

能力 ID 的文法是 `<领域>.<能力>`：领域是术语表领域词的小写形态，能力用小写字母、数字与连字符。引用时带整数接口版本，写作 `persistence.relational@1`；求解时接口版本必须相等。接口变更只增加新版本并存（`@2`），不改旧版本的语义，因此按 `@1` 写的 Provider 在当前大版本内始终可用。

| 能力 ID | 含义 | 默认实现 |
| --- | --- | --- |
| `kernel.hosting`、`kernel.capabilities`、`kernel.pipeline`、`kernel.diagnostics`、`kernel.security`、`kernel.plugins` | 宿主、能力图谱、管道、诊断、安全原语、插件抽象 | 内核自身（清单 `xihan.kernel.core`） |
| `persistence.relational` | 仓储与工作单元 | `Persistence.InMemory` |
| `caching.local`、`caching.distributed` | 进程内缓存、共享缓存与分布式锁 | `Caching.InMemory` |
| `messaging.queue`、`messaging.stream` | 队列、有序流与重放 | `Messaging.InMemory`（只提供队列） |
| `security.authentication`、`security.authorization` | 认证、授权 | `Security.InMemory` |
| `multitenancy.context`、`multitenancy.sharding` | 租户上下文、租户分片 | `MultiTenancy.InMemory` |
| `localization.resources` | 本地化资源 | `Localization.InMemory` |
| `objectstorage.blob` | 对象存储 | `ObjectStorage.FileSystem` |
| `scheduling.jobs`、`scheduling.leader` | 作业调度、领导选举 | `Scheduling.InMemory` |
| `auditing.trail` | 审计写入与查询 | `Auditing.InMemory` |
| `workflow.execution` | 工作流 | `Workflow.InMemory` |
| `ai.chat`、`ai.embedding`、`ai.tools`、`ai.knowledge` | AI 端口 | `ai.tools` 由 `AIToolsFeature` 提供 |
| `protocols.http` 等 `protocols.*` | 协议适配器 | 对应的 `Protocols.*` 包 |

每个子系统的抽象包都有一个 `<领域>Capabilities` 静态类，给出能力 ID 常量与 `Version` 常量。内核的那一个是 `KernelCapabilities`：`Hosting`、`Capabilities`、`Pipeline`、`Diagnostics`、`Security`、`Plugins` 六个常量加 `Version`，还有静态属性 `Manifest`，就是内核自己的清单。清单里只引用常量，不手写字符串。全部已登记的能力与各 Provider 的清单 ID 见[包参考](/packages/)。

## 求解过程

`AddXiHan` 的回调返回后，`AppBuilder.Build()` 依次执行：

1. 收集清单：内核清单 `xihan.kernel.core`（提供全部 `kernel.*` 能力）加上所有经 `UseFeature` 加入的特性，校验每份清单的格式与 ID 唯一。
2. 为每条 `Requires` 选出唯一的提供者（规则见「选择 Provider」）；没有被任何特性依赖的能力也按同一规则选出提供者。
3. 把选择结果回填到图谱节点：每个节点知道自己被选中提供哪些能力（`ResolvedNode.SelectedCapabilities`）。声明了能力却一个都没被选中的节点标为 shadowed，不装配、不启动；实现了 `IShadowAwareFeature` 的特性改调 `ConfigureShadowed`。节点还区分每项选中是不是被要求的（`IsRequestedFor`）：有特性依赖这项能力，或 `XiHan:Providers` / `UseProvider` 指定了提供者，是被要求的；没人依赖也没人指定、只在第 2 步兜底选出的不是。兜底选出照样算选中，图谱不变，特性可据此只为被要求的能力承担跨能力的约束。
4. 拓扑排序：被依赖者在前，`UseFeature` 的顺序只决定同一层次内的次序。
5. 解析资源绑定，校验每个特性的必需绑定（缺失报 `KERNEL_HOSTING_001`）。
6. 按拓扑顺序调用各特性的 `Configure`，再把 `App`、内置中间件、权限表、操作登记表与宿主服务注册进容器。

## 可选依赖与 Degraded

`CapabilityRequirement` 的第三个参数 `optional` 为 `true` 时，这条依赖缺失不阻止启动：特性照常 `Configure` 与启动，但状态是 `Degraded` 而不是 `Ready`，图谱里这条依赖的去向打印为 `(missing, optional)`，健康端点的 `features` 里也能看到。特性在 `Configure` 里用 `context.Node.IsDegraded` 与 `context.Node.MissingOptional` 判断缺了什么，据此跳过相关注册。可选依赖适合「有则增强、无则照常」的关系，比如有消息能力就发事件，没有就只写库。

## 最小示例

下面用 Minimal 组合装配一个把消息能力声明为可选依赖的特性，观察图谱与健康端点怎样报告 `Degraded`。

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace Shop.Notifications;

public sealed class NotificationsFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.shop.notifications",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version, optional: true),
        ],
        Entry = typeof(NotificationsFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 缺少可选依赖时只登记不依赖消息的部分；有消息能力时再登记订阅
        if (context.Node.IsDegraded)
        {
            return;
        }
    }
}
```

```csharp
using Shop.Notifications;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Minimal;

var builder = WebApplication.CreateBuilder(args);

// Minimal 组合没有消息子系统，messaging.queue@1 无人提供
builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<NotificationsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.Run();
```

启动日志里这个节点的依赖去向：

```text
xihan.shop.notifications 1.0.0
  requires kernel.hosting@1 -> xihan.kernel.core
  requires messaging.queue@1 -> (missing, optional)
```

`curl -s localhost:5000/health/ready` 返回 200，应用仍在服务，但状态是 `Degraded`：

```json
{
  "status": "pass",
  "state": "Degraded",
  "features": [
    { "id": "xihan.localization.inmemory", "state": "Ready" },
    { "id": "xihan.shop.notifications", "state": "Degraded" }
  ],
  "contributions": []
}
```

把 `UseMinimalProfile()` 换成 `UseStandardProfile()`，`Messaging.InMemory` 进入图谱，依赖去向变成 `-> xihan.messaging.inmemory`，特性与应用都回到 `Ready`。

## 选择 Provider

同一能力有多个候选时，按以下顺序选出一个：

1. 显式指定：代码里的 `AppBuilder.UseProvider(capabilityId, manifestId)`，其次配置 `XiHan:Providers:<能力 ID>`；两者都有时代码优先。指定的清单不存在或接口版本不符时，必需依赖直接失败。
2. 唯一的 `Role = Provider` 候选。
3. 唯一的 `Role = Default` 候选。默认实现只在没有任何显式 Provider 时参与竞争。
4. 以上都不成立：报 `KERNEL_CAPABILITY_004` 并列出候选。

因此加入一个 Provider 就会顶替同一能力的默认实现，不需要移除组合包里的 `UseFeature`：

```csharp
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()                // 含 Caching.InMemory（default），提供 caching.local 与 caching.distributed
    .UseFeature<RedisCachingFeature>()   // Provider：caching.distributed 改由 Redis 提供
    .UseFeature<HttpProtocolFeature>());
```

默认实现若还提供别的、未被顶替的能力，仍然装配，但只为被选中的能力注册服务。图谱里相应地打出两种标记：

```text
xihan.caching.inmemory <版本> (default)
  provides caching.local@1
  provides caching.distributed@1 (not selected)
  requires kernel.hosting@1 -> xihan.kernel.core
xihan.caching.redis <版本>
  provides caching.distributed@1
  requires kernel.hosting@1 -> xihan.kernel.core
```

Redis 的连接经资源绑定给出，见[缓存读写](/caching/basics)与[资源绑定](/configuration/bindings)。

两个显式 Provider 提供同一能力时必须指定，否则 `Build()` 报 `KERNEL_CAPABILITY_004`。配置与代码两种写法等价，代码优先：

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
    .UseProvider("caching.distributed", "xihan.caching.redis"));
```

没有被选中的 `xihan.caching.remote` 一个能力都不提供，在图谱里标为 `(shadowed)`，不装配、不启动。各 Provider 的清单 ID 写在对应包页的信息表里。

## 冲突与错误码

求解失败时 `Build()` 抛出 `FrameworkException`，消息列出冲突双方的清单 ID：

| 错误码 | 情形 | 怎么办 |
| --- | --- | --- |
| `KERNEL_CAPABILITY_001` | 必需依赖没有任何清单提供 | 加入提供该能力的默认实现或 Provider，或把依赖改为可选 |
| `KERNEL_CAPABILITY_002` | 依赖成环 | 把共享部分抽到第三个特性，让两边都依赖它 |
| `KERNEL_CAPABILITY_003` | 有清单提供同名能力，但接口版本不符 | 升级 Provider 或改依赖的版本号 |
| `KERNEL_CAPABILITY_004` | 同一能力有多个候选且没有指定 | 用 `UseProvider` 或 `XiHan:Providers` 指定 |
| `KERNEL_CAPABILITY_005` | 清单 ID 重复 | 同一特性加入了两次，或两个特性用了同一个 ID |

同一阶段还可能遇到 `KERNEL_HOSTING_001`（必需的资源绑定缺失）与 `KERNEL_VALIDATION_005`（操作契约引用了未登记的权限码），它们不是求解错误，但同样在 `Build()` 里抛出。

## 查看图谱

| 方式 | 输出 |
| --- | --- |
| 启动日志（Information 级别） | `Capability graph:` 后的全部节点 |
| 命令行宿主 `graph` 命令 | 与启动日志相同 |
| `App.DescribeGraph()` | 同上，返回字符串 |
| `App.Graph` | `ResolvedGraph` 对象：`Nodes`（拓扑序）、`Providers`（能力 ID 到提供者节点）、`this[manifestId]`、`Contains(manifestId)`、`Describe()` |

`ResolvedNode` 是图谱里的一个节点，特性在 `Configure` 里经 `context.Node` 拿到自己的那一个：

| 成员 | 含义 |
| --- | --- |
| `Manifest` | 节点的清单 |
| `Dependencies` | 被选中的依赖节点 |
| `SelectedCapabilities` | 本节点被选中提供的能力 ID |
| `IsSelectedFor(capabilityId)` | 是否被选中提供某能力 |
| `IsRequestedFor(capabilityId)` | 这项选中是不是被要求的（有人依赖或有人指定） |
| `IsShadowed` | 声明了能力却一个都没被选中 |
| `IsDegraded`、`MissingOptional` | 是否缺少可选依赖，以及缺了哪些 |

命令行宿主上直接运行：

```bash
dotnet run --project framework/samples/SimpleCli -- graph
```

## 常见问题

### 我只想换掉一个子系统的实现，要动组合包吗

不用。保留 `UseStandardProfile()`，再 `UseFeature<某个 Provider 特性>()` 即可：Provider 的 `Role` 是 `Provider`，默认实现的 `Role` 是 `Default`，前者自动胜出。这也是组合包能保持简单的原因。

### 两个 Provider 都想保留，分别服务不同用途怎么办

能力图谱对一个能力只选一个提供者。需要同时用两个（比如两个对象存储桶、两个缓存实例）时，看该子系统是否支持命名实例，例如对象存储的命名实例 Provider 见[分片、预签名与命名实例](/object-storage/advanced)；没有命名实例机制的子系统，第二个实例作为应用自己的服务注册，不走能力 ID。

### 为什么 Provider 加进来了，它的中间件却没出现在管道里

先看图谱：节点若标 `(shadowed)`，说明它提供的能力都被别的节点拿走了，它根本没有 `Configure`。再看 `(not selected)`：它可能只为被选中的那部分能力登记了中间件。最后看是否被 `XiHan:Providers` 指向了别的清单。

### 接口版本 `@1` 和包版本是一回事吗

不是。`@1` 是能力接口的版本，只在契约不兼容地变化时递增；包版本按发布节奏递增，与接口版本无关。所有框架包共享同一条包版本线，而大多数能力长期停在 `@1`。

### 应用自己的业务包要不要 `Provides`

通常不要。业务包是能力的消费者，只写 `Requires`。只有当你希望别的包按能力 ID 依赖你（比如公司内部的「客户主数据」能力有多种实现）时才声明 `Provides`，并为它定义一个 `<领域>Capabilities` 常量类。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Primitives](/packages/primitives)、[包总览](/packages/)（全部能力 ID 与各 Provider 的清单 ID）
- 下一页：[生命周期与健康](/startup/lifecycle)，看求解出的图谱怎样按顺序启动、停止与上报健康
- 延伸：[写一个 Provider](/extending/provider)、[组合包与选包](/getting-started/profiles)
