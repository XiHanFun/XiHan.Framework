# XiHan.Framework.Http

## 概述
XiHan.Framework.Http 提供 HTTP 访问相关的基础能力，包含客户端封装、请求管道与扩展接口。

## 核心能力
- HTTP 客户端的统一封装与注册
- 请求拦截、重试与异常处理策略的扩展点
- 与配置、日志等基础设施协同

## 依赖关系
- 通过 `XiHanHttpModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- HTTP 相关配置通过 Options 类型承载
- 建议在启动模块统一配置连接超时与重试策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanHttpModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义 HTTP 客户端实现或代理策略
- 统一的请求日志与诊断扩展

## 目录结构
```text
XiHan.Framework.Http/
  README.md
  XiHanHttpModule.cs
```
