# XiHan.Framework.Domain

## 概述
XiHan.Framework.Domain 提供领域层基础设施与领域模型组织方式，规范聚合、实体与领域服务的实现边界。

## 核心能力
- 领域层基础抽象与通用约定
- 领域模型组织与聚合根支持
- 与数据层、事件总线等模块协同

## 依赖关系
- 通过 `XiHanDomainModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 领域模型遵循聚合与边界上下文约定
- 领域服务与应用服务分层清晰

## 使用方式
```csharp
[DependsOn(typeof(XiHanDomainModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义领域服务与领域事件
- 聚合根的仓储与生命周期管理

## 目录结构
```text
XiHan.Framework.Domain/
  README.md
  XiHanDomainModule.cs
```
