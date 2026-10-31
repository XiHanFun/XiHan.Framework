# 结果与错误

本章说明 5.0 如何表达成功与失败：原语层的 `Result`、`Error` 与 `Maybe`，错误码文法与种类，错误为什么只携带消息键，以及宿主如何把错误翻译成 HTTP Problem Details 等协议形态。适用于写操作、中间件与 Provider 的开发者，也供对接 API 的客户端参考。

5.0 不再有 4.x 的统一响应信封，成功直接返回响应对象，失败返回 RFC 9457 Problem Details。

完整 API 见 [Primitives 包](../packages/primitives)。

## Result 与 Result&lt;T&gt;

可恢复的失败（找不到、冲突、校验不通过、外部设施不可用）用 `Result` 表达，不抛异常。框架的端口都按这个约定返回：仓储的 `AddAsync`、缓存的 `GetAsync`、消息的 `PublishAsync`、操作的 `HandleAsync`。

| 类型 | 用途 |
| --- | --- |
| `Result` | 没有返回值的成功或携带 `Error` 的失败 |
| `Result<T>` | 成功时携带值（不能为 `null`），失败时携带 `Error` |
| `Maybe<T>` | 类型层面的「可能没有」，`default` 即无值；不表达失败 |

创建与转换：

```csharp
using XiHan.Framework.Primitives;

namespace Shop.Orders;

public static class OrderErrors
{
    public const string NotFound = "SHOP_NOTFOUND_001";
}

public static class OrderMessages
{
    public const string NotFound = "shop.order.not_found";
}

public sealed record OrderView(Guid Id, string Name);

public sealed class OrderLookup
{
    private readonly Dictionary<Guid, OrderView> _orders = [];

    public Result<OrderView> Find(Guid id)
    {
        if (_orders.TryGetValue(id, out var order))
        {
            // 值与错误都可以隐式转换成 Result<T>
            return order;
        }

        return Error.NotFound(OrderErrors.NotFound, OrderMessages.NotFound, MessageArguments.Of(("id", id)));
    }
}
```

读取结果：

| 成员 | 说明 |
| --- | --- |
| `IsSuccess` / `IsFailure` | 是否成功 |
| `Error` | 失败时的错误，成功时为 `null` |
| `TryGetValue(out value)` | 成功时取出值 |
| `ValueOr(fallback)` / `ValueOrThrow()` | 取值或回退；`ValueOrThrow` 在失败时抛出 `FrameworkException` |
| `Map` / `Bind` / `Match` | 链式转换 |
| `ToResult()` | `Result<T>` 去掉值 |
| `ThrowIfFailure()` | 失败时抛出，只用于启动期、配置期这类不可恢复的场合 |

`default(Result)` 与 `default(Result<T>)` 视为未初始化的失败（`PRIMITIVES_VALIDATION_002`），用来发现忘记赋值的分支。

## 错误码

```text
ErrorCode = Domain "_" Kind "_" Sequence
Domain    = 术语表的错误码域（KERNEL、PERSISTENCE、SECURITY …）或应用自定义的大写标识
Kind      = VALIDATION | NOTFOUND | CONFLICT | DENIED | UNAVAILABLE | TIMEOUT | CANCELLED | UNEXPECTED | 领域自定义
Sequence  = 三位数字，域内唯一，发布后不复用
```

示例：`PERSISTENCE_CONFLICT_001`、`SECURITY_DENIED_003`、`SHOP_NOTFOUND_001`。`ErrorCode.IsValid`、`ErrorCode.KindOf`、`ErrorCode.DomainOf` 解析与校验错误码。

错误码面向程序，文案面向人：客户端只应依赖错误码，不解析文案。框架已登记的错误码以 XiHan Spec 的 `conventions/error-codes.md` 为准，各包页列出本包会返回的错误码。

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
| `MessageKey` | 消息键，文案在语言文件里 |
| `Details` | 参数与细节，名字到值的字典；既是模板参数，也随 Problem Details 输出标量项 |
| `Inner` | 内部错误 |
| `Message` | 按进程默认区域渲染的文案，供日志使用 |
| `WithCode(code)` | 换码，保留消息键与参数 |

### 错误只携带消息键

`Error` 的第二个参数是消息键常量，不是文案。文案放在所属包的 `Localization/<区域>.json` 里，宿主在 Egress 阶段按调用方的区域翻译：同一个错误，`Accept-Language: zh-CN` 的调用方收到中文，未指定的收到默认的英文。参数以 `MessageArguments.Of(("name", value))` 传入，模板里写 `{name}`；按位置的参数用 `MessageArguments.Positional(...)`，模板里写 `{0}`。消息键文法、语言文件格式与 10 种内置区域见[国际化](./localization)。

`Error.Literal(code, text)` 携带现成的文字而不是消息键，只用于两种场合：从线上还原对端返回的错误、把异常转换成错误。正式包里 `Error.*` 的第二个参数必须是常量，架构测试强制这一点。

## 异常

异常只表达编程错误与配置错误：参数为 `null`、清单不合法、配置值非法、启动时连不上必需的外部设施。它们在启动期暴露，不翻译成调用方语言：

| 类型 | 用途 |
| --- | --- |
| `ArgumentException` 及其子类 | 参数与配置值非法 |
| `FrameworkException(code, message, inner)` | 带错误码的框架异常；`ToError()` 转成 `Error`（文本以 `Literal` 承载） |

调用期间未处理的异常由 Observe 阶段捕获，记录日志并转成 `KERNEL_UNEXPECTED_001` 结果，调用方收到 500，不会看到异常细节。

## 翻译成协议

宿主读取管道结束后的结果，按错误码的 KIND 映射协议状态。HTTP 的映射（`Hosting.AspNetCore` 的 `ProblemResponse.StatusCodeFor`）：

| KIND | HTTP 状态 |
| --- | --- |
| `VALIDATION` | 400 |
| `NOTFOUND` | 404 |
| `CONFLICT` | 409 |
| `DENIED` | 未认证（`SECURITY_DENIED_001`）与凭据无效（`SECURITY_DENIED_003`）为 401，并带 `WWW-Authenticate` 质询；其余为 403 |
| `UNAVAILABLE` | 503 |
| `TIMEOUT` | 504 |
| `CANCELLED` | 408 |
| `UNEXPECTED` 与其他 | 500 |

Problem Details 的字段：

```json
{
  "type": "urn:xihan:error:KERNEL_VALIDATION_003",
  "title": "VALIDATION",
  "status": 400,
  "detail": "操作 'greetings.create' 的请求不符合契约：$.name: required property 'name' is missing",
  "instance": "/greetings",
  "code": "KERNEL_VALIDATION_003",
  "traceId": "5978653a746fa20f0dce9f4a2f954bac",
  "errors": { "name": ["缺少必填属性 'name'"] },
  "details": { "id": "greetings.create", "reason": "$.name: required property 'name' is missing" }
}
```

| 字段 | 内容 |
| --- | --- |
| `type` | `urn:xihan:error:<错误码>` |
| `title` | 错误码的 KIND |
| `status` | HTTP 状态码 |
| `detail` | 按调用方区域翻译的文案；响应头 `Content-Language` 给出命中的区域 |
| `instance` | 请求路径 |
| `code` | 错误码 |
| `traceId` | 本次调用的 W3C trace-id |
| `errors` | 只在校验错误时出现：字段名到文案列表 |
| `details` | `Error.Details` 里的标量项 |

gRPC、MCP 与命令行的映射分别见 [gRPC、MCP 与 CloudEvents](./protocols) 与[命令行宿主](./command-line)：gRPC 以状态码加 `xihan-error-code` 尾部元数据表达，MCP 以 `isError` 结果表达，命令行以按 KIND 的退出码加 stderr 上的 Problem JSON 表达。

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

操作契约不需要这样做：`HandleAsync` 返回的 `Result` 就是调用结果。

## 相关

- [国际化](./localization)：消息键与语言文件
- [数据校验](./validation)：校验错误的字段级 `errors`
- [操作契约](./operations)
- 规范：RFC-0001《原语》、`conventions/error-codes.md`
