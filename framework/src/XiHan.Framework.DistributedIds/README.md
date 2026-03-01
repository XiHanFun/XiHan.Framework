# XiHan.Framework.DistributedIds

## 概述
XiHan.Framework.DistributedIds 提供分布式 ID 生成能力，统一雪花算法等 ID 策略的配置与使用方式。

## 核心能力
- 分布式 ID 生成策略封装与扩展
- 统一 ID 生成器的依赖注入注册
- 与数据层、日志等基础设施协同

## 依赖关系
- 通过 `XiHanDistributedIdsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- ID 生成策略通过 Options 类型承载
- 推荐在启动模块中集中配置节点参数与时间基准

## 使用方式
```csharp
[DependsOn(typeof(XiHanDistributedIdsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义 ID 生成算法
- 多节点环境下的配置策略

## 目录结构
```text
XiHan.Framework.DistributedIds/
  README.md
  XiHanDistributedIdsModule.cs
```
