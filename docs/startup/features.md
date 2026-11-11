# 特性与清单

这一页讲框架装配的最小单元：特性（`IFeature`）与它随身携带的清单（`Manifest`）。读完这页，你能写出一个完整的应用能力包：在清单里声明身份、依赖与贡献，在 `Configure` 里登记服务、权限、中间件与操作，并知道 `FeatureContext` 上的每个入口该在什么时候用。前置知识是[装配入口](/startup/bootstrap)。

## 为什么需要

没有清单的模块只能靠「先引用谁、先注册谁」来表达依赖，换一个实现要改调用方，缺少一个依赖要等运行到那一行才知道。框架让每个特性自带一份结构化清单：提供哪些能力、依赖哪些能力、要哪些资源、贡献了什么。内核在构建期读清单求解依赖，缺失、成环、冲突都在启动前报出来；`Configure` 则只做一件事，显式登记。不扫描程序集、不靠标记接口、不靠拦截器，哪些东西进了容器，一眼能从代码里看出来。

## 特性

特性是实现 `IFeature` 的类，只有两个成员：

| 成员 | 作用 |
| --- | --- |
| `Manifest Manifest { get; }` | 清单：身份、提供与依赖的能力、资源绑定、贡献点 |
| `void Configure(FeatureContext context)` | 注册服务、中间件、权限与操作；按能力图谱的拓扑顺序调用一次 |

两个派生接口按需实现：

| 接口 | 多出的成员 | 用途 |
| --- | --- | --- |
| `IHostedFeature` | `StartAsync(IServiceProvider, CancellationToken)`、`StopAsync(CancellationToken)` | 启动时建立连接、预热，停止时释放；受启动与停止超时约束，见[生命周期与健康](/startup/lifecycle) |
| `IShadowAwareFeature` | `ConfigureShadowed(FeatureContext)` | 特性声明了能力却一个都没被选中（shadowed）时，内核改调它而不是 `Configure`，只登记与能力选择无关的服务，见[能力图谱与 Provider](/startup/capabilities) |

特性没有基类，内核也不扫描程序集。应用用 `UseFeature<T>()` 显式加入每一个特性（要求公开的无参构造函数），或用 `UseFeature(instance)` 加入已创建的实例。能力包就是包含一个或多个特性的 NuGet 包：框架里每个子系统的默认实现与 Provider 都是能力包，应用自己的业务模块也写成能力包。

## 清单

清单是装配的事实源，内核在构建期用 `Manifest.Validate()` 校验格式。清单是手写的，字段与 XiHan 规范的 `manifest.schema.json` 一一对应：

| 字段 | 必填 | 类型 | 含义 |
| --- | --- | --- | --- |
| `Id` | 是 | `string` | 清单 ID，`xihan.<领域>.<名称>`，应用内唯一，重复报 `KERNEL_CAPABILITY_005` |
| `Package` | 否 | `string?` | 所属包 ID |
| `Version` | 是 | `string` | 特性版本，SemVer |
| `Primitives` | 是 | `int` | 依赖的原语层大版本，当前为 `1` |
| `Stability` | 是 | `Stability` | `Stable`、`Preview`、`Experimental`、`Internal`、`Deprecated` |
| `Kind` | 是 | `ManifestKind` | 见下表 |
| `Role` | 否 | `ManifestRole` | `Provider`（缺省）或 `Default` |
| `Provides` | 否 | `CapabilityDeclaration` 列表 | 提供的能力与接口版本，`new CapabilityDeclaration(id, version)` |
| `Requires` | 否 | `CapabilityRequirement` 列表 | 依赖的能力、接口版本与是否可选，`new CapabilityRequirement(id, version) { Optional = true }` |
| `Bindings` | 否 | `Binding` 列表 | 需要宿主注入的资源（连接串、端点），`new Binding(name, type) { Required = false }`（缺省必需），见[资源绑定](/configuration/bindings) |
| `Contributes` | 否 | `Contributions` | 贡献点，见下一节 |
| `Activation` | 否 | `string` 列表 | 激活事件，插件按需激活时使用 |
| `Isolation` | 插件 | `PluginIsolation?` | `InProcess`、`Wasm`、`OutOfProcess`，见[隔离级别与清单](/plugins/overview) |
| `Permissions` | 插件 | `string` 列表 | 插件所需的权限 |
| `Entry` | 否 | `string?` | 入口类型全名，通常写 `typeof(当前特性).FullName` |

`Kind` 说明清单是什么东西，`Role` 说明它在同名能力的竞争里处于什么位置：

| `ManifestKind` | 含义 |
| --- | --- |
| `Capability` | 业务能力包或抽象包：声明依赖、登记操作与服务 |
| `Provider` | 某个能力的实现，包括框架的默认实现与外部 Provider |
| `Plugin` | 插件 |
| `Host` | 宿主 |
| `Protocol` | 协议适配器 |

| `ManifestRole` | 含义 |
| --- | --- |
| `Provider` | 缺省；参与同名能力的竞争，加入即顶替默认实现 |
| `Default` | 默认实现；只在没有任何显式 Provider 时被选中 |

清单里引用能力时用各抽象包的 `<领域>Capabilities` 常量，不手写字符串：`KernelCapabilities.Hosting`、`PersistenceCapabilities.Relational`、`MessagingCapabilities.Queue`、`SecurityCapabilities.Authorization`、`MultiTenancyCapabilities.Context` 等，接口版本是同一个类的 `Version` 常量。能力 ID 的完整清单见[能力图谱与 Provider](/startup/capabilities)。

## Contributes 只是声明

`Contributions` 有五个成员，都是「告诉别人我贡献了什么」：

| 成员 | 类型 | 内容 |
| --- | --- | --- |
| `Pipeline` | `PipelineContribution` 列表 | 中间件类型全名、阶段、`Before` / `After` 约束 |
| `Operations` | `string` 列表 | 操作 ID |
| `Config` | `ConfigContribution?` | 读取的配置节与可选的 Schema |
| `Permissions` | `string` 列表 | 权限码 |
| `Health` | `string` 列表 | 健康条目名 |

命令行宿主的 `graph` 命令、插件宿主与架构测试读取它，内核不据此注册任何东西：中间件、权限、操作仍要在 `Configure` 里经 `FeatureContext` 登记，两边应保持一致。`Health` 例外地有强约束：声明的每个健康条目都必须由同一个包实现并登记为 `IHealthContributor`，框架包的架构测试核对两边一致；应用不受这条测试约束，但建议同样声明。

## FeatureContext

`Configure` 收到的 `FeatureContext` 给出本特性在图谱里的节点与全部登记入口：

| 成员 | 用途 |
| --- | --- |
| `Services` | 宿主的 `IServiceCollection`，显式注册服务，见[依赖注入](/startup/dependency-injection) |
| `Configuration` | 宿主配置 |
| `Manifest`、`Node` | 本特性的清单与图谱节点：`Node.IsSelectedFor(能力)`、`Node.IsRequestedFor(能力)`、`Node.IsDegraded`、`Node.MissingOptional`、`Node.IsShadowed` |
| `AddMiddleware<T>(stage, placement)` | 把中间件挂到某个阶段，归属本特性的清单 ID，并以单例注册中间件类型，见[执行管道](/startup/pipeline) |
| `AddPermission(PermissionDefinition)` | 登记权限码；操作契约引用的权限码在装配完成时核对，未登记即启动失败（`KERNEL_VALIDATION_005`）；端点引用的权限码在宿主建请求管道时核对（`SECURITY_VALIDATION_004`） |
| `AddOperation<TOperation, TRequest, TResponse>(contract, requestInfo, responseInfo, schema)` | 登记操作契约，通常由生成的 `AddGeneratedOperations()` 代劳，见[操作契约](/web/operations) |
| `AddStartupCheck(Action<IServiceProvider>)` | 登记要等容器建好才能做的装配核对，在任何特性启动之前执行，抛出即终止启动，见[生命周期与健康](/startup/lifecycle) |
| `GetBinding(name)`、`FindBinding(name)` | 读取宿主解析出的资源绑定，后者返回 `Maybe<BindingValue>` |
| `Pipeline`、`Permissions`、`Operations`、`Bindings` | 底层的管道构建器、权限表、操作登记表与绑定表 |

## 最小示例

下面写一个订单能力包：一个带权限的操作、一个单例服务、两个权限码，并把消息能力声明为可选依赖。四个文件放在同一个项目里，项目引用 `Profiles.Standard`、`Protocols.Http` 与 `Kernel.Generators`（以分析器方式）。

操作契约（`PlaceOrder.cs`）：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace Shop.Orders;

[Operation(Id, Stability = Stability.Preview, Expose = Expose.Http, Permission = "orders.write")]
public sealed class PlaceOrder : IOperation<PlaceOrderRequest, PlaceOrderResponse>
{
    public const string Id = "orders.create";

    public ValueTask<Result<PlaceOrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new PlaceOrderResponse(Guid.NewGuid(), request.Name, request.Amount)));
}

public sealed record PlaceOrderRequest(string Name, int Amount);

public sealed record PlaceOrderResponse(Guid Id, string Name, int Amount);
```

序列化上下文（`ShopJsonContext.cs`），`Kernel.Generators` 从这里取类型元数据：

```csharp
using System.Text.Json.Serialization;

namespace Shop.Orders;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(PlaceOrderResponse))]
internal sealed partial class ShopJsonContext : JsonSerializerContext;
```

特性（`OrdersFeature.cs`）：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

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
            // 操作带权限，要求授权能力在场；装配了 Standard 组合时由 Security.InMemory 提供
            new CapabilityRequirement(SecurityCapabilities.Authorization, SecurityCapabilities.Version),
            // 可选依赖：没有消息能力时特性进入 Degraded，应用照常启动
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version) { Optional = true },
        ],
        Contributes = new Contributions
        {
            Permissions = ["orders.read", "orders.write"],
            Operations = [PlaceOrder.Id],
        },
        Entry = typeof(OrdersFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        context.Services.AddSingleton<OrderCounter>();

        // 权限码在这里登记；操作契约引用了没登记的权限码，装配完成时启动失败（KERNEL_VALIDATION_005）
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));

        // 登记代码由 Kernel.Generators 从 [Operation] 生成，含请求与响应的 JSON Schema
        context.AddGeneratedOperations();
    }
}

public sealed class OrderCounter
{
    public int Placed { get; set; }
}
```

`Program.cs`：

```csharp
using Shop.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();
app.Run();
```

运行后，启动日志的图谱里多出一个节点，三条依赖都有去向（Standard 组合包含 `Messaging.InMemory`，所以可选依赖也被满足）：

```text
xihan.shop.orders 1.0.0
  requires kernel.hosting@1 -> xihan.kernel.core
  requires security.authorization@1 -> xihan.security.inmemory
  requires messaging.queue@1 -> xihan.messaging.inmemory
```

不带凭据调用 `POST /orders`，Authorize 阶段按契约的 `Permission` 判定，返回 401、错误码 `SECURITY_DENIED_001`；在 `XiHan:Security:InMemory` 下配置一个持有 `orders.write` 的主体后，带 `Authorization: ApiKey <值>` 再调用得到 201 与订单 JSON。配置写法见[授权与权限](/security/authorization)。

## 进阶

### 一个真实能力包还登记什么

仓库 `framework/samples/SimpleApp` 的 `OrdersFeature` 比上面多三件事，都是在 `Configure` 里一行一个地显式登记：

```csharp
// 仓储登记由 Persistence.Generators 从实体类生成（Order : IAggregateRoot<Guid>）
context.Services.AddGeneratedRepositories();
// 聚合记录的 OrderPlaced 在工作单元提交前发到 orders 主题；装配了发件箱时与订单写入同事务
context.Services.AddDomainEvent<OrderPlaced>(Topic, "orders.placed", SampleJsonContext.Default.OrderPlaced, static placed => $"order/{placed.OrderId:N}");
context.Services.AddMessageHandler<OrderPlacedHandler>(Topic);
```

它的清单因此多依赖 `persistence.relational` 与 `multitenancy.context` 两个能力。仓储、领域事件与消息处理器的写法分别见[仓储读写](/persistence/repositories)、[领域事件与发件箱](/messaging/domain-events)与[发布与订阅](/messaging/pubsub)。

### 只为被选中的能力登记

一个特性可以同时提供几个能力，例如 `Caching.InMemory` 提供 `caching.local` 与 `caching.distributed`。加入 Redis Provider 后它只负责前者，这时它在 `Configure` 里用 `context.Node.IsSelectedFor(CachingCapabilities.Distributed)` 判断要不要注册分布式缓存的服务。自己写 Provider 时照这个做法，见[写一个 Provider](/extending/provider)。

### 需要启动与停止逻辑

把 `IFeature` 换成 `IHostedFeature`，在 `StartAsync` 里从根容器取服务建立连接，在 `StopAsync` 里释放。每个 `StartAsync` 受 `XiHan:Kernel:StartTimeoutSeconds` 约束，超时或抛出按失败策略处理。完整示例与状态机见[生命周期与健康](/startup/lifecycle)。

### 需要外部资源

数据库连接串、对象存储端点这类值不写进代码，清单的 `Bindings` 声明名字与类型，宿主从配置节 `XiHan:Bindings` 或 Service Binding 目录解析，`Configure` 里用 `context.GetBinding(name).Value` 读取；必需绑定缺失时 `Build()` 报 `KERNEL_HOSTING_001`。见[资源绑定](/configuration/bindings)。

## 常见问题

### 特性一定要有无参构造函数吗

`UseFeature<T>()` 需要，因为内核要自己创建实例。需要构造参数（比如按环境传选项）时，自己创建后用 `UseFeature(instance)` 加入。注意特性实例在容器建好之前就存在，构造函数里不能依赖容器服务；运行期依赖在 `IHostedFeature.StartAsync` 的 `IServiceProvider` 里取。

### 清单 ID 怎么取

文法是 `xihan.<领域>.<名称>`，全小写。应用的业务包用自己的领域词，如 `xihan.shop.orders`；框架包按子系统与机制命名，如 `xihan.caching.redis`。ID 在一个应用里唯一，也是日志、图谱与管道归属里识别这个特性的名字。

### `Contributes` 不写会怎样

装配不受影响，`Configure` 里登记的东西照常生效。少的是可见性：`graph` 命令看不到你的权限与操作，插件宿主与架构测试也无法核对。建议声明，保持与 `Configure` 一致。

### 一个包可以有多个特性吗

可以。每个特性有自己的清单与 `Configure`，各自 `UseFeature` 加入。把可独立选用的部分拆成单独的特性（例如一个子系统的「核心」与「可选的预签名地址」），应用只装配需要的那几个。

### 我能用反射扫描程序集自动 `UseFeature` 吗

框架不提供，也不建议。显式列出特性是装配可读、可裁剪、可 Native AOT 发布的前提。要少写几行就用组合包，或者自己写一个扩展方法把一组 `UseFeature` 封起来。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Primitives](/packages/primitives)、[Kernel.Generators](/packages/kernel-generators)
- 下一页：[能力图谱与 Provider](/startup/capabilities)，看清单里的 `Requires` 与 `Provides` 怎样变成一张图
- 延伸：[能力包与中间件](/extending/feature)、[操作契约](/web/operations)
