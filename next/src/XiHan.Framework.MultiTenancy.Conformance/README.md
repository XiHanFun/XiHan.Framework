# XiHan.Framework.MultiTenancy.Conformance

多租户一致性套件。任何 `ITenantStore` 实现都必须继承 `TenantStoreConformance` 并通过全部用例。

```csharp
public sealed class InMemoryTenantStoreConformanceTests : TenantStoreConformance
{
    protected override ValueTask<ITenantStore> CreateStoreAsync(IReadOnlyList<TenantInfo> tenants)
        => ValueTask.FromResult<ITenantStore>(new InMemoryTenantStore(new InMemoryMultiTenancyOptions { Tenants = tenants }));
}
```

套件覆盖：按 ID 查找、不存在为无值、ID 区分大小写、显示名、启用标记与属性完整往返、枚举全部租户。

## 依赖方向

依赖 `MultiTenancy.Abstractions` 与 xUnit 核心包。工具链包。

## 状态

P2：可用。
