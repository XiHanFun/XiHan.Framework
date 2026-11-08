来源：https://framework.docs.xihanfun.com/common/auditing

# 审计

这一章讲框架怎样把一次调用记成审计事件、哪些调用会被记录、处理器怎样补充动作与资源、怎样记录调用来源、怎样按条件分页查询，以及内存与数据库两种存储的取舍。读完后，我们能给应用加上「谁在什么时候对什么做了什么、结果如何」的操作日志。前置知识是[执行管道](/startup/pipeline)、[操作契约](/web/operations)与[调用上下文](/startup/invocation-context)。

## 为什么需要

合规、排障与安全分析都要回答同一个问题：某个动作是谁做的、什么时候、对哪个资源、成功了没有。靠业务代码在每个处理器里手写一条日志，格式不一、容易漏、被拒绝的请求根本到不了处理器。审计子系统把这件事放进管道：Observe 阶段的中间件在调用结束时按规则生成事件，认证、授权、校验的拒绝同样被记下；处理器只在需要时补充动作名、资源与细节；事件进有界的接收器，查询有统一契约，存储可换。

## 审计事件与接收器

内核定义了审计事件 `AuditEvent` 与接收器 `IAuditSink`，默认注册的 `NullAuditSink` 丢弃全部事件。审计子系统在此之上补三件事：挂在 Observe 阶段的 `AuditMiddleware` 在调用结束时决定是否生成事件；调用上下文上的 `SetAuditAction`、`SetAuditResource`、`AddAuditDetail` 让处理器补充信息；查询契约 `IAuditTrail` 按条件分页读回。

能力 `auditing.trail@1` 的 Provider 同时实现 `IAuditSink` 与 `IAuditTrail`。被能力图谱选中时，Provider 先占住 `IAuditSink`，再注册消息目录与选项，并把 `AuditMiddleware` 挂到 Observe 阶段末尾，也就是内核的 `ObserveMiddleware` 之内。Observe 之后的各阶段都在它里面执行，所以认证、授权、校验阶段的拒绝也会以结果的形式被它看到。

一条事件的字段与来源：

| 字段 | 来源 |
| --- | --- |
| `Timestamp` | 调用结束时 `TimeProvider` 的 UTC 时刻 |
| `Action` | 显式声明的动作；没有时是当前操作的 ID |
| `Outcome` | 管道写入的结果，见下文「结果怎么取」 |
| `TraceId` | 调用上下文的 trace-id |
| `Resource` | `SetAuditResource` 写入的 `xrn:` 资源名 |
| `PrincipalId` | 认证后的主体 ID，没有时取 Baggage `xihan.principal` |
| `TenantId` | 解析出的租户 ID，没有时取 Baggage `xihan.tenant` |
| `Details` | `AddAuditDetail` 追加的键值，只允许 JSON 可表示的值 |

## 启用

`Profiles.Full` 已经装配默认实现 `InMemoryAuditingFeature`。用 `Profiles.Standard` 或自己组合时显式装配：

```bash
dotnet add package XiHan.Framework.Auditing.InMemory --prerelease
```

```csharp
using XiHan.Framework.Auditing.InMemory;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<InMemoryAuditingFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

要持久化时换成 `DatabaseAuditingFeature`，见下文「数据库存储」。两个实现同时装配时，显式 Provider 顶替 `role: default` 的内存实现，内存实现既不绑定接收器也不挂中间件。

## 最小示例

一个受权限保护的操作，装配后不写任何审计代码就会被记录；这里再把导出报表记成单独的动作，带上资源名与行数：

```csharp
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace MyApp.Orders;

public sealed record ExportOrdersRequest(string Month);

public sealed record ExportOrdersResponse(int Rows);

[Operation("orders.export", Expose = Expose.Http, Permission = "orders.read")]
public sealed class ExportOrders : IOperation<ExportOrdersRequest, ExportOrdersResponse>
{
    public ValueTask<Result<ExportOrdersResponse>> HandleAsync(ExportOrdersRequest request, InvocationContext context)
    {
        var rows = 1200;

        // 动作缺省是操作 ID；导出报表单独命名，查询时按动作区分
        context.SetAuditAction("orders.export-report");
        context.SetAuditResource(new ResourceName("global", "orders", context.GetSecurity().Tenant?.Id ?? string.Empty, "report", request.Month));
        context.AddAuditDetail("month", request.Month);
        context.AddAuditDetail("rows", rows);

        return ValueTask.FromResult(Result.Success(new ExportOrdersResponse(rows)));
    }
}
```

请求与响应类型要登记到项目里的 `JsonSerializerContext`，操作由 `Kernel.Generators` 生成登记代码，写法见[操作契约](/web/operations)。把它放进一个特性（下文「记录调用来源」里的 `OrdersFeature`）并装配，运行后：调用 `orders.export` 成功时审计里多一条 `Action = orders.export-report`、`Resource = xrn:global:orders:<租户>:report/<月份>`、`Details` 含 `month` 与 `rows` 的事件；没有 `orders.read` 权限的调用被授权阶段拒绝，同样留下一条 `Outcome` 为失败、错误码是授权错误码的事件；内存实现缺省还把每条事件以 Information 级写成一行日志。

三个扩展的规则：

| 扩展 | 规则 |
| --- | --- |
| `SetAuditAction(action)` | 动作不能为空白；声明后这次调用一定记录，非操作调用也一样 |
| `SetAuditResource(resource)` | 参数是 `ResourceName`，事件里存它的 `xrn:<partition>:<service>:<tenant>:<type>/<id>` 文本；租户段可以为空 |
| `AddAuditDetail(key, value)` | 键不能为空白，同名覆盖；值只用 JSON 可表示的类型，数据库存储按类型写成 JSON |

## 哪些调用会被记录

中间件先找动作：处理器调用过 `SetAuditAction` 的，无论选择规则如何都记录；否则只看操作调用（上下文里有操作描述），按 `XiHan:Auditing:Selection` 决定。经 `UseXiHan()` 的普通端点、消息派发、作业运行这类不是操作的调用，没有显式动作就不记录。

| `Selection` | 记录的操作 |
| --- | --- |
| `Protected`（缺省） | 需要授权的操作：契约声明了 `Permission` 或 `Policy`（只用策略保护的操作同样记录） |
| `All` | 全部操作 |
| `None` | 只记显式声明了动作的调用 |

`IncludeSuccesses` 缺省为 `true`；设为 `false` 后成功的调用不再记录，只留失败。两个键的值不区分大小写，取值不合法时装配阶段抛出 `ArgumentException`，应用启动失败。

### 按操作排除

`Protected` 会把只用 `authenticated` 策略保护的只读查询也记下来（当前用户的权限、会话角色、我的登录日志……），前端每次加载都调的接口会让审计表涨得很快。`XiHan:Auditing:Exclude` 列出不想记的操作：

```json
{
  "XiHan": {
    "Auditing": {
      "Exclude": [ "permissions.current", "profile.*", "my-*" ]
    }
  }
}
```

- 每项是操作 ID（精确匹配），或以 `*` 结尾的前缀：`profile.*` 是资源 `profile` 的全部操作，`my-*` 是资源名以 `my-` 开头的全部操作；按序数比较，区分大小写。
- 只作用于按 `Selection` 挑出的操作：处理器调用了 `SetAuditAction` 的调用照样记录，排除不压掉代码要求留痕的动作。
- 不配置、或列表为空时不排除任何操作。
- 必须写成列表（环境变量写 `XiHan__Auditing__Exclude__0`）；写成单个值、某一项是空白、单独的 `*`、`*` 不在末尾、大写或其他字符，读选项时抛出 `ArgumentException`（消息指出配置键与那一项），应用启动失败。
- 不核对操作是否已登记：模块可以按配置开关，插件的操作可能在启动之后才登记，列了不存在的操作只是不命中。

## 结果怎么取

事件的 `Outcome` 按以下顺序确定：

1. 管道里已经写入结果的，用写入的结果。被授权拒绝、校验失败、处理器返回失败的调用都在这一步，错误码就是管道里的错误码。
2. 没有写入结果而抛出了异常的，记为失败：取消记 `KERNEL_CANCELLED_001`，其他异常记 `KERNEL_UNEXPECTED_001`。异常继续向外抛给 `ObserveMiddleware`，由它转成调用结果。
3. 正常返回却没有写结果的，记为成功。

写接收器时不带调用的取消令牌，被取消的调用同样留下记录。接收器抛出的异常只以 Error 级写一条日志，不改变调用结果；`IAuditSink` 的契约要求实现有界、不阻塞调用路径，两个内置实现的写入都不等待存储。

## 主体与租户从哪里来

事件在调用结束时构造，此时 Authenticate 与 Tenant 阶段已经把安全上下文写好。中间件先看安全上下文：主体已认证时取 `Principal.Id`，否则回退到 Baggage 的 `xihan.principal`；解析出租户时取 `TenantContext.Id`，否则回退到 Baggage 的 `xihan.tenant`。HTTP 加 JWT 或 API Key 的调用走第一条路；插件协议、消息派发、命令行这类只带 Baggage 的调用走回退路径。认证中间件不把主体回写到 Baggage：Baggage 是跨进程传播的载体，不是进程内状态的副本。

## 记录调用来源

协议适配器与派发器在建上下文时写入一次调用来源 `InvocationOrigin`，之后只读、不跨进程传播（规范 RFC-0003《调用上下文》）。审计事件本身不带来源字段，需要远端地址、User-Agent 或路由模板时，由应用写进细节。

| `InvocationOrigin` 成员 | 含义 |
| --- | --- |
| `Protocol` | `InvocationProtocols` 的值：`http`、`grpc`、`mcp`、`cli`、`messaging`、`job`、`workflow` |
| `Target` | HTTP 为方法与路径（不含查询串），gRPC 为方法全名，MCP 为工具名，命令行为操作 ID，消息为主题，作业为作业名，工作流为操作名（如 `workflow.start`） |
| `RemoteAddress` | 客户端 IP，不含端口；经反向代理时取宿主转发头中间件改写后的值，没有网络对端时为空 |
| `UserAgent`、`Referer` | 对应的请求头，没有时为空 |
| `Route`、`EndpointName` | 匹配到的端点的路由模板与 `WithName` 名字，没有匹配到端点或协议没有端点时为空 |

缓存回源、存储维护这类内部调用没有来源，`GetOrigin()` 返回无值。下面的中间件放在 Ingress 阶段开头，被认证或授权拒绝的调用也能带上来源：

```csharp
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace MyApp.Orders;

public sealed class AuditOriginMiddleware : IPipelineMiddleware
{
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        // 审计事件不带调用来源；需要远端地址与 User-Agent 时写进细节
        if (context.GetOrigin().TryGetValue(out var origin))
        {
            context.AddAuditDetail("protocol", origin.Protocol);
            context.AddAuditDetail("target", origin.Target);
            context.AddAuditDetail("remoteAddress", origin.RemoteAddress);
            context.AddAuditDetail("userAgent", origin.UserAgent);
        }

        return nextStep(context);
    }
}
```

在特性里登记中间件、权限与生成的操作：

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace MyApp.Orders;

public sealed class OrdersFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.myapp.orders",
        Package = "MyApp",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Permissions = ["orders.read"] },
        Entry = typeof(OrdersFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        // Ingress 在 Observe 之外：被认证或授权拒绝的调用也带上来源细节
        context.AddMiddleware<AuditOriginMiddleware>(PipelineStage.Ingress, Placement.Beginning);
        context.AddGeneratedOperations();
    }
}
```

`AddGeneratedOperations()` 由 `Kernel.Generators` 生成在项目的根命名空间（这里是 `MyApp`）。特性的写法见[特性与清单](/startup/features)。

## 查询

`IAuditTrail.QueryAsync(AuditQuery, CancellationToken)` 返回 `Result<AuditPage>`。过滤项全部可选、取交集，结果按写入顺序升序；`AuditPage.ContinuationToken` 不透明，没有下一页时为 `null`，把它原样放进下一次查询的 `ContinuationToken` 即可续读。

| `AuditQuery` 成员 | 说明 |
| --- | --- |
| `From`、`To` | 时间范围，两端都包含；`From` 晚于 `To` 时查询不合法 |
| `Action`、`PrincipalId`、`TenantId`、`TraceId` | 区分大小写的精确匹配 |
| `Succeeded` | `true` 只取成功，`false` 只取失败 |
| `PageSize` | 缺省 100，取值 1 到 `AuditQuery.MaxPageSize`（1000） |
| `ContinuationToken` | 上一页返回的续页令牌 |

下例逐页统计某租户一段时间内失败调用的动作分布，只保留计数：

```csharp
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Primitives;

namespace MyApp.Orders;

public sealed class FailureStatistics(IAuditTrail trail)
{
    // 逐页读取，只保留按动作的计数，不把整段审计装进内存
    public async ValueTask<Result<IReadOnlyDictionary<string, int>>> CountByActionAsync(string tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string? token = null;
        do
        {
            var query = new AuditQuery { TenantId = tenantId, From = from, To = to, Succeeded = false, PageSize = AuditQuery.MaxPageSize, ContinuationToken = token };
            var page = await trail.QueryAsync(query, cancellationToken);
            if (!page.TryGetValue(out var current))
            {
                return page.Error!;
            }

            foreach (var auditEvent in current.Events)
            {
                counts[auditEvent.Action] = counts.GetValueOrDefault(auditEvent.Action) + 1;
            }

            token = current.ContinuationToken;
        }
        while (token is not null);

        return Result.Success<IReadOnlyDictionary<string, int>>(counts);
    }
}
```

`FailureStatistics` 以 `builder.Services.AddSingleton<FailureStatistics>()` 登记。

::: warning 审计记录是平台级数据
审计记录不实现 `IMultiTenant`，`IAuditTrail` 不按当前租户自动过滤。给租户管理员看的界面必须显式传 `TenantId`；数据库存储里独立库租户的记录也落在应用的默认连接，靠租户列区分（规范 RFC-0015《审计》）。
:::

## 内存存储

`Auditing.InMemory` 是默认实现：一个有界环形缓冲保留最近 `Capacity` 条事件（缺省 10000），满了丢最旧的。序号持续递增并用作续页令牌，最旧的记录被淘汰后已发出的令牌仍然有效。`LogEvents` 缺省开启，每条事件以 Information 级写一条日志，字段是动作、结果（成功为 `success`，失败为错误码）、主体、租户、资源与 trace-id。数据只在单个进程里，重启即失，适合开发、测试与单实例的短期留痕。

## 数据库存储

`Auditing.Database` 依赖 `persistence.relational@1`，事件以 `AuditRecord` 经仓储落库，装配后顶替内存实现：

```bash
dotnet add package XiHan.Framework.Auditing.Database --prerelease
```

```csharp
using SqlSugar;
using XiHan.Framework.Auditing.Database;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseAuditingFeature>());
```

连接串来自绑定 `db`，写法见[资源绑定](/configuration/bindings)与[持久化与 Provider](/persistence/overview)。写入与落库的过程：

1. `WriteAsync` 只把事件放进容量为 `QueueCapacity`（缺省 10000）的队列，不等待、不抛出。队列满了或特性已停止时，新到的事件被丢弃并计入 `DroppedCount`，警告每 10 秒最多记一条并附累计数。
2. 后台循环攒够 `BatchSize`（缺省 100）条或到 `FlushIntervalSeconds`（缺省 1 秒）就取出一批，一次 `AddRangeAsync` 写入。
3. 整批异常或逐项被仓储拒绝都只记 Error 日志、计入 `FailedCount` 并丢弃该批，不重试：存储不可用时重试只会堆积，审计不能反过来拖垮业务。
4. 停止时先停循环、再停止接收，然后把队列里剩余的事件刷完。

每条记录在出队时分配写入序号 `Sequence`，取上一个加一与时钟 UTC 刻度的较大者：同一节点严格递增并与提交顺序一致，不同节点之间只按时钟大致有序。查询按 `Sequence` 升序、多取一条判断下一页，续页令牌就是本页最后一条的序号。

读写都不经管道：每次在根提供者的新作用域里直接开一个不带租户的工作单元，返回即提交，不加入调用方的事务。审计在调用结束后才知道结果，此时 Transaction 阶段已经提交或回滚，失败的调用同样要记。

`Error.Inner` 不持久化；带消息键的错误按 `ErrorCode` 与 `ErrorMessageKey` 还原，读取方可以再按调用方区域翻译。表结构、长文本列与索引见 [Auditing.Database](/packages/auditing-database)。

## 配置

```json
{
  "XiHan": {
    "Auditing": {
      "Selection": "Protected",
      "IncludeSuccesses": true,
      "Exclude": [ "permissions.current" ],
      "InMemory": { "Capacity": 10000, "LogEvents": true },
      "Database": { "QueueCapacity": 10000, "BatchSize": 100, "FlushIntervalSeconds": 1, "LogEvents": true }
    }
  }
}
```

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Auditing:Selection` | `Protected` / `All` / `None` | `Protected` | 没有显式动作时记哪些操作 |
| `XiHan:Auditing:IncludeSuccesses` | bool | `true` | 为 `false` 只记失败 |
| `XiHan:Auditing:Exclude` | 字符串列表：操作 ID 或 `*` 结尾的前缀 | 空 | 按选择规则挑出的操作命中即不记，显式动作照记 |
| `XiHan:Auditing:InMemory:Capacity` | int，大于 0 | 10000 | 内存存储保留的事件数 |
| `XiHan:Auditing:InMemory:LogEvents` | bool | `true` | 每条事件写一条 Information 日志 |
| `XiHan:Auditing:Database:QueueCapacity` | int，大于 0 | 10000 | 等待落库的事件数上限 |
| `XiHan:Auditing:Database:BatchSize` | int，大于 0 | 100 | 一批落库的事件数上限 |
| `XiHan:Auditing:Database:FlushIntervalSeconds` | 数值，大于 0 | 1 | 不足一批时的刷写周期（秒） |
| `XiHan:Auditing:Database:LogEvents` | bool | `true` | 每条事件（含被丢弃的）写一条 Information 日志 |

## 错误码

| 错误码 | 何时出现 |
| --- | --- |
| `AUDITING_VALIDATION_001` | 查询不合法：页大小越界、起止颠倒、续页令牌不可识别 |
| `AUDITING_UNAVAILABLE_001` | 审计存储不可达（数据库存储的查询抛出基础设施异常，`reason` 只写异常类型的全名，完整异常进日志） |
| `KERNEL_CANCELLED_001` | 事件的结果：调用被取消且管道没有写入结果 |
| `KERNEL_UNEXPECTED_001` | 事件的结果：调用抛出未处理异常且管道没有写入结果 |

## 与其他能力的关系

- 可观测性：`LogEvents` 的日志经 `Microsoft.Extensions.Logging` 输出，装配 OpenTelemetry 导出后随日志信号一起导出，见[日志与可观测性](/common/observability)；审计事件与链路共享同一个 trace-id，可以从审计事件跳到链路。数据库存储的四个计数可以接到应用自己的 `Meter`，示例见 [Auditing.Database](/packages/auditing-database#使用示例)。
- 多租户：审计不参与租户分片，见[租户分片与分库](/multi-tenancy/sharding)。
- 测试：自写 Provider 继承 `Auditing.Conformance` 的 `AuditTrailConformance` 并通过全部用例，见[一致性套件与门禁](/testing/conformance)。
- 审计事件只有管道产生的这一种，没有实体变更审计：要记录字段级的改动，由应用在处理器里用 `AddAuditDetail` 写进细节，或在领域事件里自行实现。

## 常见问题

### 为什么我的端点没有被记录

审计只自动记录操作调用；经 `UseXiHan()` 的普通端点、消息处理器、作业没有操作描述，缺省不记。要记就在处理代码里调用 `context.SetAuditAction("…")`，声明了动作的调用一定记录。

### 审计表涨得太快

三个开关按需组合：`Selection` 从 `Protected` 收窄到 `None`（只记显式动作）；`IncludeSuccesses = false` 只留失败；`Exclude` 排掉前端每次加载都调的只读查询。显式 `SetAuditAction` 的调用不受这三者影响。

### 审计写入失败会不会影响业务

不会。接收器的写入不带调用的取消令牌、不等待存储；数据库实现只是入队，队列满或落库失败只记日志与计数，不重试、不抛出，调用结果不变。代价是极端情况下事件会丢，`DroppedCount` 与 `FailedCount` 能看到数量。

### 怎样查某次请求的审计

Problem Details、日志与审计事件共用 trace-id：拿到请求返回的 trace-id，用 `AuditQuery { TraceId = … }` 精确查到那一条，再到链路系统看同一个 trace。

### 租户管理员能看别的租户的审计吗

`IAuditTrail` 不按当前租户过滤，给租户看的界面必须由应用显式传 `TenantId` 并做好授权。审计是平台级数据，不随租户分库。

## 相关包与下一步

- [Auditing.Abstractions](/packages/auditing-abstractions)：事件、扩展、查询契约与选项
- [Auditing.InMemory](/packages/auditing-inmemory)：默认实现与容量
- [Auditing.Database](/packages/auditing-database)：数据库实现、表结构与计数
- 下一页：[日志与可观测性](/common/observability)
