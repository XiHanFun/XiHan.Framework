# XiHan.Framework.Data

## 概述
XiHan.Framework.Data 提供数据访问基础设施与 SqlSugar 集成入口，统一仓储、数据初始化与数据库上下文管理。

## 核心能力
- SqlSugar 客户端统一注册与配置
- 仓储基类与数据访问抽象实现
- 数据库初始化、表结构初始化与种子数据支持

## 依赖关系
- 通过 `XiHanDataModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 配置节：`XiHan:Data:SqlSugarCore`
- 数据访问策略通过 `XiHanSqlSugarCoreOptions` 进行配置

## 使用方式
```csharp
[DependsOn(typeof(XiHanDataModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义仓储实现与数据初始化流程
- SqlSugar AOP 与全局过滤器扩展

## 目录结构
```text
XiHan.Framework.Data/
  README.md
  XiHanDataModule.cs
```
