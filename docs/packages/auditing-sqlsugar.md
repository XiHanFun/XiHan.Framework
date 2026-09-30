# XiHan.Framework.Auditing.SqlSugar

> 审计日志的 SqlSugar 持久化提供程序：6 类日志实体（按月分表）与 6 个写入器，替换 [Auditing](./auditing) 的空写入器后日志才真正落库。

- **NuGet**：`XiHan.Framework.Auditing.SqlSugar`
- **模块类**：`XiHanAuditingSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Auditing](./auditing)（记录模型与写入器契约）、[Data](./data)（SqlSugar 客户端、雪花主键、建表）

## 概述

[Auditing](./auditing) 负责「采集什么、怎么异步化、怎么脱敏」，但它的 6 个写入器契约（5 个 `IXxxLogWriter` 加 `IEntityDiffLogWriter`）默认实现全是空实现——日志采集到就被丢弃。本包补上最后一步：把这 6 类日志写进数据表。

启用本包后，[Web.Api](./web-api) 各中间件与过滤器采集到的记录、以及 SqlSugar 原生 `Aop.OnDiffLogEvent` 产出的实体差异记录，会自动落到 6 张按月分表的数据表，应用侧不需要再写任何写入代码——前提是该开的开关都开了：实体差异日志还要求 `XiHan:Data:SqlSugarCore:EnableDiffLog = true`，只开建表开关时 `sys_diff_log` 仍是一张空表（见「安装与启用」）。

## 何时使用

- 需要把访问 / 操作 / 异常 / 接口 / 登录日志存进关系型数据库
- 需要实体差异审计：每笔走框架仓储的写留下 before/after 快照与变更字段清单，软删除与恢复也计入
- 日志量级大到需要按月切表，同时希望建表由框架在启动时完成
- 已在用 [Data](./data)，希望审计日志与业务数据走同一套多数据源、读写分离与租户路由

## 安装与启用

```bash
dotnet add package XiHan.Framework.Auditing.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanAuditingSqlSugarModule))]
public class MyModule : XiHanModule { }
```

`XiHanAuditingSqlSugarModule.ConfigureServices` 调用 `services.AddXiHanAuditingSqlSugar()`，以 `services.Replace` 逐个顶替 [Auditing](./auditing) 用 `TryAddScoped` 注册的 6 个空写入器：

| 契约 | 默认实现 | 本包实现 |
| --- | --- | --- |
| `IAccessLogWriter` | `NullAccessLogWriter` | `SqlSugarAccessLogWriter` |
| `IApiLogWriter` | `NullApiLogWriter` | `SqlSugarApiLogWriter` |
| `IExceptionLogWriter` | `NullExceptionLogWriter` | `SqlSugarExceptionLogWriter` |
| `ILoginLogWriter` | `NullLoginLogWriter` | `SqlSugarLoginLogWriter` |
| `IOperationLogWriter` | `NullOperationLogWriter` | `SqlSugarOperationLogWriter` |
| `IEntityDiffLogWriter` | `NullEntityDiffLogWriter` | `SqlSugarEntityDiffLogWriter` |

本包**没有自己的配置节**，三条既有配置决定它的行为：

- **建表**：`XiHan:Data:SqlSugarCore:EnableTableInitialization`（默认 `false`）。打开后 [Data](./data) 的 `DbInitializer` 扫描全部 `[SugarTable]` 实体，对这 6 个实体走 `CodeFirst.SplitTables().InitTables()` 建出当月分表
- **队列**：`XiHan:Auditing:LogQueue` 的 5 个 `EnableXxxLogQueue`（默认全 `false`，即同步写入器调用），语义见 [Auditing](./auditing)
- **实体差异日志**：`XiHan:Data:SqlSugarCore:EnableDiffLog`（默认 `false`）。[Data](./data) 只在打开它时才把 `SqlSugarDiffLogAop` 挂到 SqlSugar 客户端上；未打开时本包的 `SqlSugarEntityDiffLogWriter` 照常注册，但收不到任何记录。开启后还需业务写走框架仓储——仓储的 Insert / Update / Delete 内建 `.EnableDiffLogEvent`，裸 SQL 与直接用 `ISqlSugarClient` 绕过仓储的写入不产生差异记录

## 表结构

```
sys_access_log   sys_api_log   sys_diff_log
sys_exception_log   sys_login_log   sys_operation_log
```

| 约定 | 值 |
| --- | --- |
| 表名 | `sys_` 前缀、全小写下划线 |
| 分表 | `[SplitTable(SplitType.Month)]` 按月；`[SugarTable]` 模板含 `{year}{month}{day}` 三个变量，实际表名形如 `sys_operation_log_20260901` |
| 分表字段 | `Created_Time`（`DateTimeOffset`，实体上 `override` 基类属性并标注 `[SplitField]`） |
| 列名 | Pascal_Snake_Case，每列带简体中文 `ColumnDescription` |
| 主键 | `Basic_Id`，`long`，`IsIdentity = false`，雪花 ID |
| 实体基类 | `SugarCreationEntity<long>`，实现 `ISplitTableEntity` |
| 大文本列 | `ColumnDataType = StaticConfig.CodeFirst_BigString`，由 SqlSugar 按当前数据库方言挑选类型 |
| 定长列 | 标注 `Length`（`Trace_Id` 64、`Path` 512、`User_Agent` 512、`Query_String` 2048 等），超长值由 `AuditingLogMapper` 截断到列宽 |

6 张表都带可空的 `Tenant_Id`（`long?`，无索引）：记录产生时所属的租户；平台就是 0 号租户，平台记录落库为 `0`（Data 的插入 AOP 按当前作用域租户补写，未设置时为 0），不是 `NULL`，查询平台日志按 `Tenant_Id = 0` 过滤。实体不实现 `IMultiTenantEntity`，不受多租户读取过滤影响，查询某租户的日志时自行按 `Tenant_Id` 过滤。

`SysLoginLog` 的 `Login_Time` 是记录模型自带的业务时间，与分表字段 `Created_Time` 各占一列：前者由应用写入，后者由写入器在落库时生成。

6 张表的列与 [Auditing](./auditing) 的记录模型逐字段对应，外加基类的 `Basic_Id`、`Row_Version`、`Created_Time`、`Created_Id`、`Created_By`。

## 工作原理

```text
采集端（Web.Api 中间件 / 过滤器）—— 已脱敏
   └→ IXxxLogPipeline.WriteAsync(record)
         ├ 队列关闭（默认）→ 直接 await IXxxLogWriter.WriteAsync(record)
         └ 队列打开         → XxxLogQueueWorker 攒批 → 逐条 WriteAsync
                                                │
                                                ▼
                          AuditingLogMapper.ToEntity(record, id, createdTime)
                                                │  纯函数：主键与时间由调用方传入
                                                ▼
                          ISqlSugarClientResolver.GetClientForEntity<TEntity>()
                                                │
                                                ▼
                          Insertable(entity).SplitTable().ExecuteCommandAsync()

实体差异日志走另一条分支（由 Data 包的 AOP 回调直接调用写入器，在业务写操作的同一个调用栈内同步完成）：
   SqlSugarDiffLogAop → AuditingLogMapper.ToEntity → GetCurrentClient()
   → Insertable(entity).SplitTable().ExecuteCommandAsync()  —— 与业务写同一事务
```

写入器做三件事：向 `IDistributedIdGenerator<long>` 取主键、取 `DateTimeOffset.UtcNow` 作为创建时间、把记录交给映射器后插入对应分表。访问 / 接口 / 异常 / 登录 / 操作这 5 类写入器按记录的 `TenantId` 落戳，并在 `ICurrentTenant.Change(record.TenantId)` 作用域内取客户端并插入，即按记录所属租户路由，不依赖写入时的环境租户——队列模式下写入发生在后台线程，环境里已没有请求的租户；`TenantId` 为 `null` 时切入平台。字段搬运全在 `AuditingLogMapper`，它是静态纯方法，可脱离数据库单测。

客户端经 `ISqlSugarClientResolver` 取得，因此这 5 张可按 `[ModuleDataSource]` 路由的日志表遵循 [Data](./data) 的多数据源与租户路由规则：默认落当前租户的主库。

**写入器自身不开事务，但「是否与业务事务同行」取决于取客户端的方法。** 访问 / 接口 / 异常 / 登录 / 操作这 5 类写入器经 `GetClientForEntity<TEntity>()` 取客户端；实体差异日志写入器经 `GetCurrentClient()` 取客户端——两者都会在存在事务型工作单元时自动登记进该事务（[Data](./data) 的 `EnlistCurrentUnitOfWork` 规则），区别在于 `GetClientForEntity` 支持 `[ModuleDataSource]` 按实体路由、`GetCurrentClient` 固定解析当前布局主库。

- **访问 / 接口 / 异常 / 操作** 这 4 类由框架采集，写日志的时机在 `await next()` 之后，此刻业务工作单元已经结束，因此实际不受它影响。开启队列（`XiHan:Auditing:LogQueue`）时更是由后台 Worker 在请求之外消费。
- **登录日志**由应用在自己的登录分支里调 `ILoginLogPipeline`，若这个调用发生在有活动事务型工作单元的作用域内，登录日志会与那笔事务同行。
- **实体差异日志**必须与触发它的业务写入同一个事务——`SqlSugarDiffLogAop` 在业务写操作的同一个调用栈内同步调用写入器，业务回滚时差异日志随之回滚。详见[审计日志指南](../guide/auditing)「写入器的事务契约」一节。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `SysAccessLog` / `SysApiLog` / `SysExceptionLog` / `SysLoginLog` / `SysOperationLog` | 5 张分表的实体，各带无参与 `(long basicId)` 两个公开构造函数 |
| `SysDiffLog` | 实体差异日志实体，字段与 `EntityDiffLogRecord` 逐一对应 |
| `AuditingLogMapper` | 静态映射器，6 个按记录类型重载的 `ToEntity(record, basicId, createdTime)` |
| `SqlSugarAccessLogWriter` 等 5 个 | 写入器实现（`Scoped`），构造参数为 `ISqlSugarClientResolver` 与 `IDistributedIdGenerator<long>` |
| `SqlSugarEntityDiffLogWriter` | 实体差异日志写入器（`Scoped`），经 `GetCurrentClient()` 而非 `GetClientForEntity` 取客户端 |
| `XiHanAuditingSqlSugarServiceCollectionExtensions` | `AddXiHanAuditingSqlSugar()`：以 `Replace` 注册 6 个写入器 |
| `XiHanAuditingSqlSugarModule` | 模块类，`[DependsOn(XiHanAuditingModule, XiHanDataModule)]`，只做装配 |

## 使用示例

### 1. 启用并建表

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "EnableTableInitialization": true
      }
    }
  }
}
```

```csharp
[DependsOn(typeof(XiHanAuditingSqlSugarModule))]
public class AppFoundationModule : XiHanModule { }
```

启动时 `DbInitializer` 建出 6 张当月分表；此后每月首次写入时由 SqlSugar 补建新分表。

### 2. 跨月查询日志

分表后必须显式给出时间区间，SqlSugar 才会合并多张分表：

```csharp
var client = clientResolver.GetClientForEntity<SysOperationLog>();

var logs = await client.Queryable<SysOperationLog>()
    .Where(item => item.TraceId == traceId)
    .SplitTable(begin, end)
    .ToListAsync();
```

### 3. 换掉某个写入器

应用侧要加自己的处理（如同时推送到消息队列），注册顺序在本模块之后，并同样使用 `Replace`：

```csharp
services.Replace(ServiceDescriptor.Scoped<IOperationLogWriter, MyOperationLogWriter>());
```

## 扩展点 / 自定义

- **换落库方式**：`Replace` 掉任一 `IXxxLogWriter`，例如改写成批量 `INSERT` 或同时转发到外部收集器
- **换映射规则**：`AuditingLogMapper` 的 6 个方法按记录类型重载，写入器自行映射即可绕开
- **指定库位**：这 6 个实体在本包内，无法给它们标注 `[ModuleDataSource]`。要让审计表落到指定模块库，替换 `IEntityModuleDataSourceResolver`（`Replace` 掉默认实现），对 `SysAccessLog` 等实体类型返回模块数据源名，再按 [Data](./data) 的模块分库路由；写入器按 `GetClientForEntity<SysAccessLog>()` 等基础实体类型取客户端，因此继承出的子类不会改变写入器的路由。实体差异日志表例外，见下方「注意事项」

## 注意事项与最佳实践

- **`TryAdd` 不生效**。[Auditing](./auditing) 已用 `TryAddScoped` 占了 6 个空实现，覆盖必须用 `Replace`。用错的表现是：注册"成功"、无报错、日志依然不落库。
- **建表时机**。打开 `EnableTableInitialization` 时启动即建表；未打开时，SqlSugar 的分表插入会在目标分表缺失时自行建表——日志不会丢，但表结构问题（权限、字符集）会推迟到首条日志才暴露。生产环境建议开启动期初始化。
- **跨月查询要显式 `SplitTable(begin, end)`**，否则只命中单个分表。
- **逐条插入**。`IXxxLogWriter.WriteAsync` 是单条契约，Worker 攒批后仍逐条调用，高吞吐下是 N 次数据库往返；需要批量写入请替换写入器。
- **`CreatedTime` 按 UTC 写入**，分表切换点随之按 UTC 划分，跨时区部署时切表时刻不等于本地零点。
- **脱敏在采集端完成**。记录到达写入器时已脱敏，写入器不再处理，重复脱敏会二次遮蔽已遮蔽的内容。
- **实体差异日志固定落主库**。`SqlSugarEntityDiffLogWriter` 经 `GetCurrentClient()` 取客户端，不支持 `[ModuleDataSource]` 路由；业务实体声明了模块数据源时，该实体的差异日志仍落在当前布局主库。
- **差异日志与发件箱同用时的登记冲突**。业务实体位于模块库又开启 `EnableDiffLog` 时，差异日志写入器经 `GetCurrentClient()` 把主库也登记进工作单元，登记变成两个库；此时在同一工作单元内以 `onUnitOfWorkComplete: false` 发布分布式事件，会因发件箱入箱遇到多个登记连接而抛异常（见 [EventBus](./eventbus)）。避开方式：事件改在工作单元完成时发布（`onUnitOfWorkComplete: true`），或该类实体不开差异日志。
- **差异日志写失败会把业务一起回滚**。`SqlSugarDiffLogAop` 的整体 try/catch 只保证异常不外抛、错误进日志，保护不了已被数据库中止的事务：差异日志的 `INSERT` 与业务写同一事务，PostgreSQL 下事务内的任何报错都会让事务进入 aborted 状态（SQL Server 开 `XACT_ABORT ON` 时同理），随后的提交失败，那笔业务写入随之回滚。其余 5 类日志一般在业务事务之外落库，写入失败只是丢日志（登录日志若在活动事务作用域内调用，同样随那笔事务回滚）。
- **定长列由映射层截断**。路径、查询串、User-Agent、来源页、异常类型等列宽有限，而采集端不夹长度；超长的自由文本在 SQL Server / MySQL 严格模式下会让整条 `INSERT` 抛错（队列模式下 `FlushAsync` 的 try 在逐条循环外层，一条失败连带丢弃整批），MySQL 非严格模式则会静默截断。`AuditingLogMapper` 统一把这些列截到列宽，保留前缀；请求体、响应体、异常堆栈等大文本列不设上限。
- **MySQL 上每月第一笔差异日志可能提交业务事务**。实体差异日志与业务写同一事务，当月分表尚不存在时，`SplitTable()` 插入会自动建分表；MySQL 的 DDL 会隐式提交当前事务，业务事务因此被提前提交。建议预先建好当月与下月分表。
- **日志落库失败不重试**。Worker 捕获写入异常仅记 `LogWarning`，这一批日志会丢失，需要可靠性请在写入器内自行兜底。

## 依赖模块

- [XiHan.Framework.Auditing](./auditing)（记录模型、写入器契约）
- [XiHan.Framework.Data](./data)（`ISqlSugarClientResolver`、`DbInitializer`、SqlSugar 传递依赖）

`IDistributedIdGenerator<long>` 经 `XiHanDataModule → XiHanDistributedIdsModule` 间接获得，本包不额外声明依赖。

## 相关模块

- [XiHan.Framework.Web.Api](./web-api)（采集 5 类日志的中间件与过滤器）
- [XiHan.Framework.DistributedIds](./distributed-ids)（雪花主键）
- [XiHan.Framework.Logging](./logging)（运行日志，与审计日志是两回事）
