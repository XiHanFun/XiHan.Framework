# XiHan.Framework.Traffic.SqlSugar

## 概述

`XiHan.Framework.Traffic` 的 SqlSugar 持久化提供程序，为灰度规则提供只读落库查询与内存缓存。

## 核心能力

- `IGrayRuleRepository` 的 SqlSugar 只读实现：内存缓存 + 到期自动刷新 + 显式 `RefreshAsync`
- 表结构由 `DbInitializer` 在应用启动时创建（需开启 `EnableTableInitialization`）
- 仓储本身不提供写方法，规则的增删改由应用层直接对 `sys_gray_rule` 表操作

## 依赖关系

依赖 `XiHan.Framework.Traffic`（契约与规则模型）与 `XiHan.Framework.Data`（SqlSugar 数据访问）。

## 配置与约定

配置节 `XiHan:Traffic:SqlSugar`，`RefreshInterval` 控制缓存刷新间隔，默认 30 秒，必须大于零，否则启动校验失败。

表名 `sys_gray_rule`；主键 `Basic_Id` 即规则的 `RuleId`（`string`，由应用层赋值，非自增）。不分表、不做多库路由。

`EnableDbInitialization` 与 `EnableTableInitialization` 默认均为 `false`，不开启则不会自动建表。

## 使用方式

在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanTrafficSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

## 扩展点

注册经 `XiHan.Framework.Traffic` 既有的 `ReplaceGrayRuleRepository<TRepository>()` 完成，顶替默认的内存实现。多实例部署下每个实例各自独立刷新缓存，规则变更最坏情况下需要等到一个 `RefreshInterval` 才能在所有实例生效。

## 目录结构

```
Entities/                        灰度规则实体
Extensions/DependencyInjection/  注册扩展
Mapping/                         实体与模型的双向映射
Options/                         缓存刷新间隔配置
Repositories/                    只读仓储实现
```
