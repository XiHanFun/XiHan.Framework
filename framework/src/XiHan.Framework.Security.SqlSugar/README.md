# XiHan.Framework.Security.SqlSugar

## 概述

`XiHan.Framework.Security` 的 SqlSugar 持久化提供程序，为密码历史记录提供落库实现。

## 核心能力

- `IPasswordHistoryStore` 的 SqlSugar 实现：按用户查询最近的密码哈希
- 补充方法 `RecordPasswordAsync`：写入新密码哈希并按上限裁剪旧记录
- 表结构由 `DbInitializer` 在应用启动时创建（需开启 `EnableTableInitialization`）

## 依赖关系

依赖 `XiHan.Framework.Security`（契约）与 `XiHan.Framework.Data`（SqlSugar 数据访问、雪花 ID 生成器）。

## 配置与约定

表名 `sys_password_history`；主键 `Basic_Id` 为雪花 ID，非自增。不分表、不做多库路由。索引 `idx_sys_password_history_user`（`User_Id` 升序、`Created_Time` 降序）。

`EnableDbInitialization` 与 `EnableTableInitialization` 默认均为 `false`，不开启则不会自动建表，首次写入会报表不存在。

## 使用方式

在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanSecuritySqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

密码修改成功后，显式调用 `RecordPasswordAsync` 记录新哈希——该方法不在 `IPasswordHistoryStore` 契约上，需注入 `SqlSugarPasswordHistoryStore` 具体类型或自行在应用层扩展契约。

## 扩展点

注册以 `services.Replace` 顶替 `XiHan.Framework.Security` 注册的 `DefaultPasswordHistoryStore`。应用侧若要再次替换，同样使用 `Replace`——`TryAdd` 不会生效。

## 目录结构

```
Entities/                        密码历史实体
Extensions/DependencyInjection/  注册扩展
Services/                        SqlSugarPasswordHistoryStore
```
