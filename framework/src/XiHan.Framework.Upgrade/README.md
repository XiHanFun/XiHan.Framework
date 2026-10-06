# XiHan.Framework.Upgrade

## 概述
XiHan.Framework.Upgrade 提供分布式安全升级的底层引擎与流程编排能力，统一升级状态、版本语义比较与脚本发现规则。该项目只提供基础能力与扩展点，不包含具体数据库实现与业务实体。

## 核心能力
- 升级流程编排与状态管理，覆盖检测、锁定、迁移与结果回写
- 语义化版本比较与强制升级判定
- 迁移脚本发现与排序规则的统一抽象
- 维护模式、程序文件替换与滚动重启的可插拔扩展
- 多租户升级流程的统一调度

## 依赖关系
- 通过 `XiHanUpgradeModule` 参与模块化生命周期
- 具体依赖由业务侧模块提供实现并组合

## 配置与约定
- 配置节：`XiHan:Upgrade`
- 关键配置项：`MinSupportVersion`、`MigrationsRootPath`、`LockResourceKey`、`LockExpirySeconds`、`EnableAutoCheckOnStartup`
- 运行时实现要求：业务侧必须提供 `IUpgradeMigrationExecutor`（默认实现直接抛异常）；`IUpgradeVersionStore`、`IUpgradeLockProvider` 的默认实现 `DefaultUpgradeVersionStore` / `DefaultUpgradeLockProvider` 为有界进程内实现，只适用于开发与单实例，生产与多节点需替换为数据库版本存储与分布式锁
- 启动行为：`EnableAutoCheckOnStartup`（默认 `true`）开启时，应用初始化后建出版本记录并同步执行待执行脚本，失败即中断启动；与数据模块的建表、播种配合时，经 `IDbSchemaUpgrader` 接入，新建的库用 `IUpgradeEngine.BaselineAsync` 登记为最新版本

## 使用方式
```csharp
[DependsOn(typeof(XiHanUpgradeModule))]
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册业务侧升级实现
        // services.AddScoped<IUpgradeVersionStore, MyVersionStore>();
        // services.AddScoped<IUpgradeLockProvider, MyLockProvider>();
        // services.AddScoped<IUpgradeMigrationExecutor, MyMigrationExecutor>();
    }
}
```

## 扩展点
- `IUpgradeVersionStore`：版本与升级状态存储
- `IUpgradeLockProvider`：分布式锁实现
- `IUpgradeMigrationExecutor`：迁移执行器
- `IUpgradeTenantProvider`：多租户分发
- `IUpgradeMaintenanceModeManager` / `IUpgradeFileUpdater` / `IRollingRestartCoordinator`：运维流程扩展
- `IUpgradeScriptProvider`：脚本发现来源

## 目录结构
```text
XiHan.Framework.Upgrade/
  README.md
  XiHanUpgradeModule.cs
  Abstractions/
  Enums/
  Extensions/
  Models/
  Options/
  Services/
  Utils/
```
