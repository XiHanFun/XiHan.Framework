# XiHan.Framework.Threading

## 概述
XiHan.Framework.Threading 提供并发与线程相关的基础工具与扩展点，统一异步锁、并发控制与调度工具。

## 核心能力
- 异步锁与并发控制工具
- 线程与任务相关的基础工具封装
- 与后台任务与缓存等模块协同

## 依赖关系
- 通过 `XiHanThreadingModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 线程相关能力以代码约定生效
- 建议在基础设施层集中管理并发策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanThreadingModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义并发控制策略
- 自定义异步调度与任务封装

## 目录结构
```text
XiHan.Framework.Threading/
  README.md
  XiHanThreadingModule.cs
```
