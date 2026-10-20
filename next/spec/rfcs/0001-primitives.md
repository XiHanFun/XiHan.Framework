# RFC-0001 原语层

> 状态：草案 | 日期：2026-10-21 | 对应 ADR：0002

## 摘要

定义 `XiHan.Framework.Primitives`（Ring 0）的全部公开类型及其跨语言语义与线上编码。原语层零行为、零依赖、只增不改。

## 动机

十年稳定要求只冻结极小的一层。原语层是所有包、所有语言 SDK、所有协议适配器共享的词汇；它一旦漂移，上面的一切都要跟着变。

## 公开面（.NET 形态）

命名空间 `XiHan.Framework.Primitives`，清单模型在子命名空间 `XiHan.Framework.Primitives.Manifests`。

### Result

```csharp
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }
    public static Result Success();
    public static Result Failure(Error error);
    public static implicit operator Result(Error error);
}

public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public Error? Error { get; }
    public bool TryGetValue([MaybeNullWhen(false)] out T value);
    public T ValueOrThrow();                       // 失败时抛 FrameworkException(Error.Code)
    public static Result<T> Success(T value);
    public static Result<T> Failure(Error error);
    public static implicit operator Result<T>(T value);
    public static implicit operator Result<T>(Error error);
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure);
    public Result<TOut> Map<TOut>(Func<T, TOut> map);
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind);
}
```

线上编码（JSON）：

```json
{ "ok": true, "value": … }
{ "ok": false, "error": { "code": "PERSISTENCE_CONFLICT_001", "message": "…", "details": { … }, "inner": { … } } }
```

### Error

```csharp
public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Details = null,
    Error? Inner = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, object?>? details = null);
    public static Error NotFound(string code, string message);
    public static Error Conflict(string code, string message);
    public static Error Denied(string code, string message);
    public static Error Unavailable(string code, string message);
    public static Error Unexpected(string code, string message, Error? inner = null);
}
```

- `Code` 必须符合 `spec/conventions/error-codes.md` 的格式；`Message` 面向人，不参与程序判断。
- `Details` 只允许 JSON 可表示的值。

### Maybe

```csharp
public readonly record struct Maybe<T>
{
    public bool HasValue { get; }
    public bool TryGetValue([MaybeNullWhen(false)] out T value);
    public T ValueOr(T fallback);
    public static Maybe<T> Some(T value);
    public static Maybe<T> None { get; }
    public static implicit operator Maybe<T>(T value);
    public TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone);
    public Maybe<TOut> Map<TOut>(Func<T, TOut> map);
}
```

线上编码：有值为值本身，无值为 `null`。

### TypedId

```csharp
public readonly record struct TypedId<TTag, TValue>(TValue Value)
    where TValue : notnull, IEquatable<TValue>
{
    public override string ToString();
}
```

`TTag` 只用于区分类型，不承载数据：`TypedId<Order, Guid>` 与 `TypedId<Customer, Guid>` 不可互相赋值。线上编码为 `TValue` 的编码（string / uuid / int64）。

### InvocationContext

```csharp
public sealed class InvocationContext
{
    public required CancellationToken CancellationToken { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public required IReadOnlyDictionary<string, string> Baggage { get; init; }
    public IDictionary<object, object?> Items { get; }
    public required IServiceProvider Services { get; init; }
    public InvocationContext WithBaggage(string key, string value);
}
```

语义见 RFC-0003。

### 稳定性与生命周期

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
```

### 异常与框架信息

```csharp
public class FrameworkException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; }
    public Error ToError();
}

public static class FrameworkInfo
{
    public static string Name { get; }            // "XiHan Framework"
    public static string Version { get; }         // 程序集信息版本
    public static string SpecVersion { get; }     // 实现的 Spec 版本，"1"
    public static Uri Repository { get; }
}
```

### 清单模型

`XiHan.Framework.Primitives.Manifests` 提供与 `spec/manifest/manifest.schema.json` 一一对应的不可变记录：`Manifest`、`ManifestKind`、`ManifestRole`、`PluginIsolation`、`CapabilityDeclaration`、`CapabilityRequirement`、`Binding`、`Contributions`、`PipelineContribution`、`ConfigContribution`。只做数据与校验，不做解析与发现（那是内核的事）。

## 跨语言语义

| 原语 | 必须一致 |
| --- | --- |
| Result | 成功与失败互斥；失败必有 Error；JSON 形态 |
| Error | Code 格式；Details 仅 JSON 值；Inner 链 |
| Maybe | 与 `null` 的编码等价，但类型层面不可为空 |
| TypedId | 编码为底层值；标签不上线 |
| InvocationContext | 字段集合与传播载体（RFC-0003） |
| Stability | 五级含义与弃用流程 |
| LifecycleState | 九态与迁移规则 |

## 冻结规则

只增不改；破坏即新包 `XiHan.Framework.Primitives.V2` 并存。公开面以 `PublicAPI.Shipped.txt` 为准。

## 一致性向量

`spec/conformance/vectors/primitives/*.json`：Result/Error/Maybe/TypedId 的编码与解码用例、Error.Code 合法与非法用例、LifecycleState 合法迁移表。

## 备选方案

- 把原语放进 Kernel：否决，内核会随小版本变化，原语层必须独立冻结。
- 用异常代替 Result：否决，可恢复失败不应走异常，且跨语言不可移植。
