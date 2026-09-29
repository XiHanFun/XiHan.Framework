# XiHan.Framework.Settings.SqlSugar

## 概述

`XiHan.Framework.Settings` 的 SqlSugar 持久化提供程序，把设置存储 `ISettingStore` 从空实现替换为落库实现。

## 核心能力

- `ISettingStore` 的 SqlSugar 实现，四个契约方法（读单个、批量读、写、删）全部落库
- 按 `(设置名, 提供者名, 提供者键)` 三元组定位一条设置值，全局/租户/用户等任意作用域通用
- 三列复合唯一索引保证同一组合永远只有一行，并发首次创建同一设置不会产生重复数据
- 表结构由 `DbInitializer` 在应用启动时创建（需开启建表初始化，见下）

## 依赖关系

依赖 `XiHan.Framework.Settings`（`ISettingStore` 契约）与 `XiHan.Framework.Data`（SqlSugar 数据访问）。

## 配置与约定

表名 `sys_setting`；列名 Pascal_Snake_Case；主键 `Basic_Id` 为雪花 ID，非自增；`Provider_Name`/`Provider_Key` 为非空列，未指定时存归一化占位符（空字符串）；不分表、单库。

建表需要开启 `XiHan.Framework.Data` 的建表初始化，**默认是关闭的**：

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

未开启建表初始化又没有手工建表时，首次读写即抛「表不存在」。

## 使用方式

在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanSettingsSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

## 扩展点

需要自定义落库行为时，实现 `ISettingStore` 并用 `services.Replace` 顶替本包的注册。

存储以 `services.Replace` 顶替 `XiHan.Framework.Settings` 注册的空实现（`NullSettingStore` 用 `[Dependency(TryRegister = true)]` 登记）。应用侧若要再次替换，同样使用 `Replace`——`TryAdd` 不会生效。

## 目录结构

```
Entities/                        设置值实体
Extensions/DependencyInjection/  注册扩展
Stores/                          ISettingStore 的 SqlSugar 实现
```
