# XiHan.Framework.Threading

> 并发上下文基础：统一取当前 `CancellationToken`（含临时覆盖），以及基于 `AsyncLocal` 的环境数据上下文与可嵌套的环境作用域（Ambient Scope）。

- **NuGet**：`XiHan.Framework.Threading`
- **模块类**：`XiHanThreadingModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Core`），无第三方 NuGet

## 概述

XiHan.Framework.Threading 为框架提供异步执行时的**上下文管理**基础设施，而非通用并发原语。它做两件事：一是统一「当前 `CancellationToken`」的获取与临时覆盖，让调用链深处无需层层传参就能拿到令牌；二是基于 `AsyncLocal` 实现「环境数据上下文」与「环境作用域」，让隐式状态可以安全地跨 `await` 边界传递、支持嵌套与自动还原。它是取消令牌传播、以及缓存 / 本地化 / 工作单元等模块协同的底层支撑。

> 说明：本包**不包含**异步信号量、读写锁、优先级调度或背压等重量级并发工具——它只提供取消令牌与环境作用域两类轻量上下文机制。

## 何时使用

- 需要在调用链深处统一取到「当前」的 `CancellationToken`，而不层层传参。
- 需要临时覆盖当前执行上下文的取消令牌，退出作用域后自动还原（`Use()`）。
- 需要基于 `AsyncLocal` 存放跨异步边界的隐式状态，并支持嵌套作用域与还原（`IAmbientScopeProvider<T>`）。
- 不适用：需要限流信号量 / 锁 / 调度队列时，本包不提供，请另找专用工具。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Threading
```

```csharp
[DependsOn(typeof(XiHanThreadingModule))]
public class MyModule : XiHanModule { }
```

模块在 `ConfigureServices` 中调用 `AddXiHanThreading()`，注册：

- `ICancellationTokenProvider` → 单例 `NullCancellationTokenProvider.Instance`（默认返回 `CancellationToken.None` 或当前覆盖值）。
- 开放泛型 `IAmbientScopeProvider<>` → `AmbientDataContextAmbientScopeProvider<>`（Singleton）。
- 另注意：`AsyncLocalAmbientDataContext` 自身实现 `ISingletonDependency`，在框架常规约定扫描下作为 `IAmbientDataContext` 的单例实现被解析。

## 工作原理

- **取消令牌覆盖**：`CancellationTokenProviderBase` 用一个键为 `CancellationTokenOverrideContextKey`（`"XiHan.Framework.Threading.CancellationToken.Override"`）的环境作用域保存 `CancellationTokenOverride`。调用 `Use(token)` 时 `BeginScope` 压入一层覆盖值并返回 `IDisposable`；释放时弹出、还原到外层值。`Token` 属性优先返回覆盖值，否则回落到具体实现（`NullCancellationTokenProvider` 回落 `CancellationToken.None`）。
- **环境作用域栈**：`AmbientDataContextAmbientScopeProvider<T>` 内部用静态 `ConcurrentDictionary` 存 `ScopeItem`（含 `Value` 与指向外层的 `Outer`），在 `IAmbientDataContext` 中只按 key 存当前 `ScopeItem` 的 Id。`BeginScope` 压栈、返回的 `IDisposable` 出栈并把上下文指回外层 Id（外层为空则清除），从而实现跨 `await` 的嵌套与自动还原。
- **AsyncLocal 存储**：`AsyncLocalAmbientDataContext` 用 `ConcurrentDictionary<string, AsyncLocal<object?>>` 承载每个 key 的异步本地值，保证值随执行流（含 `await` 续体）传播且线程安全。

## 核心能力

- **取消令牌提供者**：`ICancellationTokenProvider` 统一提供当前 `CancellationToken`，避免层层传参。
- **令牌临时覆盖**：`CancellationTokenOverride` + 提供者 `Use()`，在作用域内临时替换令牌，退出后还原。
- **令牌回落扩展**：`ICancellationTokenProvider.FallbackToProvider(preferred)`，优先用传入令牌，否则回落到提供者的 `Token`。
- **环境数据上下文**：`AsyncLocalAmbientDataContext` 基于 `AsyncLocal` 提供线程安全的异步本地存储。
- **环境作用域**：`IAmbientScopeProvider<T>` 支持嵌套作用域与值覆盖，用栈式管理隐式状态。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `ICancellationTokenProvider` | 取消令牌提供者。`CancellationToken Token { get; }`、`IDisposable Use(CancellationToken cancellationToken)` |
| `CancellationTokenProviderBase` | 抽象基类，实现 `Use()`/覆盖机制骨架；常量 `CancellationTokenOverrideContextKey`；受保护成员 `OverrideValue`、`CancellationTokenOverrideScopeProvider` |
| `NullCancellationTokenProvider` | 空实现，单例 `Instance`；`Token` 返回覆盖值或 `CancellationToken.None` |
| `CancellationTokenOverride` | 令牌覆盖值容器，持有只读 `CancellationToken CancellationToken` |
| `CancellationTokenProviderExtensions` | `CancellationToken FallbackToProvider(this ICancellationTokenProvider, CancellationToken preferred = default)` |
| `IAmbientDataContext` | 环境数据上下文。`void SetData(string key, object? value)`、`object? GetData(string key)` |
| `AsyncLocalAmbientDataContext` | `IAmbientDataContext` 的 `AsyncLocal` 实现（`ISingletonDependency`） |
| `IAmbientScopeProvider<T>` | 环境作用域提供者。`T? GetValue(string contextKey)`、`IDisposable BeginScope(string contextKey, T value)` |
| `AmbientDataContextAmbientScopeProvider<T>` | 基于 `IAmbientDataContext` 的作用域提供者实现，栈式嵌套 + 自动还原 |

DI 入口：`XiHanThreadingServiceCollectionExtensions.AddXiHanThreading(this IServiceCollection)`。

## 使用示例

### 统一取当前取消令牌 + 临时覆盖

```csharp
public class Worker
{
    private readonly ICancellationTokenProvider _tokenProvider;

    public Worker(ICancellationTokenProvider tokenProvider) => _tokenProvider = tokenProvider;

    public async Task RunAsync(CancellationToken preferred)
    {
        // 优先用调用方传入的令牌，否则回落到当前上下文令牌
        var token = _tokenProvider.FallbackToProvider(preferred);

        // 在一段范围内临时覆盖当前令牌，using 结束后自动还原
        using (_tokenProvider.Use(token))
        {
            await DoWorkAsync(); // 内部深处再取 _tokenProvider.Token 即为覆盖后的令牌
        }
    }

    private Task DoWorkAsync() => Task.CompletedTask;
}
```

### 用环境作用域跨 await 传递隐式状态

```csharp
public class ContextFlow
{
    private const string Key = "MyModule.CorrelationId";
    private readonly IAmbientScopeProvider<string> _scope;

    public ContextFlow(IAmbientScopeProvider<string> scope) => _scope = scope;

    public async Task HandleAsync(string correlationId)
    {
        using (_scope.BeginScope(Key, correlationId))
        {
            await StepAsync(); // 续体中 _scope.GetValue(Key) 仍能取到 correlationId
        }
        // 作用域结束后 GetValue(Key) 还原为外层值（或 null）
    }

    private Task StepAsync() => Task.CompletedTask;
}
```

## 扩展点 / 自定义

- **自定义令牌提供者**：继承 `CancellationTokenProviderBase`，复用其 `Use()` 覆盖机制，只需实现 `Token` 属性从你的上下文（如 `HttpContext.RequestAborted`）取令牌，并在 DI 中替换 `ICancellationTokenProvider`。上层 Web 模块通常会提供 HTTP 请求级的实现来覆盖默认的 `NullCancellationTokenProvider`。
- **自定义环境上下文**：如需非 `AsyncLocal` 的存储策略，可另实现 `IAmbientDataContext` 并替换默认注册。

## 注意事项与最佳实践

- `NullCancellationTokenProvider` 是**兜底**：不覆盖时 `Token` 恒为 `CancellationToken.None`，即「永不取消」。要获得真实的请求取消，需由上层注册基于 `HttpContext` 的提供者。
- `Use()` 与 `BeginScope()` 返回的 `IDisposable` **必须**用 `using` 或显式释放，否则覆盖 / 作用域不会还原，会污染后续执行流。
- 环境作用域基于 `AsyncLocal`：值随 `await` 续体流动，但**不会**反向流回父任务；跨 `Task.Run` 等新执行流时按 `AsyncLocal` 捕获语义传播。

## 依赖模块

- 内部依赖：仅 [XiHan.Framework.Core](./core)，无第三方 NuGet 依赖。

## 相关模块

- [XiHan.Framework.Caching](./caching) — 与本包的取消令牌 / 上下文机制协同。
- [XiHan.Framework.Localization](./localization) — 依赖本包做线程 / 作用域协调。
- [XiHan.Framework.Timing](./timing) — 其 `CurrentTimezoneProvider` 同样用 `AsyncLocal` 做请求级隔离，思路一致。
