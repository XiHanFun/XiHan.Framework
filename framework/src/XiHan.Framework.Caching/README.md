# XiHan.Framework.Caching

## 概述
XiHan.Framework.Caching 提供缓存能力的统一抽象与基础实现入口，支持与业务模块解耦的缓存访问方式。

## 核心能力
- 缓存服务的统一注册与生命周期管理
- 缓存键与策略的基础约定
- 与日志、配置和多租户等能力协同

## 依赖关系
- 通过 `XiHanCachingModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 缓存配置通过 Options 类型承载
- 业务侧可根据环境选择具体缓存实现

未启用 Redis 时，`DefaultDistributedLock` 仅在当前进程内互斥，最多保存 10,000 个资源键。过期清理会按观测到的具体锁条目删除；续期与接管会替换条目，避免清扫误删新持有者。多实例互斥需启用 Redis Provider。

## 使用方式
```csharp
[DependsOn(typeof(XiHanCachingModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义缓存提供者与序列化策略
- 自定义缓存键规范与失效策略

## 目录结构
```text
XiHan.Framework.Caching/
  README.md
  XiHanCachingModule.cs
```
