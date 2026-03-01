# XiHan.Framework.Messaging

## 概述
XiHan.Framework.Messaging 提供消息处理与发送的基础能力，统一消息模型与消息服务的扩展入口。

## 核心能力
- 消息模型与发送接口的统一抽象
- 与事件总线、通知等能力协同
- 可扩展的消息通道与适配器注册

## 依赖关系
- 通过 `XiHanMessagingModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 消息通道与配置通过 Options 类型承载
- 建议在启动模块统一配置消息通道与策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanMessagingModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义消息通道与发送实现
- 消息模板与路由策略扩展

## 目录结构
```text
XiHan.Framework.Messaging/
  README.md
  XiHanMessagingModule.cs
```
