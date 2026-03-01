# XiHan.Framework.Serialization

## 概述
XiHan.Framework.Serialization 提供序列化相关能力与统一的序列化配置入口，支持多种序列化策略的扩展。

## 核心能力
- 序列化设置的统一注册与配置
- 常用序列化策略与扩展点
- 与 Web API 等模块协同

## 依赖关系
- 通过 `XiHanSerializationModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 序列化配置通过 Options 类型承载
- 推荐在启动模块中统一配置序列化策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanSerializationModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义序列化格式与转换器
- 统一的序列化兼容与版本策略

## 目录结构
```text
XiHan.Framework.Serialization/
  README.md
  XiHanSerializationModule.cs
```
