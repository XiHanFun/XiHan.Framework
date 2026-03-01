# XiHan.Framework.Validation.Abstractions

## 概述
XiHan.Framework.Validation.Abstractions 提供校验相关的抽象契约与基础模型，统一校验策略的接口定义。

## 核心能力
- 校验接口与校验结果模型定义
- 校验策略的抽象与扩展点
- 与应用服务校验流程协同

## 依赖关系
- 通过 `XiHanValidationAbstractionsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 抽象层不包含具体校验实现
- 校验规则由业务模块统一定义

## 使用方式
```csharp
[DependsOn(typeof(XiHanValidationAbstractionsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义校验器与规则注册
- 自定义校验结果与错误编码策略

## 目录结构
```text
XiHan.Framework.Validation.Abstractions/
  README.md
  XiHanValidationAbstractionsModule.cs
```
