# XiHan.Framework.ObjectMapping

## 概述
XiHan.Framework.ObjectMapping 提供对象映射的基础能力与扩展点，统一实体、DTO 与视图模型之间的转换方式。

## 核心能力
- 对象映射服务的统一注册
- 映射规则与配置的集中管理
- 与应用服务与数据层协同

## 依赖关系
- 通过 `XiHanObjectMappingModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 映射规则通过 Options 类型或映射配置进行注册
- 建议在应用层集中管理映射规则

## 使用方式
```csharp
[DependsOn(typeof(XiHanObjectMappingModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义映射配置与规则扫描
- 自定义映射性能优化策略

## 目录结构
```text
XiHan.Framework.ObjectMapping/
  README.md
  XiHanObjectMappingModule.cs
```
