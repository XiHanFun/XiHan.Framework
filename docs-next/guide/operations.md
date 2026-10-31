# 操作契约

本章说明 5.0 对外暴露业务的方式：用 `[Operation]` 与 `IOperation<TRequest, TResponse>` 声明操作，由源生成器产出登记代码与 JSON Schema，再由协议适配器把同一份契约发布到 HTTP、gRPC、MCP 与命令行。适用于写业务接口的开发者。

操作契约取代 4.x 的动态 API：不再从应用服务的方法名推导路由，一个操作就是一个类。

完整 API 见 [Kernel 包](../packages/kernel)与 [Kernel.Generators 包](../packages/kernel-generators)。

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

命令行上是 `<resource> <verb> --字段 值`。请求类型是否声明 `id` 属性、是否带嵌套对象会改变 HTTP 路由的形态，完整规则见 [HTTP 与 OpenAPI](./http)。

## 声明操作

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

`Order` 是实现了实体协议的聚合根，写法见[领域模型](./domain)。

`HandleAsync` 的约定：

- 返回 `Result<TResponse>`，成功值不能为 `null`；可恢复的失败返回 `Error`，不抛异常。
- 取消令牌用 `context.CancellationToken`，它同时受调用方取消与截止时间约束。
- 操作类按作用域注册，构造函数注入所需服务；每次调用一个新实例。

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
| `All` / `None` | 全部 / 都不发布（仍可在进程内经调用器调用） |

不写 `Expose` 时取程序集级缺省：

```csharp
using XiHan.Framework.Kernel.Operations;

[assembly: OperationDefaults(Expose = Expose.Http | Expose.Grpc)]
```

操作依赖隐式的 `Expose.All` 时，生成器诊断 `XHG004` 能报告出来；它缺省关闭，在 `.editorconfig` 里写 `dotnet_diagnostic.XHG004.severity = error` 即强制每个操作写明。

只读由契约推出：动词是 `get` 或 `list`，或以 `ReadOnly = true` 声明的非标准动词，`OperationContract.IsReadOnly` 为真。HTTP 适配器据此让没有嵌套对象的只读自定义动词使用 `GET`，MCP 适配器据此给出只读提示。

## 登记序列化类型与 Schema

请求与响应类型要登记到项目里的 `JsonSerializerContext`：

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

`Kernel.Generators` 从这里取序列化元数据，并为每个操作生成请求与响应的 JSON Schema（draft 2020-12）。属性名按序列化上下文的命名策略；不可空且没有默认值的属性进 `required`；嵌套对象进 `$defs`；封闭泛型的定义名带实参名（`PageResultOfOrderResponse`）。请求类型上的 DataAnnotations 折进 Schema，由 Validate 阶段执行：

| 注解 | Schema |
| --- | --- |
| `[Required]` | 进 `required` 并去掉可空；字符串另加 `minLength: 1`（`AllowEmptyStrings = true` 除外） |
| `[Range]` | `minimum` / `maximum`，开区间用 `exclusiveMinimum` / `exclusiveMaximum` |
| `[StringLength]`、`[MinLength]`、`[MaxLength]`、`[Length]` | 字符串的 `minLength` / `maxLength`，集合的 `minItems` / `maxItems` |
| `[RegularExpression]` | `pattern` |

其他注解（如 `[EmailAddress]`）不折进 Schema，需要时在请求类型上实现 `IValidatableObject`，见[数据校验](./validation)。项目开启 `GenerateDocumentationFile` 时，类型与属性的 XML `<summary>` 折进 Schema 的 `description`，OpenAPI 与 MCP 的字段说明由此而来。

## 登记操作

特性在 `Configure` 里调用生成的 `AddGeneratedOperations()`，一次登记项目里全部操作；也可以按操作调用生成的 `Add<类名>()`：

```csharp
public void Configure(FeatureContext context)
{
    context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));
    context.AddGeneratedOperations();
}
```

生成的代码在项目 `RootNamespace` 下的 `GeneratedOperations` 静态类里。生成器的诊断：

| 诊断 | 情形 |
| --- | --- |
| `XHG001` | 标注 `[Operation]` 的类没有实现 `IOperation<TRequest, TResponse>` |
| `XHG002` | 请求或响应类型没有被任何 `JsonSerializerContext` 登记 |
| `XHG003` | 操作 ID 不符合 `<resource>.<verb>` 文法 |
| `XHG004` | 缺省关闭：操作依赖隐式的 `Expose.All` |
| `XHG005` | `create` / `update` / `delete` 声明了 `ReadOnly = true` |

不用生成器时手写登记，契约、序列化元数据与 Schema 自己给出：

```csharp
context.AddOperation<CreateOrder, PlaceOrderRequest, OrderResponse>(
    new OperationContract(CreateOrder.Id, Stability.Preview, Expose.Http, permission: "orders.write"),
    OrdersJsonContext.Default.PlaceOrderRequest,
    OrdersJsonContext.Default.OrderResponse);
```

操作登记表 `OperationRegistry` 的 ID 必须唯一（重复报 `KERNEL_OPERATION_001`），容量 10000（满了报 `KERNEL_OPERATION_002`），都是配置期异常。`Permission` 引用的权限码要先用 `AddPermission` 登记；装配时不核对，未登记的权限码在调用时由 Authorize 阶段返回 `SECURITY_NOTFOUND_001`。

## 标题与描述

面向人的标题与描述不写在契约里，放在消息目录，按约定键查找：

| 键 | 用途 |
| --- | --- |
| `<操作 ID>.title` | 短语，如「下单」；MCP tool 的 `title`、OpenAPI 的 `summary` |
| `<操作 ID>.description` | 行为与规则；MCP tool 的 `description`、OpenAPI 的 `description` |

键里操作 ID 的 `-` 换成 `_`（`order_lines.list.description`）。描述不写授权要求，适配器按契约统一呈现。文案写在应用的 `Localization/<区域>.json` 里，见[国际化](./localization)：

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

中间件与端点里可以从上下文读当前操作：`context.GetOperation()` 得到 `OperationDescriptor`（契约、归属特性、请求与响应类型、Schema），`context.GetOperationRequest()` 得到解码后的请求对象，`context.GetOperationPayload()` 得到原始载荷。

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

这样构造的上下文没有凭据与租户：经过 Authenticate 阶段时是匿名调用，带 `Permission` 的操作会被拒绝。后台调用需要身份时，用 `context.SetCredential(new AuthenticationCredential(scheme, value))`（`Security.Abstractions`）放入凭据；需要租户时在 `App.CreateContext` 的 `baggage` 参数里给出 `xihan.tenant`（`BaggageKeys.Tenant`），由 Tenant 阶段对照租户存储核对。细节见[认证](./authentication)与[多租户](./multi-tenancy)。

| 错误码 | 情形 |
| --- | --- |
| `KERNEL_NOTFOUND_001` | 操作没有登记，调用不进管道 |
| `KERNEL_VALIDATION_001` | 载荷不是 JSON、为 `null` 或类型不匹配 |
| `KERNEL_VALIDATION_002` | 类型化调用的请求或响应类型与登记不符 |
| `KERNEL_VALIDATION_003` / `004` | 不满足请求 Schema / 自校验失败 |

## 相关

- [HTTP 与 OpenAPI](./http)
- [gRPC、MCP 与 CloudEvents](./protocols)
- [命令行宿主](./command-line)
- [授权](./authorization)：权限码与策略
- [数据校验](./validation)
- 规范：RFC-0011《操作契约与 XGI》、`conventions/operation-ids.md`
