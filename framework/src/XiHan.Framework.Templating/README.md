# XiHan.Framework.Templating

## 概述
XiHan.Framework.Templating 提供模板渲染相关的基础能力与扩展点，统一模板加载、渲染与缓存策略。

## 核心能力
- 模板引擎的统一注册与封装
- 模板加载、渲染与缓存的基础能力
- 与文件系统、对象映射等模块协同

## 依赖关系
- 通过 `XiHanTemplatingModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 模板相关配置通过 Options 类型承载
- 建议在启动模块统一配置模板来源与缓存策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanTemplatingModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义模板引擎与渲染策略
- 自定义模板资源加载方式

## 目录结构
```text
XiHan.Framework.Templating/
  README.md
  XiHanTemplatingModule.cs
```
