# XiHan.Framework.Logging

## 概述
XiHan.Framework.Logging 提供日志能力的基础实现与扩展点，统一日志记录、格式化与输出策略。

## 核心能力
- 日志记录器的统一注册与注入
- 日志输出与格式化策略的基础实现
- 与诊断、审计等模块协同

## 依赖关系
- 通过 `XiHanLoggingModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 日志配置通过 Options 类型承载
- 建议在启动模块中统一配置日志级别与输出通道

## 使用方式
```csharp
[DependsOn(typeof(XiHanLoggingModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义日志提供者与输出通道
- 统一的日志格式与脱敏策略

## 目录结构
```text
XiHan.Framework.Logging/
  README.md
  XiHanLoggingModule.cs
```
