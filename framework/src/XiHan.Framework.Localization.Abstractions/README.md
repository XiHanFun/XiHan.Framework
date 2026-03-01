# XiHan.Framework.Localization.Abstractions

## 概述
XiHan.Framework.Localization.Abstractions 提供本地化能力的抽象契约，定义资源获取、文化信息与本地化服务接口。

## 核心能力
- 本地化资源访问的统一接口
- 文化信息与语言切换的基础约定
- 与应用层服务的协作契约

## 依赖关系
- 通过 `XiHanLocalizationAbstractionsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 抽象层不包含具体资源实现
- 资源命名与组织规范由业务模块统一约定

## 使用方式
```csharp
[DependsOn(typeof(XiHanLocalizationAbstractionsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 本地化资源提供者实现
- 自定义文化解析与回退策略

## 目录结构
```text
XiHan.Framework.Localization.Abstractions/
  README.md
  XiHanLocalizationAbstractionsModule.cs
```
