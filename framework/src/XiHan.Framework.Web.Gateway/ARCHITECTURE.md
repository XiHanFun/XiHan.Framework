# Gateway 与 Traffic 模块架构设计

## 架构理念

> **Gateway 执行灰度，Infrastructure 提供能力，Application 不感知灰度。**

## 核心分层

```text
┌────────────────────────────────────────────────────┐
│           Application / 管理系统                     │
│                                                     │
│  职责：                                              │
│  ✓ 灰度规则的 CRUD                                   │
│  ✓ 规则配置和维护                                     │
│  ✓ 灰度效果监控                                      │
│                                                     │
│  ✗ 不负责：规则执行、流量判断                          │
└────────────────────────────────────────────────────┘
                          ↓ 配置
┌────────────────────────────────────────────────────┐
│       XiHan.Framework.Traffic (基础设施层)           │
│                                                     │
│  职责：                                              │
│  ✓ 定义灰度规则模型 (IGrayRule)                      │
│  ✓ 定义匹配器抽象 (IGrayMatcher)                     │
│  ✓ 定义决策抽象 (IGrayDecision)                      │
│  ✓ 提供默认实现 (DefaultGrayRuleEngine)              │
│                                                     │
│  ✗ 不负责：Web 处理、具体执行、业务逻辑                 │
└────────────────────────────────────────────────────┘
                          ↑ 依赖
┌────────────────────────────────────────────────────┐
│    XiHan.Framework.Web.Gateway (Web 执行层)         │
│                                                     │
│  职责：                                              │
│  ✓ 构建灰度上下文 (GrayContext)                      │
│  ✓ 执行灰度决策 (调用 IGrayRuleEngine)               │
│  ✓ 注入决策结果到 HttpContext                        │
│  ✓ 路由转发                                          │
│  ✓ 请求追踪                                          │
│  ✓ 异常处理                                          │
│                                                     │
│  ✗ 不负责：规则定义、规则管理                          │
└────────────────────────────────────────────────────┘
                          ↓ 处理
┌────────────────────────────────────────────────────┐
│              Web.Api / Controllers                  │
│                                                     │
│  职责：                                              │
│  ✓ 处理业务逻辑                                      │
│  ✓ (可选) 根据灰度决策执行不同逻辑                     │
│                                                     │
│  ✗ 不负责：灰度判断、规则管理                          │
└────────────────────────────────────────────────────┘
```

## 职责边界

### Traffic 模块（基础设施层）

#### ✅ 应该做的事情

1. **定义抽象和模型**
   - 灰度规则接口 (`IGrayRule`)
   - 灰度匹配器接口 (`IGrayMatcher`)
   - 灰度决策接口 (`IGrayDecision`)
   - 规则引擎接口 (`IGrayRuleEngine`)
   - 规则仓储接口 (`IGrayRuleRepository`)

2. **提供默认实现**
   - 默认规则引擎 (`DefaultGrayRuleEngine`)
   - 内存规则仓储 (`InMemoryGrayRuleRepository`)
   - 内置匹配器（百分比、用户ID、租户ID、Header）

3. **保持通用性**
   - 不依赖 Web
   - 不依赖特定框架
   - 可被其他模块复用（Message、EventBus、Tasks）

#### ❌ 不应该做的事情

1. ❌ 处理 HTTP 请求
2. ❌ 解析请求头
3. ❌ 执行路由转发
4. ❌ 提供规则管理界面
5. ❌ 关心业务逻辑

### Gateway 模块（Web 执行层）

#### ✅ 应该做的事情

1. **流量入口治理**
   - 统一请求入口
   - TraceId 注入
   - 全局异常捕获
   - 请求上下文构建

2. **灰度路由执行**
   - 从 HttpContext 构建 `GrayContext`
   - 调用 `IGrayRuleEngine` 执行决策
   - 将决策结果注入到 `HttpContext`
   - 记录灰度决策日志

3. **策略执行**
   - 限流（未来）
   - 熔断（未来）
   - 超时控制
   - 黑白名单

4. **Web 安全**
   - CORS
   - Security Headers
   - 防刷

#### ❌ 不应该做的事情

1. ❌ 定义灰度规则结构
2. ❌ 管理灰度规则（CRUD）
3. ❌ 实现业务逻辑
4. ❌ 处理领域模型

### Application 层（规则管理）

#### ✅ 应该做的事情

1. **规则管理**
   - 灰度规则的 CRUD
   - 规则配置持久化
   - 规则版本管理
   - 规则审计日志

2. **效果监控**
   - 灰度流量统计
   - 灰度效果分析
   - 问题回滚决策

#### ❌ 不应该做的事情

1. ❌ 执行灰度判断
2. ❌ 解析 HTTP 请求
3. ❌ 构建灰度上下文

## 数据流

### 1. 正常请求流程

```text
Client Request
    ↓
[GatewayExceptionMiddleware]      ← 捕获异常
    ↓
[RequestTracingMiddleware]         ← 注入 TraceId
    ↓
[GrayRoutingMiddleware]            ← 执行灰度决策
    ├─ 构建 GrayContext
    ├─ 调用 IGrayRuleEngine.DecideAsync()
    │    ├─ 加载规则 (IGrayRuleRepository)
    │    ├─ 匹配规则 (IGrayMatcher)
    │    └─ 返回 IGrayDecision
    └─ 注入 HttpContext.Items["GrayDecision"]
    ↓
[Controller]                        ← 处理业务逻辑
    └─ (可选) HttpContext.GetGrayDecision()
    ↓
Response
```

### 2. 规则管理流程

```text
管理后台 (Application)
    ↓
规则 CRUD
    ↓
持久化到数据库
    ↓
IGrayRuleRepository 刷新
    ↓
Gateway 下次请求生效
```

## 设计模式

### 1. 策略模式 (Strategy Pattern)

```text
IGrayMatcher (策略接口)
    ├─ PercentageGrayMatcher
    ├─ UserIdGrayMatcher
    ├─ TenantIdGrayMatcher
    ├─ HeaderGrayMatcher
    └─ CustomGrayMatcher (用户自定义)
```

### 2. 责任链模式 (Chain of Responsibility)

```text
规则按优先级排序
    ↓
依次匹配
    ├─ 命中 → 返回决策
    └─ 未命中 → 继续
```

### 3. 仓储模式 (Repository Pattern)

```text
IGrayRuleRepository (抽象)
    ├─ InMemoryGrayRuleRepository (内存实现)
    └─ DatabaseGrayRuleRepository (数据库实现)
```

### 4. 依赖注入 (Dependency Injection)

```text
Gateway 依赖抽象 (IGrayRuleEngine)
    ↓
不依赖具体实现
    ↓
可随时替换实现
```

## 扩展点

### 1. 自定义匹配器

```csharp
public class CustomMatcher : IGrayMatcher
{
    public GrayRuleType RuleType => GrayRuleType.Custom;
    
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        // 自定义匹配逻辑
    }
}

services.AddGrayMatcher<CustomMatcher>();
```

### 2. 自定义规则仓储

```csharp
public class DatabaseRepository : IGrayRuleRepository
{
    // 从数据库读取规则
}

services.ReplaceGrayRuleRepository<DatabaseRepository>();
```

### 3. 自定义规则引擎

```csharp
public class CustomEngine : IGrayRuleEngine
{
    // 自定义决策逻辑
}

services.Replace<IGrayRuleEngine, CustomEngine>();
```

## 常见误区

### ❌ 误区 1：在 Application 中判断灰度

```csharp
// ❌ 错误做法
public class ProductAppService
{
    public async Task<ProductDto> GetProductAsync(Guid id)
    {
        if (IsGrayRequest()) // ❌ 应用服务不应该感知灰度
        {
            return await GetProductV2Async(id);
        }
        return await GetProductV1Async(id);
    }
}
```

**正确做法：**

```csharp
// ✅ 方案 1：在 Controller 层处理
public class ProductController
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        if (HttpContext.IsGrayRequest())
        {
            return Ok(await _productAppService.GetProductV2Async(id));
        }
        return Ok(await _productAppService.GetProductAsync(id));
    }
}

// ✅ 方案 2：使用路由分流
[Route("api/v1/products")]
public class ProductV1Controller { }

[Route("api/v2/products")]
public class ProductV2Controller { }
```

### ❌ 误区 2：在 Domain 中感知灰度

```csharp
// ❌ 错误做法
public class Product : Entity
{
    public decimal CalculatePrice()
    {
        if (IsGrayVersion) // ❌ 领域模型不应该感知灰度
        {
            return _price * 0.9m;
        }
        return _price;
    }
}
```

**正确做法：**

```csharp
// ✅ 灰度逻辑不在领域层
public class Product : Entity
{
    public decimal CalculatePrice() => _price;
    public decimal CalculatePriceWithDiscount(decimal rate) => _price * rate;
}
```

### ❌ 误区 3：Gateway 管理规则

```csharp
// ❌ 错误做法
[Route("api/gateway/rules")]
public class GatewayRuleController // ❌ Gateway 不应该提供管理接口
{
    [HttpPost]
    public Task CreateRule([FromBody] GrayRule rule) { }
}
```

**正确做法：**

```csharp
// ✅ 规则管理在 Application 层
[Route("api/admin/gray-rules")]
public class GrayRuleManagementController
{
    private readonly IGrayRuleManagementService _service;
    
    [HttpPost]
    public Task CreateRule([FromBody] CreateGrayRuleDto dto)
    {
        return _service.CreateRuleAsync(dto);
    }
}
```

## 最佳实践

### 1. 规则优先级设计

| 优先级范围 | 用途 | 示例 |
|-----------|------|------|
| 1-10 | 紧急规则 | Header 白名单、IP 白名单 |
| 11-50 | 用户/租户白名单 | 内测用户、Beta 租户 |
| 51-100 | 百分比灰度 | 5%, 10%, 20% 流量 |
| 101+ | 其他规则 | 自定义规则 |

### 2. 灰度发布流程

```text
Phase 1: 内部测试      (用户白名单)
    ↓
Phase 2: 小范围灰度    (5% 流量)
    ↓
Phase 3: 扩大灰度      (20% 流量)
    ↓
Phase 4: 大范围灰度    (50% 流量)
    ↓
Phase 5: 全量发布      (100% 流量)
```

### 3. 监控指标

- 灰度流量占比
- 灰度版本错误率
- 灰度版本响应时间
- 规则命中率

### 4. 回滚策略

1. **立即回滚**：发现严重问题，禁用所有灰度规则
2. **降级回滚**：降低灰度百分比（50% → 20% → 5%）
3. **定向回滚**：保留白名单，禁用百分比规则

## 总结

这个架构设计遵循以下原则：

1. **单一职责原则** - 每个模块只做一件事
2. **依赖倒置原则** - 依赖抽象而非具体
3. **开闭原则** - 对扩展开放，对修改封闭
4. **接口隔离原则** - 接口职责清晰
5. **最少知识原则** - 模块间耦合最小化

**核心思想：**

> **Gateway 是执行者，Traffic 是能力提供者，Application 是规则管理者。**
> 
> **三者职责明确，边界清晰，互不干涉。**
