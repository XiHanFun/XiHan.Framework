# XiHan.Framework.Web.Api.SqlSugar

> 接口幂等的 SqlSugar 存储：把幂等记录落到数据库，多实例共用同一个库时幂等键跨实例一致。

- **NuGet**：`XiHan.Framework.Web.Api.SqlSugar`
- **模块类**：`XiHanWebApiSqlSugarModule`
- **所在层**：Web 层
- **关键依赖**：`XiHan.Framework.Web.Api`、`XiHan.Framework.Data`

## 概述

`XiHan.Framework.Web.Api` 接口幂等的 SqlSugar 存储提供程序。默认的 `IIdempotencyStore` 是进程内实现：重启即失，也不跨实例共享。本包把幂等记录落到数据库，多实例共用同一个库时幂等键在实例间一致。

## 核心能力

- 记录实体 `sys_idempotency_record`，由 `SqlSugarIdempotencyStore` 实现 `IIdempotencyStore`
- 取得、释放、标记不确定、清理各在独立连接上执行并立即提交，不随外层事务回滚
- 完成写入（状态 + 响应快照）经解析器客户端登记到当前工作单元：事务型工作单元内与业务同一事务提交或回滚，业务回滚则不留完成记录
- 唯一索引 `ux_sys_idempotency_record_key_hash` 串行化同一键的并发取得，跨进程、跨实例有效
- 事务型端点的处理中记录租约到期后可被接管，旧拥有者随后的完成写入失败；非事务型端点不接管
- 过期的完成记录与结果不确定记录在同一键再次使用时惰性删除，批量清理由应用调用 `PurgeExpiredAsync`
- 表结构由 `[TableInitialization]` 经 CodeFirst 建立，可重复执行，不做 DROP；需开启 `XiHan:Data:SqlSugarCore` 下的建表初始化（`EnableDbInitialization` 与 `EnableTableInitialization`）

## 依赖关系

依赖 `XiHan.Framework.Web.Api`（幂等契约与选项）与 `XiHan.Framework.Data`（SqlSugar 数据访问）。

## 配置与约定

复用 `XiHan:Web:Api:Idempotency`（`XiHanIdempotencyOptions`），本包没有自己的配置节。本包用到其中三项：

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `CompletedRetention` | `24:00:00` | 完成记录与结果不确定记录的保留时长，写入完成或标记不确定时据此计算 `Expires_Time` |
| `ProcessingLease` | `00:05:00` | 事务型端点处理中记录的租约时长，写入 `Lease_Expires_Time` |
| `MaxKeyLength` | `128` | 不能超过 128（`Idempotency_Key` 列长度），超过时应用启动失败 |

其余字段（`MaxEntries`、`MaxTotalResponseBytes` 只约束进程内存储）不影响本包。

### 数据表

表名 `sys_idempotency_record`，主键 `Basic_Id`（`Guid`）。

| 列 | 说明 |
| --- | --- |
| `Key_Hash` | 记录键摘要，租户、主体、方法、端点、幂等键的 SHA-256；唯一索引 `ux_sys_idempotency_record_key_hash` |
| `Tenant_Id` / `Subject_Id` / `Http_Method` / `Endpoint` / `Idempotency_Key` | 组成记录键的原始值，仅供排查；`Endpoint` 超过 512 字符时截断写入，记录键以 `Key_Hash` 为准 |
| `Fingerprint` | 请求摘要，同一键、不同摘要返回冲突 |
| `Status` | 0 处理中，1 已完成，2 结果不确定 |
| `Owner_Token` | 拥有者令牌，完成、释放、标记不确定都按它匹配 |
| `Is_Transactional` | 动作是否在事务型工作单元内执行 |
| `Lease_Expires_Time` | 处理中租约到期时间 |
| `Expires_Time` | 完成记录或结果不确定记录的过期时间，处理中时为空 |
| `Response_Status` / `Response_Body` | 响应状态码与响应快照（JSON 字节） |
| `Created_Time` / `Completed_Time` | 创建与完成时间 |

所有时间按 UTC 存储；过期与租约到期的比较在 SQL 条件中进行。

### 连接与事务

- `TryAcquireAsync`、`ReleaseAsync`、`MarkIndeterminateAsync`、`PurgeExpiredAsync` 在新开的非事务工作单元内，用解析器客户端 `CopyNew()` 出的独立连接执行并立即提交。
- `CompleteAsync` 直接使用解析器返回的客户端；事务型工作单元内，完成记录与业务数据同一事务。业务回滚时完成写入一并回滚，记录保持处理中，由动作异常路径释放。
- `ReleaseAsync` 只删除处理中记录，不影响已完成记录。

### 租约与接管

- 事务型端点：处理中记录的租约（`ProcessingLease`）到期后，新请求可接管并获得新的拥有者令牌，旧拥有者之后的 `CompleteAsync` 因令牌不符而抛 `InvalidOperationException`。
- 非事务型端点：不接管，处理中记录保持处理中或标记为结果不确定，之后携带同一键的请求得到进行中或不确定的结果。

### 租户与库

记录实体未声明模块数据源，按当前租户所在库布局的主库解析；租户使用独立库时，记录写在该租户的库里。`CopyNew()` 复制出的客户端不携带运行时追加的 AOP 与查询过滤器，本表不依赖它们。

## 使用方式

在应用启动模块上声明依赖 `XiHanWebApiSqlSugarModule`，即用 SqlSugar 存储替换进程内存储（作用域生命周期）：

```csharp
[DependsOn(typeof(XiHanWebApiSqlSugarModule))]
public class MyAppModule : XiHanModule
{
}
```

并在配置中开启建表初始化：

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "EnableDbInitialization": true,
        "EnableTableInitialization": true
      }
    }
  }
}
```

### 清理过期记录

框架不运行后台清理任务。过期的完成记录与结果不确定记录只在同一键再次使用时惰性删除，批量清理由应用自行排程调用 `SqlSugarIdempotencyStore.PurgeExpiredAsync`，返回删除的记录数：

```csharp
public class IdempotencyPurgeService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<SqlSugarIdempotencyStore>();
            await store.PurgeExpiredAsync(stoppingToken);
        }
    }
}
```

## 扩展点

需要自定义存储行为时，实现 `XiHan.Framework.Web.Api.Idempotency.IIdempotencyStore` 并在 DI 中 `Replace`。

## 目录结构

```
Entities/                        幂等记录实体
Idempotency/                     SqlSugar 幂等存储
Extensions/DependencyInjection/  服务注册扩展
XiHanWebApiSqlSugarModule.cs     模块类
```
