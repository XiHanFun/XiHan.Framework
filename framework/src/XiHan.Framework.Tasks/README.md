# XiHan.Framework.Tasks

## 概述
XiHan.Framework.Tasks 提供任务调度与后台服务的基础能力，包括定时任务、任务锁与执行管道。

## 核心能力
- 定时任务与后台服务的统一注册
- 任务锁与执行管道的基础实现
- 与日志、配置等基础设施协同

## 依赖关系
- 通过 `XiHanTasksModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 任务调度配置通过 Options 类型承载
- 建议在启动模块统一配置调度器与执行策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanTasksModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义任务调度器与执行策略
- 自定义任务锁实现与分布式协调

## 目录结构
```text
XiHan.Framework.Tasks/
  README.md
  XiHanTasksModule.cs
```
