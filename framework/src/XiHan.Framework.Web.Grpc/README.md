# XiHan.Framework.Web.Grpc

## 概述
XiHan.Framework.Web.Grpc 提供 gRPC 相关的基础能力与集成入口，统一 gRPC 服务注册与配置方式。

## 核心能力
- gRPC 服务的统一注册与配置
- gRPC 相关中间件与管道集成
- 与 Web Core 与序列化模块协同

## 依赖关系
- 通过 `XiHanWebGrpcModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- gRPC 配置通过 Options 类型承载
- 建议在启动模块统一配置 gRPC 服务与端点

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebGrpcModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义 gRPC 拦截器与协议扩展
- 自定义 gRPC 服务发现与注册策略

## 目录结构
```text
XiHan.Framework.Web.Grpc/
  README.md
  XiHanWebGrpcModule.cs
```
