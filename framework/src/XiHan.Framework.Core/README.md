# XiHan.Framework.Core

## 概述
XiHan.Framework.Core 是框架基础设施的核心实现，提供模块化系统、应用生命周期管理、依赖注入扩展与基础运行时服务。

## 核心能力
- 模块化系统与模块生命周期管理
- 应用启动与初始化流程编排
- 核心依赖注入扩展与基础设施服务
- 应用上下文、环境与配置访问支持

## 依赖关系
- 作为框架基础层被其它模块依赖
- 不包含独立模块类，提供核心抽象与基类

## 配置与约定
- 核心能力主要以代码约定生效
- 业务模块可通过继承 `XiHanModule` 集成模块化能力

## 使用方式
```csharp
public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 使用 Core 提供的模块化与基础设施能力
    }
}
```

## 扩展点
- 模块加载与生命周期贡献者
- 应用初始化与关机流程的扩展

## 目录结构
```text
XiHan.Framework.Core/
  README.md
  Modularity/
```
