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
- 运行时实现要求：业务侧必须提供 `IUpgradeVersionStore`、`IUpgradeLockProvider`、`IUpgradeMigrationExecutor` 的实现

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
