# XiHan.Framework.Traffic

## 概述
XiHan.Framework.Traffic 提供流量治理相关的基础能力与扩展点，统一限流、熔断与流量策略的接入方式。

## 核心能力
- 流量治理策略的基础抽象与注册
- 统一的流量控制与策略扩展入口
- 与缓存、日志与网关模块协同

## 依赖关系
- 通过 `XiHanTrafficModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 流量策略通过 Options 类型承载
- 建议在应用侧统一定义全局与局部策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanTrafficModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义限流与熔断策略
- 自定义流量采样与指标输出

## 目录结构
```text
XiHan.Framework.Traffic/
  README.md
  XiHanTrafficModule.cs
```
