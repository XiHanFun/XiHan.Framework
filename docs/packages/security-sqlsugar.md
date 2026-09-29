# XiHan.Framework.Security.SqlSugar

> 密码历史记录的 SqlSugar 持久化提供程序：替换 [Security](./security) 的内存存储后，「新密码不能与最近 N 次相同」的校验才跨进程重启、跨实例生效。

- **NuGet**：`XiHan.Framework.Security.SqlSugar`
- **模块类**：`XiHanSecuritySqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Security](./security)（`IPasswordHistoryStore` 契约）、[Data](./data)（SqlSugar 客户端、建表）

## 概述

[Security](./security) 的密码策略服务在校验新密码时，经 `IPasswordHistoryStore.GetRecentPasswordHashesAsync` 取回该用户最近的密码哈希逐一比对。主包的默认实现 `DefaultPasswordHistoryStore` 把历史存在进程内的静态字典里：进程重启即丢、多实例之间不共享。

本包把密码历史落到数据表 `sys_password_history`，并补上一个写方法 `RecordPasswordAsync`，用于在密码修改成功后记录新哈希。

## 何时使用

- 启用了密码策略中的「禁止重复使用最近 N 次密码」
- 应用会重启，或以多实例部署

## 安装与启用

```bash
dotnet add package XiHan.Framework.Security.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanSecuritySqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

建表需要开启 [Data](./data) 的建表初始化，**默认是关闭的**：

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

未开启建表初始化又没有手工建表时，首次读写即抛「表不存在」。

## 表结构

单张表 `sys_password_history`，不分表、单库：

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `long`，主键，非自增 | 雪花 ID |
| `Row_Version` | `long` | 版本标识列 |
| `User_Id` | `long`，非空 | 用户标识 |
| `Password_Hash` | `string(512)`，非空 | 密码哈希 |
| `Created_Time` | 时间，非空 | 记录时间 |
| `Created_Id` / `Created_By` | 可空 | 创建者信息 |

索引 `idx_sys_password_history_user`：`User_Id` 升序、`Created_Time` 降序，覆盖按用户取最近记录的读取与裁剪查询。

## 工作原理

- **读**：`GetRecentPasswordHashesAsync(userId, count)` 按 `Created_Time` 降序（相同时再按 `Basic_Id` 降序）取该用户最近 `count` 条，返回时按由旧到新排列，与主包内存实现的顺序一致；`count` 不大于 0 时返回空集合
- **写**：`RecordPasswordAsync(userId, passwordHash, maxHistoryCount = 10)` 插入一行后，按记录时间降序取回该用户的全部记录标识，删除第 `maxHistoryCount` 条之后的旧记录

两个方法都经 `ISqlSugarClientResolver.GetClientForEntity` 取客户端：调用方处于事务型工作单元时，写入与裁剪随该工作单元一起提交或回滚。

## 配置

无需任何 `XiHan:` 配置节。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanSecuritySqlSugarModule` | 模块类，声明依赖即启用 |
| `SqlSugarPasswordHistoryStore` | `IPasswordHistoryStore` 的 SqlSugar 实现，注册为 `Scoped`；另有 `RecordPasswordAsync` |
| `SysPasswordHistory` | 密码历史实体 |

## 注意事项与最佳实践

- **`RecordPasswordAsync` 目前没有调用方**：它不在 `IPasswordHistoryStore` 契约上，框架里也没有任何地方在密码修改后记录历史。应用需要在修改密码成功后自行注入 `SqlSugarPasswordHistoryStore` 并调用它；不调用时表永远是空的，历史校验永远通过
- **写入与裁剪不是原子的**：同一用户并发修改密码时，历史可能暂时多保留一条，下次写入时裁掉
- **按记录时间排序**：同一毫秒内写入的两条记录，先后顺序取决于数据库的时间精度

## 扩展点 / 自定义

注册以 `services.Replace` 顶替主包以 `TryAddScoped` 注册的 `DefaultPasswordHistoryStore`。应用侧若要再次替换，同样使用 `Replace`——`TryAdd` 不会生效。

## 依赖模块

- [Security](./security)：`IPasswordHistoryStore` 契约与密码策略服务
- [Data](./data)：SqlSugar 客户端解析、建表初始化

## 相关模块

- [Settings.SqlSugar](./settings-sqlsugar)：同一套简单存储落库范式
