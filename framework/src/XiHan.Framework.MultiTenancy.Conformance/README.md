# XiHan.Framework.MultiTenancy.Conformance

## 关于

`XiHan.Framework.MultiTenancy.Conformance` 是多租户的一致性测试套件，给写租户存储与分片解析提供程序的人验证实现用，不是业务代码的依赖。

## 主要特性

* `TenantStoreConformance`：按 ID 查找、启用标记与属性往返、枚举
* `TenantShardResolverConformance`：分库解析、共库为无值、失效后一致
* `TenantAdministrationConformance`：租户管理的逐项结果、删除留墓碑；写入之前查过的租户在写入之后立即看见变化；另一个实例在时限内看见变化（多实例一致的实现覆写 `StartPeerAsync`）

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.MultiTenancy.Conformance --prerelease
```

### 使用

继承对应基类并实现工厂方法：

```csharp
public sealed class InMemoryTenantStoreConformanceTests : TenantStoreConformance
{
    protected override ValueTask<ITenantStore> CreateStoreAsync(IReadOnlyList<TenantInfo> tenants)
        => ValueTask.FromResult<ITenantStore>(new InMemoryTenantStore(new InMemoryMultiTenancyOptions { Tenants = tenants }));
}
```

分片解析器的实现继承 `TenantShardResolverConformance` 并实现 `CreateResolverAsync`。

租户管理的实现继承 `TenantAdministrationConformance`，实现 `StartAsync()`（返回读空租户表的目录）与 `AdministerAsync(action)`（在一次调用里使用租户管理）。承诺多实例一致的实现覆写 `StartPeerAsync()`，返回与被测实例共用持久化与缓存后端的另一个实例的目录，另一个实例要在 `PeerVisibilityTimeout`（缺省 5 秒）内看见新增、停用与删除；默认返回 `null`，该用例跳过。

## 主要类型

此库提供的主要类型：

* `TenantStoreConformance`
* `TenantShardResolverConformance`
* `TenantAdministrationConformance`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 多租户契约：[XiHan.Framework.MultiTenancy.Abstractions](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.Abstractions)
* 内存实现：[XiHan.Framework.MultiTenancy.InMemory](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.InMemory)
* 数据库实现：[XiHan.Framework.MultiTenancy.Database](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.Database)

## 反馈与贡献

`XiHan.Framework.MultiTenancy.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
