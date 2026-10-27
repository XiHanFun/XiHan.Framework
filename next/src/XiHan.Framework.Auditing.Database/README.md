# XiHan.Framework.Auditing.Database

`auditing.trail@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0015）。审计事件以 `AuditRecord` 经 `persistence.relational@1` 的仓储读写：写入只进有界队列、不阻塞调用路径，后台循环攒够一批或到刷写间隔就一次批量落库；查询按写入序号升序分页。装配后顶替默认的 `Auditing.InMemory`。

```csharp
builder.AddXiHan(x => x
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseAuditingFeature>());

// EF Core 应用在自己的 DbContext 里登记实体：列长按记录上的 [StringLength] 自动映射，[LongText] 的列按 EF Core 默认建长文本
modelBuilder.Entity<AuditRecord>(static a =>
{
    a.Property(static x => x.DetailsJson).HasColumnType("text");
    a.Property(static x => x.ErrorDetailsJson).HasColumnType("text");
    a.Property(static x => x.ErrorMessage).HasColumnType("text");
    a.HasIndex(static x => x.Sequence).IsUnique();
    a.HasIndex(static x => x.TraceId);
    a.HasIndex(static x => x.Timestamp);
});

// 任何地方查询（不要求活动的工作单元）
var page = await trail.QueryAsync(new AuditQuery { TenantId = "acme", Succeeded = false, PageSize = 50 }, ct);

// 测试或停机前让队列立即落库
await host.Services.GetRequiredService<DatabaseAuditTrail>().FlushAsync(ct);
```

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

| 类型 | 用途 |
| --- | --- |
| `DatabaseAuditingFeature` | 登记 `AuditRecord` 的仓储；被选中时把 `DatabaseAuditTrail` 绑定为 `IAuditSink` 与 `IAuditTrail` 并挂 `AuditMiddleware`（Observe 末尾）；启动后运行刷写循环，停止时先停循环再把队列里剩余的事件刷完 |
| `Trail.AuditRecord` | 存储行：`Sequence`（写入序号）、`Timestamp`、`Action`、`Succeeded`、`ErrorCode` / `ErrorMessageKey` / `ErrorMessage` / `ErrorDetailsJson`、`TraceId`、`Resource`、`PrincipalId`、`TenantId`、`DetailsJson`；只追加，不软删除、无并发戳；租户只是普通列，不实现 `IMultiTenant`。字符串列显式声明长度（`[StringLength(n)]`：动作 256、错误码 64、消息键 256、trace-id 64、资源 1024、主体 256、租户 128），错误文案与两个 JSON 列是长文本（`[LongText]`），SqlSugar 与 EF Core 都据此建表（ADR-0036） |
| `Trail.DatabaseAuditTrail` | 单例接收器与查询：`WriteAsync` 进队列，`FlushAsync` 立即刷写并等待完成，`QueryAsync` 分页查询；`PendingCount` / `PersistedCount` / `FailedCount` / `DroppedCount` 计数 |
| `DatabaseAuditingOptions` | `XiHan:Auditing:Database`：`QueueCapacity`（10000）、`BatchSize`（100）、`FlushIntervalSeconds`（1）、`LogEvents`（true） |

中间件的选项（`Selection`、`IncludeSuccesses`）仍在 `XiHan:Auditing`（`AuditingOptions`）。

## 规则

- 有界队列与丢弃：`WriteAsync` 只是 `TryWrite` 进容量 `QueueCapacity` 的队列，永不阻塞、永不抛出；队列满了（或特性已停止）新到的事件被丢弃并计入 `DroppedCount`，警告每 10 秒最多记一条并附累计数。`LogEvents` 为真时每条事件（含被丢弃的）先以 Information 级写日志。
- 批量刷写：后台循环攒够 `BatchSize` 条或到 `FlushInterval` 就取出一批，一次 `AddRangeAsync` 写入；整批异常（存储不可达、提交失败）或逐项被仓储拒绝都只记日志、计入 `FailedCount` 并丢弃该批，不重试——审计不能反过来拖垮业务，存储不可用时重试只会堆积。刷写串行：后台循环与 `FlushAsync` 互斥。
- `Sequence` 的顺序语义：出队时分配，取上一个加一与时钟 UTC 刻度的较大者。同一节点严格递增、与提交顺序一致；不同节点之间按时钟大致有序（时钟差之内的交错不保证），所以跨节点的"写入顺序"是时钟序。UUID v7 的 `Id` 同毫秒内不单调，不当顺序用。
- 查询与续页：过滤项（`From` / `To` 含边界、`Action`、`PrincipalId`、`TenantId`、`TraceId`、`Succeeded`）都是 SQL 条件，按 `Sequence` 升序，多取一条判断是否有下一页；续页令牌是本页最后一条的 `Sequence`（十进制文本），不可解析 → `AUDITING_VALIDATION_001`；存储不可达 → `AUDITING_UNAVAILABLE_001`（`reason` 是异常消息）。可空字符串列只写相等比较，不写 `!= null`。
- 细节与错误：`Details` 与 `Error.Details` 手写成 JSON 对象（不经反射，AOT 兼容），值只允许 JSON 可表示的——`null`、字符串、布尔、整数与浮点、`DateTimeOffset` / `DateTime` / `Guid`（写成字符串）、`JsonElement` / `JsonNode`、嵌套字典与列表，其余按 `ToString()`；读回时每个值是 `JsonElement`。失败结果按 `ErrorCode` 与 `ErrorMessageKey` 还原（`new Error(code, key, details)`，读取方可再按调用方区域翻译）；没有键的字面错误用写入时按进程默认区域渲染的 `ErrorMessage` 原样还原（`Error.Literal`）。`Error.Inner` 不持久化。
- 读写不走管道：写入由 Observe 阶段的中间件或后台线程发起，查询不要求调用方有活动的工作单元；每个操作在根提供者的新作用域里直接开一个工作单元（不带租户），返回即提交。不加入调用方的事务：审计在调用结束后才知道结果，失败的调用同样要记。
- 停止：`StopAsync` 先停循环，再停止接收并把队列里剩余的事件刷完（用宿主给的停止令牌）；之后到达的事件按丢弃计数。
- 表结构由应用管理（`InitTables` 只用于开发与测试）：`DetailsJson` / `ErrorDetailsJson` / `ErrorMessage` 用长文本类型（`text` / `nvarchar(max)` / `longtext`）；建议索引 `AuditRecord(Sequence)`（唯一）、`AuditRecord(TraceId)`、`AuditRecord(Timestamp)`，按查询习惯再加 `(TenantId, Timestamp)` 或 `(PrincipalId, Timestamp)`。

## 依赖方向

依赖 `XiHan.Framework.Auditing.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`；不引用任何数据库驱动，具体数据库由装配的持久化 Provider 决定。

## 状态

P5：可用，通过 `Auditing.Conformance`；对 SQLite（`Persistence.SqlSugar`）验证实体映射与分页查询。
