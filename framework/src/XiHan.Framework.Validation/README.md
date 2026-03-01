# XiHan.Framework.Validation

## 概述
XiHan.Framework.Validation 提供校验能力的基础实现与集成入口，统一校验流程与规则执行方式。

## 核心能力
- 校验服务的统一注册与调用
- 校验规则执行与错误结果管理
- 与应用服务与数据输入校验协同

## 依赖关系
- 通过 `XiHanValidationModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 校验配置通过 Options 类型承载
- 建议在应用层集中管理校验规则

## 使用方式
```csharp
[DependsOn(typeof(XiHanValidationModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义校验器与规则集合
- 自定义校验失败处理策略

## 目录结构
```text
XiHan.Framework.Validation/
  README.md
  XiHanValidationModule.cs
```
