# XiHan.Framework.Auditing

> 审计日志基础设施：6 类日志记录、Channel 异步队列 + 后台批量消费者、采集管道、敏感数据脱敏、写入器契约（默认空实现）。

- **NuGet**：`XiHan.Framework.Auditing`
- **模块类**：`XiHanAuditingModule`
- **所在层**：基础设施层（横切关注点）
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（Security / MultiTenancy.Abstractions）

## 概述

这个包提供**与传输协议、ORM 都无关**的审计日志采集基础设施。它定义了 6 类日志记录模型、一套"采集管道 → 异步队列 → 后台消费者 → 写入器"的链路，以及落库用的写入器契约——但**它自己不落库**。所有 `IXxxLogWriter` 的默认实现都是空实现（`NullXxxLogWriter`），日志采集到就丢掉。

这是刻意的分层：框架负责"采集什么、怎么异步化、怎么脱敏"，应用负责"存到哪张表"。应用侧实现各 `IXxxLogWriter` 与 `IEntityAuditContextProvider` 并注册进容器，日志才会真正落库。

框架内部有两个消费方：

- [Web.Api](./web-api) 的中间件负责填充并调用访问 / 操作 / 异常 / 接口 / 登录日志管道
- [Data](./data) 的 `EntityChangeInterceptor` 与 `SqlSugarDiffLogAop` 负责实体变更日志

## 何时使用

- 需要记录谁在什么时候访问了哪个接口、耗时多久、返回什么状态码
- 需要审计实体的字段级变更（改前 / 改后 / 变更了哪些字段）
- 需要登录审计（成功、失败、设备、IP）
- 需要把上述日志**异步批量**落库，避免拖慢主请求
- 需要请求体 / 查询串在落日志前自动脱敏（密码、令牌、身份证号等）

## 安装与启用

```bash
dotnet add package XiHan.Framework.Auditing
```

```csharp
[DependsOn(typeof(XiHanAuditingModule))]
public class MyModule : XiHanModule { }
```

> 用 [Web.Api](./web-api) 或 [Data](./data) 时它已作为传递依赖被自动引入，通常不必单独安装。

`XiHanAuditingModule.ConfigureServices` 调用 `services.AddXiHanAuditing(config)`，注册：

- 配置绑定：`XiHan:Auditing:LogQueue` → `XiHanAuditingLogQueueOptions`
- 队列：`ILogQueue<>` → `ChannelLogQueue<>`（单例，有界 Channel）
- 5 个后台消费者（`AddHostedService`）：`AccessLogQueueWorker`、`OperationLogQueueWorker`、`ExceptionLogQueueWorker`、`ApiLogQueueWorker`、`LoginLogQueueWorker`
- 5 个采集管道（`AddScoped`）：`IAccessLogPipeline`、`IOperationLogPipeline`、`IExceptionLogPipeline`、`IApiLogPipeline`、`ILoginLogPipeline`
- 5 个写入器契约（`TryAddScoped`，**默认空实现**）：`IAccessLogWriter` → `NullAccessLogWriter` 等
- 实体变更审计（`TryAddScoped`）：`IEntityAuditContextProvider` → `DefaultEntityAuditContextProvider`，`IEntityDiffLogWriter` → `NullEntityDiffLogWriter`

## 工作原理

### 采集链路

```text
中间件 / 拦截器
   └→ IXxxLogPipeline.WriteAsync(record)
         ├ EnableXxxLogQueue = false（默认）→ 直接 await IXxxLogWriter.WriteAsync(record)  ← 同步落库
         └ EnableXxxLogQueue = true          → 入队 ILogQueue<TRecord>
                                                  └→ XxxLogQueueWorker 攒批 → IXxxLogWriter.WriteAsync 逐条写
```

**队列默认全部关闭**（5 个 `EnableXxxLogQueue` 默认 `false`），此时管道**同步调用写入器**——注意它传的是 `CancellationToken.None`，请求取消也会把这条日志写完。

### 后台消费者的攒批

`XxxLogQueueWorker` 继承 `BackgroundService`。若对应队列未启用，`ExecuteAsync` 直接返回（不占线程）。否则 `await foreach` 消费队列，满足任一条件即 flush：

- 累计条数 ≥ `BatchSize`
- 距上次 flush 已超过 `BatchDelayMilliseconds`

flush 时新建一个 DI scope 解析 `IXxxLogWriter`，**逐条**调用 `WriteAsync`（不是批量 SQL——批量与否取决于你的写入器实现）。写入异常只 `LogWarning`，不会让 Worker 崩溃。host 停止时把残留批次写完。

### 队列本身

`ChannelLogQueue<TRecord>` 包装一个 `Channel.CreateBounded<TRecord>`，容量 `QueueCapacity`，`FullMode` 固定为 `BoundedChannelFullMode.Wait`。于是队列暴露出两种语义明确的入队方式：

- `TryEnqueue(record)`：不等待。入队成功返回 `true`；**队列满时返回 `false` 且记录未入队**
- `EnqueueAsync(record, ct)`：队列满时等待空位，直到有空位或被取消

"满了丢不丢"是**管道层的策略**，由 `DropOnFull` 决定走哪个方法，而不是下沉给 Channel：

- `DropOnFull = false`（默认）→ 管道调 `EnqueueAsync`：队列满时**入队方等待**（反压到请求线程）
- `DropOnFull = true` → 管道调 `TryEnqueue`：队列满时丢弃当前记录，并记一条 `LogWarning`

> 这里不能用 `BoundedChannelFullMode.DropWrite`——那个模式下 `TryWrite` 队列满时也返回 `true`（静默丢弃），管道便无从得知记录已被丢掉，警告日志永远不会触发。

### 实体变更审计

`IEntityAuditContextProvider` 决定"审计谁"与"记录哪些上下文字段"：

- `CreateBaseRecord()`：默认实现从 `ICurrentUser` / `ICurrentTenant` 填 `UserId` / `UserName` / `TenantId`
- `ShouldAudit(Type)`：默认排除 `XiHan.Framework.Auditing` 命名空间下的类型，以及类型全名含 `AuditLog` / `DiffLog` 的类型（**防止审计日志表自己被审计，导致无限递归**）
- `ShouldAuditByName(string tableName)`：供 SQL 命令级拦截器按表名判断，规则同上（不区分大小写）

## 核心能力

- **6 类日志记录**：访问、操作、异常、接口、登录、实体变更
- **异步队列 + 攒批消费**：有界 Channel，可配容量 / 批大小 / 批间隔 / 满时策略
- **逐类开关**：5 个队列各自独立启用，关闭时退化为同步写入
- **敏感数据脱敏** `LogSanitizer`：静态类，键名命中敏感词整体掩码、身份证号按模式掩码
- **写入器契约与空实现**：`TryAddScoped` 注册，应用侧注册同接口实现即可覆盖
- **实体变更审计防递归**：默认上下文提供器排除审计日志自身
- **路径忽略**：`IgnoredPathPrefixes` 默认忽略 `/hubs`（SignalR 协商 / 长轮询 / 心跳高频且无审计价值）

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `AccessLogRecord` | 访问日志：`TraceId`、`UserId`、`Method`、`Path`、`QueryString`、`RequestBody`、`StatusCode`、`RemoteIp`、`UserAgent`、`Referer`、`ElapsedMilliseconds`、`ResponseSize` 等 16 个字段 |
| `OperationLogRecord` | 操作日志：在访问日志基础上带 `ControllerName` / `ActionName` / `RequestParams` / `ResponseResult` |
| `ApiLogRecord` | 接口日志：最全，另含 `ClientId` / `AppId` / `IsSignatureValid` / `SignatureAlgorithm` / `RequestSize` / `IsSuccess` 等 23 个字段 |
| `ExceptionLogRecord` | 异常日志：`ExceptionType` / `ExceptionMessage` / `ExceptionStackTrace` / `RequestHeaders` 等 |
| `LoginLogRecord` | 登录日志：`LoginResult` / `Message` / `LoginIp` / `DeviceId` / `LoginTime` |
| `EntityDiffLogRecord` | 实体变更日志：`OperationType` / `EntityType` / `EntityId` / `BeforeData` / `AfterData` / `ChangedFields` |
| `ILogQueue<TRecord>` | 日志队列契约：`Count` / `TryEnqueue` / `EnqueueAsync` / `DequeueAllAsync` |
| `ChannelLogQueue<TRecord>` | 基于有界 `Channel` 的默认实现（单例，开放泛型注册） |
| `IAccessLogPipeline` 等 5 个 | 采集管道契约（`Scoped`）：`WriteAsync(record, ct)` |
| `IAccessLogWriter` 等 5 个 | 写入器契约（`Scoped`，默认 `NullXxxLogWriter`）——**应用侧实现它才会落库** |
| `IEntityAuditContextProvider` | 实体审计上下文：`CreateBaseRecord()` / `ShouldAudit(Type)` / `ShouldAuditByName(string)` |
| `IEntityDiffLogWriter` | 实体差异日志写入器（默认 `NullEntityDiffLogWriter`） |
| `LogSanitizer` | 静态脱敏器：`MaskSensitiveData(string?)` / `MaskQueryString(string?)`，掩码常量 `Mask = "***"` |
| `XiHanAuditingLogQueueOptions` | 配置选项；配置节 `XiHan:Auditing:LogQueue` |

## 配置

- **配置节名**：`XiHan:Auditing:LogQueue`（来自 `XiHanAuditingLogQueueOptions.SectionName`）

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `IgnoredPathPrefixes` | `string[]` | `["/hubs"]` | 不记录任何请求日志的路径前缀（不区分大小写） |
| `EnableAccessLogQueue` | `bool` | `false` | 访问日志是否走异步队列 |
| `EnableOperationLogQueue` | `bool` | `false` | 操作日志是否走异步队列 |
| `EnableExceptionLogQueue` | `bool` | `false` | 异常日志是否走异步队列 |
| `EnableApiLogQueue` | `bool` | `false` | 接口日志是否走异步队列 |
| `EnableLoginLogQueue` | `bool` | `false` | 登录日志是否走异步队列 |
| `QueueCapacity` | `int` | `10000` | 队列容量（有界 Channel） |
| `DropOnFull` | `bool` | `false` | 队列满时丢弃（`true`）还是等待（`false`） |
| `BatchSize` | `int` | `100` | 批处理大小 |
| `BatchDelayMilliseconds` | `int` | `200` | 批处理间隔（毫秒） |

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
    }
  }
}
```

## 使用示例

### 1. 实现写入器让日志真正落库（必做）

不实现写入器，日志采集了也会被 `NullXxxLogWriter` 丢掉。契约用 `TryAddScoped` 注册，因此**你注册同接口的实现就会覆盖默认空实现**：

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
        await _repository.InsertAsync(new SysOperationLog
        {
            TraceId = record.TraceId,
            UserId = record.UserId,
            Path = record.Path,
            StatusCode = record.StatusCode,
            ElapsedMilliseconds = record.ElapsedMilliseconds
        });
    }
}
```

### 2. 自定义实体审计上下文

默认提供器只填用户与租户。要补 HTTP 上下文（请求路径、IP、RequestId），或改写"审计哪些实体"的规则：

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

    // 只审计业务实体
    public bool ShouldAudit(Type entityType) =>
        entityType.Namespace?.StartsWith("MyApp.Domain") == true;

    public bool ShouldAuditByName(string tableName) =>
        !tableName.Contains("log", StringComparison.OrdinalIgnoreCase);
}
```

> 覆盖 `ShouldAudit` / `ShouldAuditByName` 时，**务必把审计日志表自己排除掉**，否则写审计日志的 SQL 会再触发一次审计，无限递归。

### 3. 落日志前脱敏

```csharp
record.RequestBody = LogSanitizer.MaskSensitiveData(httpRequestBody);
record.QueryString = LogSanitizer.MaskQueryString(httpRequest.QueryString.Value);
```

命中的敏感键名包括 `password` / `pwd` / `secret` / `token` / `authorization` / `otp` / `verifycode` / `bankcard` / `idcard` 等；值层面还会把 15/18 位身份证号按"保留首尾、掩码中段"处理。脱敏只作用于日志副本，不影响业务管道里的原始请求。

## 扩展点 / 自定义

- **换队列实现**：`ILogQueue<>` 以开放泛型单例注册，可替换为自己的实现（如落 Redis 队列跨进程消费）
- **换上下文提供器**：注册自己的 `IEntityAuditContextProvider` 覆盖 `DefaultEntityAuditContextProvider`
- **批量写入**：Worker 是**逐条**调用 `WriteAsync` 的；想要一条 `INSERT` 写多行，在写入器内部自行缓冲，或替换 Worker

## 注意事项与最佳实践

- **不实现写入器 = 日志静默丢弃**。5 个 `IXxxLogWriter` 与 `IEntityDiffLogWriter` 的默认实现都是空实现，且用 `TryAddScoped` 注册——不报错、不告警，日志就是没了。排查"日志页面恒空"时，第一步确认你的写入器有没有被注册进容器。
- **队列默认全关，日志是同步写的**。5 个 `EnableXxxLogQueue` 默认 `false`，此时 `IXxxLogPipeline.WriteAsync` 直接 `await` 写入器——**落库耗时会计入请求响应时间**。生产环境建议逐个打开队列。
- **`DropOnFull = false`（默认）会反压请求线程**。队列满时 `EnqueueAsync` 等待，写入器慢就会拖慢接口。高流量下要么扩容写入器，要么改用 `DropOnFull = true` 接受丢日志。
- **`DropOnFull = true` 时丢弃是静默于业务、显式于日志的**。被丢的记录不会落库，但每丢一条都会记一条 `LogWarning`（含 `TraceId`）。看到这条警告说明写入器的吞吐已跟不上采集速度。
- **同步路径传的是 `CancellationToken.None`**。队列关闭时，客户端断开连接不会中止日志写入——这是有意为之（避免日志被请求取消打断），但意味着慢写入器会拖住线程。
- **审计日志表必须排除在审计之外**。默认 `ShouldAudit` / `ShouldAuditByName` 已排除类型全名或表名含 `AuditLog` / `DiffLog` 的目标；自定义提供器时若忘了这条，写审计日志的 SQL 会再次触发审计，无限递归直至栈溢出或磁盘写满。
- **`IgnoredPathPrefixes` 默认只忽略 `/hubs`**。健康检查、探针、静态资源等高频无价值路径建议一并加入，否则访问日志会被它们淹没。
- **Worker 的写入异常只记 `LogWarning`**。日志落库失败不会让应用崩溃，但也不会重试——这批日志就丢了。需要可靠性时在写入器内部自行重试或落本地文件兜底。

## 依赖模块

- [XiHan.Framework.Security](./security)（`ICurrentUser`）
- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions)（`ICurrentTenant`）

## 相关模块

- [XiHan.Framework.Web.Api](./web-api)（中间件采集访问 / 操作 / 异常 / 接口 / 登录日志）
- [XiHan.Framework.Data](./data)（`EntityChangeInterceptor`、`SqlSugarDiffLogAop` 采集实体变更）
- [XiHan.Framework.Logging](./logging)（结构化运行日志，与审计日志是两回事）
- [XiHan.Framework.Observability](./observability)
