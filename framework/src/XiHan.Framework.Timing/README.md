# XiHan.Framework.Timing

## 概述
XiHan.Framework.Timing 提供时间相关的基础能力与抽象，包括时间提供者、时区处理与时间策略扩展。

## 核心能力
- 时间提供者与时间策略的统一抽象
- 时区与时间格式处理的基础能力
- 与任务调度、日志等模块协同

## 依赖关系
- 通过 `XiHanTimingModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 时间策略通过 Options 类型承载
- 业务侧可统一定义时间基准与时区策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanTimingModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义时间提供者
- 自定义时区与时间格式策略

## 目录结构
```text
XiHan.Framework.Timing/
  README.md
  XiHanTimingModule.cs
```
