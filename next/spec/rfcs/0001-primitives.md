# RFC-0001 原语层

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：0002 | 参考实现：`next/src/XiHan.Framework.Primitives`

## 摘要

定义 `XiHan.Framework.Primitives`（Ring 0）的全部公开类型及其跨语言语义与线上编码。原语层零行为、零依赖、只增不改。

## 动机

十年稳定要求只冻结极小的一层。原语层是所有包、所有语言 SDK、所有协议适配器共享的词汇；它一旦漂移，上面的一切都要跟着变。

## 公开面（.NET 形态）

命名空间 `XiHan.Framework.Primitives`，清单模型在子命名空间 `XiHan.Framework.Primitives.Manifests`。完整签名以 `PublicAPI.Unshipped.txt` 为准，5.0.0 发布时转入 `PublicAPI.Shipped.txt` 并冻结。

### Result

```csharp
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Error? Error { get; }                        // 失败时非 null；default 值视为 PRIMITIVES_VALIDATION_002
    public static Result Success();
    public static Result Failure(Error error);
    public static Result<T> Success<T>(T value);        // 值不能为 null
    public static Result<T> Failure<T>(Error error);
    public static implicit operator Result(Error error);
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure);
    public Result Bind(Func<Result> next);
    public void ThrowIfFailure();                       // 抛 FrameworkException(Error.Code)
}

public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Error? Error { get; }
    public static implicit operator Result<T>(T value);
    public static implicit operator Result<T>(Error error);
    public bool TryGetValue([MaybeNullWhen(false)] out T value);
    public T ValueOrThrow();
    public T ValueOr(T fallback);
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure);
    public Result<TOut> Map<TOut>(Func<T, TOut> map);
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind);
    public Result ToResult();
}
```

工厂方法放在非泛型 `Result` 上，不在泛型类型上声明静态成员。成功值不允许为 `null`："成功但没有值"用 `Result` 或 `Result<Maybe<T>>`。

线上编码（JSON）：

```json
{ "ok": true, "value": … }
{ "ok": false, "error": { "code": "PERSISTENCE_CONFLICT_001", "message": "…", "messageKey": "persistence.entity.stale_stamp", "details": { … }, "inner": { … } } }
```

### Error 与 ErrorCode

```csharp
public sealed record Error
{
    public Error(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null, Error? inner = null);
    public string Code { get; }
    public string? MessageKey { get; }                              // 消息键；Literal 创建的为 null
    public string? Text { get; }                                    // 已解析的文案；未解析为 null
    public string Message { get; }                                  // Text，否则按进程默认区域解析，再否则"键 (参数)"
    public IReadOnlyDictionary<string, object?>? Details { get; }   // 模板参数，也是结构化细节；按引用比较
    public Error? Inner { get; }
    public string Kind { get; }                                     // 错误码的 KIND 段
    public static Error Literal(string code, string text, IReadOnlyDictionary<string, object?>? details = null, Error? inner = null);   // 线上还原、异常转换
    public static Error Validation(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null);
    public static Error NotFound(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null);
    public static Error Conflict(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null);
    public static Error Denied(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null);
    public static Error Unavailable(string code, string messageKey, IReadOnlyDictionary<string, object?>? details = null);
    public static Error Unexpected(string code, string messageKey, Error? inner = null, IReadOnlyDictionary<string, object?>? details = null);
    public Error WithCode(string code);                             // 换码，保留键、文案、参数
    public Error WithText(string text);                             // 填入已解析文案
}

// 消息原语（RFC-0010）：键文法与模板格式化、参数字典、key.L()、环境作用域
public static class MessageTemplate { IsValidKey; ValidateKey; DomainOf; Describe; Format; TryFormat; PlaceholdersOf; }
public static class MessageArguments { Of(params (string, object?)[]); Positional(params object?[]); }
public static class MessageKeyExtensions { L(this string key); L(key, params (string, object?)[]); L(key, params object?[]); L(key, IReadOnlyDictionary); }
public interface IMessageResolver { bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, out string? text); }
public sealed class MessageScope : IDisposable { static Current; static Default; static Use(resolver, locale); static SetDefault(resolver, locale); Resolve(key, arguments); }

public static class ErrorCode
{
    public const string InvalidArgument = "PRIMITIVES_VALIDATION_001";
    public const string UninitializedResult = "PRIMITIVES_VALIDATION_002";
    public static bool IsValid(string? code);
    public static string Validate(string code, string? paramName = null);
    public static string KindOf(string code);
    public static string DomainOf(string code);
}
```

- `Code` 必须符合 `<领域大写>_<KIND>_<NNN>`；工厂方法校验 KIND 段与方法名一致。
- `Message` 面向人，不参与程序判断；`Details` 只允许 JSON 可表示的值。
- `MessageKey` 文法 `^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$`，文案在语言文件里（RFC-0010）；`message` 线上字段是发送端已解析的文案，`messageKey` 让接收端可再翻译。

### Maybe

```csharp
public static class Maybe
{
    public static Maybe<T> Some<T>(T value);            // 值不能为 null
    public static Maybe<T> None<T>();
    public static Maybe<T> From<T>(T? value) where T : class;
    public static Maybe<T> From<T>(T? value) where T : struct;
}

public readonly record struct Maybe<T>
{
    public bool HasValue { get; }                       // default 即无值
    public static implicit operator Maybe<T>(T value);
    public bool TryGetValue([MaybeNullWhen(false)] out T value);
    public T ValueOr(T fallback);
    public T ValueOrThrow();
    public TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone);
    public Maybe<TOut> Map<TOut>(Func<T, TOut> map);
    public Maybe<TOut> Bind<TOut>(Func<T, Maybe<TOut>> bind);
    public Result<T> ToResult(Error error);
}
```

线上编码：有值为值本身，无值为 `null`。

### TypedId

```csharp
public interface ITypedId<out TValue> where TValue : notnull
{
    TValue Value { get; }
}

public readonly record struct TypedId<TTag, TValue>(TValue Value) : ITypedId<TValue>
    where TValue : notnull, IEquatable<TValue>;
```

`TTag` 只用于区分类型，不承载数据。线上编码为 `TValue` 的编码（string / uuid / int64）；用户自定义的标识类型实现 `ITypedId<TValue>` 即可被生成器与协议适配器识别。

### InvocationContext 与 BaggageKeys

```csharp
public sealed class InvocationContext
{
    public required CancellationToken CancellationToken { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public required string TraceId { get; init; }                        // 32 位小写十六进制
    public required string SpanId { get; init; }                         // 16 位小写十六进制
    public required IReadOnlyDictionary<string, string> Baggage { get; init; }   // 键必须符合 xihan.<name>
    public IDictionary<object, object?> Items { get; }                   // 不传播；派生上下文共享
    public required IServiceProvider Services { get; init; }
    public bool TryGetBaggage(string key, [MaybeNullWhen(false)] out string value);
    public InvocationContext WithBaggage(string key, string value);
    public bool IsExpired(DateTimeOffset now);
    public TimeSpan? RemainingTime(DateTimeOffset now);
}

public static class BaggageKeys
{
    public const string Tenant = "xihan.tenant";
    public const string Principal = "xihan.principal";
    public const string Correlation = "xihan.correlation";
    public const string IdempotencyKey = "xihan.idempotency-key";
    public const string Locale = "xihan.locale";
    public static IReadOnlyList<string> Registered { get; }
    public static bool IsWellFormed(string? key);
    public static bool IsRegistered(string? key);
}
```

语义见 RFC-0003。

### 稳定性、生命周期与管道阶段

```csharp
public enum Stability { Stable, Preview, Experimental, Internal, Deprecated }

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
public sealed class ApiLevelAttribute(Stability stability, string since) : Attribute
{
    public Stability Stability { get; }
    public string Since { get; }
    public string? ScheduledStable { get; init; }
}

public enum LifecycleState { Declared, Resolved, Configured, Started, Ready, Degraded, Draining, Stopped, Failed }

public static class LifecycleStateExtensions
{
    public static bool CanTransitionTo(this LifecycleState from, LifecycleState to);
    public static bool IsServing(this LifecycleState state);     // Ready 或 Degraded
    public static bool IsTerminal(this LifecycleState state);    // Stopped
}

public enum PipelineStage { Ingress, Observe, Resilience, Authenticate, Tenant, Authorize, Validate, Idempotency, Transaction, Handle, Egress }

public static class PipelineStageExtensions
{
    public static IReadOnlyList<PipelineStage> All { get; }
    public static string ToManifestName(this PipelineStage stage);
    public static bool TryParse(string? name, out PipelineStage stage);
}
```

合法迁移表是规范性内容，完整列表在 `spec/conformance/vectors/primitives/lifecycle-transitions.json`。管道阶段的名称与顺序是规范性内容（RFC-0004）。

### 异常与框架信息

```csharp
public class FrameworkException(string code, string message, Exception? innerException = null) : Exception
{
    public string Code { get; }
    public Error ToError();
}

public static class FrameworkInfo
{
    public const string Name = "XiHan Framework";
    public const string SpecVersion = "1";
    public static string Version { get; }         // 构建期生成的常量，不经反射
    public static Uri Repository { get; }
    public static Uri Documentation { get; }
}
```

### 清单模型（`Manifests`）

与 `spec/manifest/manifest.schema.json` 一一对应的不可变记录，只做数据与格式校验：

| 类型 | 内容 |
| --- | --- |
| `Manifest` | 全部字段；`Validate()` 检查跨字段约束（插件必须声明隔离、非插件不得声明权限、不得重复提供或依赖） |
| `ManifestKind` / `ManifestRole` / `PluginIsolation` | 枚举 |
| `CapabilityDeclaration` | `id@version`，可 `Parse` / `TryParse` |
| `CapabilityRequirement` | `id@[^]version[?]`，可 `Parse` / `TryParse`，`IsSatisfiedBy(declaration)` |
| `Binding` | 绑定名与资源类型 |
| `Contributions` / `PipelineContribution` / `ConfigContribution` | 贡献点 |
| `CapabilityId` / `ManifestId` | 格式校验与领域段提取 |

接口版本是整数；`^N` 与 `N` 等价。收集、解析与求解是内核的职责。

## 跨语言语义

| 原语 | 必须一致 |
| --- | --- |
| Result | 成功与失败互斥；失败必有 Error；JSON 形态 |
| Error | Code 格式与 KIND 段；Details 仅 JSON 值；Inner 链 |
| Maybe | 与 `null` 的编码等价，但类型层面不可为空 |
| TypedId | 编码为底层值；标签不上线 |
| InvocationContext | 字段集合、W3C 格式、Baggage 键格式与传播载体（RFC-0003） |
| Stability | 五级含义与弃用流程 |
| LifecycleState | 九态与迁移表 |
| PipelineStage | 十一阶段的名称与顺序 |
| 清单 | Schema、ID 格式、依赖文本语法 |

线上 JSON 编码由协议适配器与源生成器实现，原语层不含序列化代码（保持零行为与零反射）。

## 冻结规则

只增不改；破坏即新包 `XiHan.Framework.Primitives.V2` 并存。公开面以 `PublicAPI.Shipped.txt` 为准。

## 一致性向量

`spec/conformance/vectors/primitives/`：`error-codes.json`、`lifecycle-transitions.json`、`capability-requirements.json`、`baggage-keys.json`。Result/Error/Maybe/TypedId 的编码向量随协议适配器（P3）补充。

## 备选方案

- 把原语放进 Kernel：否决，内核会随小版本变化，原语层必须独立冻结。
- 用异常代替 Result：否决，可恢复失败不应走异常，且跨语言不可移植。
- 在泛型类型上声明工厂静态成员（`Result<T>.Success`）：否决，统一放在非泛型 `Result` / `Maybe` 上。
