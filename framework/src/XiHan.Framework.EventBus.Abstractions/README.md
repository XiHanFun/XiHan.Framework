# XiHan.Framework.EventBus.Abstractions

## 概述
XiHan.Framework.EventBus.Abstractions 提供事件总线的抽象契约与基础模型，统一事件发布与订阅的接口规范。

## 核心能力
- 事件总线接口与事件模型定义
- 发布/订阅契约的统一抽象
- 与应用服务及领域事件的边界协同
- 可靠投递契约：`IEventOutbox` / `IEventInbox` 及出入站事件信息
- 发件箱投递目标契约：
  - `OutboxDeliveryTarget`：投递目标，对应一个需要单独扫描发件箱的租户，带启用标记
  - `OutboxDeliveryTargetPage`：投递目标目录的一页与下一页游标
  - `IOutboxDeliveryTargetProvider`：由应用实现的投递目标目录，分页读取与按租户查找
  - `ITenantScopedEventOutbox`：按当前租户上下文定位存储的发件箱，可统计待送数
  - `IOutboxPendingEventCounter`：统计投递目标在全部按租户定位存储的发件箱（实现 `ITenantScopedEventOutbox`）中的待送事件数

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
- 应用实现 `IOutboxDeliveryTargetProvider` 提供需要单独扫描发件箱的租户目录
- 事件模型的扩展与序列化策略

## 目录结构
```text
XiHan.Framework.EventBus.Abstractions/
  README.md
  XiHanEventBusAbstractionsModule.cs
```
