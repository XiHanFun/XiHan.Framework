# XiHan.Framework.Upgrade.SqlSugar

> 升级版本记录的 SqlSugar 持久化提供程序：版本状态与迁移历史落库，替换 [Upgrade](./upgrade) 的内存存储后，升级进度才跨进程重启保留、多实例共享。

- **NuGet**：`XiHan.Framework.Upgrade.SqlSugar`
- **模块类**：`XiHanUpgradeSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Upgrade](./upgrade)（`IUpgradeVersionStore` 契约与模型）、[Data](./data)（SqlSugar 客户端、雪花 ID）

## 概述

[Upgrade](./upgrade) 的升级引擎在启动检查与执行迁移时，经 `IUpgradeVersionStore` 读写「当前应用版本、数据库版本、是否升级中」以及每个迁移脚本的执行历史。主包的默认实现 `DefaultUpgradeVersionStore` 把这些状态存在进程内：重启后会认为从未升级过。

本包把版本状态存进 `sys_upgrade_version`（每个租户一行），把迁移历史追加进 `sys_upgrade_migration_history`。

## 何时使用

- 使用升级引擎管理数据库迁移，且应用会重启或以多实例部署

## 安装与启用

```bash
dotnet add package XiHan.Framework.Upgrade.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanUpgradeSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

两张表由 `EnsureTablesAsync` 建出，升级流程每次执行都会调用它，**不依赖** [Data](./data) 的 `EnableTableInitialization` 开关。

## 表结构

两张表都不分表、单库，主键 `Basic_Id` 为雪花 ID，非自增。

**`sys_upgrade_version`**：版本状态，每个租户一行

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Tenant_Id` | `long`，可空 | 租户标识，宿主为空 |
| `Tenant_Key` | `string(64)`，非空，**唯一索引** `uq_sys_upgrade_version_tenant_key` | 租户键：宿主为 `host`，租户为 `tenant:{id}` |
| `App_Version` / `Db_Version` | `string(32)`，非空 | 应用版本、数据库版本 |
| `Min_Support_Version` | `string(32)`，可空 | 最小支持版本 |
| `Is_Upgrading` | 布尔，非空 | 是否升级中 |
| `Upgrade_Node` | `string(128)`，可空 | 正在执行升级的节点 |
| `Upgrade_Start_Time` | 时间，可空 | 升级开始时间 |

**`sys_upgrade_migration_history`**：迁移历史，只追加

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Tenant_Id` / `Tenant_Key` | 同上 | 租户 |
| `Version` | `string(32)`，非空 | 版本 |
| `Script_Name` | `string(256)`，非空 | 脚本名称 |
| `Executed_Time` | 时间，非空 | 执行时间 |
| `Success` | 布尔，非空 | 是否成功 |
| `Node_Name` | `string(128)`，可空 | 执行节点 |
| `Error_Message` | 大文本，可空 | 错误信息 |

迁移历史没有唯一索引：同一脚本失败后重试，会留下多条记录。

## 工作原理

- **租户键**：宿主与租户用非空字符串列 `Tenant_Key` 区分。可空的 `Tenant_Id` 在不同数据库上对「等于空值」的比较与唯一性判断行为不一致，所有按租户的查询都走 `Tenant_Key`
- **`GetOrCreateAsync`**：按当前租户的 `Tenant_Key` 查找版本行，查不到就插入。两个调用同时判定「不存在」时，唯一索引让其中一个插入失败；失败后按租户键重查，查到即返回查到的行，查不到则抛出原始异常；重查本身失败时抛出 `AggregateException`，同时包含原插入异常与重查异常
- **原地回写**：`SetUpgradingAsync`、`SetUpgradeCompletedAsync`、`SetUpgradeFailedAsync`、`UpdateDbVersionAsync` 在更新数据库后，把同样的值写回调用方传入的 `UpgradeVersionState` 对象。传入的对象必须来自 `GetOrCreateAsync`，`Id` 不大于 0 时抛 `ArgumentException`

## 配置

无需任何 `XiHan:` 配置节。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanUpgradeSqlSugarModule` | 模块类，声明依赖即启用 |
| `SqlSugarUpgradeVersionStore` | `IUpgradeVersionStore` 的 SqlSugar 实现，注册为 `Scoped` |
| `SysUpgradeVersion` / `SysUpgradeMigrationHistory` | 版本状态、迁移历史实体 |

## 注意事项与最佳实践

- **多实例互斥不在本包**：本包只做数据存取，多个实例同时执行升级由 `IUpgradeLockProvider` 负责互斥
- **PostgreSQL 事务内的插入竞态**：若 `GetOrCreateAsync` 或 `TryCreateBaselineAsync` 在事务型工作单元内执行，PostgreSQL 上唯一冲突会使整个事务中止，随后的重查也会失败，此时抛出 `AggregateException`，同时包含原插入异常与重查异常。在事务外调用即可避免
- **版本与脚本名的比较口径**：写入与 `HasMigrationHistoryAsync` 查询都会先对版本做首尾空白裁剪；版本、脚本名的大小写是否区分由数据库排序规则决定（MySQL、SQL Server 默认不区分，PostgreSQL、SQLite 区分），本包不统一大小写
- **已存在但索引缺失的旧表**：`EnsureTablesAsync` 对同名索引已存在的表不重建索引；旧表里若已有同一 `Tenant_Key` 的重复行，首次建唯一索引会失败，需先清理

## 扩展点 / 自定义

注册以 `services.Replace` 顶替主包注册的 `DefaultUpgradeVersionStore`。应用侧若要再次替换，同样使用 `Replace`。

## 依赖模块

- [Upgrade](./upgrade)：升级引擎、`IUpgradeVersionStore` 契约与模型
- [Data](./data)：SqlSugar 客户端解析、雪花 ID 生成器

## 相关模块

- [Settings.SqlSugar](./settings-sqlsugar)：同一套「唯一索引 + 冲突后重查」的落库范式
