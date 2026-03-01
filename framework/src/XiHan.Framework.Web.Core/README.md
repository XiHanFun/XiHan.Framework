# XiHan.Framework.Web.Core

## 概述
XiHan.Framework.Web.Core 提供 Web 基础设施能力，包含应用构建、HttpContext 支持与 Web 环境集成。

## 核心能力
- Web 应用构建与基础服务注册
- HttpContext 与当前主体访问支持
- 与多租户、序列化等模块协同

## 依赖关系
- 通过 `XiHanWebCoreModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- Web 基础能力以代码约定为主
- 建议在启动模块统一配置中间件与管道顺序

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebCoreModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义 Web 中间件与扩展方法
- 自定义 Web 环境与请求上下文策略

## 目录结构
```text
XiHan.Framework.Web.Core/
  README.md
  XiHanWebCoreModule.cs
```
