# XiHan.Framework.Script

## 概述
XiHan.Framework.Script 提供脚本执行能力与脚本引擎的基础抽象，统一脚本管理与执行入口。

## 核心能力
- 脚本引擎的统一注册与封装
- 脚本执行与上下文管理的基础能力
- 与配置、日志等基础设施协同

## 依赖关系
- 通过 `XiHanScriptModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 脚本执行相关配置通过 Options 类型承载
- 建议在应用侧统一管理脚本来源与权限

## 使用方式
```csharp
[DependsOn(typeof(XiHanScriptModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义脚本引擎实现
- 自定义脚本运行时上下文

## 目录结构
```text
XiHan.Framework.Script/
  README.md
  XiHanScriptModule.cs
```
