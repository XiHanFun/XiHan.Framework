# XiHan.Framework.Web.Gateway

曦寒框架 Web 网关模块，提供流量入口治理、路由决策和策略执行能力。

## 核心职责

### ✅ Gateway 负责的事情

1. **流量入口治理**
   - 统一请求入口
   - 请求上下文构建
   - TraceId 注入
   - 全局异常捕获

2. **路由决策执行**
   - 灰度路由决策执行
   - 路由转发（配合其他模块）
   - 版本路由

3. **策略执行层**
   - 限流策略执行
   - 熔断策略执行
   - 超时控制
   - 黑白名单

4. **Web 安全能力**
   - CORS 处理
   - Security Headers
   - 防刷保护

### ❌ Gateway 不负责的事情

1. **规则定义与管理**
   - 灰度规则的 CRUD
   - 规则配置的持久化
   - 规则的业务逻辑

2. **业务逻辑处理**
   - 应用服务调用
   - 业务数据处理
   - 领域逻辑

## 架构理念

```text
[Client Request]
       ↓
[Gateway] ← 执行层（判断 + 路由）
   ├─ 构建上下文
   ├─ 执行灰度决策 ←─────┐
   ├─ 注入决策结果         │
   └─ 异常兜底             │
       ↓                   │
[Application]              │
                          │
[Traffic Module] ← 抽象层（规则模型 + 接口）
   ├─ IGrayRule           │
   ├─ IGrayMatcher ───────┘
   ├─ IGrayDecision
   └─ IGrayRuleEngine
```

### 关键点

> **Gateway 执行灰度，Infrastructure 提供能力，Application 不感知灰度。**

- **灰度策略执行** 在 `Web.Gateway`
- **灰度规则模型** 在 `Framework.Traffic`
- **灰度规则管理** 在 `Application / 管理系统`

## 使用方式

### 1. 基础配置

```csharp
// Program.cs
builder.Services.AddGateway(options =>
{
    options.EnableGrayRouting = true;
    options.EnableRequestTracing = true;
    options.RequestTimeoutSeconds = 30;
});

app.UseGateway(); // 包含：异常处理 + 追踪 + 灰度
```

### 2. 灰度路由配置

```json
{
  "GrayRouting": {
    "Rules": [
      {
        "RuleId": "rule-percentage-10",
        "RuleName": "10% 流量灰度",
        "RuleType": 1,
        "IsEnabled": true,
        "Priority": 100,
        "TargetVersion": "v2",
        "Configuration": "{\"percentage\":10}"
      }
    ]
  }
}
```

### 3. 在代码中使用灰度决策

```csharp
public class MyController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        // 获取灰度决策
        var decision = HttpContext.GetGrayDecision();
        
        if (decision?.IsGray == true)
        {
            // 执行灰度版本逻辑
            return Ok(new { Version = "v2", Data = "Gray Data" });
        }
        
        // 执行正常版本逻辑
        return Ok(new { Version = "v1", Data = "Normal Data" });
    }
}
```

### 4. 自定义灰度匹配器

```csharp
public class CustomGrayMatcher : IGrayMatcher
{
    public GrayRuleType RuleType => GrayRuleType.Custom;
    
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        // 实现自定义匹配逻辑
        return true;
    }
    
    public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken)
    {
        return Task.FromResult(IsMatch(context, rule));
    }
}

// 注册
services.AddGrayMatcher<CustomGrayMatcher>();
```

## 内置灰度匹配器

### 1. PercentageGrayMatcher (百分比灰度)

```json
{
  "RuleType": 1,
  "Configuration": "{\"percentage\":10}"
}
```

随机 10% 的流量命中灰度。

### 2. UserIdGrayMatcher (用户白名单)

```json
{
  "RuleType": 2,
  "Configuration": "{\"userIds\":[\"user1\",\"user2\"]}"
}
```

指定用户命中灰度。

### 3. TenantIdGrayMatcher (租户灰度)

```json
{
  "RuleType": 3,
  "Configuration": "{\"tenantIds\":[\"guid1\",\"guid2\"]}"
}
```

指定租户命中灰度。

### 4. HeaderGrayMatcher (请求头灰度)

```json
{
  "RuleType": 4,
  "Configuration": "{\"headerName\":\"X-Gray\",\"headerValue\":\"true\"}"
}
```

带有特定 Header 的请求命中灰度。

## 中间件执行顺序

```text
1. GatewayExceptionMiddleware   ← 最外层异常捕获
2. RequestTracingMiddleware      ← TraceId 注入
3. GrayRoutingMiddleware         ← 灰度决策
4. [Your App Middlewares]        ← 你的中间件
```

## 扩展点

### 1. 自定义规则仓储

```csharp
public class DatabaseGrayRuleRepository : IGrayRuleRepository
{
    // 从数据库读取规则
}

services.ReplaceGrayRuleRepository<DatabaseGrayRuleRepository>();
```

### 2. 自定义规则引擎

```csharp
public class CustomGrayRuleEngine : IGrayRuleEngine
{
    // 实现自定义决策逻辑
}

services.Replace<IGrayRuleEngine, CustomGrayRuleEngine>();
```

## 注意事项

### ✅ 正确的做法

1. Gateway 只负责**判断和路由**
2. 规则定义放在 **Infrastructure.Traffic**
3. 规则管理放在 **Application / 管理系统**
4. 业务代码**不感知**灰度规则

### ❌ 错误的做法

1. ❌ 在 Application 中判断灰度
2. ❌ 在 Domain 中感知灰度
3. ❌ Gateway 管理规则配置
4. ❌ 把灰度当作 Feature Toggle

## 与其他模块的关系

- **依赖 `Framework.Traffic`** - 获取灰度规则模型和抽象
- **依赖 `Framework.Web.Core`** - Web 通用能力
- **依赖 `Framework.MultiTenancy`** - 租户解析
- **依赖 `Framework.Logging`** - 日志记录
- **被 `Web.Api` 使用** - 作为入口中间件

## 常见问题

### Q: Gateway 和 Traffic 的边界是什么？

**A:** 
- `Traffic` 定义规则模型和抽象（What）
- `Gateway` 执行规则判断和路由（How）

### Q: 灰度和 Feature Toggle 的区别？

**A:**
- **灰度发布**：流量治理，基于请求上下文路由
- **Feature Toggle**：功能开关，基于配置控制功能启用

### Q: 为什么不在 Application 中判断灰度？

**A:** 因为灰度是**流量治理能力**，不是业务逻辑。在 Application 中判断会：
1. 污染业务代码
2. 破坏层次边界
3. 无法统一管理

### Q: 生产环境如何使用？

**A:** 
1. 替换 `InMemoryGrayRuleRepository` 为数据库实现
2. 实现规则管理的后台界面
3. 配置监控和告警
4. 做好灰度决策的日志记录

## 版本历史

- **v1.0.0** - 初始版本
  - 灰度路由基础功能
  - 请求追踪
  - 异常处理
  - 内置 4 种灰度匹配器

## 许可证

MIT License - 详见 LICENSE 文件
