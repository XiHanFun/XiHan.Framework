# XiHan.Framework.Metadata

## 概述
XiHan.Framework.Metadata 提供框架元数据与基础信息输出，集中维护框架名称、版本与运行时信息。

## 核心能力
- 框架基础信息与版本信息的统一管理
- 运行时入口程序集信息读取
- 为日志与诊断提供统一元数据来源

## 依赖关系
- 作为基础库被其它模块引用
- 不包含独立模块类

## 配置与约定
- 元数据以内置常量与运行时信息组合提供
- 建议在诊断与日志场景统一使用该模块

## 使用方式
```csharp
var summary = XiHanMetadata.GetSummary();
```

## 扩展点
- 可以在业务侧封装自定义元信息输出格式

## 目录结构
```text
XiHan.Framework.Metadata/
  README.md
  XiHanMetadata.cs
```
