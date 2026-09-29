# XiHan.Framework.Upgrade.SqlSugar

## 概述

`XiHan.Framework.Upgrade` 的 SqlSugar 持久化提供程序，为升级版本记录与迁移历史提供落库实现。

## 核心能力

- `IUpgradeVersionStore` 的 SqlSugar 实现：版本状态每租户一行、迁移历史追加写入
- `EnsureTablesAsync` 内部直接建表，不依赖全局的 `EnableTableInitialization` 开关
- `Set*`/`Update*` 系列方法更新数据库的同时，原地回写调用方传入的状态对象

## 依赖关系

依赖 `XiHan.Framework.Upgrade`（契约与模型）与 `XiHan.Framework.Data`（SqlSugar 数据访问、雪花 ID 生成器）。

## 配置与约定

表名 `sys_upgrade_version`、`sys_upgrade_migration_history`；主键 `Basic_Id` 为雪花 ID，非自增。用 `Tenant_Key` 字符串列（`host` 或 `tenant:{id}`）区分宿主与租户，不依赖对可空 `Tenant_Id` 的唯一性判断。不分表、不做多库路由。

`EnableDbInitialization` 与 `EnableTableInitialization` 默认均为 `false`，但本包的 `EnsureTablesAsync` 不受这两个开关影响——升级流程每次执行都会显式调用它来确保表存在。

## 使用方式

在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanUpgradeSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

## 扩展点

注册以 `services.Replace` 顶替 `XiHan.Framework.Upgrade` 注册的 `DefaultUpgradeVersionStore`。多实例并发执行升级由 `IUpgradeLockProvider`（另一个契约）负责互斥，本包只负责数据存取。

## 目录结构

```
Entities/                        版本状态与迁移历史实体
Extensions/DependencyInjection/  注册扩展
Mapping/                         实体与模型的双向映射、租户键构建
Services/                        SqlSugarUpgradeVersionStore
```
