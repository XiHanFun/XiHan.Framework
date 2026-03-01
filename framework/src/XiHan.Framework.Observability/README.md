# XiHan.Framework.Observability

## 概述
XiHan.Framework.Observability 提供可观测性能力的基础集成入口，包含监控、诊断与运行时观察能力的扩展点。

## 核心能力
- 监控与诊断能力的统一注册与入口
- 运行时指标与追踪的基础协作
- 与日志与告警系统协同

## 依赖关系
- 通过 `XiHanObservabilityModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 观测相关配置通过 Options 类型承载
- 建议在运行环境中统一配置采样与导出策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanObservabilityModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义指标采集与导出实现
- 自定义链路追踪与日志关联策略

## 目录结构
```text
XiHan.Framework.Observability/
  README.md
  XiHanObservabilityModule.cs
```
