# 审计

审计日志回答的是「谁、在什么时候、对什么做了什么」——请求访问、业务操作、异常、开放接口调用、登录、实体字段级变更。框架负责**采集、异步化、脱敏**，但不决定存到哪张表：落库由你实现的写入器完成。

本章讲这条链路怎么接通、哪些环节是自动的、哪些必须你自己写。完整 API 清单与配置全表见 [Auditing 包文档](../packages/auditing)。

## 审计日志不是运行日志

两者经常被混为一谈，但在框架里是完全独立的两套东西：

| | 运行日志（`ILogger`） | 审计日志 |
| --- | --- | --- |
| 目的 | 排障、观察 | 追责、合规、业务回溯 |
| 去处 | 控制台 / 文件 / 日志系统 | 业务库表（由写入器决定） |
| 触发 | 代码显式写 | 中间件 / 过滤器 / ORM AOP 自动采集 |
| 内容 | 自由文本 + 结构化字段 | 固定记录模型（6 类） |
| 脱敏 | 无 | 采集点强制脱敏 |

「请求开始 / 请求结束」这类 `ILogger` 输出属于运行日志，和审计日志表是两条互不相干的路径。

## 六类记录与它们的采集点

| 记录模型 | 谁采集 | 触发条件 |
| --- | --- | --- |
| `AccessLogRecord` | `XiHanRequestLoggingMiddleware`（Web.Api） | 每个未命中 `IgnoredPathPrefixes` 的请求 |
| `OperationLogRecord` | `XiHanActionLoggingFilter`（全局 MVC 过滤器） | Action 执行完成，且请求方法不是 GET / HEAD / OPTIONS |
| `ExceptionLogRecord` | `ExceptionLogReporter`（由 `XiHanApiResponseResultFilter` 与 `XiHanExceptionLoggingMiddleware` 调用） | 未处理异常 |
| `ApiLogRecord` | `XiHanApiLoggingMiddleware` | **仅当**请求携带开放接口安全头（AccessKey / Signature） |
| `LoginLogRecord` | 无 | 由应用自己调 `ILoginLogPipeline` |
| `EntityDiffLogRecord` | `SqlSugarDiffLogAop`（Data 包） | 仓储写操作，且 `EnableDiffLog = true` |

::: warning 登录日志没有自动采集方
框架只提供 `LoginLogRecord` 与 `ILoginLogPipeline`，没有任何中间件会去填它。登录日志需要应用在登录成功 / 失败的分支里自己写：

```csharp
await loginLogPipeline.WriteAsync(new LoginLogRecord
{
    UserId = user.Id,
    UserName = user.UserName,
    LoginResult = 0,          // 语义由应用定义：0 = 成功
    Message = "登录成功",
    LoginIp = ip,
    LoginTime = DateTimeOffset.UtcNow
});
```
:::

## 采集链路

```text
中间件 / 过滤器 / ORM AOP
   └→ IXxxLogPipeline.WriteAsync(record)          （Scoped）
        ├ EnableXxxLogQueue = false（默认）
        │     └→ 直接 await IXxxLogWriter.WriteAsync(record)      ← 同步落库，计入请求耗时
        └ EnableXxxLogQueue = true
              └→ ILogQueue<TRecord>（Singleton，有界 Channel）
                    └→ XxxLogQueueWorker（HostedService）攒批
                          └→ 新建 DI 作用域 → 逐条 IXxxLogWriter.WriteAsync
```

三段职责很清楚：

- **管道**决定「同步写还是入队」；
- **队列**只提供两种语义明确的入队方式（`TryEnqueue` 满时返回 `false`，`EnqueueAsync` 满时等待），丢不丢由管道按 `DropOnFull` 选；
- **消费者**攒批（`BatchSize` 条或距上次 flush 超过 `BatchDelayMilliseconds` 即 flush），在新作用域里**逐条**调用写入器，写入异常只记 `LogWarning`、不重试。

::: warning 队列默认全部关闭
5 个 `EnableXxxLogQueue` 默认都是 `false`。此时管道**同步 await 写入器**——写库耗时直接计入接口响应时间，且传的是 `CancellationToken.None`（客户端断连也会把这条日志写完）。生产环境建议逐类打开队列。
:::

## 安装与启用

用 [Web.Api](../packages/web-api) 或 [Data](../packages/data) 时，`XiHanAuditingModule` 已作为传递依赖引入，无需单独安装。只在非 Web、非数据场景单独用时才需要显式依赖：

```csharp
[DependsOn(typeof(XiHanAuditingModule))]
public class MyModule : XiHanModule;
```

模块的 `ConfigureServices` 调 `services.AddXiHanAuditing(config)`，一次性装好：配置绑定（`XiHan:Auditing:LogQueue`）、开放泛型队列 `ILogQueue<>` → `ChannelLogQueue<>`（单例）、5 个后台消费者、5 个采集管道，以及**默认为空实现**的写入器契约。

## 让日志真正落库

::: danger 不实现写入器 = 日志静默丢弃
5 个 `IXxxLogWriter` 与 `IEntityDiffLogWriter` 的默认实现全是空实现（`NullXxxLogWriter`），采集到就扔掉——不报错、不告警。排查「日志页面恒空」时，第一件事是确认你的写入器有没有进容器。
:::

写入器契约用 `TryAddScoped` 注册，所以应用侧注册同接口实现即可生效：

```csharp
public class OperationLogWriter : IOperationLogWriter, IScopedDependency
{
    private readonly IRepositoryBase<SysOperationLog, long> _repository;

    public OperationLogWriter(IRepositoryBase<SysOperationLog, long> repository)
    {
        _repository = repository;
    }

    public async Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default)
    {
        await _repository.AddAsync(new SysOperationLog
        {
            TraceId = record.TraceId,
            UserId = record.UserId,
            UserName = record.UserName,
            ControllerName = record.ControllerName,
            ActionName = record.ActionName,
            Method = record.Method,
            Path = record.Path,
            RequestParams = record.RequestParams,
            StatusCode = record.StatusCode,
            ElapsedMilliseconds = record.ElapsedMilliseconds
        }, cancellationToken);
    }
}
```

两种注册方式都行：

| 方式 | 写法 | 说明 |
| --- | --- | --- |
| 约定注册 | 实现类同时标记 `IScopedDependency` | 框架的空实现先注册，你的实现后注册，解析时后者胜出 |
| 显式替换 | `context.Services.Replace(ServiceDescriptor.Scoped<IOperationLogWriter, OperationLogWriter>())` | 容器里只留一条，意图最明确 |

也可以在实现类上加 `[Dependency(ReplaceServices = true)]`，由约定注册器走 `Replace`。

## 实体变更审计

这是唯一不经 HTTP 层的一类，走的是 SqlSugar 原生 `Aop.OnDiffLogEvent`。要产生记录，**三个条件缺一不可**：

1. 配置 `XiHan:Data:SqlSugarCore:EnableDiffLog = true`（默认 `false`，不开则 AOP 根本不挂）；
2. 应用实现并注册 `IEntityDiffLogWriter`；
3. 写操作走框架仓储——仓储的 Insert / Update / Delete 已经内建 `.EnableDiffLogEvent(typeof(TEntity))`，业务代码不用改。

裸 SQL、直接用 `ISqlSugarClient` 绕过仓储的写入**不会**产生变更记录。

### AOP 产出什么

| 方面 | 行为 |
| --- | --- |
| 记录粒度 | **每个受影响的行一条** `EntityDiffLogRecord`，按主键值配对前后镜像（不按下标） |
| `OperationType` | `Create` / `Delete` / `Update`；更新时若 `Is_Deleted` 由 false 翻 true 记为 `Delete`、由 true 翻 false 记为 `Restore` |
| `ChangedFields` | `{Field, Before, After}` 数组；前后镜像都在且无字段变更的行被跳过 |
| 快照裁剪 | 单列值超 1000 字符先截断；整条快照 JSON 超 8000 字符时产出**合法的**截断标记对象，不会从中间切断出非法 JSON |
| 失败处理 | AOP 内部整体 try/catch，审计失败只记错误日志，绝不影响主业务 |

### 写入器的事务契约

```csharp
public class EntityDiffLogWriter : IEntityDiffLogWriter, IScopedDependency
{
    private readonly ISqlSugarClientResolver _clientResolver;

    public EntityDiffLogWriter(ISqlSugarClientResolver clientResolver)
    {
        _clientResolver = clientResolver;
    }

    public async Task WriteAsync(EntityDiffLogRecord record, CancellationToken cancellationToken = default)
    {
        // 必须用当前工作单元的连接，与业务写在同一事务里
        await _clientResolver.GetCurrentClient()
            .Insertable(new SysDiffLog
            {
                OperationType = record.OperationType,
                EntityType = record.EntityType,
                EntityId = record.EntityId,
                BeforeData = record.BeforeData,
                AfterData = record.AfterData,
                ChangedFields = record.ChangedFields,
                UserId = record.UserId,
                TenantId = record.TenantId
            })
            .ExecuteCommandAsync(cancellationToken);
    }
}
```

::: danger 不要在差异日志写入器里另开连接
必须经 `ISqlSugarClientResolver.GetCurrentClient()` 使用当前工作单元连接：业务回滚时审计行随之回滚。改用 `CopyNew()` 或独立连接，会在业务回滚后留下「从未生效的变更」的幽灵记录。
:::

同时，写入器要走**裸 `Insertable`、不挂 `EnableDiffLogEvent`**，否则写审计的 SQL 会再次触发 Diff 事件。`IEntityAuditContextProvider.ShouldAudit` 的默认排除名单（`XiHan.Framework.Auditing` 命名空间、类型全名含 `AuditLog` / `DiffLog`）是第二道保险。

### 自定义审计上下文

`IEntityAuditContextProvider` 决定「审计哪些实体」和「记录哪些上下文字段」。默认实现 `DefaultEntityAuditContextProvider` 只从 `ICurrentUser` / `ICurrentTenant` 填 `UserId` / `UserName` / `TenantId`。要补 HTTP 上下文就自己实现：

```csharp
public class HttpEntityAuditContextProvider : IEntityAuditContextProvider, IScopedDependency
{
    private readonly IHttpContextAccessor _accessor;

    public HttpEntityAuditContextProvider(IHttpContextAccessor accessor) => _accessor = accessor;

    public EntityDiffLogRecord CreateBaseRecord()
    {
        var http = _accessor.HttpContext;
        return new EntityDiffLogRecord
        {
            RequestPath = http?.Request.Path,
            RequestMethod = http?.Request.Method,
            OperationIp = http?.Connection.RemoteIpAddress?.ToString()
        };
    }

    public bool ShouldAudit(Type entityType) =>
        entityType.Namespace?.StartsWith("MyApp.Domain") == true;
}
```

覆盖 `ShouldAudit` 时**务必把审计表自己排除掉**，否则写审计的写入器一旦走了仓储，就会自我触发。

## 审计与 AOP 的关系

「审计」在框架里用了三种完全不同的机制，别混着理解：

| 机制 | 用在哪 | 是不是 Castle 拦截器 |
| --- | --- | --- |
| 中间件 + MVC 过滤器 | 访问 / 操作 / 异常 / 接口日志 | 否，是 ASP.NET Core 管线 |
| SqlSugar `Aop.OnDiffLogEvent` | 实体变更日志 | 否，是 ORM 原生事件 |
| SqlSugar `DataExecuting` | 审计**字段**（`CreatedTime` / `ModifiedTime` / `TenantId`）自动赋值 | 否，同样是 ORM 事件 |

也就是说，审计链路**不依赖动态代理**，不需要接口虚方法、不受代理失效影响。第三项虽然也叫「审计字段」，但它只是在写库时补列值，和审计日志表毫无关系，详见 [数据访问](./data)。

::: warning 写路径别叠 `.EnableQueryFilter()`
框架仓储的 `Updateable` / `Deleteable` 已经自动挂了一次全局过滤器。再显式挂一次会把同一份过滤烘进 WHERE 两遍、生成同名参数，叠上 Diff 的重查即崩。
:::

## 脱敏

`LogSanitizer` 是静态类，也是全框架敏感判定的唯一入口。判定分两步：**名字归一化后命中敏感词**，且**不以元数据后缀结尾**。

- 归一化 = 只保留字母数字并转小写，于是 `X-Api-Key`、`Connection_String`、`apiKey` 都能命中同一份规则；
- 命中即**整体掩码**为 `***`（`LogSanitizer.Mask`），不保留片段——`Authorization: Bearer …` 这类值留一半也等同泄露；
- 元数据后缀豁免：`Token_Type`、`Last_Password_Change_Time`、`Max_Output_Tokens` 这种「关于秘密的元数据」**不掩**，掩掉它们等于把审计价值一起掩掉；
- 值层面另外识别 15 / 18 位身份证号，按「保留首尾各 3 位、掩中段」处理。

| 方法 | 用途 |
| --- | --- |
| `MaskSensitiveData(string?)` | JSON / 表单文本，正则掩敏感键的字面量值（字符串 / 数字 / 布尔 / null），嵌套对象与数组掩不掉 |
| `MaskQueryString(string?)` | URL 查询串 |
| `MaskJsonFields(string?)` | 解析 JSON 对象逐键掩码，敏感键的值是嵌套对象 / 数组时也能整体掩掉；根不是对象或解析失败时回落到正则 |
| `MaskHeaders(...)` | 请求头，头名敏感则整体掩，其余头的值再走一遍通用脱敏 |
| `MaskFieldValue(name, value)` | 字段级：名字敏感返回掩码，否则原值 |
| `IsSensitiveName(string?)` | 只做判定，供自定义脱敏逻辑复用 |

框架已经在这些位置内建了脱敏，你不必重复处理：

| 位置 | 处理 |
| --- | --- |
| 请求体 / 查询串 | 在 `XiHanRequestLoggingMiddleware` 捕获点即脱敏并存入 `HttpContext.Items`，访问 / 接口 / 异常日志复用同一份副本 |
| 响应体 | `XiHanApiLoggingMiddleware` 落库前脱敏（仅 JSON / XML / text 类型转文本，有界捕获 4096 字节） |
| 操作日志参数与结果 | `XiHanActionLoggingFilter` 序列化后脱敏 |
| 异常日志请求头 | `ExceptionLogReporter` 走 `MaskHeaders` |
| 实体快照列值 | `SqlSugarDiffLogAop` 按列名走 `MaskFieldValue` |

自己组装记录（比如登录日志）时，需要自己调用相应方法。

::: tip 变更判定用原值，掩码在之后
差异日志比较字段是否变更时用的是**原值**，掩码发生在写进 `ChangedFields` 之后。否则改密码前后都成 `***`，会被判定为「未变更」而完全不留痕迹。
:::

## 与多租户的配合

6 类记录里只有 `EntityDiffLogRecord` 自带 `TenantId`，由 `DefaultEntityAuditContextProvider` 取 `ICurrentUser.TenantId ?? ICurrentTenant?.Id` 填充。另外 5 类记录模型**没有租户字段**——需要按租户分区就在写入器里补，但补之前要看清下面这条。

::: danger 队列模式下写入器读不到当前用户 / 租户
后台消费者在自己的线程上用 `IServiceScopeFactory.CreateScope()` 新建作用域：没有 `HttpContext`，AsyncLocal 的租户上下文也不流到那里。写入器里读 `ICurrentUser` / `ICurrentTenant` 会得到空值。

凡是写入器需要的身份信息，必须在**采集点**放进 record。
:::

两种模式行为不同，别依赖「碰巧能读到」：

| 模式 | 写入器执行位置 | 能否读到用户 / 租户上下文 |
| --- | --- | --- |
| 队列关闭（默认） | 请求作用域内同步执行 | 能 |
| 队列开启 | 后台线程新作用域 | 不能 |
| 实体差异日志 AOP | 请求线程内同步执行 | 能 |

审计表本身按你自己的实体定义走既有多租户规则；跨租户查审计要在平台态下进行，见 [多租户](./multi-tenancy)。

## 配置

审计的行为由两个配置节共同决定：

| 配置键 | 默认 | 说明 |
| --- | --- | --- |
| `XiHan:Auditing:LogQueue:IgnoredPathPrefixes` | `["/hubs"]` | 完全不记请求日志的路径前缀（不区分大小写） |
| `XiHan:Auditing:LogQueue:EnableAccessLogQueue` 等 5 项 | `false` | 各类日志是否走异步队列；关闭 = 同步写 |
| `XiHan:Auditing:LogQueue:QueueCapacity` | `10000` | 有界 Channel 容量 |
| `XiHan:Auditing:LogQueue:DropOnFull` | `false` | `false` = 队列满时等待（反压请求线程）；`true` = 丢弃并记一条 `LogWarning` |
| `XiHan:Auditing:LogQueue:BatchSize` | `100` | 攒批条数阈值 |
| `XiHan:Auditing:LogQueue:BatchDelayMilliseconds` | `200` | 攒批时间阈值 |
| `XiHan:Data:SqlSugarCore:EnableDiffLog` | `false` | 实体变更日志总开关（属于 Data 包配置节） |

```json
{
  "XiHan": {
    "Auditing": {
      "LogQueue": {
        "IgnoredPathPrefixes": ["/hubs", "/health"],
        "EnableAccessLogQueue": true,
        "EnableOperationLogQueue": true,
        "EnableExceptionLogQueue": true,
        "EnableApiLogQueue": true,
        "EnableLoginLogQueue": true,
        "QueueCapacity": 10000,
        "DropOnFull": false,
        "BatchSize": 100,
        "BatchDelayMilliseconds": 200
      }
    },
    "Data": {
      "SqlSugarCore": {
        "EnableDiffLog": true
      }
    }
  }
}
```

完整字段表见 [Auditing 包文档](../packages/auditing)。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 日志页面恒空 | 写入器没注册，默认空实现把记录丢掉了 |
| 数据变更日志恒空 | `EnableDiffLog` 没开（默认 `false`），或 `IEntityDiffLogWriter` 没实现 |
| 登录日志一条没有 | 框架不采集登录日志，要自己调 `ILoginLogPipeline` |
| 接口日志一条没有 | 只在请求携带开放接口安全头时才采集 |
| GET 请求没有操作日志 | 过滤器有意跳过 GET / HEAD / OPTIONS |
| SignalR 请求没有访问日志 | `/hubs` 在 `IgnoredPathPrefixes` 默认值里 |
| 接口响应变慢 | 队列默认关闭，写入器是同步执行的 |
| 高峰期请求线程被拖住 | `DropOnFull = false` 时队列满会等待空位 |
| 写入器里 `ICurrentUser` 为空 | 队列模式在后台新作用域执行，身份要在采集点写进 record |
| 批量更新产生 N 条变更记录 | 每个受影响行一条，是预期行为 |
| 更新了但没有变更记录 | 前后镜像都在且没有字段真正变化的行会被跳过 |
| 不敏感的字段被掩成 `***` | 名字归一化后命中了敏感词子串 |
| `Token_Type` 之类没被掩 | 元数据后缀豁免，故意保留审计价值 |
| 写库失败但没人告诉我 | 消费者的写入异常只记 `LogWarning`，不重试 |
| 绕过仓储的写没有变更记录 | 差异日志只覆盖走仓储的写操作 |

## 下一步

- [数据访问](./data)：仓储写路径、审计字段自动赋值
- [工作单元与事务](./uow)：差异日志与业务同事务的前提
- [多租户](./multi-tenancy)：租户上下文与平台态
- [Web 应用开发](./web)：中间件与过滤器在管线里的位置
- [Auditing 包文档](../packages/auditing)：完整 API 与配置表
- [Logging 包文档](../packages/logging)：结构化运行日志（与审计日志是两回事）
