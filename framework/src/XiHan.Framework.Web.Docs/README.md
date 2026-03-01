# XiHan.Framework.Web.Docs

## 概述
XiHan.Framework.Web.Docs 提供 Web API 文档能力的基础集成入口，统一 OpenAPI 文档与文档服务的配置方式。

## 核心能力
- OpenAPI 文档生成与配置支持
- 文档服务与分组策略的基础约定
- 与 Web API 模块协同

## 依赖关系
- 通过 `XiHanWebDocsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 文档相关配置通过 Options 类型承载
- 建议在启动模块统一配置文档标题与分组

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebDocsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义文档分组与过滤策略
- 自定义文档输出格式与扩展信息

## 目录结构
```text
XiHan.Framework.Web.Docs/
  README.md
  XiHanWebDocsModule.cs
```
