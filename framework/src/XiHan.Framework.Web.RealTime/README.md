# XiHan.Framework.Web.RealTime

## 概述
XiHan.Framework.Web.RealTime 提供实时通信相关的基础能力与集成入口，统一连接管理与实时消息分发。

## 核心能力
- 实时连接与会话管理的基础能力
- 实时消息分发与广播的扩展点
- 与 Web Core 与日志模块协同

## 依赖关系
- 通过 `XiHanWebRealTimeModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 实时通信配置通过 Options 类型承载
- 建议在启动模块统一配置连接与协议策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebRealTimeModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义连接管理与消息路由
- 自定义实时协议与扩展能力

## 目录结构
```text
XiHan.Framework.Web.RealTime/
  README.md
  XiHanWebRealTimeModule.cs
```
