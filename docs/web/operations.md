# 操作契约

这页讲框架对外暴露业务的方式：用 `[Operation]` 与 `IOperation<TRequest, TResponse>` 把一个业务动作声明成操作，由源生成器产出登记代码与 JSON Schema，再由协议适配器把同一份契约发布到 HTTP、gRPC、MCP 与命令行。读完这页，我们能写出一个带权限要求的操作，让它出现在 `POST /orders` 上，并且知道一次调用在管道里经过了哪些阶段。

前置知识：[特性与清单](/startup/features)、[执行管道](/startup/pipeline)与 [ASP.NET Core 宿主](/web/hosting)。

## 为什么需要

传统写法里，控制器方法既是业务入口又是 HTTP 细节的载体：路由、状态码、模型绑定、权限特性都写在上面。要再暴露给 gRPC 或让大模型调用，就得再写一套入口，几套入口的校验与授权规则很容易不一致。

框架把业务入口从协议里抽出来：一个操作就是一个类，只声明操作 ID、权限与暴露的协议，输入输出是普通的记录类型。路由、protobuf 消息、MCP tool 的输入 Schema 都由适配器从这一份契约推导，校验与授权在管道里做一次，对每种协议都成立。路由不从方法名推导，HTTP 上长什么样由操作 ID 与请求的形状决定。

## 操作 ID

操作 ID 的文法是 `<resource>.<verb>`：资源是小写复数名词，多个词用连字符连接；动词是小写动词。

```text
orders.create    orders.get    orders.list    order-lines.delete    orders.cancel
```

协议适配器从操作 ID 推导各协议上的名字，推导规则可以按操作覆盖：

| 动词 | HTTP | gRPC | MCP tool |
| --- | --- | --- | --- |
| `create` | `POST /{resource}`（201） | `{Resource}/Create` | `{resource}_create` |
| `get` | `GET /{resource}/{id}` | `{Resource}/Get` | `{resource}_get` |
| `list` | `GET /{resource}` | `{Resource}/List` | `{resource}_list` |
| `update` | `PUT /{resource}/{id}` | `{Resource}/Update` | `{resource}_update` |
| `delete` | `DELETE /{resource}/{id}` | `{Resource}/Delete` | `{resource}_delete` |
| 其他 | `POST /{resource}/{id}:{verb}` 或 `POST /{resource}:{verb}` | `{Resource}/{Verb}` | `{resource}_{verb}` |

命令行上是 `<resource> <verb> --字段 值`。请求类型是否声明 `id` 属性、是否带嵌套对象会改变 HTTP 路由的形态，完整规则见 [HTTP 路由与载荷](/web/routing)。

## 声明操作的两个部件

一个操作由两部分组成：`[Operation(Id, ...)]` 是契约，声明操作 ID、稳定性、暴露的协议、权限或策略与是否只读；`IOperation<TRequest, TResponse>` 是实现，`HandleAsync(TRequest request, InvocationContext context)` 返回 `ValueTask<Result<TResponse>>`。

`[Operation]` 的属性：

| 属性 | 缺省 | 说明 |
| --- | --- | --- |
| `Id` | 必填 | 操作 ID |
| `Stability` | `Stable` | 稳定性；`Internal` 的操作不对外发布 |
| `Expose` | 程序集级缺省，再无则 `Expose.All` | 暴露给哪些协议，见下表 |
| `Permission` | 无 | 调用所需的权限码，Authorize 阶段判定 |
| `Policy` | 无 | 调用所需的授权策略名，与 `Permission` 二选一 |
| `ReadOnly` | `false` | 声明非标准动词的只读操作（如 `tree`），`create` / `update` / `delete` 不能声明 |

`Expose` 是标志位枚举：

| 值 | 发布到 |
| --- | --- |
| `Http` | `Protocols.Http`，含 OpenAPI |
| `Grpc` | `Protocols.Grpc`，含 `/operations.proto` |
| `Mcp` | `Protocols.Mcp` 的 tool |
| `CommandLine` | `Hosting.CommandLine` 的命令 |
| `Ai` | `ai.tools@1` 目录里的模型工具 |
| `All` / `None` | 5.0 的五个协议 / 都不发布（仍可在进程内经调用器调用）；`All` 的值冻结，框架以后新增的协议不在其中，要在新协议上发布须显式开启（ADR-0136） |

`HandleAsync` 的约定：

- 返回 `Result<TResponse>`，成功值不能为 `null`；可恢复的失败返回 `Error`，不抛异常，写法见[结果与错误处理](/web/errors)。
- 取消令牌用 `context.CancellationToken`，它同时受调用方取消与截止时间约束。
- 操作类按作用域注册，构造函数注入所需服务；每次调用一个新实例。

## 最小示例

下面以示例应用的订单能力为底：一个 `orders.create` 操作，要求权限 `orders.write`，暴露给 HTTP、gRPC 与 MCP。项目要引用 `XiHan.Framework.Kernel.Generators`（分析器引用，不进运行时依赖），用 NuGet 包时按分析器引用即可：

```bash
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
```

操作类与请求、响应类型：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace Shop.Orders;

[Operation(Id, Stability = Stability.Preview, Expose = Expose.Http | Expose.Grpc | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(IRepository<Order, Guid> orders, IIdGenerator<Guid> ids) : IOperation<PlaceOrderRequest, OrderResponse>
{
    public const string Id = "orders.create";

    public async ValueTask<Result<OrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
    {
        var order = new Order { Id = ids.NewId(), Name = request.Name, Amount = request.Amount };

        // 仓储返回 Result：失败直接作为操作结果，由宿主翻译成协议上的错误
        var added = await orders.AddAsync(order, context.CancellationToken);
        return added.IsFailure ? added.Error! : new OrderResponse(order.Id, order.Name, order.Amount);
    }
}

public sealed record PlaceOrderRequest(string Name, int Amount);

public sealed record OrderResponse(Guid Id, string Name, int Amount);
```

`Order` 是实现了实体协议的聚合根，写法见[领域模型](/persistence/domain)；仓储与标识生成器见[仓储读写](/persistence/repositories)与[标识生成](/persistence/identifiers)。

请求与响应类型登记进项目的 `JsonSerializerContext`，生成器从这里取序列化元数据：

```csharp
using System.Text.Json.Serialization;

namespace Shop.Orders;

// camelCase 与不区分大小写：HTTP 适配器把路由值、查询串与请求体合并成 JSON 对象交给操作；
// 允许从字符串读数字：路由值与查询串都是字符串
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
internal sealed partial class OrdersJsonContext : JsonSerializerContext;
```

特性在 `Configure` 里登记权限码与操作。清单按能力 ID 声明依赖，写法见[特性与清单](/startup/features)：

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

namespace Shop.Orders;

public sealed class OrdersFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "shop.orders",
        Package = "Shop",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
            new CapabilityRequirement(SecurityCapabilities.Authorization, SecurityCapabilities.Version),
        ],
        Contributes = new Contributions { Permissions = ["orders.write"] },
        Entry = typeof(OrdersFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 权限码先登记，操作契约才能引用它；没登记的权限码让装配失败
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));
        // 登记代码由 Kernel.Generators 从 [Operation] 生成，含请求 / 响应 JSON Schema
        context.AddGeneratedOperations();
    }
}
```

最后在 `Program.cs` 里装配特性与 HTTP 适配器，把操作映射成端点：

```csharp
using Shop.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, OrdersJsonContext.Default));
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();   // orders.create → POST /orders
app.Run();
```

Standard 组合带内存的 ApiKey 认证与授予，在 `appsettings.json` 里给一个主体与一条角色授予（配置的全貌见[认证](/security/authentication)与 [RBAC 授予与条件](/security/grants)）：

```json
{ "XiHan": { "Security": { "InMemory": {
  "Principals": { "alice": { "ApiKey": "dev-alice-key", "DisplayName": "Alice", "Roles": "editor" } },
  "Grants": { "role": { "editor": "orders.write" } }
} } } }
```

运行后以 `alice` 的 ApiKey 下单：

```bash
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-alice-key" \
  -H "Content-Type: application/json" -d '{"name":"widget","amount":3}'
```

得到 201 与响应类型的 JSON：`{"id":"...","name":"widget","amount":3}`。不带 `Authorization` 头时得到 401 `SECURITY_DENIED_001`；换一个角色里没有 `orders.write` 的主体调用，得到 403 `SECURITY_DENIED_004`。

## 程序集级缺省

不写 `Expose` 时取程序集级缺省，一个项目只写一次：

```csharp
using XiHan.Framework.Kernel.Operations;

[assembly: OperationDefaults(Expose = Expose.Http | Expose.Grpc)]
```

既没写 `Expose` 又没有程序集级缺省时，操作隐式地暴露给全部协议。生成器诊断 `XHG004` 能报告这种依赖隐式 `Expose.All` 的操作；它缺省关闭，在 `.editorconfig` 里写 `dotnet_diagnostic.XHG004.severity = error` 即强制每个操作写明。

## 只读操作

只读由契约推出：动词是 `get` 或 `list`，或以 `ReadOnly = true` 声明的非标准动词，`OperationContract.IsReadOnly` 为真。HTTP 适配器据此让没有嵌套对象的只读自定义动词使用 `GET`，MCP 适配器据此给出只读提示。

装配了持久化时，Transaction 阶段为只读操作开只读工作单元：不开数据库事务，处理器里的新增、更新、删除返回 `PERSISTENCE_UNEXPECTED_001`（操作实现的缺陷，HTTP 500）。只读操作要写数据（记访问次数、补建缺失的行），就不要声明只读，见[工作单元与事务](/persistence/unit-of-work)。

## 登记序列化类型与 Schema

`Kernel.Generators` 从 `JsonSerializerContext` 取序列化元数据，并为每个操作生成请求与响应的 JSON Schema（draft 2020-12）。属性名按序列化上下文的命名策略；不可空且没有默认值的属性进 `required`；嵌套对象进 `$defs`；封闭泛型的定义名带实参名（`PageResultOfOrderResponse`）。请求类型上的 DataAnnotations 折进 Schema，由 Validate 阶段执行：

| 注解 | Schema |
| --- | --- |
| `[Required]` | 进 `required` 并去掉可空；字符串另加 `minLength: 1`（`AllowEmptyStrings = true` 除外） |
| `[Range]` | `minimum` / `maximum`，开区间用 `exclusiveMinimum` / `exclusiveMaximum` |
| `[StringLength]`、`[MinLength]`、`[MaxLength]`、`[Length]` | 字符串的 `minLength` / `maxLength`，集合的 `minItems` / `maxItems` |
| `[RegularExpression]` | `pattern` |

其他注解（如 `[EmailAddress]`）不折进 Schema，需要时在请求类型上实现 `IValidatableObject`，见[数据校验](/web/validation)。项目开启 `GenerateDocumentationFile` 时，类型与属性的 XML `<summary>`（位置记录取主构造参数的 `<param>`）折进 Schema 的 `description`，OpenAPI 与 MCP 的字段说明由此而来。

## 登记操作与生成器诊断

特性在 `Configure` 里调用生成的 `AddGeneratedOperations()`，一次登记项目里全部操作；也可以按操作调用生成的 `Add<类名>()`。生成的代码在项目 `RootNamespace` 下的 `internal` 静态类 `GeneratedOperations` 里，只供本项目的特性调用。生成器的诊断：

| 诊断 | 情形 |
| --- | --- |
| `XHG001` | 标注 `[Operation]` 的类没有实现 `IOperation<TRequest, TResponse>` |
| `XHG002` | 请求或响应类型没有被任何 `JsonSerializerContext` 登记 |
| `XHG003` | 操作 ID 不符合 `<resource>.<verb>` 文法 |
| `XHG004` | 缺省关闭：操作依赖隐式的 `Expose.All` |
| `XHG005` | `create` / `update` / `delete` 声明了 `ReadOnly = true` |
| `XHG006` | 警告：字典的键类型 System.Text.Json 不支持，运行期序列化会抛出 |

不用生成器时用 `context.AddOperation<TOperation, TRequest, TResponse>(contract, requestInfo, responseInfo)` 手写登记，契约（`OperationContract`）、序列化元数据与 Schema 自己给出。

操作登记表 `OperationRegistry` 的规则：ID 必须唯一（重复报 `KERNEL_OPERATION_001`），容量 10000（满了报 `KERNEL_OPERATION_002`），都是配置期异常；`Permission` 引用的权限码要用 `AddPermission` 登记（可以在别的特性里，只要装配完成时已有），装配完成时一次核对，未登记即 `KERNEL_VALIDATION_005`、启动失败并一次列出全部；`Policy` 引用的策略在授权能力启动时核对，不存在即 `SECURITY_VALIDATION_004`、启动失败。装配完成之后登记的操作（插件握手之后）在登记时核对；插件的代理操作按归属成组加载与卸载，见[隔离级别与清单](/plugins/overview)。策略的核对见[授权与权限](/security/authorization)。

## 标题与描述

面向人的标题与描述不写在契约里，放在消息目录，按约定键查找：

| 键 | 用途 |
| --- | --- |
| `<操作 ID>.title` | 短语，如「下单」；MCP tool 的 `title`、OpenAPI 的 `summary` |
| `<操作 ID>.description` | 行为与规则；MCP tool 的 `description`、OpenAPI 的 `description` |

键里操作 ID 的 `-` 换成 `_`（`order_lines.list.description`）。描述不写授权要求，适配器按契约统一呈现。文案写在应用的 `Localization/<区域>.json` 里，登记方式见 [ASP.NET Core 宿主](/web/hosting)的「区域协商与本地化」，消息键的约定见[全球化和本地化](/common/localization)：

```json
{
  "orders.create.title": "下单",
  "orders.create.description": "按名称与金额下单。"
}
```

## 调用过程

协议适配器把线上消息翻译成 `OperationRequest`（操作 ID 加 UTF-8 JSON 载荷）交给 `IOperationInvoker`，调用器把操作描述与载荷放进调用上下文，让调用走完整条管道：

1. Authorize 阶段在端点没有显式授权要求时，按契约的 `Permission` / `Policy` 判定。
2. Validate 阶段按请求 Schema 校验载荷、解码成请求对象、执行 `IValidatableObject`。
3. Transaction 阶段开启工作单元。
4. Handle 阶段执行 `HandleAsync`，编码响应。

中间件与端点里可以从上下文读当前操作：`context.FindOperation()` 得到 `OperationDescriptor`（契约、归属特性、请求与响应类型、Schema），`context.FindOperationRequest()` 得到解码后的请求对象，`context.FindOperationPayload()` 得到原始载荷。三个方法都返回 `Maybe<T>`，当前调用不是操作时为无值。

## 在进程内调用操作

后台任务、测试或另一个协议入口需要调用操作时，经 `IOperationInvoker` 走同一条管道。类型化入口不经 JSON 编解码：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace Shop.Orders;

public sealed class NightlyOrderSeeder(IServiceScopeFactory scopes)
{
    public async Task<Result<OrderResponse>> SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken);
        var invoker = scope.ServiceProvider.GetRequiredService<IOperationInvoker>();
        return await invoker.InvokeAsync<PlaceOrderRequest, OrderResponse>(context, CreateOrder.Id, new PlaceOrderRequest("seed", 1));
    }
}
```

| 方法 | 语义 |
| --- | --- |
| `InvokeAsync(context, OperationRequest)` | 以 JSON 载荷调用，经完整管道；返回 `OperationResponse`（结果加响应载荷） |
| `InvokeAsync<TRequest, TResponse>(context, id, request)` | 类型化调用，经完整管道；请求在 Validate 阶段按序列化元数据编码一次再校验 |
| `ExecuteAsync(...)` 两种重载 | 调用方已在管道内（如宿主端点）时直接执行，不再经过管道、不校验 |

这样构造的上下文没有凭据与租户：经过 Authenticate 阶段时是匿名调用，带 `Permission` 的操作会被拒绝。后台调用需要身份时，用 `context.SetCredential(new AuthenticationCredential(scheme, value))`（`Security.Abstractions`）放入凭据；需要租户时在 `App.CreateContext` 的 `baggage` 参数里给出 `xihan.tenant`（`BaggageKeys.Tenant`），由 Tenant 阶段对照租户存储核对。细节见[认证](/security/authentication)与[租户解析与隔离](/multi-tenancy/resolution)。

调用器自己的错误码：操作没有登记是 `KERNEL_NOTFOUND_001`（调用不进管道）；载荷不是 JSON、为 `null` 或类型不匹配是 `KERNEL_VALIDATION_001`（`reason` 是 JSON 路径，如 `$.quantity`）；类型化调用的请求或响应类型与登记不符是 `KERNEL_VALIDATION_002`；不满足请求 Schema 与自校验失败分别是 `KERNEL_VALIDATION_003` 与 `KERNEL_VALIDATION_004`。

## 常见问题

### 一个操作能不能有多个 HTTP 路由，或者按方法名自动生成一组端点

不能。一个操作就是一个类、一条操作 ID，HTTP 上对应一条路由。需要两个动作就写两个操作（`orders.create` 与 `orders.cancel`），它们各有自己的权限要求与 Schema。要换路由用配置覆盖，见 [HTTP 路由与载荷](/web/routing)。

### `HandleAsync` 里能不能抛异常表示业务失败

不要。可恢复的失败（找不到、冲突、校验不过、无权）返回 `Error`，宿主按错误码的 KIND 段映射状态码并写 Problem Details；抛出的异常由内核翻译成 `KERNEL_UNEXPECTED_001`、HTTP 500，并按缺陷记录。`Error` 的构造与错误码规则见[结果与错误处理](/web/errors)。

### 请求类型没有任何字段怎么写

写一个空记录即可，如示例应用的 `public sealed record ListOrdersRequest;`。它仍要登记进 `JsonSerializerContext`，HTTP 上这个操作接受空载荷，`GET /orders` 不带任何参数就能调用。

### 后台任务调用操作时总被 401 拒绝

`App.CreateContext` 建出的上下文没有凭据，Authenticate 阶段按匿名处理，带 `Permission` 的操作就会被拒。给上下文放入服务账号的凭据（`SetCredential`），或者让后台任务只调用不要求权限的操作。不要绕过管道直接 `new` 操作类，那样会丢掉校验与事务。

## 相关包与下一步

- 相关包：[Kernel](/packages/kernel)、[Kernel.Generators](/packages/kernel-generators)、[Protocols.Http](/packages/protocols-http)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)
- 下一步：[HTTP 路由与载荷](/web/routing)，看操作 ID 与请求形状怎样决定 HTTP 路由，以及载荷从哪里来
- 同一份契约的其他载体：[gRPC](/protocols/grpc)、[MCP](/protocols/mcp)、[命令行宿主](/protocols/command-line)、[工具与知识库](/ai/tools)
- 相关章节：[授权与权限](/security/authorization)、[数据校验](/web/validation)、[结果与错误处理](/web/errors)
- 规范：RFC-0011《操作契约与 XGI》、`conventions/operation-ids.md`《操作 ID》
