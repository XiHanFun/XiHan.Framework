# 数据校验

本章说明 Validate 阶段如何校验操作的请求：按生成的 JSON Schema 校验载荷，再对实现 `IValidatableObject` 的请求执行自校验，失败时以字段级错误返回。适用于给操作请求加约束的开发者。

5.0 不在运行期反射读取 DataAnnotations，也不集成第三方校验库；注解在编译期由源生成器折进 Schema。

完整 API 见 [Kernel 包](../packages/kernel)。

## 两步校验

内核的内置中间件 `ValidationMiddleware` 登记在 Validate 阶段开头，只处理带操作描述的调用，按顺序执行：

1. 取 JSON 载荷：HTTP、gRPC、MCP、命令行进来的调用用协议适配器绑定好的载荷（空载荷视为 `{}`）；类型化调用（`IOperationInvoker.InvokeAsync<TRequest, TResponse>`）按序列化元数据把请求对象编码一次。
2. 操作带 Schema 时按请求 Schema 校验载荷。不通过返回 `KERNEL_VALIDATION_003`；载荷不是 JSON 返回 `KERNEL_VALIDATION_001`。
3. 解码成请求对象并记录在上下文。请求实现了 `IValidatableObject` 时调用 `Validate`，返回的每条 `ValidationResult` 按成员名列为违规，返回 `KERNEL_VALIDATION_004`。

任一步失败，调用以 VALIDATION 种类的结果结束，不进入 Idempotency、Transaction 与 Handle 阶段，HTTP 上是 400。

## 用注解声明约束

请求类型上的 DataAnnotations 由 `Kernel.Generators` 在编译期折进请求 Schema（对照表见[操作契约](./operations#登记序列化类型与-schema)）。位置记录的主构造参数上的注解同样生效：

```csharp
using System.ComponentModel.DataAnnotations;

namespace Shop.Orders;

public sealed record PlaceOrderRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Name,
    [Range(1, 1_000_000)] int Amount);
```

不可空、没有默认值的属性本身就进 `required`，不需要 `[Required]`；`[Required]` 的额外作用是让字符串至少有一个字符。`[EmailAddress]`、`[Url]` 等注解不折进 Schema，写成自校验。

Schema 校验支持的关键字：`type`（含可空）、`properties`、`required`、`additionalProperties`、`items`、`enum`、`const`、`anyOf` / `oneOf` / `allOf`、`$ref`、`minimum` / `maximum` 与开区间、`minLength` / `maxLength`、`pattern`、`minItems` / `maxItems`。`format` 等其他关键字不断言。一次最多报告 32 条违规，嵌套深度上限 64。

## 自校验

Schema 表达不了的规则写在请求类型的 `IValidatableObject.Validate` 里：

```csharp
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

public static class ShopMessages
{
    public const string OrderNameBlank = "shop.order.name_blank";
}
```

- `ValidationContext` 带请求类型名与本次调用的服务提供器，`validationContext.GetService(...)` 能取到作用域服务。
- `ErrorMessage` 是应用自己的文案，用 `key.L()` 取，得到的是调用方的语言；对应的键要写进应用的语言文件，见[国际化](./localization)。
- 成员名按 camelCase 转成序列化名，成为字段级错误的键。

## 失败结果

校验失败的 `Error` 带这些细节：

| 细节 | 内容 |
| --- | --- |
| `id` | 操作 ID |
| `reason` | 第一条违规的文本 |
| `violations` | 全部违规的文本，每条形如 `$.path: reason` |
| `errors` | 违规对象（`SchemaViolation`）：字段键、原因、消息键与参数 |

宿主把 `errors` 翻译成调用方语言并按字段分组。HTTP 的 Problem Details：

```json
{
  "type": "urn:xihan:error:KERNEL_VALIDATION_003",
  "title": "VALIDATION",
  "status": 400,
  "detail": "操作 'greetings.create' 的请求不符合契约：$.name: required property 'name' is missing",
  "code": "KERNEL_VALIDATION_003",
  "errors": { "name": ["缺少必填属性 'name'"] },
  "details": { "id": "greetings.create", "reason": "$.name: required property 'name' is missing" }
}
```

字段键是去掉根 `$` 的点分路径（`lines[0].sku` 这类嵌套字段同样适用）；缺少必填属性的违规挂在缺失属性自己的路径上。

| 错误码 | 情形 |
| --- | --- |
| `KERNEL_VALIDATION_001` | 载荷不是 JSON、为 `null` 或与请求类型不匹配 |
| `KERNEL_VALIDATION_002` | 类型化调用的请求或响应类型与登记不符 |
| `KERNEL_VALIDATION_003` | 载荷不满足请求 Schema |
| `KERNEL_VALIDATION_004` | `IValidatableObject` 自校验失败 |

## 不经过校验的路径

- `IOperationInvoker.ExecuteAsync` 用于调用方已经在管道内的场合，直接执行操作，不校验。
- 宿主端点在进入管道之前绑定载荷（HTTP 的路由值、请求体与查询串）；绑定失败直接写 Problem Details，不进入管道。
- `XiHan:Kernel:UseBuiltInMiddleware=false` 时 `ValidationMiddleware` 不登记，应用需要自己在 Validate 阶段提供校验。

需要额外校验（如按租户配置的业务规则）时，在 Validate 阶段末尾登记应用自己的中间件，用 `context.GetOperationRequest()` 取解码后的请求对象。

## 相关

- [操作契约](./operations)
- [结果与错误](./errors)
- [国际化](./localization)
- 规范：RFC-0011《操作契约与 XGI》的「校验」一节
