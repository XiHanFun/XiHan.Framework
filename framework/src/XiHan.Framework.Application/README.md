# XiHan.Framework.Application

## 概述
XiHan.Framework.Application 提供应用层基础设施，包含应用服务基类、CRUD 服务规范与应用层通用能力。

## 核心能力
- 应用服务基类与生命周期约定
- CRUD/批量 CRUD 应用服务基类封装
- 应用层通用功能的扩展与集成入口

## 依赖关系
- 通过 `XiHanApplicationModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 应用服务实现 `IApplicationService` 以暴露为动态 API
- 配置通过 Options 类型承载，由应用侧绑定 `IConfiguration`

## 使用方式
```csharp
[DependsOn(typeof(XiHanApplicationModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义应用服务基类与通用拦截逻辑
- 应用层 DTO 映射与校验策略

## 目录结构
```text
XiHan.Framework.Application/
  README.md
  XiHanApplicationModule.cs
```
