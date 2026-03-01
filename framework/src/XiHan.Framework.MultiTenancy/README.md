# XiHan.Framework.MultiTenancy

## 概述
XiHan.Framework.MultiTenancy 提供多租户能力的基础实现与集成入口，统一租户解析、上下文管理与配置协同。

## 核心能力
- 当前租户上下文与切换实现
- 租户解析与解析顺序的基础配置
- 与设置、认证等模块协同

## 依赖关系
- 通过 `XiHanMultiTenancyModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 多租户策略通过 Options 类型承载
- 推荐在 Web 模块中配置租户解析来源

## 使用方式
```csharp
[DependsOn(typeof(XiHanMultiTenancyModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义租户解析器与缓存策略
- 自定义租户配置存储实现

## 目录结构
```text
XiHan.Framework.MultiTenancy/
  README.md
  XiHanMultiTenancyModule.cs
```
