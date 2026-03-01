# XiHan.Framework.SearchEngines

## 概述
XiHan.Framework.SearchEngines 提供搜索引擎相关的基础能力与集成入口，便于统一接入不同搜索引擎实现。

## 核心能力
- 搜索引擎适配与调用的统一入口
- 搜索索引与查询策略的基础抽象
- 与配置与日志等基础设施协同

## 依赖关系
- 通过 `XiHanSearchEnginesModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 搜索引擎配置通过 Options 类型承载
- 建议在启动模块中统一配置索引与连接参数

## 使用方式
```csharp
[DependsOn(typeof(XiHanSearchEnginesModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义搜索引擎适配器
- 自定义索引策略与查询优化

## 目录结构
```text
XiHan.Framework.SearchEngines/
  README.md
  XiHanSearchEnginesModule.cs
```
