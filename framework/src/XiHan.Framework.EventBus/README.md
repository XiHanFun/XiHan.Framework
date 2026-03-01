# XiHan.Framework.EventBus

## 概述
XiHan.Framework.EventBus 提供事件总线实现与集成能力，支持进程内与跨模块的事件发布与订阅。

## 核心能力
- 事件发布与订阅的基础实现
- 与应用服务、领域事件的集成入口
- 统一事件处理与异常管理策略

## 依赖关系
- 通过 `XiHanEventBusModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 事件总线策略通过 Options 类型承载
- 事件命名、路由与版本策略由业务模块统一定义

## 使用方式
```csharp
[DependsOn(typeof(XiHanEventBusModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义事件总线实现与订阅者
- 事件处理的重试与补偿策略

## 目录结构
```text
XiHan.Framework.EventBus/
  README.md
  XiHanEventBusModule.cs
```
