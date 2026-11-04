# XiHan.Framework.Caching.Conformance

## 关于

`XiHan.Framework.Caching.Conformance` 是缓存提供程序的一致性测试套件，给写提供程序的人用：任何 `ICacheStore` 实现继承 `CacheStoreConformance` 并通过全部用例，换提供程序时语义一致。

## 主要特性

* 读写、覆盖、删除、不存在才写、比较删
* 绝对与滑动过期，键区分大小写，大值
* 基于存储的分布式锁互斥与到期

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Caching.Conformance --prerelease
```

### 使用

在提供程序的测试项目里引用本包与 xUnit v3，每个用例创建一个新存储：

```csharp
public sealed class RedisCacheStoreConformanceTests : CacheStoreConformance
{
    protected override ICacheStore CreateStore() => new RedisCacheStore(...);
}
```

过期用例按真实时间等待，`ExpirationGrace` 可按提供程序调整。

## 主要类型

此库提供的主要类型：

* `CacheStoreConformance`：测试基类，实现 `CreateStore()`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)
* [一致性套件文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 抽象包：[XiHan.Framework.Caching.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Caching.Abstractions)
* Redis 实现：[XiHan.Framework.Caching.Redis](https://www.nuget.org/packages/XiHan.Framework.Caching.Redis)

## 反馈与贡献

`XiHan.Framework.Caching.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
