# XiHan.Framework.Localization

## 概述
XiHan.Framework.Localization 提供本地化能力的基础实现与集成入口，用于统一资源加载与文化切换逻辑。

## 核心能力
- 本地化服务的注册与运行时解析
- 资源管理与文化信息的统一处理
- 与 Web、设置等模块协同

## 依赖关系
- 通过 `XiHanLocalizationModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 本地化策略通过 Options 类型承载
- 推荐在启动模块中统一配置资源路径与文化集合

## 使用方式
```csharp
[DependsOn(typeof(XiHanLocalizationModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义资源提供者与缓存策略
- 自定义文化解析与回退策略

## 目录结构
```text
XiHan.Framework.Localization/
  README.md
  XiHanLocalizationModule.cs
```
