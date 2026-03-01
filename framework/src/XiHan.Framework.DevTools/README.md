# XiHan.Framework.DevTools

## 概述
XiHan.Framework.DevTools 提供开发期辅助能力与调试工具的统一入口，便于提升本地与测试环境的开发效率。

## 核心能力
- 开发期工具与调试能力的封装
- 面向开发流程的辅助功能与约定
- 与日志、配置等基础设施协同

## 依赖关系
- 通过 `XiHanDevToolsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 配置通过 Options 类型承载
- 建议仅在开发或测试环境启用相关能力

## 使用方式
```csharp
[DependsOn(typeof(XiHanDevToolsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义开发期诊断与调试工具
- 开发环境专用的功能开关

## 目录结构
```text
XiHan.Framework.DevTools/
  README.md
  XiHanDevToolsModule.cs
```
