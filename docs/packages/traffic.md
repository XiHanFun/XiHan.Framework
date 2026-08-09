# XiHan.Framework.Traffic

> 流量治理：灰度路由（已完整实现）+ 限流 / 熔断策略抽象（仅接口，实现留给上层）。

- **NuGet**：`XiHan.Framework.Traffic`
- **模块类**：`XiHanTrafficModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（Core / MultiTenancy.Abstractions）。规则配置解析用 `System.Text.Json`。

## 概述

XiHan.Framework.Traffic 是框架的流量治理库，覆盖灰度路由、限流、熔断三类场景，但三者成熟度不同：

- **灰度路由（GrayRouting）——已完整实现**：内置规则引擎 `IGrayRuleEngine` + 四个匹配器（百分比 / 用户 / 租户 / 请求头）+ 内存规则仓储，开箱即可做灰度决策。
- **限流（RateLimiting）——仅策略接口**：只有 `IRateLimitPolicy` 抽象，**无内置实现**。
- **熔断（CircuitBreaker）——仅策略接口**：只有 `ICircuitBreakerPolicy` 抽象，**无内置实现**。

设计上本库定位为「治理策略的抽象与灰度决策核心」，不在网关/中间件里做请求拦截执行；真正的入站限流、熔断实现在 Web 层（如 `XiHan.Framework.Web.Api`）。规则的**来源**（内存/数据库）与**匹配逻辑**解耦，规则可动态替换。

## 何时使用

- 做灰度发布：按百分比、用户 ID、租户 ID 或请求头把部分流量分流到新版本 / 新服务。
- 想把灰度规则的来源（内存、数据库）与匹配逻辑解耦，规则由应用层管理、网关只读消费。
- 需要一套与业务解耦的限流 / 熔断策略**抽象**，便于在网关或服务层统一接入自研或第三方实现。

不该用它的场景：想要开箱即用的入站限流 / 熔断——本库不提供实现，应在 Web.Api 层接入（或自行实现上述两个策略接口）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Traffic
```

```csharp
[DependsOn(typeof(XiHanTrafficModule))]
public class MyModule : XiHanModule { }
```

`XiHanTrafficModule.ConfigureServices` 默认调用 `AddGrayRouting()`，注册（`TryAdd` 语义，可被替换）：

- `IGrayRuleEngine` → `DefaultGrayRuleEngine`
- `IGrayRuleRepository` → `InMemoryGrayRuleRepository`（**生产环境应替换为数据库实现**）
- 四个内置匹配器 `IGrayMatcher`：`PercentageGrayMatcher`、`UserIdGrayMatcher`、`TenantIdGrayMatcher`、`HeaderGrayMatcher`

> 限流 `IRateLimitPolicy` 与熔断 `ICircuitBreakerPolicy` **不会**被模块自动注册任何实现——它们是留给上层接入的策略契约。

## 工作原理（灰度决策）

`DefaultGrayRuleEngine.DecideAsync(context)` 的决策流程：

1. 从 `IGrayRuleRepository.GetEnabledRulesAsync()` 取全部启用规则；无规则则直接返回「未命中」。
2. 按 `Priority` **升序**排序（数字越小优先级越高）。
3. 逐条规则：先校验有效期（`GrayRule.EffectiveTime` / `ExpiryTime`），跳过失效规则；再按 `rule.RuleType` 找到对应 `IGrayMatcher`（找不到打 Warning 跳过）。
4. 命中即**短路返回** `GrayDecision.Gray(targetVersion, matchedRuleId, reason)`（`TargetVersion` 取自规则，默认 `"gray"`）。
5. 全部未命中返回 `GrayDecision.NotGray(...)`；决策过程异常被捕获并降级为「未命中」（fail-open，不影响主流程）。

匹配器读取 `GrayRule.Configuration`（JSON 字符串）作为规则参数，例如百分比匹配器解析 `{ "percentage": 10 }`，用 `Random.Shared` 取 1–100 判断是否落入灰度区间。

## 核心能力

- **灰度路由**：规则引擎按优先级匹配，输出灰度决策（是否命中、目标版本 / 服务、命中规则 Id、原因）。
- **多维匹配器**：内置百分比、用户 ID、租户 ID、请求头四种；`GrayRuleType` 另预留 `IpAddress`、`Custom` 类型（无内置匹配器，需自行实现）。
- **规则来源可插拔**：仓储接口只读，默认内存实现可用 `ReplaceGrayRuleRepository<T>()` 换成数据库实现；自定义匹配器用 `AddGrayMatcher<T>()` 注册。
- **限流 / 熔断策略抽象**：`IRateLimitPolicy` / `ICircuitBreakerPolicy` 定义统一契约，供上层接入实现。

## 主要 API / 类型

### 灰度路由（GrayRouting）

| 类型 | 说明 |
| --- | --- |
| `IGrayRuleEngine` / `DefaultGrayRuleEngine` | 灰度规则引擎：`Task<IGrayDecision> DecideAsync(GrayContext, CancellationToken)` |
| `IGrayMatcher` | 匹配器契约：`GrayRuleType RuleType { get; }`、`bool IsMatch(GrayContext, IGrayRule)`、`Task<bool> IsMatchAsync(...)` |
| `PercentageGrayMatcher` / `UserIdGrayMatcher` / `TenantIdGrayMatcher` / `HeaderGrayMatcher` | 四个内置匹配器 |
| `IGrayRuleRepository` / `InMemoryGrayRuleRepository` | 规则仓储（**只读**）：`GetEnabledRulesAsync(...)`、`GetRuleByIdAsync(id, ...)`、`RefreshAsync(...)`；`InMemoryGrayRuleRepository` 另暴露 `AddRule` / `RemoveRule` / `Clear`（非接口成员，仅供测试直接灌规则用） |
| `IGrayRule` / `GrayRule` | 灰度规则契约与实现（字段见下） |
| `IGrayDecision` / `GrayDecision` | 决策结果契约与实现，工厂 `GrayDecision.Gray(...)` / `GrayDecision.NotGray(reason)` |
| `GrayContext` | 灰度上下文（字段见下） |
| `GrayRuleType` | 规则类型枚举：`Percentage=1`、`UserId=2`、`TenantId=3`、`Header=4`、`IpAddress=5`、`Custom=99` |

**`GrayRule` 字段**：`RuleId`、`RuleName`、`RuleType`、`IsEnabled`、`Priority`（越小越优先）、`TargetVersion`、`TargetServiceId`、`Configuration`（JSON 规则参数）、`EffectiveTime`、`ExpiryTime`、`CreatedTime`、`UpdatedTime`、`Remark`。

`Configuration` 按 `RuleType` 存不同结构：

| RuleType | Configuration 示例 |
| --- | --- |
| Percentage | `{ "percentage": 10 }` |
| UserId | `{ "userIds": ["user1", "user2"] }` |
| TenantId | `{ "tenantIds": ["tenant1", "tenant2"] }` |
| Header | `{ "headerName": "X-Gray", "headerValue": "true" }` |
| IpAddress | `{ "ipAddresses": ["192.168.1.1"] }` |

**`GrayContext` 字段**：`UserId`、`TenantId`、`RequestPath`、`RequestMethod`、`ClientIpAddress`、`Headers`（不区分大小写字典）、`ExtensionData`。

**`IGrayDecision` 字段**：`IsGray`、`TargetVersion`、`TargetServiceId`、`MatchedRuleId`、`Reason`、`ExtensionData`。

### 限流 / 熔断（仅抽象）

| 类型 | 说明 |
| --- | --- |
| `IRateLimitPolicy` | 限流策略：`string PolicyName { get; }`、`Task<bool> IsAllowedAsync(string key, CancellationToken)`。**无内置实现。** |
| `ICircuitBreakerPolicy` | 熔断策略：`string PolicyName { get; }`、`bool IsOpen(string key)`、`void RecordSuccess(string key)`、`void RecordFailure(string key)`。**无内置实现。** |

### DI 扩展方法（`XiHanTrafficServiceCollectionExtensions`）

| 方法 | 说明 |
| --- | --- |
| `IServiceCollection.AddGrayRouting()` | 注册灰度引擎、内存仓储与四个内置匹配器（模块默认已调用） |
| `IServiceCollection.AddGrayMatcher<TMatcher>()` | 追加自定义匹配器（如 IpAddress / Custom） |
| `IServiceCollection.ReplaceGrayRuleRepository<TRepository>()` | 用数据库等实现替换默认内存仓储 |

## 使用示例

### 执行一次灰度决策

```csharp
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;

var context = new GrayContext
{
    UserId = 1001,
    TenantId = 1,
    RequestPath = "/api/orders",
    ClientIpAddress = "192.168.1.10"
};

IGrayDecision decision = await grayRuleEngine.DecideAsync(context);
if (decision.IsGray)
{
    // 命中灰度：路由到 decision.TargetVersion / decision.TargetServiceId 指向的新版本
    // decision.MatchedRuleId / decision.Reason 可用于日志与追踪
}
```

### 替换规则仓储 + 注册自定义匹配器

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    var services = context.Services;

    // 换成数据库规则仓储（应用层管理规则的增删改）
    services.ReplaceGrayRuleRepository<DbGrayRuleRepository>();

    // 补齐 IpAddress 类型的匹配器（内置未提供）
    services.AddGrayMatcher<IpAddressGrayMatcher>();
}
```

## 扩展点 / 自定义

- **数据库规则仓储**：实现 `IGrayRuleRepository`（只读三方法），`ReplaceGrayRuleRepository<T>()` 替换默认内存实现；规则的写入 / 管理由应用层自行提供。
- **新增匹配维度**：实现 `IGrayMatcher`（暴露 `RuleType` 并解析 `GrayRule.Configuration`），`AddGrayMatcher<T>()` 注册；这是补齐 `IpAddress` / `Custom` 的标准做法。
- **接入限流 / 熔断**：实现 `IRateLimitPolicy` / `ICircuitBreakerPolicy` 并在 Web 层挂到请求管道；本库不提供实现，也不自动注册。

## 注意事项与最佳实践

- 灰度规则**按 `Priority` 升序**匹配，命中即短路——把更具体、更高优先的规则设更小的 `Priority`。
- `PercentageGrayMatcher` 基于每次请求的随机数判断，**同一用户多次请求可能落在不同分支**；需要稳定分流请自行实现基于用户/租户哈希的匹配器。
- 默认 `InMemoryGrayRuleRepository` 是进程内存储，多实例不共享、重启丢失——生产务必替换为数据库仓储。
- 决策异常被吞并降级为「未命中」（fail-open），不会因规则问题阻断主请求；但要留意日志排查。
- `IpAddress` / `Custom` 枚举值已预留，但**无对应内置匹配器**，直接使用会因「找不到匹配器」被跳过。

## 依赖模块

- [XiHan.Framework.Core](./core) — 模块化与依赖注入基座。
- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions) — 租户抽象，支撑按租户 ID 灰度。

## 相关模块

- [XiHan.Framework.Web.Gateway](./web-gateway) — 网关层，灰度决策的典型执行方。
- [XiHan.Framework.Web.Api](./web-api) — 入站限流 / 熔断的真正实现所在层。
- [XiHan.Framework.Observability](./observability) — 可观测性，配合流量治理做指标与追踪。
