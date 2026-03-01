# XiHan.Framework.Web.Gateway

## 概述
XiHan.Framework.Web.Gateway 提供网关层基础能力与集成入口，支持统一的网关配置与流量入口管理。

## 核心能力
- 网关能力的统一注册与配置入口
- 网关级别的路由与流量管理扩展点
- 与 Web API、流量治理等模块协同

## 依赖关系
- 通过 `XiHanWebGatewayModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 网关配置通过 Options 类型承载
- 建议在启动模块统一配置网关路由与策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebGatewayModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义网关路由与策略
- 自定义网关鉴权与限流策略

## 目录结构
```text
XiHan.Framework.Web.Gateway/
  README.md
  XiHanWebGatewayModule.cs
```
