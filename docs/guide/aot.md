# 序列化与 Native AOT

本章说明 5.0 为什么要求源生成的序列化、哪些地方需要登记 `JsonSerializerContext`、哪些包兼容 Native AOT，以及发布 AOT 应用的步骤。适用于所有写操作、领域事件与缓存的开发者，以及要以 Native AOT 发布的团队。

4.x 的对象映射与序列化封装在 5.0 不再提供，序列化直接使用 System.Text.Json。

## 零反射的约束

原语层、内核与全部抽象包不使用运行时反射：服务显式注册，操作与仓储的登记代码由源生成器产出，配置按键显式读取而不用反射绑定，JSON 编解码只经源生成的类型元数据（`JsonTypeInfo`）。这些包的项目都设置了 `IsAotCompatible`，裁剪与 AOT 分析器的警告在构建时视为错误。

因此应用与框架交换的类型要在编译期登记到 `JsonSerializerContext`：

| 用途 | 登记方式 |
| --- | --- |
| 操作的请求与响应 | 项目里任一 `JsonSerializerContext` 上的 `[JsonSerializable]`；`Kernel.Generators` 找不到时报 `XHG002` |
| 领域事件映射成消息 | `AddDomainEvent<T>(topic, type, jsonTypeInfo, subject)` 的 `JsonTypeInfo<T>` 参数 |
| 共享缓存的值 | 容器里注册带 `TypeInfoResolver` 的 `JsonSerializerOptions`，或直接注册 `ICacheSerializer` |
| 最小 API 端点的返回值 | `ConfigureHttpJsonOptions` 把上下文插到解析链最前面 |

```csharp
using System.Text.Json.Serialization;

namespace Shop.Orders;

public sealed record PlaceOrderRequest(string Name, int Amount);

public sealed record OrderResponse(Guid Id, string Name, int Amount);

public sealed record OrderPlaced(Guid OrderId, DateTimeOffset At);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
[JsonSerializable(typeof(OrderPlaced))]
internal sealed partial class ShopJsonContext : JsonSerializerContext;
```

示例选项的含义：属性名用 camelCase；读取不区分大小写；允许从字符串读数字，因为 HTTP 适配器把路由值与查询串（都是字符串）合并进请求对象。

共享缓存的序列化器按以下优先级确定：容器里已有 `ICacheSerializer` 时用它；否则已注册 `JsonSerializerOptions` 时用它创建 `JsonCacheSerializer`；两者都没有时，非 AOT 的 Provider（如 `Caching.Redis`）退回反射版序列化器。AOT 应用必须走前两种方式，未登记的类型会抛出 `NotSupportedException`，不会静默降级。

## 包的 AOT 兼容性

| 兼容 Native AOT | 不兼容 Native AOT |
| --- | --- |
| `Primitives`、`Kernel`、`Domain` 与全部 `.Abstractions` | `Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` |
| 全部 `InMemory` 默认实现、`ObjectStorage.FileSystem` | `Caching.Redis`、`Caching.Remote` |
| `MultiTenancy.Database`、`Security.Database`、`Localization.Database` 等 Database 包 | `Security.Jwt`、`Security.OpenIdConnect` |
| `Hosting.AspNetCore`、`Hosting.CommandLine` | `Messaging.RabbitMQ`、`Messaging.Kafka`、`ObjectStorage.S3` |
| `Protocols.Http`、`Protocols.Grpc`、`Protocols.Mcp`、`Protocols.CloudEvents` | `AI.OpenAI`、`AI.VectorData` |
| `Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full` | `Plugins.Protocol`、`Plugins.InProcess`、`Plugins.OutOfProcess`、`Plugins.Wasm` |

表中「兼容」以各项目的 `IsAotCompatible` 设置为准，各包页的信息表也标出了这一项。Database 包本身兼容，但它们经仓储访问数据库，实际能否 AOT 取决于应用选中的持久化 Provider：当前的 EF Core 与 SqlSugar Provider 都不兼容，`Persistence.InMemory` 兼容。

`Plugins.InProcess` 是框架里唯一允许反射的地方（加载插件程序集），相关 API 标注了 `RequiresDynamicCode`。

## 发布 AOT 应用

仓库的 `next/samples/SimpleApp` 是 AOT 冒烟项目，项目文件里声明了 `PublishAot`，CI 每次都以 Native AOT 发布它。按它的写法：

1. 只引用兼容 AOT 的包，持久化用 `Persistence.InMemory` 或自己实现兼容 AOT 的 Provider。
2. 项目文件声明 `PublishAot`，并打开最小 API 的请求委托生成器，去掉端点参数绑定里的反射：

   ```xml
   <PropertyGroup>
     <PublishAot>true</PublishAot>
     <EnableRequestDelegateGenerator>true</EnableRequestDelegateGenerator>
   </PropertyGroup>
   ```

   `PublishAot` 要写在项目文件里，不要在命令行传 `-p:PublishAot=true`：命令行属性是全局属性，会一路传给引用的项目。引用了 netstandard2.0 的分析器或生成器项目（如 `Kernel.Generators`）时，这些项目会报 NETSDK1207。

3. 把源生成的上下文放到 HTTP JSON 解析链最前面，端点返回的类型不走反射序列化：

   ```csharp
   builder.Services.ConfigureHttpJsonOptions(options =>
       options.SerializerOptions.TypeInfoResolverChain.Insert(0, ShopJsonContext.Default));
   ```

4. 应用自己的配置类需要绑定时，打开配置绑定源生成器（`<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>`），或像框架一样按键读取。
5. 发布：

   ```bash
   dotnet publish -c Release -o ./publish
   ```

发布时出现的裁剪或 AOT 警告都要处理，不要用 `NoWarn` 压掉；它们通常指向某个未登记的序列化类型或引用了不兼容 AOT 的包。

## 对象映射

5.0 不提供对象映射组件。实体与响应之间的转换写成显式的构造或静态方法，例如 `new OrderResponse(order.Id, order.Name, order.Amount)`；需要大量映射时选用编译期生成代码的映射库，保持零反射。

## 相关

- [操作契约](./operations)
- [消息与事件](./messaging)：领域事件的序列化
- [缓存与分布式锁](./caching)：缓存序列化器
- [部署与打包](./deployment)
