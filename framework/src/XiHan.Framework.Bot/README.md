# XiHan.Framework.Bot

## 概述
XiHan.Framework.Bot 提供机器人与消息交互场景的基础能力与扩展点，便于在统一框架内接入外部机器人平台。

## 核心能力
- 机器人接入的统一抽象与服务注册
- 消息处理与发送流程的基础约定
- 与消息、日志等模块协同工作

## 依赖关系
- 通过 `XiHanBotModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 接入配置通过 Options 类型承载
- 推荐在启动模块中集中管理机器人相关配置

## 使用方式
```csharp
[DependsOn(typeof(XiHanBotModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 外部机器人平台的适配实现
- 消息处理与路由策略扩展

## 目录结构
```text
XiHan.Framework.Bot/
  README.md
  XiHanBotModule.cs
```
