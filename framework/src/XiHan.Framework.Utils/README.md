# XiHan.Framework.Utils

## 概述
XiHan.Framework.Utils 提供通用工具类与基础辅助能力，包括反射、集合、线程、日志工具等常用功能。

## 核心能力
- 通用工具方法与基础组件封装
- 反射、集合、线程与运行时辅助能力
- 为其它模块提供基础工具支撑

## 依赖关系
- 作为基础库被其它模块引用
- 不包含独立模块类

## 配置与约定
- 工具类以代码约定为主
- 建议在基础设施层统一封装常用工具调用

## 使用方式
```csharp
var version = ReflectionHelper.GetEntryAssemblyVersion();
```

## 扩展点
- 可在业务侧封装更高层级的工具集

## 目录结构
```text
XiHan.Framework.Utils/
  README.md
```
