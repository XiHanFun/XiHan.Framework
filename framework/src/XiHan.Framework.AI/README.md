# XiHan.Framework.AI

## 概述
XiHan.Framework.AI 提供 AI 相关基础能力与可扩展的接入点，统一模型调用、上下文组织与应用侧配置的入口。

## 核心能力
- 统一 AI 能力的服务注册与依赖注入入口
- 提供模型调用的抽象与扩展点，便于接入不同提供方
- 支持与日志、配置等基础设施协同工作

## 依赖关系
- 通过 `XiHanAIModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 配置通过 Options 类型承载，由应用侧绑定 `IConfiguration`
- 未显式配置时使用默认值或由业务模块补齐

## 使用方式
```csharp
[DependsOn(typeof(XiHanAIModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- AI 能力实现的服务注册与替换
- 与应用侧配置、日志与审计的集成

## 目录结构
```text
XiHan.Framework.AI/
  README.md
  XiHanAIModule.cs
```
