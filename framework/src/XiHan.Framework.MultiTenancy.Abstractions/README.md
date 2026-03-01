# XiHan.Framework.MultiTenancy.Abstractions

## 概述
XiHan.Framework.MultiTenancy.Abstractions 提供多租户能力的抽象契约，包括租户上下文、解析与隔离的接口定义。

## 核心能力
- 当前租户上下文与切换接口
- 租户解析与识别的基础契约
- 多租户相关特性与配置模型

## 依赖关系
- 通过 `XiHanMultiTenancyAbstractionsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 抽象层不包含具体解析实现
- 租户识别策略由业务模块统一定义

## 使用方式
```csharp
[DependsOn(typeof(XiHanMultiTenancyAbstractionsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义租户解析贡献者
- 自定义租户上下文实现

## 目录结构
```text
XiHan.Framework.MultiTenancy.Abstractions/
  README.md
  XiHanMultiTenancyAbstractionsModule.cs
```
