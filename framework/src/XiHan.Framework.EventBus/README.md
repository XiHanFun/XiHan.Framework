# XiHan.Framework.EventBus

## 概述
XiHan.Framework.EventBus 提供事件总线实现与集成能力，支持进程内与跨模块的事件发布与订阅。

## 核心能力
- 事件发布与订阅的基础实现
- 与应用服务、领域事件的集成入口
- 统一事件处理与异常管理策略
- 发件箱/收件箱后台循环；注册投递目标目录且发件箱实现 `ITenantScopedEventOutbox` 时，由 `OutboxDeliveryTargetScanner` 按投递目标轮转扫描发件箱
- `OutboxPendingEventCounter`：统计投递目标在全部按租户定位存储的发件箱（实现 `ITenantScopedEventOutbox`）中的待送事件数

## 依赖关系
- 通过 `XiHanEventBusModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 事件总线策略通过 Options 类型承载
- 事件命名、路由与版本策略由业务模块统一定义
- 事件盒后台处理配置节 `XiHan:EventBus:EventBoxes`，发件箱投递目标相关项：

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `OutboxBatchSize` | `100` | 发件箱单批处理数量；按投递目标轮转扫描时为每个发件箱配置的每轮预算 |
| `OutboxTargetPageSize` | `100` | 投递目标目录单页读取数量 |
| `OutboxRoundTimeLimitMilliseconds` | `30000` | 单轮扫描时间上限（毫秒），达到后本轮不再访问新目标 |
| `OutboxTargetDirectoryMaxBackoffMilliseconds` | `60000` | 目录读取失败后的最大退避时长（毫秒） |

- 按投递目标轮转扫描：宿主布局与目录目标组成一个循环，每轮从上一轮停下处继续，同一目标每轮至多访问一次；目录读取失败时保留游标并指数退避，退避期间照常扫描宿主布局；游标只在进程内存中，重启后从目录首页开始

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
