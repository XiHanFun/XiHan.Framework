# XiHan.Framework.Auditing.Database

审计的数据库 Provider：事件先进有界队列，后台批量写成 `AuditRecord`，查询按写入序号分页。提供 `auditing.trail@1`，适用于要持久化操作日志、按租户 / 主体 / 时间 / trace-id 查询的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Auditing.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `auditing.trail@1`（`role: provider`）；依赖 `kernel.hosting@1`、`persistence.relational@1` |
| 特性入口 | `DatabaseAuditingFeature`（清单 `xihan.auditing.database`，`IHostedFeature`） |
| 配置节 | `XiHan:Auditing`（中间件选项）、`XiHan:Auditing:Database` |
| 依赖 | `XiHan.Framework.Auditing.Abstractions`、`XiHan.Framework.Persistence.Abstractions`；不引用数据库驱动，具体数据库由持久化 Provider 决定 |
| AOT | 兼容（细节与错误细节的 JSON 手写，不经反射序列化） |
| 稳定性 | Preview |

## 概述

`DatabaseAuditTrail` 是单例，实现 `IAuditSink` 与 `IAuditTrail`（ADR-0028）：

- 写入：`WriteAsync` 只把事件放进容量为 `QueueCapacity` 的队列，永不阻塞、永不抛出。队列满了或特性已停止时，新到的事件被丢弃并计入 `DroppedCount`，警告每 10 秒最多一条并附累计数。
- 落库：特性启动后运行后台循环，攒够 `BatchSize` 条（写入时满一批即唤醒）或到 `FlushInterval` 就取一批映射成 `AuditRecord`，一次 `AddRangeAsync` 写入。整批异常或逐项被仓储拒绝都只记 Error 日志、计入 `FailedCount` 并丢弃，不重试。刷写串行，后台循环与 `FlushAsync` 互斥。
- 序号：出队时分配 `Sequence`，取上一个加一与时钟 UTC 刻度的较大者。同一节点严格递增、与提交顺序一致；不同节点之间只按时钟大致有序，时钟差之内的交错不保证。
- 查询：`query.Validate()` 后把过滤项组合成规约，按 `Sequence` 升序、多取一条判断下一页；续页令牌是本页最后一条的 `Sequence`。
- 作用域：读写都在根提供者的新作用域里直接开工作单元（不带租户），返回即提交、异常即回滚，不经管道，也不加入调用方的事务。
- 停止：先停循环，再停止接收并把队列里剩余的事件刷完；之后到达的事件按丢弃计数。

审计记录是平台级数据：`AuditRecord` 不实现 `IMultiTenant`，租户只是普通列，不参与租户分片，独立库租户的记录也落应用的默认连接（RFC-0015，ADR-0057）。

## 何时使用

生产环境需要留存操作日志、多实例共享一份审计、管理界面要按条件分页查询时。只要近期留痕、不需要持久化时用 [Auditing.InMemory](./auditing-inmemory)。装配本包即顶替内存实现。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Auditing.Database" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包要求 `persistence.relational@1`，与一个持久化 Provider 一起装配：

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

var app = builder.Build();
app.UseXiHan();
app.Run();
```

特性登记 `AuditRecord` 的仓储。用 EF Core 时在应用自己的 `DbContext` 里登记实体，列长按记录上的 `[StringLength]` 映射，三个长文本列显式给类型：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Auditing.Database.Trail;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditRecord>(static audit =>
        {
            audit.Property(static x => x.DetailsJson).HasColumnType("text");
            audit.Property(static x => x.ErrorDetailsJson).HasColumnType("text");
            audit.Property(static x => x.ErrorMessage).HasColumnType("text");
            audit.HasIndex(static x => x.Sequence).IsUnique();
            audit.HasIndex(static x => x.TraceId);
            audit.HasIndex(static x => x.Timestamp);
        });
    }
}
```

EF Core Provider 的装配见 [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Auditing:Database:QueueCapacity` | int，大于 0 | 10000 | 等待落库的事件数上限，满了丢弃新到的 |
| `XiHan:Auditing:Database:BatchSize` | int，大于 0 | 100 | 一批落库的事件数上限，攒够即刷写 |
| `XiHan:Auditing:Database:FlushIntervalSeconds` | 数值，大于 0 | 1 | 不足一批时的刷写周期（秒） |
| `XiHan:Auditing:Database:LogEvents` | bool | `true` | 每条事件（含被丢弃的）入队前以 Information 级写一条日志 |

中间件的 `Selection` 与 `IncludeSuccesses` 仍在 `XiHan:Auditing`，见 [Auditing.Abstractions](./auditing-abstractions#配置)。值不合法时 `DatabaseAuditingOptions.FromConfiguration` 抛出 `ArgumentException`。

```json
{
  "XiHan": {
    "Auditing": {
      "Selection": "Protected",
      "IncludeSuccesses": true,
      "Database": { "QueueCapacity": 10000, "BatchSize": 100, "FlushIntervalSeconds": 1, "LogEvents": true }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseAuditingFeature` | 能力包入口；启动后运行刷写循环，停止时刷完剩余事件 |
| `DatabaseAuditingOptions` | `QueueCapacity`、`BatchSize`、`FlushInterval`、`LogEvents`；`ConfigurationSection` 为 `XiHan:Auditing:Database` |
| `Trail.DatabaseAuditTrail` | 单例接收器与查询；`FlushAsync` 立即刷写并等待完成；`PendingCount`、`PersistedCount`、`FailedCount`、`DroppedCount` 四个计数 |
| `Trail.AuditRecord` | 存储行，`IEntity<Guid>`，只追加 |

`AuditRecord` 的列：

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | `Guid` | 主键，由 `IIdGenerator<Guid>` 生成；同毫秒内不单调，不当顺序用 |
| `Sequence` | `long` | 写入序号，排序与续页令牌 |
| `Timestamp` | `DateTimeOffset` | 事件时间 |
| `Action` | 字符串，256 | 动作 |
| `Succeeded` | `bool` | 是否成功 |
| `ErrorCode` | 字符串，64 | 失败时的错误码 |
| `ErrorMessageKey` | 字符串，256 | 失败时的消息键；字面错误为空 |
| `ErrorMessage` | 长文本 | 写入时按进程默认区域渲染的错误文案 |
| `ErrorDetailsJson` | 长文本 | 错误细节的 JSON 对象 |
| `TraceId` | 字符串，64 | trace-id |
| `Resource` | 字符串，1024 | `xrn:` 资源名 |
| `PrincipalId` | 字符串，256 | 主体 ID |
| `TenantId` | 字符串，128 | 租户 ID，普通列 |
| `DetailsJson` | 长文本 | 细节的 JSON 对象 |

字符串列以 `[StringLength(n)]` 声明长度，长文本列以 `Persistence.Abstractions` 的 `[LongText]` 标记，SqlSugar 的 CodeFirst 与 EF Core 都据此建表（ADR-0036）。

## 使用示例

把四个计数接到应用自己的 `Meter`，经 OpenTelemetry 导出时在 `XiHan:Observability:OpenTelemetry:Meters` 里加上 `MyApp.Auditing`：

```csharp
using System.Diagnostics.Metrics;
using XiHan.Framework.Auditing.Database.Trail;

namespace MyApp.Diagnostics;

public sealed class AuditingMetrics : IDisposable
{
    private readonly Meter _meter = new("MyApp.Auditing");

    public AuditingMetrics(DatabaseAuditTrail trail)
    {
        // 计数在 DatabaseAuditTrail 里原子累加，这里只是按需读出
        _meter.CreateObservableGauge("myapp.audit.pending", () => trail.PendingCount, "{event}");
        _meter.CreateObservableCounter("myapp.audit.persisted", () => trail.PersistedCount, "{event}");
        _meter.CreateObservableCounter("myapp.audit.failed", () => trail.FailedCount, "{event}");
        _meter.CreateObservableCounter("myapp.audit.dropped", () => trail.DroppedCount, "{event}");
    }

    public void Dispose() => _meter.Dispose();
}
```

登记为单例并在启动后解析一次（如 `app.Services.GetRequiredService<AuditingMetrics>()`），仪表才会创建。测试里断言前要让队列立即落库时调用 `FlushAsync`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Database.Trail;

namespace MyApp.Tests;

public static class AuditTestExtensions
{
    // 与后台循环互斥：循环正在刷写时等它这一批结束，再刷剩下的
    public static Task FlushAuditAsync(this IServiceProvider services, CancellationToken cancellationToken)
        => services.GetRequiredService<DatabaseAuditTrail>().FlushAsync(cancellationToken);
}
```

## 行为与限制

- 丢审计而不是拒服务：存储不可用或写入速度跟不上时，事件在队列满后被丢弃，丢了多少看 `DroppedCount` 与告警日志；落库失败的看 `FailedCount`。审计不可用不改变应用健康等级，清单不声明健康条目。
- 写入顺序：同一节点内续页不会越过尚未落库的事件；跨节点只是时钟序，不引入中心计数器。
- 细节值按类型写成 JSON：`null`、字符串、布尔、整数与浮点、`DateTimeOffset` / `DateTime` / `Guid`（写成字符串）、`JsonElement` / `JsonNode`、嵌套字典与列表，其余按 `ToString()`；读回时每个值是 `JsonElement`。
- 错误还原：带消息键的错误还原为 `new Error(code, key, details)`，读取方可再按调用方区域翻译；没有键的字面错误按写入时渲染的 `ErrorMessage` 原样还原。`Error.Inner` 不持久化。
- 表结构由应用管理：`XiHan:Persistence:SqlSugar:InitTables` 只用于开发与测试。建议索引 `(Sequence)` 唯一、`(TraceId)`、`(Timestamp)`，按查询习惯再加 `(TenantId, Timestamp)` 或 `(PrincipalId, Timestamp)`。
- 查询的可空字符串列只做相等比较；过滤项为空即不参与。

| 错误码 | 何时出现 |
| --- | --- |
| `AUDITING_VALIDATION_001` | 页大小越界、起止颠倒、续页令牌不是非负整数 |
| `AUDITING_UNAVAILABLE_001` | 查询时存储抛出基础设施异常，`reason` 是异常消息 |

## 相关

- [审计](../guide/auditing)
- [Auditing.Abstractions](./auditing-abstractions)
- [Auditing.InMemory](./auditing-inmemory)
- [持久化](../guide/persistence)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- 规范：RFC-0015《审计》
