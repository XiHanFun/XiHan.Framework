来源：https://framework.docs.xihanfun.com/web/errors

# 结果与错误处理

这一章讲框架怎样表达成功与失败：什么时候抛异常、什么时候返回 `Result`，错误码的文法与种类，`Error` 为什么只携带消息键而不是文案，以及宿主怎样把同一个错误翻译成 HTTP 状态、gRPC 状态与命令行退出码。读完你能在操作里正确地返回一个「找不到」，并知道客户端会收到什么。前置知识是[操作契约](/web/operations)与[执行管道](/startup/pipeline)。

## 为什么需要

失败有两种。一种是程序或配置写错了：参数为 `null`、配置值非法、启动时连不上必需的外部设施，这类失败应该尽早、尽响地暴露，不该被捕获后继续运行。另一种是业务里预期会发生的：订单找不到、库存不足、校验不通过、下游暂时不可用，这类失败是正常流程的一部分，调用方要能按类别处理。用异常统一表达两者，前者会被业务层的 `catch` 吞掉，后者让每个调用点都要猜可能抛什么。

框架把两者分开：配置与编程错误抛 `FrameworkException` / `ArgumentException`；可恢复的失败返回 `Result` / `Error`，不抛异常。`Error` 带一个有文法的错误码，宿主按错误码的 KIND 段映射到协议状态，文案按调用方的语言在出口翻译。框架没有统一响应信封：成功直接返回响应对象，失败返回 RFC 9457 Problem Details。

## 异常还是结果

| 情形 | 表达方式 | 例子 |
| --- | --- | --- |
| 参数不合法、配置值非法 | `ArgumentException` 及其子类 | `FromConfiguration` 读到负的超时；`SecretName` 不合文法 |
| 框架层面的编程或装配错误 | `FrameworkException(code, message, inner)` | 必需绑定缺失 `KERNEL_HOSTING_001`；首次加载远程配置失败 |
| 调用方预期会处理的失败 | `Result` / `Result<T>` 携带 `Error` | 找不到、冲突、校验不通过、下游不可用 |
| 类型层面的「可能没有」 | `Maybe<T>` | 仓储 `FindAsync` 的返回值；它不表达失败 |

异常的消息面向开发与运维，是英文，不翻译；`FrameworkException.ToError()` 可以把它转成 `Error`（文本以 `Literal` 承载）。`Result` 的错误面向调用方，只带消息键与参数，由宿主翻译成调用方的语言。

## Result、Result&lt;T&gt; 与 Maybe&lt;T&gt;

框架的端口都按 `Result` 约定返回：仓储的 `AddAsync`、缓存的 `GetAsync`、消息的 `PublishAsync`、操作的 `HandleAsync`。

| 类型 | 用途 |
| --- | --- |
| `Result` | 没有返回值的成功，或携带 `Error` 的失败 |
| `Result<T>` | 成功时携带值（不能为 `null`），失败时携带 `Error` |
| `Maybe<T>` | 可能没有值；`default` 即无值；`ToResult(error)` 把无值转成失败 |

创建：`Result.Success()`、`Result.Success(value)`、`Result.Failure(error)`；值与 `Error` 都能隐式转换成 `Result<T>`，`Error` 能隐式转换成 `Result`。读取：

| 成员 | 说明 |
| --- | --- |
| `IsSuccess` / `IsFailure` | 是否成功 |
| `Error` | 失败时的错误，成功时为 `null` |
| `TryGetValue(out value)` | 成功时取出值 |
| `ValueOr(fallback)` / `ValueOrThrow()` | 取值或回退；`ValueOrThrow` 在失败时抛出 `FrameworkException` |
| `Map` / `Bind` / `Match` | 链式转换：`Map` 变换值，`Bind` 接另一个 `Result`，`Match` 分别处理成败 |
| `ToResult()` | `Result<T>` 去掉值 |
| `ThrowIfFailure()` | 失败时抛出，只用于启动期、配置期这类不可恢复的场合 |

`default(Result)` 与 `default(Result<T>)` 视为未初始化的失败（`PRIMITIVES_VALIDATION_002`），用来发现忘记赋值的分支。

## 错误码

错误码面向程序，文案面向人：客户端只应依赖错误码，不解析文案。文法：

```text
ErrorCode = Domain "_" Kind "_" Sequence
Domain    = 术语表的错误码域（KERNEL、PERSISTENCE、SECURITY …）或应用自定义的大写标识
Kind      = VALIDATION | NOTFOUND | CONFLICT | DENIED | UNAVAILABLE | TIMEOUT | CANCELLED | UNEXPECTED | 领域自定义
Sequence  = 三位数字，域内唯一，发布后不复用
```

示例：`PERSISTENCE_CONFLICT_001`、`SECURITY_DENIED_003`、`SHOP_NOTFOUND_001`。`ErrorCode.IsValid`、`ErrorCode.KindOf`、`ErrorCode.DomainOf` 解析与校验错误码，`ErrorCode.Validate` 不合法时抛出 `ArgumentException`。框架已登记的错误码以 XiHan Spec 的 `conventions/error-codes.md` 为准，各包参考页列出本包会返回的错误码。

KIND 决定每种协议上的状态。三个宿主的映射都只看 KIND，应用自定义的域（如 `SHOP`）自动适用：

| KIND | HTTP 状态 | gRPC 状态 | 命令行退出码 |
| --- | --- | --- | --- |
| `VALIDATION` | 400 | `InvalidArgument` | 2 |
| `NOTFOUND` | 404 | `NotFound` | 3 |
| `CONFLICT` | 409 | `Aborted` | 5 |
| `DENIED` | 403；未认证 `SECURITY_DENIED_001` 与凭据无效 `SECURITY_DENIED_003` 为 401 并带 `WWW-Authenticate` 质询 | `PermissionDenied`；同样两个码为 `Unauthenticated` | 4 |
| `UNAVAILABLE` | 503 | `Unavailable` | 6 |
| `TIMEOUT` | 504 | `DeadlineExceeded` | 7 |
| `CANCELLED` | 408 | `Cancelled` | 8 |
| `UNEXPECTED` 与其他 | 500 | `Internal` | 1 |

HTTP 的映射在 `Hosting.AspNetCore` 的 `ProblemResponse.StatusCodeFor`，命令行的在 `CommandLineExitCodes.For`，用法错误另有退出码 64。MCP 不走状态码：操作失败是 `isError: true` 的结果，文本是 `<错误码>: <文案>`。

## Error

`Error` 不可变，由工厂方法创建，工厂方法校验错误码的 KIND 段与方法名一致：

| 工厂方法 | KIND |
| --- | --- |
| `Error.Validation(code, messageKey, details)` | `VALIDATION` |
| `Error.NotFound(code, messageKey, details)` | `NOTFOUND` |
| `Error.Conflict(code, messageKey, details)` | `CONFLICT` |
| `Error.Denied(code, messageKey, details)` | `DENIED` |
| `Error.Unavailable(code, messageKey, details)` | `UNAVAILABLE` |
| `Error.Unexpected(code, messageKey, inner, details)` | `UNEXPECTED` |
| `new Error(code, messageKey, details, inner)` | 其他种类（`TIMEOUT`、`CANCELLED` 与领域自定义） |

| 成员 | 说明 |
| --- | --- |
| `Code` / `Kind` | 错误码与它的 KIND 段 |
| `MessageKey` | 消息键，文案在语言文件里；`Literal` 创建的错误为 `null` |
| `Text` | `Literal` 携带的现成文字，其他情况为 `null` |
| `Details` | 参数与细节，名字到值的字典；既是模板参数，也随 Problem Details 输出标量项 |
| `Inner` | 内部错误 |
| `Message` | 按进程默认区域渲染的文案，供日志使用 |
| `WithCode(code)` / `WithText(text)` | 换码或换文字，保留其他成员 |

### 错误只携带消息键

`Error` 的第二个参数是消息键常量，不是文案。文案放在所属包的 `Localization/<区域>.json` 里，宿主在 Egress 阶段按调用方的区域翻译：同一个错误，`Accept-Language: zh-CN` 的调用方收到中文，未指定的收到默认的英文。参数以 `MessageArguments.Of(("name", value))` 传入，模板里写 `{name}`；按位置的参数用 `MessageArguments.Positional(...)`，模板里写 `{0}`。写法：

```csharp
Error.NotFound(OrderErrors.NotFound, OrderMessages.NotFound, MessageArguments.Of(("id", id)))
```

三个参数分别是：错误码常量（`SHOP_NOTFOUND_001`）、消息键常量（`shop.order.not_found`）、模板参数。消息键文法、语言文件格式与 10 种内置区域见[全球化和本地化](/common/localization)。

### Error.Literal 的边界

`Error.Literal(code, text, details, inner)` 携带现成的文字而不是消息键。它只用于两种场合：从线上还原对端返回的错误（文字已经是翻译过的），以及把异常转换成错误（`FrameworkException.ToError()` 就是这样做的）。业务代码里的失败一律用消息键；正式包里 `Error.*` 的第二个参数必须是常量，架构测试强制这一点。

## 最小示例

我们写一个按 ID 取订单的操作，找不到时返回 `NOTFOUND`，并看客户端收到什么。先定义错误码与消息键：

```csharp
// OrderErrors.cs 与 OrderMessages.cs
namespace Shop.Orders;

public static class OrderErrors
{
    public const string NotFound = "SHOP_NOTFOUND_001";
}

public static class OrderMessages
{
    public const string NotFound = "shop.order.not_found";
}
```

语言文件 `Localization/en-US.json` 与 `Localization/zh-CN.json`（其余八种区域各一份，见[全球化和本地化](/common/localization)）：

```json
{ "shop.order.not_found": "Order '{id}' does not exist." }
```

```json
{ "shop.order.not_found": "订单 '{id}' 不存在。" }
```

项目文件把它们嵌入为资源：

```xml
<ItemGroup>
  <EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />
</ItemGroup>
```

操作用仓储的 `FindAsync` 取 `Maybe<Order>`，无值时转成失败：

```csharp
// GetOrder.cs
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace Shop.Orders;

[Operation(Id, Expose = Expose.Http)]
public sealed class GetOrder(IRepository<Order, Guid> orders) : IOperation<GetOrderRequest, OrderResponse>
{
    public const string Id = "orders.get";

    public async ValueTask<Result<OrderResponse>> HandleAsync(GetOrderRequest request, InvocationContext context)
    {
        var found = await orders.FindAsync(request.Id, context.CancellationToken);

        // Maybe 表示「可能没有」，在这里才决定「没有」算失败；错误只带码、键与参数，文案由宿主按调用方语言翻译
        return found
            .ToResult(Error.NotFound(OrderErrors.NotFound, OrderMessages.NotFound, MessageArguments.Of(("id", request.Id))))
            .Map(static order => new OrderResponse(order.Id, order.Name, order.Amount));
    }
}

public sealed record GetOrderRequest(Guid Id);

public sealed record OrderResponse(Guid Id, string Name, int Amount);
```

`Order` 是实现了实体协议的聚合根，写法见[领域模型](/persistence/domain)；请求与响应要登记到 `JsonSerializerContext`，特性里调用生成的 `AddGeneratedOperations()`，见[操作契约](/web/operations)。`Program.cs` 装配 HTTP 协议并注册应用的消息目录：

```csharp
using Shop.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());

// 应用自己的消息目录：Problem Details 的 detail 按 Accept-Language 翻译，默认英文
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("shop", typeof(OrderMessages)));

var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();
app.Run();
```

运行后请求一个不存在的订单：

```bash
curl -i -H "Accept-Language: zh-CN" localhost:5000/orders/0f8fad5b-d9cb-469f-a165-70867728950e
```

得到 404 与 Problem Details：

```json
{
  "type": "urn:xihan:error:SHOP_NOTFOUND_001",
  "title": "NOTFOUND",
  "status": 404,
  "detail": "订单 '0f8fad5b-d9cb-469f-a165-70867728950e' 不存在。",
  "instance": "/orders/0f8fad5b-d9cb-469f-a165-70867728950e",
  "code": "SHOP_NOTFOUND_001",
  "traceId": "5978653a746fa20f0dce9f4a2f954bac",
  "details": { "id": "0f8fad5b-d9cb-469f-a165-70867728950e" }
}
```

去掉 `Accept-Language` 再请求，`detail` 变成英文，其余字段不变。同一个操作暴露到 gRPC 时是 `NotFound` 状态加尾部元数据 `xihan-error-code: SHOP_NOTFOUND_001`，到命令行时退出码 3。

## 翻译成协议

宿主读取管道结束后的结果，按 KIND 映射协议状态。HTTP 的 Problem Details 字段：

| 字段 | 内容 |
| --- | --- |
| `type` | `urn:xihan:error:<错误码>` |
| `title` | 错误码的 KIND |
| `status` | HTTP 状态码 |
| `detail` | 按调用方区域翻译的文案；响应头 `Content-Language` 给出命中的区域 |
| `instance` | 请求路径 |
| `code` | 错误码 |
| `traceId` | 本次调用的 W3C trace-id |
| `errors` | 只在校验错误时出现：字段名到文案列表，见[数据校验](/web/validation) |
| `details` | `Error.Details` 里的标量项 |

gRPC 以状态码加 `xihan-error-code` 尾部元数据表达，`grpc-message` 是翻译后的文案，见 [gRPC](/protocols/grpc)；MCP 以 `isError` 结果表达，见 [MCP](/protocols/mcp)；命令行以按 KIND 的退出码加 stderr 上的一行 JSON（`code`、`detail`、`traceId`）表达，见[命令行宿主](/protocols/command-line)。

截止时间到了一律是 `KERNEL_TIMEOUT_001`（504），调用方同时取消、下游把取消写成 `KERNEL_CANCELLED_001` 结果也一样；408 只表示调用方在截止时间到达之前自己取消，见[弹性与限流](/common/resilience)。

## 在端点里返回错误

最小 API 端点在 Handle 阶段内执行，把失败写进调用上下文，由宿主统一翻译：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Minimal;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseMinimalProfile());

var app = builder.Build();
app.UseXiHan();

app.MapGet("/reports/{id}", (string id, HttpContext http) =>
{
    // 结果写进上下文，Egress 由宿主翻译为 Problem Details
    http.GetXiHanContext().ValueOrThrow().SetOutcome(
        Error.NotFound("SHOP_NOTFOUND_002", "shop.report.not_found", MessageArguments.Of(("id", id))));
    return Results.Empty;
});

app.Run();
```

操作契约不需要这样做：`HandleAsync` 返回的 `Result` 就是调用结果。端点里使用框架的其他方式见[端点里使用框架](/web/endpoints)。

## 异常的处理

调用期间未处理的异常由 Observe 阶段捕获，记录日志并转成 `KERNEL_UNEXPECTED_001` 结果，调用方收到 500，不会看到异常细节。所以操作里不要用异常表达业务失败：它会被记成一次意外错误，客户端拿不到有意义的错误码。

启动期的异常不转换：`AddXiHan` 与特性 `Configure` 里抛出的 `FrameworkException` / `ArgumentException` 直接让进程退出，消息里带错误码或键名，便于在部署日志里定位。雪花标识生成器是一个例外：它的 `PERSISTENCE_UNAVAILABLE_004` / `005` 以异常抛出，`SnowflakeIdFeature` 在 Resilience 阶段把它们转成同码的 `UNAVAILABLE` 结果，调用方收到 503，见[标识生成](/persistence/identifiers)。

## 常见问题

### 什么时候该抛异常，什么时候该返回 Result

问自己「调用方能不能对这个失败做点什么」。能（重试、提示用户、换一条路径）就返回 `Result`；不能（代码或配置写错了）就抛异常让它尽早暴露。框架的端口全部返回 `Result`，启动期的装配错误全部抛异常。

### 自己的错误码怎么起名

域用应用自己的大写标识（`SHOP`），KIND 从八个标准种类里选，序号三位、域内唯一、发布后不复用。工厂方法会校验 KIND 与方法名一致：`Error.NotFound("SHOP_CONFLICT_001", ...)` 在构造时抛出 `ArgumentException`。

### 为什么不能直接写一句中文当错误消息

文案一旦写进代码，就只能是一种语言，而且散落在各处无法统一维护。消息键让同一个错误按调用方的 `Accept-Language` 出不同语言的文案，键与文案的对应由语言文件管理。调试时看 `Error.Message`，它按进程默认区域渲染。

### Error.Details 里能放什么

模板参数与想随 Problem Details 输出的标量（字符串、数字、布尔、`Guid` 等）。嵌套对象不输出到 `details`。不要放凭据或个人数据：它会原样进入响应。

### 需要带上内部原因怎么办

用 `Inner`：`Error.Unexpected(code, messageKey, inner)` 或 `new Error(code, messageKey, details, inner)`。`Inner` 进日志，不进 Problem Details。

## 相关包与下一步

- [Primitives](/packages/primitives)：`Result`、`Error`、`Maybe`、`ErrorCode`、`MessageArguments`、`FrameworkException`
- [Kernel](/packages/kernel)：`SetOutcome` 与内核错误码
- [Hosting.AspNetCore](/packages/hosting-aspnetcore)：`ProblemResponse` 与 HTTP 映射
- 规范：RFC-0001《原语》、`conventions/error-codes.md`
- 下一页：[数据校验](/web/validation)
