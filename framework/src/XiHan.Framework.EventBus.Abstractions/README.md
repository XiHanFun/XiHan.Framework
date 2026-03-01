# XiHan.Framework.EventBus.Abstractions

## 概述
XiHan.Framework.EventBus.Abstractions 提供事件总线的抽象契约与基础模型，统一事件发布与订阅的接口规范。

## 核心能力
- 事件总线接口与事件模型定义
- 发布/订阅契约的统一抽象
- 与应用服务及领域事件的边界协同

## 依赖关系
- 通过 `XiHanEventBusAbstractionsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 抽象层不包含具体实现
- 事件命名与版本策略由业务模块约定

## 使用方式
```csharp
[DependsOn(typeof(XiHanEventBusAbstractionsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 事件总线实现可在基础设施层替换
- 事件模型的扩展与序列化策略

## 目录结构
```text
XiHan.Framework.EventBus.Abstractions/
  README.md
  XiHanEventBusAbstractionsModule.cs
```
