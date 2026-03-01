# XiHan.Framework.Web.Api

## 概述
XiHan.Framework.Web.Api 提供 Web REST API 基础能力与动态 API 支持，统一控制器生成、路由约定与 API 行为。

## 核心能力
- 动态 API 生成与约定路由
- API 行为与异常处理的统一配置
- 与序列化、多租户等模块协同

## 依赖关系
- 通过 `XiHanWebApiModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 动态 API 配置通过 `DynamicApiOptions` 进行统一管理
- API 行为与 JSON 序列化策略由模块内统一配置

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebApiModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义动态 API 约定与路由策略
- 自定义 API 过滤器与异常处理

## 目录结构
```text
XiHan.Framework.Web.Api/
  README.md
  XiHanWebApiModule.cs
```
