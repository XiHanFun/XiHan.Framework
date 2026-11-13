来源：https://framework.docs.xihanfun.com/web/validation

# 数据校验

这一章讲 Validate 阶段怎样校验操作的请求：先按编译期生成的 JSON Schema 校验载荷，再对实现 `IValidatableObject` 的请求执行自校验，失败时以字段级错误返回。读完你能给请求加约束、写一条 Schema 表达不了的业务规则，并看懂客户端收到的 400 响应。前置知识是[操作契约](/web/operations)与[结果与错误处理](/web/errors)。

## 为什么需要

进入业务代码的请求必须是合法的：必填字段都在、数字在范围内、字符串不超长。如果每个操作自己写一遍 `if` 检查，规则会散落各处，HTTP、gRPC、MCP、命令行四种入口还得各自保证一致；如果在运行期用反射读注解，Native AOT 下又跑不起来。

框架把校验放进内核管道的 Validate 阶段，对所有协议入口统一执行：请求类型上的 DataAnnotations 由源生成器在编译期折进 JSON Schema，运行期按 Schema 校验载荷，零反射；Schema 表达不了的规则写在请求类型的 `IValidatableObject.Validate` 里。两步任一失败，调用以 `VALIDATION` 种类的结果结束，不进入 Handle 阶段，HTTP 上是 400 且带字段级 `errors`。框架不在运行期反射读取 DataAnnotations，也不集成第三方校验库。

## 两步校验

内核的内置中间件 `ValidationMiddleware` 登记在 Validate 阶段开头，只处理带操作描述的调用，按顺序执行：

| 步骤 | 做什么 | 失败码 |
| --- | --- | --- |
| 1. 取载荷 | HTTP、gRPC、MCP、命令行进来的调用用协议适配器绑定好的 JSON 载荷（空载荷视为 `{}`）；类型化调用（`IOperationInvoker.InvokeAsync<TRequest, TResponse>`）按序列化元数据把请求对象编码一次 | 载荷不是 JSON：`KERNEL_VALIDATION_001` |
| 2. Schema 校验 | 操作带 Schema 时按请求 Schema 校验载荷 | `KERNEL_VALIDATION_003` |
| 3. 解码与自校验 | 解码成请求对象并记录在上下文；请求实现了 `IValidatableObject` 时调用 `Validate`，每条 `ValidationResult` 按成员名列为违规 | `KERNEL_VALIDATION_004` |

任一步失败，调用不进入 Idempotency、Transaction 与 Handle 阶段。管道阶段的全貌见[执行管道](/startup/pipeline)。

## 最小示例

我们给下单请求加两类约束：名称 1 到 64 个字符、金额 1 到 1000000（注解，进 Schema），名称不能只有空白（自校验）。先写请求类型：

```csharp
// PlaceOrderRequest.cs
using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Primitives;

namespace Shop.Orders;

public sealed record PlaceOrderRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Name,
    [Range(1, 1_000_000)] int Amount) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Schema 只能约束长度，表达不了「全是空白」；文案走消息键，按调用方语言取
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult(ShopMessages.OrderNameBlank.L(), [nameof(Name)]);
        }
    }
}
```

消息键与语言文件（`Localization/en-US.json`、`Localization/zh-CN.json`，其余区域各一份，以 `LogicalName="Localization/%(Filename)%(Extension)"` 嵌入，见[全球化和本地化](/common/localization)）：

```csharp
// ShopMessages.cs
namespace Shop;

public static class ShopMessages
{
    public const string OrderNameBlank = "shop.order.name_blank";
}
```

```json
{ "shop.order.name_blank": "The order name must not be blank." }
```

```json
{ "shop.order.name_blank": "订单名称不能只有空白。" }
```

操作本身不写任何检查，拿到的请求一定合法：

```csharp
// CreateOrder.cs
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace Shop.Orders;

[Operation(Id, Expose = Expose.Http)]
public sealed class CreateOrder : IOperation<PlaceOrderRequest, OrderResponse>
{
    public const string Id = "orders.create";

    public ValueTask<Result<OrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
        => ValueTask.FromResult<Result<OrderResponse>>(new OrderResponse(Guid.NewGuid(), request.Name, request.Amount));
}

public sealed record OrderResponse(Guid Id, string Name, int Amount);
```

请求与响应登记到 `JsonSerializerContext`，特性里调用生成的 `AddGeneratedOperations()`，`Program.cs` 装配 `HttpProtocolFeature`、注册消息目录并 `MapXiHanOperations()`，写法与[结果与错误处理](/web/errors)的最小示例相同。运行后发一个坏请求：

```bash
curl -i -X POST localhost:5000/orders -H "Content-Type: application/json" -H "Accept-Language: zh-CN" -d '{"amount":0}'
```

得到 400：

```json
{
  "type": "urn:xihan:error:KERNEL_VALIDATION_003",
  "title": "VALIDATION",
  "status": 400,
  "detail": "操作 'orders.create' 的请求不符合契约：$.name: required property 'name' is missing",
  "instance": "/orders",
  "code": "KERNEL_VALIDATION_003",
  "traceId": "5978653a746fa20f0dce9f4a2f954bac",
  "errors": { "name": ["缺少必填属性 'name'"], "amount": ["值必须 >= 1"] },
  "details": { "id": "orders.create", "reason": "$.name: required property 'name' is missing" }
}
```

再发 `{"name":"   ","amount":3}`：Schema 通过（长度是 3），自校验拒绝，`code` 变成 `KERNEL_VALIDATION_004`，`errors` 是 `{ "name": ["订单名称不能只有空白。"] }`。两种拒绝的 `errors` 形状相同，前端表单据此定位字段。

## 用注解声明约束

请求类型上的 DataAnnotations 由 `Kernel.Generators` 在编译期折进请求 Schema，位置记录的主构造参数上的注解同样生效：

| 注解 | Schema |
| --- | --- |
| `[Required]` | 进 `required` 并去掉可空；字符串另加 `minLength: 1`（`AllowEmptyStrings = true` 除外） |
| `[Range]` | `minimum` / `maximum`，开区间用 `exclusiveMinimum` / `exclusiveMaximum` |
| `[StringLength]`、`[MinLength]`、`[MaxLength]`、`[Length]` | 字符串的 `minLength` / `maxLength`，集合的 `minItems` / `maxItems` |
| `[RegularExpression]` | `pattern` |

不可空、没有默认值的属性本身就进 `required`，不需要 `[Required]`；`[Required]` 的额外作用是让字符串至少有一个字符。`[EmailAddress]`、`[Url]` 等注解不折进 Schema，写成自校验。

Schema 校验支持的关键字：`type`（含可空）、`properties`、`required`、`additionalProperties`、`items`、`enum`、`const`、`anyOf` / `oneOf` / `allOf`、`$ref`、`minimum` / `maximum` 与开区间、`minLength` / `maxLength`、`pattern`、`minItems` / `maxItems`。`format` 等其他关键字不断言。一次最多报告 32 条违规，嵌套深度上限 64。

## 自校验

Schema 表达不了的规则（字段之间的关系、业务上的黑名单、需要查服务的检查）写在请求类型的 `IValidatableObject.Validate` 里：

- `ValidationContext` 带请求类型名与本次调用的服务提供器，`validationContext.GetService(typeof(T))` 能取到作用域服务。
- `ErrorMessage` 是应用自己的文案，用 `key.L()` 取，得到的是调用方的语言；对应的键要写进应用的语言文件。
- `MemberNames` 里的成员名按 camelCase 转成序列化名，成为字段级错误的键。
- 自校验在 Schema 通过之后才执行，所以方法体里可以假定注解约束已经满足。

## 失败结果

校验失败的 `Error` 带这些细节：

| 细节 | 内容 |
| --- | --- |
| `id` | 操作 ID |
| `reason` | 第一条违规的文本 |
| `violations` | 全部违规的文本，每条形如 `$.path: reason` |
| `errors` | 违规对象（`SchemaViolation`）：字段键、原因、消息键与参数 |

宿主把 `errors` 翻译成调用方语言并按字段分组。字段键是去掉根 `$` 的点分路径（`lines[0].sku` 这类嵌套字段同样适用）；缺少必填属性的违规挂在缺失属性自己的路径上。

| 错误码 | 情形 |
| --- | --- |
| `KERNEL_VALIDATION_001` | 载荷不是 JSON、为 `null` 或与请求类型不匹配 |
| `KERNEL_VALIDATION_002` | 类型化调用的请求或响应类型与登记不符 |
| `KERNEL_VALIDATION_003` | 载荷不满足请求 Schema |
| `KERNEL_VALIDATION_004` | `IValidatableObject` 自校验失败 |

gRPC 上这些失败是 `InvalidArgument`，命令行上退出码 2，MCP 上是 `isError` 结果，映射表见[结果与错误处理](/web/errors)。

## 不经过校验的路径

- `IOperationInvoker.ExecuteAsync` 用于调用方已经在管道内的场合，直接执行操作，不校验；要校验就用 `InvokeAsync`。
- 宿主端点的载荷在 Validate 阶段开头才绑定（HTTP 的路由值、请求体与查询串），排在授权之后；绑定失败（请求体超过上限、不是 JSON）和校验失败一样写成 Problem Details。
- `XiHan:Kernel:UseBuiltInMiddleware=false` 时 `ValidationMiddleware` 不登记，应用需要自己在 Validate 阶段提供校验。

需要额外校验（如按租户配置的业务规则）时，在 Validate 阶段末尾登记应用自己的中间件，用 `context.FindOperationRequest()` 取解码后的请求对象（`Maybe<object>`），失败用 `context.SetOutcome(Error.Validation(...))` 结束调用：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .ConfigurePipeline(pipeline => pipeline.UseAt<TenantRulesMiddleware>(PipelineStage.Validate)));
```

中间件的写法见[执行管道](/startup/pipeline)。

## 常见问题

### 为什么不用 FluentValidation 这类库

它们在运行期用反射或表达式树构建规则，Native AOT 下不可用，而且只在一种入口生效。框架的两步校验对 HTTP、gRPC、MCP、命令行统一执行，注解进 Schema 还能同时生成 OpenAPI 与 MCP 的 `inputSchema`。复杂规则写在 `IValidatableObject.Validate` 里。

### 校验错误的文案能自定义吗

Schema 违规的文案是内核的消息键（`kernel.validation.required`、`kernel.validation.minimum` 等），按调用方语言翻译，不按字段自定义。需要业务化的提示就把规则放进自校验，用自己的消息键。

### 嵌套对象与集合里的字段怎么报

字段键是点分路径，如 `lines[0].sku`。嵌套对象进 `$defs`，它们属性上的注解同样折进对应的定义；自校验要自己递归调用子对象的 `Validate`。

### 一次请求有多处错误，会全部报出来吗

Schema 校验一次最多报告 32 条违规；自校验的每条 `ValidationResult` 都报。两步不混报：Schema 不通过时不执行自校验。

### 响应也会校验吗

不会。响应 Schema 用于 OpenAPI、gRPC 的 protobuf 映射与 MCP 的 `outputSchema`，运行期不断言。类型化调用的响应类型与登记不符时报 `KERNEL_VALIDATION_002`，那是编程错误。

## 相关包与下一步

- [Kernel](/packages/kernel)：`ValidationMiddleware`、`SchemaViolation`、`JsonSchemaValidator`
- [Kernel.Generators](/packages/kernel-generators)：注解折进 Schema 的生成器
- [Primitives](/packages/primitives)：`Error.Validation` 与 `MessageKeyExtensions.L`
- 规范：RFC-0011《操作契约与 XGI》的「校验」一节
- 下一章：[领域模型](/persistence/domain)
