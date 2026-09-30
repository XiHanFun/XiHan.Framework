# XiHan.Framework.Auditing.SqlSugar

## 概述
`XiHan.Framework.Auditing` 的 SqlSugar 持久化提供程序，提供审计日志的实体定义与落库实现。

## 核心能力
- 6 类审计日志（访问 / 接口 / 异常 / 登录 / 操作 / 实体差异）的 SqlSugar 实体与写入器
- 按月自动分表，表名形如 `sys_operation_log_20260901`
- 表结构由 `DbInitializer` 在应用启动时创建，需开启 `XiHan:Data:SqlSugarCore:EnableTableInitialization`（默认 `false`）
- 主键为雪花 ID，由 `XiHan.Framework.DistributedIds` 生成
- 实体差异日志写入器与触发它的业务写入同一个工作单元事务，业务回滚时差异日志随之回滚；其余 5 类日志也会自动登记进当前事务型工作单元，差别只在写入时机——访问 / 接口 / 异常 / 操作这 4 类由框架在业务工作单元结束之后才写入，实际不受它影响；登录日志由应用自行调用，落在活动事务作用域内时与那笔事务同行

## 依赖关系
依赖 `XiHan.Framework.Auditing`（日志记录模型与写入器契约）与 `XiHan.Framework.Data`（SqlSugar 数据访问）。

## 配置与约定
表名 `sys_` 前缀、全小写下划线；列名 Pascal_Snake_Case；主键 `Basic_Id` 为雪花 ID，非自增。分表字段为 `Created_Time`。6 张表都带可空的 `Tenant_Id`（无索引），记录产生时所属的租户；平台就是 0 号租户，平台记录落库为 `0`（由 Data 的插入 AOP 按当前作用域租户补写），不是 `NULL`。访问 / 接口 / 异常 / 登录 / 操作 5 类写入器按记录的租户落戳，并在 `ICurrentTenant.Change(record.TenantId)` 作用域内取客户端并插入，不依赖写入时的环境租户。

建表沿用 `XiHan.Framework.Data` 的开关 `XiHan:Data:SqlSugarCore:EnableTableInitialization`，默认 `false`；未开启时由 SqlSugar 在分表插入缺少目标表时补建。请求体、响应体、异常堆栈等大文本列为 `CodeFirst_BigString`，不设上限；其余定长列由映射层按列宽截断，避免超长的路径、查询串、User-Agent 让整批日志写入失败。

实体差异日志另需 `XiHan:Data:SqlSugarCore:EnableDiffLog`（默认 `false`）打开 `SqlSugarDiffLogAop`，且业务写走框架仓储；只开建表开关时本包的写入器虽已注册却收不到记录，`sys_diff_log` 的各张分表都不会有一行。

## 使用方式
在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanAuditingSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

## 扩展点
需要自定义落库行为时，实现 `XiHan.Framework.Auditing`（含 `.Writers`）下的对应接口并在 DI 中替换。

写入器以 `services.Replace` 顶替 `XiHan.Framework.Auditing` 注册的空实现。应用侧若要再次替换，同样使用 `Replace`——`TryAdd` 不会生效。

## 目录结构
```text
XiHan.Framework.Auditing.SqlSugar/
  Entities/
    SysAccessLog.cs
    SysApiLog.cs
    SysDiffLog.cs
    SysExceptionLog.cs
    SysLoginLog.cs
    SysOperationLog.cs
  Extensions/
    DependencyInjection/
      XiHanAuditingSqlSugarServiceCollectionExtensions.cs
  Mapping/
    AuditingLogMapper.cs
  Writers/
    SqlSugarAccessLogWriter.cs
    SqlSugarApiLogWriter.cs
    SqlSugarEntityDiffLogWriter.cs
    SqlSugarExceptionLogWriter.cs
    SqlSugarLoginLogWriter.cs
    SqlSugarOperationLogWriter.cs
  README.md
  XiHanAuditingSqlSugarModule.cs
```
