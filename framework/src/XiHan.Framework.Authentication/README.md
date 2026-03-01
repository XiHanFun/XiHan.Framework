# XiHan.Framework.Authentication

## 概述
XiHan.Framework.Authentication 提供认证相关的基础能力与策略支持，封装认证流程、凭据验证与认证结果模型。

## 核心能力
- 认证流程与结果模型的统一抽象
- 用户凭据验证策略与安全策略扩展
- 与授权、日志等基础设施协同

## 依赖关系
- 通过 `XiHanAuthenticationModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 认证策略与配置通过 Options 类型承载
- 推荐在启动模块中统一配置认证参数与策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanAuthenticationModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义认证服务实现
- 自定义认证策略与凭据验证流程

## 目录结构
```text
XiHan.Framework.Authentication/
  README.md
  XiHanAuthenticationModule.cs
```
