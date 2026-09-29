# XiHan.Framework.Settings.SqlSugar

> 设置存储的 SqlSugar 持久化提供程序：按 (设置名, 提供者名, 提供者键) 三元组落库，替换 [Settings](./settings) 的空存储后设置读写才真正持久化。

- **NuGet**：`XiHan.Framework.Settings.SqlSugar`
- **模块类**：`XiHanSettingsSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Settings](./settings)（`ISettingStore` 契约）、[Data](./data)（SqlSugar 客户端、建表）

## 概述

[Settings](./settings) 提供了完整的设置定义、值提供者链与作用域读写骨架，但持久化层只有 `NullSettingStore`——读永远是 `null`，写是空操作，设置改了也不会被记住。

本包把设置值落到数据表，四个契约方法（`GetOrNullAsync` / `GetAllAsync` / `SetAsync` / `DeleteAsync`）全部实现为真实的库操作。

## 何时使用

- 用了 `ISettingManager` 做运行时可改的配置项，且希望修改能跨进程重启保留
- 需要按全局 / 用户（未来还有租户）等不同作用域各自持久化设置值

不需要本包的场景：只用编译期常量或 appsettings 静态配置，不需要运行时可写设置。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Settings.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanSettingsSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

建表需要开启 [Data](./data) 的库初始化与建表初始化，**默认都是关闭的**：

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

`sys_setting` 不是分表，未开启建表初始化又没有手工建表时，首次读写即抛「表不存在」。

## 表结构

单张表 `sys_setting`，不分表：

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `long`，主键，非自增 | 雪花 ID |
| `Row_Version` | `long` | 版本标识列（本包不激活乐观锁校验，见「注意事项」） |
| `Setting_Name` | `string(128)`，非空 | 设置名称 |
| `Provider_Name` | `string(32)`，非空 | 提供者名称，如 `"G"`（全局）、`"U"`（用户）；未指定时存归一化占位符（空字符串） |
| `Provider_Key` | `string(64)`，非空 | 提供者键，如用户 ID；全局设置时存归一化占位符（空字符串） |
| `Setting_Value` | 大文本，可空 | 设置值，写空即等价于从未写入 |

**`(Setting_Name, Provider_Name, Provider_Key)` 有复合唯一索引 `uk_sys_setting_key`**：同一组合在数据库层面永远只有一行，并发首次创建同一设置不会产生重复数据，见「注意事项」。

## 工作原理

### 读写

四个方法都先经 `ISqlSugarClientResolver.GetClientForEntity<SysSetting>()` 取客户端（`SysSetting` 未声明 `[DataSource]`，等价于取当前库），把入参 `providerName`/`providerKey` 归一化（`null` → 空字符串）后按 `(Setting_Name, Provider_Name, Provider_Key)` 精确匹配一行。

`SetAsync` 是「先查后写」：查到已有行就整行更新；查不到就插入新行。若插入因唯一索引冲突失败（两个调用者同时首次创建同一设置），会重新按业务键查询——查到就转为更新，最终只留一行；查不到（例如隔离级别看不见另一事务已提交的行）就把原始的唯一约束冲突异常重新抛给调用方；重新查询本身失败时抛出 `AggregateException`，同时包含原插入异常与重查异常。

**在事务型工作单元内，`GetClientForEntity` 会把本包的读写钉在当前事务上**（`ISqlSugarClientResolver.GetClientForEntity` 内部无条件登记进当前工作单元，不是本包可以关闭的行为）。这意味着：SQLite/MySQL 下，上一段的“重新查询转为更新”通常按预期工作；**PostgreSQL 下，一旦某条语句在事务内失败（含唯一约束冲突），整个事务立即进入 `aborted` 状态，同一事务里的后续命令（包括这次重新查询）也会失败**——竞态因此在 PostgreSQL 上会以 `AggregateException`（同时包含原插入异常与重查异常）的形式暴露，并连带拖垮调用方当次业务事务里的其他写入，不是“`SetAsync` 单独失败”这么轻。

## 配置

无需任何 `XiHan:` 配置节——四个契约方法不需要可配置项。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanSettingsSqlSugarModule` | 模块类，声明依赖即启用 |
| `SqlSugarSettingStore` | `ISettingStore` 的 SqlSugar 实现，注册为 `Scoped` |
| `SysSetting` | 设置值实体 |

## 注意事项与最佳实践

- **并发首次创建同一设置由唯一索引兜底，但不保证不抛异常**：数据库唯一约束保证最终只有一行；SQLite/MySQL 下竞态通常被静默吸收转为更新，**PostgreSQL 下且处于事务型工作单元内时，竞态会以异常形式暴露、并拖垮调用方当次事务的其他写入**——本包的读写会被自动登记进当前事务（`ISqlSugarClientResolver.GetClientForEntity` 的固有行为，不是本包可以关闭的开关）。并发更新同一个已存在的设置仍是普通的先查后改，后写覆盖前写，不做乐观锁
- **空字符串与 `null` 的 `providerKey` 不可区分**：两者归一化后是同一个值，会读写同一行。当前主包的调用点都不会传空字符串，这是已接受的边界
- **Oracle 把空字符串当作 `NULL`**：本包用空字符串做归一化哨兵值，这个设计在 Oracle 上不成立——`Provider_Key = ''` 会被 Oracle 存成 `NULL`，全局设置的唯一索引语义随之退化回“NULL 各不相同”的老问题。当前仓库未把 Oracle 列为支持方言，记录在案，接入 Oracle 前需要换一个非空字符串哨兵
- **不做乐观锁**：`Row_Version` 列存在但未激活校验（未调用 SqlSugar 的 `IsEnableUpdateVersionValidation()`）
- **`"T"`（租户）提供者当前只写不读**：这是 [Settings](./settings) 主包的既有行为，`SettingManager.ResolveProvider` 会把 `Tenant` 作用域写成 `("T", tenantId)`，但读取路径尚未接入对应的值提供者；本包的存储层对任意 `providerName` 一视同仁，缺口不在本包
- **加密与校验在上层完成**：`SettingManager` 负责加密（`XiHanAesOptions`）与自定义校验（`SettingDefinition.Validator`），本包收到的 `value` 是最终存储值，原样落库
- **升级到带唯一索引的版本时，历史脏数据会让建表在启动时失败**：`CodeFirst.InitTables(...)` 在检测到同名索引已存在时会跳过、不会重建；但对一张已经存在、且 `(Setting_Name, Provider_Name, Provider_Key)` 有重复行的旧表首次创建这个索引时，数据库会因为违反唯一约束而拒绝建索引，应用因此在启动阶段报错。升级前需要先清理重复数据

## 扩展点 / 自定义

需要完全自定义存储行为时，实现 `ISettingStore` 并在 DI 中 `Replace`。

## 依赖模块

- [Settings](./settings)：`ISettingStore` 契约与设置管理骨架
- [Data](./data)：SqlSugar 客户端解析、建表初始化

## 相关模块

- [Auditing.SqlSugar](./auditing-sqlsugar) / [EventBus.SqlSugar](./eventbus-sqlsugar)：同一套落库范式的其他实现
