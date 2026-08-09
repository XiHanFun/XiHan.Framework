# 数据校验

框架的校验能力分散在三个地方：ASP.NET Core 的模型验证负责请求入口，业务异常负责规则判断，扩展属性验证器负责动态字段。这一章讲清楚各自的边界，以及校验失败最终变成什么业务码回给前端。

## 校验能力的真实边界

::: warning 先看清这一点
`XiHan.Framework.Validation` 是**空壳包**——只有一个 `XiHanValidationModule`，`ConfigureServices` 不注册任何服务。框架**没有**统一的 `IObjectValidator`、没有校验拦截器、没有「进方法自动校验入参」的 AOP。

真正干活的是 ASP.NET Core 内置的模型验证（DataAnnotations）。校验包提供的只是一个**异常契约**。
:::

三条实际可用的校验路径：

| 路径 | 由谁执行 | 触发时机 | 失败结果 |
| --- | --- | --- | --- |
| DataAnnotations 特性 | ASP.NET Core 模型绑定 | 进入 Action 之前 | HTTP 400 + 业务码 `400` |
| 业务规则判断 | 你自己写的 `if` + 抛异常 | 方法体内 | 按异常类型映射，见下文 |
| 扩展属性校验 | `ExtensibleObjectValidator` | 你显式调用 | 抛 `XiHanValidationException` |

## 安装与启用

主力路径（DataAnnotations）**不需要装任何校验包**——它属于 ASP.NET Core，`XiHan.Framework.Web.Api` 已经把响应格式接好了。

只有当你需要 `XiHanValidationException` 这个类型时才引包：

```bash
dotnet add package XiHan.Framework.Validation.Abstractions
```

```csharp
[DependsOn(typeof(XiHanValidationAbstractionsModule))]
public class MyModule : XiHanModule { }
```

::: tip 通常你已经有它了
`XiHanObjectMappingModule` 已 `[DependsOn]` 了 `XiHanValidationAbstractionsModule`，所以引入对象映射的项目会传递拿到校验抽象。

而 `XiHan.Framework.Validation`（实现包）**没有任何框架模块依赖它**，引不引都不影响运行时行为。
:::

## 核心用法

### 请求 DTO 上标注解

这是 95% 的场景该走的路。在输入 DTO 上写标准的 `System.ComponentModel.DataAnnotations` 特性：

```csharp
using System.ComponentModel.DataAnnotations;

public class UserCreateDto
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(32, MinimumLength = 4, ErrorMessage = "用户名长度需在 4-32 之间")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Range(0, 150, ErrorMessage = "年龄需在 0-150 之间")]
    public int Age { get; set; }
}
```

动态 API 生成的控制器会被写入 `[ApiController]` 特性（由 `DynamicApiControllerFactory` 在构建类型时发射），因此**自动模型验证默认生效**：模型无效时请求根本不会进入你的方法体。

::: warning ErrorMessage 一定要写
`InvalidModelStateResponseFactory` 拼接的是 `ModelState` 里的 `ErrorMessage`。不写就会回落到 .NET 的默认英文提示（如 `The Email field is not a valid e-mail address.`），直接展示给用户很难看。
:::

### 业务规则用异常表达

DataAnnotations 只能校验单字段的格式与范围。「用户名是否已被占用」「订单状态是否允许取消」这类需要查库或跨字段的规则，在方法体里判断并抛异常：

```csharp
public async Task<UserDto> CreateAsync(UserCreateDto input)
{
    if (await _users.AnyAsync(u => u.UserName == input.UserName))
    {
        // 输入格式合法，但当前状态不允许 → 422
        throw new InvalidOperationException("用户名已被占用");
    }

    if (input.Age < 18 && input.NeedGuardian is false)
    {
        // 请求本身有问题 → 400
        throw new UserFriendlyException("未成年用户必须填写监护人");
    }

    // ...
}
```

选哪个异常决定了返回码，见下一节的映射表。

### 扩展属性的校验

给实体动态挂的扩展属性（`IHasExtraProperties`）不经过模型绑定，需要显式调用 `ExtensibleObjectValidator`。注册属性时把校验特性放进 `Attributes`：

```csharp
ObjectExtensionManager.Instance
    .AddOrUpdateProperty<SysUser, string>("IdCardNo", property =>
    {
        property.Attributes.Add(new RequiredAttribute());
        property.Attributes.Add(new StringLengthAttribute(18) { MinimumLength = 15 });

        // 特性表达不了的规则用委托
        property.Validators.Add(context =>
        {
            if (context.Value is string value && value.StartsWith("000"))
            {
                context.ValidationErrors.Add(
                    new ValidationResult("身份证号非法", [context.ExtensionPropertyInfo.Name]));
            }
        });
    });
```

三种调用方式：

```csharp
// 1. 只问结果
var ok = ExtensibleObjectValidator.IsValid(user);

// 2. 拿到全部错误明细
List<ValidationResult> errors = ExtensibleObjectValidator.GetValidationErrors(user);

// 3. 失败即抛 XiHanValidationException
ExtensibleObjectValidator.GuardValue(user, "IdCardNo", value);
```

::: danger GuardValue 抛出的异常会变成 500
详见下面「异常映射」一节——`XiHanValidationException` 没有被异常映射表覆盖。生产代码建议用 `GetValidationErrors` 拿到明细后，自行转成业务异常或 `ApiResponse.Failure`。
:::

## 关键机制

### 模型校验失败返回的是 400，不是 11000

`XiHanWebApiServiceCollectionExtensions.AddXiHanWebApiMvc` 里配置的工厂如下（要点摘录）：

```csharp
options.InvalidModelStateResponseFactory = actionContext =>
{
    var traceId = actionContext.HttpContext.TraceIdentifier;
    var errors = actionContext.ModelState.Values
        .SelectMany(v => v.Errors)
        .Select(e => e.ErrorMessage)
        .Where(e => !string.IsNullOrWhiteSpace(e))
        .Distinct()
        .ToArray();

    var message = errors.Length > 0 ? string.Join("; ", errors) : "请求参数校验失败";

    return new BadRequestObjectResult(ApiResponse.BadRequest(message, traceId));
};
```

产出的响应体：

```json
{
  "code": 400,
  "message": "请求错误",
  "data": "用户名不能为空; 邮箱格式不正确",
  "traceId": "0HN7A...",
  "timestamp": "2026-08-05T10:00:00+00:00"
}
```

三个必须记住的点：

| 事实 | 含义 |
| --- | --- |
| `code` 是 `400` | **不是 11000**。框架的自动模型校验从不产出 11000 |
| 明细在 `data` 而不是 `message` | `message` 固定是业务码描述「请求错误」；多条错误用 `; ` 拼成一个字符串放进 `data` |
| 错误经过 `Distinct()` | 不同字段的相同文案会被去重，且**不携带字段名** |

::: warning 前端取值顺序
错误明细放在 `data`，所以前端提取错误信息时要**优先读 `data`**，`message` 只是通用码描述。这个约定在整个响应管线里是一致的：`ApiResponse.BadRequest` / `UnprocessableEntity` / `ServiceUnavailable` 都把具体错误放 `data`。
:::

### 11000 业务码需要你自己发

`ApiResponseCodes.ValidationFailed = 11000`（描述「数据校验失败」）定义在 `XiHan.Framework.Application.Contracts`，但**框架源码里没有任何一处产出它**。它是留给应用层显式使用的业务语义码。

想要发 11000，用通用工厂 `ApiResponse.Failure`：

```csharp
return ApiResponse.Failure(
    ApiResponseCodes.ValidationFailed,
    "手机号已被其他账号绑定");
```

得到：

```json
{
  "code": 11000,
  "message": "数据校验失败",
  "data": "手机号已被其他账号绑定"
}
```

::: tip 什么时候值得用 11000
`400` 表达「请求格式不对」，`11000` 表达「格式没问题，是业务约束没过」，前端可以据此区分「是我参数拼错了」和「该提示用户改输入」。若不需要这个区分度，直接用 `422` 更省事。

注意 `ApiResponse.IsSuccess` 的判定是 `Code` 落在 `[200, 300)`，所以 `11000` 天然是失败态。
:::

要让 11000 从异常路径自动产出，得自己扩展映射——框架的映射表是静态方法 `XiHanApiResponseResultFilter.MapException`，不接受外部注册。实践中更省力的做法是在应用服务里直接返回 `ApiResponse.Failure(...)`，而不是抛异常。

### 异常映射表

未处理异常由 `XiHanApiResponseResultFilter`（同时是 `IAsyncExceptionFilter`）统一转成响应：

| 抛出的异常 | HTTP 状态 | 业务码 | 消息位置 |
| --- | --- | --- | --- |
| `ServiceUnavailableException` | 503 | 503 | `data` |
| `UserFriendlyException` | 400 | 400 | `data` |
| `BusinessException` | 400 | 400 | `data` |
| `UnauthorizedAccessException` | 401 | 401 | `data`（固定「未授权访问」） |
| `KeyNotFoundException` | 404 | 404 | — |
| `ArgumentException` | 400 | 400 | `data` |
| `InvalidOperationException` | 422 | 422 | `data` |
| 其它任何异常 | 500 | 500 | `data` 留空 |

::: danger XiHanValidationException 落在「其它」分支
`XiHanValidationException` 继承链是 `XiHanException` → `Exception`，它**不实现** `IBusinessException`、也不是 `BusinessException` 或 `ArgumentException` 的派生类型。

所以直接把它抛到 Web 层，用户看到的是 **500 服务器内部错误**，且 `ValidationErrors` 里的明细**不会**出现在响应里（500 分支的 `data` 刻意留空以免泄露内部细节）。

要把校验错误正确回给前端，请转换后再抛：

```csharp
var errors = ExtensibleObjectValidator.GetValidationErrors(user);
if (errors.Count > 0)
{
    throw new UserFriendlyException(
        string.Join("; ", errors.Select(e => e.ErrorMessage)));
}
```
:::

### 自我日志不会自动触发

`XiHanValidationException` 实现了 `IExceptionWithSelfLogging`，其 `Log(ILogger)` 方法会把 `ValidationErrors` 逐条格式化成「存在 N 个验证错误：」的文本，按 `LogLevel`（默认 `Warning`）写出，并把 `MemberNames` 附在消息后的括号里。

但这个方法只在 `ILogger.LogException(ex)` 扩展方法内部被调用，而 **MVC 请求管线并不调用 `LogException`**。想要这份明细日志，必须自己显式调：

```csharp
catch (XiHanValidationException ex)
{
    _logger.LogException(ex);   // 这时 Log(ILogger) 才会执行
    throw;
}
```

### 校验消息的国际化

`InvalidModelStateResponseFactory` 拼接的是 `ErrorMessage` 原文，**不走本地化管线**。要让校验提示随请求语言变化，用 DataAnnotations 自带的资源机制：

```csharp
[Required(
    ErrorMessageResourceType = typeof(ValidationResource),
    ErrorMessageResourceName = "UserNameRequired")]
public string UserName { get; set; } = string.Empty;
```

响应过滤器的本地化只覆盖两类：业务码的通用描述（资源名 `ApiResponse`，键为语义码名如 `BadRequest`），以及 `BusinessException` 携带的 `LocalizableMessage`。

## 配置

校验本身**没有任何配置节**——两个校验包都不定义 Options，`appsettings.json` 里没有 `XiHan:Validation` 之类的键。

唯一可调的是模型校验的响应形态，通过覆盖 `InvalidModelStateResponseFactory`。在 `AddXiHanWebApiMvc()` 之后重新配置即可（后注册的覆盖先注册的）：

```csharp
services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        // 例：改为按字段名分组返回，并使用 11000 业务码
        var details = context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var response = ApiResponse.Failure(ApiResponseCodes.ValidationFailed);
        response.Data = details;
        response.TraceId = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(response);
    };
});
```

想彻底关掉自动 400、改为在方法体内手工检查 `ModelState`，设 `options.SuppressModelStateInvalidFilter = true`。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 校验失败返回 400 而不是 11000 | 这是框架的默认行为，11000 需要应用层显式产出 |
| 前端拿到的 `message` 永远是「请求错误」 | 明细在 `data` 里，改前端提取逻辑优先读 `data` |
| 错误提示是英文 | DTO 特性没写 `ErrorMessage` |
| 不知道是哪个字段错了 | 默认工厂只拼接消息文本、不带字段名；需要字段名就覆写 `InvalidModelStateResponseFactory` |
| 两个字段的相同报错只显示一条 | 默认工厂对消息做了 `Distinct()` 去重 |
| 抛 `XiHanValidationException` 却返回 500 | 它不在异常映射表内，转成 `UserFriendlyException` 再抛 |
| `ValidationErrors` 明细在日志里找不到 | 自我日志需显式调用 `logger.LogException(ex)` |
| 扩展属性上的 `[Required]` 没生效 | 扩展属性不经模型绑定，须显式调用 `ExtensibleObjectValidator` |
| 引了 `XiHan.Framework.Validation` 但没有任何变化 | 该包不注册服务，属正常现象 |
| DTO 特性完全不起作用 | 检查控制器是否有 `[ApiController]`；手写的控制器不会自动带上 |

## 下一步

- [Web 应用开发](./web)：响应管线与统一返回信封
- [动态 API](./dynamic-api)：控制器生成规则与 `[ApiController]` 的注入时机
- [对象映射](../packages/objectmapping)：`ExtensibleObjectValidator` 与扩展属性体系所在的包
- [国际化](../packages/localization)：可本地化消息的解析规则
- [Validation 包](../packages/validation)、[Validation.Abstractions 包](../packages/validation-abstractions)：完整类型清单
- [Application.Contracts 包](../packages/application-contracts)：`ApiResponse` 与 `ApiResponseCodes` 全量码表
