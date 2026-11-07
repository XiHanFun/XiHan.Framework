# 23. JSON 序列化与 Native AOT

这一章讲框架为什么要求源生成的序列化、哪些地方需要登记 `JsonSerializerContext`、怎样把上下文插进 HTTP 的解析链、缓存与领域事件对序列化器的要求、哪些包能以 Native AOT 发布，以及发布 AOT 应用的步骤。读完你能把一个用 Standard 组合的应用发布成单个原生可执行文件。前置知识是[操作契约](/web/operations)与[配置模型与选项](/configuration/options)。

## 为什么需要

Native AOT 把应用编译成原生代码：启动快、内存小、不带 JIT。代价是运行期不能再靠反射发现类型，`JsonSerializer.Serialize(obj)` 这种依赖反射元数据的调用会在裁剪后失败或在编译期报警告。只要应用里有一处反射序列化，AOT 发布就会失败或留下隐患。

框架的原语层、内核与全部抽象包都不使用运行期反射：服务显式注册，操作与仓储的登记代码由源生成器产出，配置按键显式读取，JSON 编解码只经源生成的类型元数据（`JsonTypeInfo`）。这些包的项目都设置了 `IsAotCompatible`，裁剪与 AOT 分析器的警告在构建时视为错误。应用要做的只有一件事：把与框架交换的类型登记到 `JsonSerializerContext`，并把上下文交到需要它的地方。框架不提供对象映射与序列化封装，序列化直接使用 System.Text.Json。

## 零反射的约束

应用与框架交换的类型要在编译期登记到 `JsonSerializerContext`，登记方式按用途不同：

| 用途 | 登记方式 |
| --- | --- |
| 操作的请求与响应 | 项目里任一 `JsonSerializerContext` 上的 `[JsonSerializable]`；`Kernel.Generators` 找不到时报 `XHG002` |
| 领域事件映射成消息 | `AddDomainEvent<T>(topic, type, jsonTypeInfo, subject)` 的 `JsonTypeInfo<T>` 参数 |
| 共享缓存的值 | 容器里注册带 `TypeInfoResolver` 的 `JsonSerializerOptions`，或直接注册 `ICacheSerializer` |
| 最小 API 端点的返回值 | `ConfigureHttpJsonOptions` 把上下文插到解析链最前面 |
| 应用自己的选项绑定 | 项目属性 `EnableConfigurationBindingGenerator`，或像框架一样按键读取 |

## 最小示例

我们把一个带下单操作的应用发布成 Native AOT。先写序列化上下文，登记全部与外界交换的类型：

```csharp
// ShopJsonContext.cs
using System.Text.Json.Serialization;

namespace Shop;

public sealed record PlaceOrderRequest(string Name, int Amount);

public sealed record OrderResponse(Guid Id, string Name, int Amount);

public sealed record OrderPlaced(Guid OrderId, DateTimeOffset At);

public sealed record Greeting(string Message);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
[JsonSerializable(typeof(OrderPlaced))]
[JsonSerializable(typeof(Greeting))]
internal sealed partial class ShopJsonContext : JsonSerializerContext;
```

`Program.cs` 把上下文插到 HTTP JSON 解析链最前面，其余与普通应用一样：

```csharp
// Program.cs
using Shop;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

// 源生成的序列化上下文放在解析链最前面，端点返回的类型不走反射序列化
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ShopJsonContext.Default));

builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();
app.MapGet("/greet", (string? name) => new Greeting($"Hello, {name ?? "XiHan"}!"));
app.Run();
```

项目文件声明 AOT 发布并打开最小 API 的请求委托生成器：

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <PublishAot>true</PublishAot>
    <EnableRequestDelegateGenerator>true</EnableRequestDelegateGenerator>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
  </PropertyGroup>
</Project>
```

发布：

```bash
dotnet publish -c Release -o ./publish
```

`./publish` 里是一个原生可执行文件，没有 `.dll`。运行它，`curl localhost:5000/greet?name=Ada` 返回 `{"message":"Hello, Ada!"}`，`POST /orders` 照常返回 201。构建期如果某个请求或响应类型漏登记，`Kernel.Generators` 报 `XHG002` 让编译失败；端点返回了未登记的类型，发布期会出现 IL2026 / IL3050 这类裁剪与 AOT 警告。

## 登记 JsonSerializerContext

示例选项的含义：属性名用 camelCase；读取不区分大小写；允许从字符串读数字，因为 HTTP 适配器把路由值与查询串（都是字符串）合并进请求对象。

- `Kernel.Generators` 从当前项目的 `JsonSerializerContext` 取序列化元数据，并为每个操作生成请求与响应的 JSON Schema。引用的其他程序集里的上下文不参与解析：契约类型放在哪个项目，就在那个项目里登记。
- 嵌套在请求或响应里的类型（集合元素、子对象）不必单独 `[JsonSerializable]`，生成器会随根类型一起生成元数据。
- 多个上下文可以共存，用 `TypeInfoResolverChain` 按顺序组合；同一类型登记在两个上下文里时按链上的顺序取第一个。

## 端点返回值与解析链

操作契约的请求与响应由框架用登记的元数据编解码，不经 HTTP 的 `JsonOptions`。但 `MapGet` 这类最小 API 端点返回的对象由 ASP.NET Core 序列化，走 `Microsoft.AspNetCore.Http.Json.JsonOptions` 的解析链，缺省末尾是反射解析器。`TypeInfoResolverChain.Insert(0, ShopJsonContext.Default)` 把源生成的上下文放到最前面，端点返回的类型先在这里命中；AOT 发布时反射解析器不可用，没有命中的类型会抛出 `NotSupportedException`。`EnableRequestDelegateGenerator` 则让端点参数的绑定在编译期生成，去掉参数绑定里的反射。

## 领域事件与消息

聚合记录的领域事件要映射成消息发布，登记时直接传源生成的 `JsonTypeInfo<T>`：

```csharp
// 在特性的 Configure 里：OrderPlaced 发到 orders 主题，类型 orders.placed，subject 是订单
context.Services.AddDomainEvent<OrderPlaced>("orders", "orders.placed", ShopJsonContext.Default.OrderPlaced, static placed => $"order/{placed.OrderId:N}");
```

`ShopJsonContext.Default.OrderPlaced` 是生成器按 `[JsonSerializable(typeof(OrderPlaced))]` 生成的属性。消息信封的其他字段见[领域事件与发件箱](/messaging/domain-events)。

## 缓存序列化器

跨进程缓存（Redis、Remote）把值序列化后存进字节存储，序列化器在 `AddXiHan` 装配时按以下顺序决定：

1. 容器里已登记的 `ICacheSerializer`。
2. 已登记的 `JsonSerializerOptions`：用它构造 `JsonCacheSerializer`。
3. 两者都没有：非 AOT 的 Provider 退回 `JsonCacheSerializer.CreateReflectionBased()`，它标注了 `RequiresUnreferencedCode` / `RequiresDynamicCode`，AOT 下不可用。

`JsonCacheSerializer` 要求选项带 `TypeInfoResolver`（源生成上下文），没有时构造即抛 `ArgumentException`；上下文里没有登记的类型抛 `NotSupportedException`，不悄悄退回反射。AOT 应用必须走前两种方式，并且要在 `AddXiHan` 之前登记：

```csharp
using System.Text.Json;
using XiHan.Framework.Caching.Abstractions.Serialization;

builder.Services.AddSingleton<ICacheSerializer>(new JsonCacheSerializer(new JsonSerializerOptions { TypeInfoResolver = ShopJsonContext.Default }));
```

内存缓存不序列化，所以只在内存缓存下跑过的代码换成 Redis 后，值类型第一次需要能被序列化；不在上下文里的类型会在那时抛出。缓存的用法见[缓存读写](/caching/basics)。

## 包的 AOT 兼容性

下表以各项目的 `IsAotCompatible` 设置为准，各包参考页的信息表也标出了这一项。

| 子系统 | 兼容 Native AOT | 不兼容 |
| --- | --- | --- |
| 基础 | `Primitives`、`Kernel`、`Domain`、全部 `.Abstractions` | — |
| 宿主与协议 | `Hosting.AspNetCore`、`Hosting.CommandLine`、`Protocols.Http`、`Protocols.Grpc`、`Protocols.Mcp`、`Protocols.CloudEvents` | — |
| 组合包 | `Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full` | — |
| 持久化 | `Persistence.InMemory` | `Persistence.EntityFrameworkCore`（EF Core 运行期构建模型）、`Persistence.SqlSugar`（实体映射与表达式翻译基于反射） |
| 缓存 | `Caching.InMemory`、`Caching.Hybrid` | `Caching.Redis`（StackExchange.Redis 未声明 AOT 兼容）、`Caching.Remote`（无源生成序列化器时退回反射） |
| 消息 | `Messaging.InMemory` | `Messaging.RabbitMQ`（RabbitMQ.Client 未声明 AOT 兼容）、`Messaging.Kafka`（librdkafka 本机库） |
| 安全 | `Security.InMemory`、`Security.Database`、`Security.Ip2Region` | `Security.Jwt`、`Security.OpenIdConnect`（Microsoft.IdentityModel 未声明 AOT 兼容） |
| 多租户、本地化、审计、调度、工作流 | 全部 `InMemory` 与 `Database` 实现、`Scheduling.Kubernetes` | — |
| 对象存储 | `ObjectStorage.FileSystem` | `ObjectStorage.S3`（AWS SDK 请求编组基于反射） |
| 配置与观测 | `Configuration.Nacos`、`Observability.Host`、`Observability.OpenTelemetry` | — |
| AI | `AI.Abstractions` | `AI.OpenAI`、`AI.VectorData`（SDK 未声明 AOT 兼容或基于反射映射） |
| 插件 | — | `Plugins.Protocol`、`Plugins.InProcess`、`Plugins.OutOfProcess`、`Plugins.Wasm`（protobuf 描述符反射、程序集加载或原生绑定） |
| 工具链 | `Kernel.Generators` 与 `Persistence.Generators` 生成的代码 | 生成器、分析器与 `Cli` 本身是编译期或工具组件，不随应用发布 |

Database 包本身兼容，但它们经仓储访问数据库，实际能否 AOT 取决于应用选中的持久化 Provider：当前 EF Core 与 SqlSugar Provider 都不兼容，`Persistence.InMemory` 兼容。`Plugins.InProcess` 是框架里唯一允许反射的地方（加载插件程序集），相关 API 标注了 `RequiresDynamicCode`。

## 发布 AOT 应用

仓库的 `framework/samples/SimpleApp` 是 AOT 冒烟项目，项目文件里声明了 `PublishAot`，CI 每次都以 Native AOT 发布它。按它的写法：

1. 只引用兼容 AOT 的包，持久化用 `Persistence.InMemory` 或自己实现兼容 AOT 的 Provider。
2. 项目文件声明 `PublishAot`，并打开最小 API 的请求委托生成器与配置绑定生成器（见上面的项目文件）。`PublishAot` 要写在项目文件里，不要在命令行传 `-p:PublishAot=true`：命令行属性是全局属性，会一路传给引用的项目，引用了 netstandard2.0 的分析器或生成器项目（如 `Kernel.Generators`）时会报 NETSDK1207。
3. 把源生成的上下文放到 HTTP JSON 解析链最前面。
4. 共享缓存与领域事件按上文登记序列化器与 `JsonTypeInfo`。
5. `dotnet publish -c Release -o ./publish`。

发布时出现的裁剪或 AOT 警告都要处理，不要用 `NoWarn` 压掉；它们通常指向某个未登记的序列化类型或引用了不兼容 AOT 的包。

## 常见问题

### 不发布 AOT，还需要登记 JsonSerializerContext 吗

需要。操作的请求与响应必须登记，否则 `Kernel.Generators` 报 `XHG002`，生成不出 Schema；这是 OpenAPI、gRPC 与 MCP 共用的契约元数据，与是否 AOT 无关。只有最小 API 端点返回值与缓存值在非 AOT 下可以退回反射。

### 为什么框架不提供对象映射

映射库多数在运行期用反射或表达式树，与零反射冲突。实体与响应之间的转换写成显式的构造或静态方法，例如 `new OrderResponse(order.Id, order.Name, order.Amount)`；需要大量映射时选用编译期生成代码的映射库，保持零反射。

### 发布时报 IL2026 或 IL3050 怎么办

看警告指向的调用：多半是某个类型走了反射序列化（端点返回值没进解析链、缓存值没登记）或引用了不兼容 AOT 的包。登记类型或换 Provider，不要 `NoWarn`。

### 请求类型在另一个类库里，怎么登记

`Kernel.Generators` 只解析当前项目里的 `JsonSerializerContext`。把上下文放在声明操作的项目里，`[JsonSerializable]` 可以引用其他程序集的类型。

### 怎样确认应用真的没有反射

以 `PublishAot` 发布并运行一遍全部端点。框架自己的包在构建时就把 AOT 分析器的警告视为错误，应用项目也可以在项目文件里设 `IsAotCompatible` 让分析器在每次构建时检查。

## 相关包与下一步

- [Kernel.Generators](/packages/kernel-generators)：`XHG002` 与 Schema 生成
- [Caching.Abstractions](/packages/caching-abstractions)：`ICacheSerializer` 与 `JsonCacheSerializer`
- [Messaging.Abstractions](/packages/messaging-abstractions)：`AddDomainEvent`
- [Hosting.AspNetCore](/packages/hosting-aspnetcore)：Web 宿主的 AOT 兼容性
- 下一章：[24.1 隔离级别与清单](/plugins/overview)
