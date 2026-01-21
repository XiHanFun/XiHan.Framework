# XiHan.Framework.Traffic

曦寒框架流量治理模块，提供流量治理的基础抽象和规则模型。

## 定位

> **这是基础设施层（Infrastructure），不是执行层。**

- ✅ 提供流量治理的**抽象和模型**
- ✅ 定义灰度路由、限流、熔断的**规则结构**
- ❌ 不负责具体的执行逻辑
- ❌ 不负责 Web 相关的处理

## 核心概念

### 灰度路由 (Gray Routing)

灰度路由是一种**运行时流量分配策略**，根据请求上下文（用户、租户、Header 等）决定请求的路由目标。

#### 三层架构

```text
┌─────────────────────────────────────────┐
│ Application / 管理系统                    │
│ - 灰度规则的 CRUD                         │
│ - 规则配置和维护                          │
└─────────────────────────────────────────┘
              ↓ 配置
┌─────────────────────────────────────────┐
│ XiHan.Framework.Traffic (本模块)         │
│ - 规则模型 (IGrayRule)                   │
│ - 匹配器抽象 (IGrayMatcher)              │
│ - 决策抽象 (IGrayDecision)               │
│ - 规则引擎抽象 (IGrayRuleEngine)         │
└─────────────────────────────────────────┘
              ↑ 依赖
┌─────────────────────────────────────────┐
│ XiHan.Framework.Web.Gateway (执行层)     │
│ - 构建灰度上下文                          │
│ - 执行灰度决策                            │
│ - 路由转发                                │
└─────────────────────────────────────────┘
```

## 核心接口

### IGrayRule - 灰度规则接口

定义灰度规则的基本结构：

```csharp
public interface IGrayRule
{
    string RuleId { get; }        // 规则唯一标识
    string RuleName { get; }      // 规则名称
    GrayRuleType RuleType { get; } // 规则类型
    bool IsEnabled { get; }       // 是否启用
    int Priority { get; }         // 优先级
}
```

### IGrayMatcher - 灰度匹配器接口

负责判断请求上下文是否命中某个灰度规则：

```csharp
public interface IGrayMatcher
{
    GrayRuleType RuleType { get; }
    bool IsMatch(GrayContext context, IGrayRule rule);
    Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken);
}
```

### IGrayDecision - 灰度决策接口

表示灰度路由的最终决策结果：

```csharp
public interface IGrayDecision
{
    bool IsGray { get; }           // 是否命中灰度
    string? TargetVersion { get; } // 目标版本
    string? MatchedRuleId { get; } // 匹配的规则ID
    string? Reason { get; }        // 决策原因
}
```

### IGrayRuleEngine - 灰度规则引擎接口

负责执行灰度规则的匹配逻辑：

```csharp
public interface IGrayRuleEngine
{
    Task<IGrayDecision> DecideAsync(GrayContext context, CancellationToken cancellationToken);
}
```

### IGrayRuleRepository - 灰度规则仓储接口

负责灰度规则的读取：

```csharp
public interface IGrayRuleRepository
{
    Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken);
    Task<IGrayRule?> GetRuleByIdAsync(string ruleId, CancellationToken cancellationToken);
    Task RefreshAsync(CancellationToken cancellationToken);
}
```

## 灰度规则类型

```csharp
public enum GrayRuleType
{
    Percentage = 1,    // 百分比灰度
    UserId = 2,        // 用户白名单
    TenantId = 3,      // 租户灰度
    Header = 4,        // 请求头灰度
    IpAddress = 5,     // IP 灰度
    Custom = 99        // 自定义
}
```

## 内置实现

### 1. DefaultGrayRuleEngine

默认的灰度规则引擎实现，按优先级执行规则匹配。

### 2. InMemoryGrayRuleRepository

基于内存的规则仓储，仅用于演示和测试。

**⚠️ 生产环境应替换为数据库实现。**

### 3. 内置匹配器

- **PercentageGrayMatcher** - 百分比灰度
- **UserIdGrayMatcher** - 用户白名单
- **TenantIdGrayMatcher** - 租户灰度
- **HeaderGrayMatcher** - 请求头灰度

## 使用方式

### 1. 注册服务

```csharp
services.AddGrayRouting(); // 注册灰度路由服务
```

### 2. 自定义匹配器

```csharp
public class IpAddressGrayMatcher : IGrayMatcher
{
    public GrayRuleType RuleType => GrayRuleType.IpAddress;
    
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        // 实现 IP 匹配逻辑
        return true;
    }
    
    public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken)
    {
        return Task.FromResult(IsMatch(context, rule));
    }
}

// 注册
services.AddGrayMatcher<IpAddressGrayMatcher>();
```

### 3. 自定义规则仓储

```csharp
public class DatabaseGrayRuleRepository : IGrayRuleRepository
{
    private readonly IDbContext _dbContext;
    
    public async Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.GrayRules
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);
    }
    
    // ... 其他方法
}

// 替换默认实现
services.ReplaceGrayRuleRepository<DatabaseGrayRuleRepository>();
```

## 扩展领域

除了灰度路由，本模块还预留了以下流量治理能力的接口：

### 限流 (Rate Limiting)

```csharp
public interface IRateLimitPolicy
{
    string PolicyName { get; }
    Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken);
}
```

### 熔断 (Circuit Breaker)

```csharp
public interface ICircuitBreakerPolicy
{
    string PolicyName { get; }
    bool IsOpen(string key);
    void RecordSuccess(string key);
    void RecordFailure(string key);
}
```

## 设计原则

### 1. 单一职责

> **Traffic 只负责定义"是什么"，不负责"怎么做"。**

### 2. 依赖倒置

> **执行层（Gateway）依赖抽象层（Traffic），抽象不依赖具体。**

### 3. 开闭原则

> **对扩展开放（自定义匹配器），对修改封闭（核心接口稳定）。**

### 4. 职责边界清晰

```text
✅ Traffic 的职责：
- 定义灰度规则的结构
- 提供匹配器抽象
- 提供决策模型

❌ Traffic 不负责：
- Web 请求处理
- 路由转发
- 规则管理界面
```

## 与其他模块的关系

- **被 `Web.Gateway` 依赖** - 提供灰度路由抽象
- **被 `Message` / `EventBus` 依赖** - 可能用于消息路由
- **被 `Tasks` 依赖** - 可能用于任务调度的流量控制
- **依赖 `Core`** - 基础模块化能力
- **依赖 `MultiTenancy.Abstractions`** - 租户抽象

## 常见问题

### Q: 为什么要单独一个 Traffic 模块？

**A:** 因为流量治理是**通用基础设施能力**，不仅 Web 需要，Message、EventBus、Tasks 等都可能需要。独立模块避免重复。

### Q: 为什么不把灰度规则管理也放这里？

**A:** 因为规则管理是**业务功能**，应该在 Application 层。Traffic 只提供能力，不提供业务。

### Q: 生产环境如何使用？

**A:**
1. 实现 `IGrayRuleRepository`，从数据库读取规则
2. 实现规则管理的后台界面（Application 层）
3. 配置规则变更的通知机制
4. 添加规则审计日志

### Q: 如何保证规则的实时性？

**A:**
1. 使用分布式缓存（Redis）+ 变更通知
2. 定期刷新 `IGrayRuleRepository.RefreshAsync()`
3. 配合配置中心（如 Nacos、Apollo）使用

## 最佳实践

### 1. 规则优先级设计

```text
1-10:    紧急规则（Header、IP 白名单）
11-50:   用户/租户白名单
51-100:  百分比灰度
101+:    其他规则
```

### 2. 规则配置结构

```json
{
  "RuleId": "rule-001",
  "RuleName": "用户白名单",
  "RuleType": 2,
  "Priority": 20,
  "TargetVersion": "v2",
  "Configuration": "{\"userIds\":[\"user1\"]}",
  "EffectiveTime": "2026-01-22T00:00:00Z",
  "ExpiryTime": "2026-02-22T00:00:00Z"
}
```

### 3. 规则匹配逻辑

1. 加载所有启用的规则
2. 按优先级从低到高排序
3. 逐个匹配，命中即返回
4. 所有规则都不匹配，返回默认版本

## 扩展建议

### 1. A/B Testing 支持

可以在 `GrayDecision` 中增加实验组标识：

```csharp
public class ABTestDecision : GrayDecision
{
    public string ExperimentId { get; set; }
    public string VariantId { get; set; }
}
```

### 2. 灰度指标收集

在匹配器中记录灰度命中情况：

```csharp
public class MetricsGrayMatcher : IGrayMatcher
{
    private readonly IMetricsCollector _metrics;
    
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        var result = _innerMatcher.IsMatch(context, rule);
        _metrics.RecordGrayMatch(rule.RuleId, result);
        return result;
    }
}
```

## 版本历史

- **v1.0.0** - 初始版本
  - 灰度路由核心抽象
  - 4 种内置匹配器
  - 默认规则引擎实现
  - 限流和熔断接口预留

## 许可证

MIT License - 详见 LICENSE 文件
