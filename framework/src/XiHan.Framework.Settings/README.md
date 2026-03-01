# XiHan.Framework.Settings

## 概述
XiHan.Framework.Settings 提供统一的设置管理能力，支持多来源配置与运行时读取策略。

## 核心能力
- 设置值提供者与优先级体系
- 运行时设置读取与缓存策略
- 与多租户、应用层协同

## 依赖关系
- 通过 `XiHanSettingsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 设置相关配置通过 Options 类型承载
- 建议在启动模块中统一配置设置值来源

## 使用方式
```csharp
[DependsOn(typeof(XiHanSettingsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义设置值提供者
- 自定义设置缓存与刷新策略

## 目录结构
```text
XiHan.Framework.Settings/
  README.md
  XiHanSettingsModule.cs
```
