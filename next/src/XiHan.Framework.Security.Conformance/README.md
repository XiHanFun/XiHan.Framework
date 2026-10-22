# XiHan.Framework.Security.Conformance

安全一致性套件。任何 `IPermissionGrantStore` 实现都必须继承 `PermissionGrantStoreConformance` 并通过全部用例。

```csharp
public sealed class InMemoryPermissionGrantStoreConformanceTests : PermissionGrantStoreConformance
{
    protected override ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants)
        => ValueTask.FromResult<IPermissionGrantStore>(new InMemoryPermissionGrantStore(new InMemorySecurityOptions { Grants = grants }));
}
```

套件覆盖：用户与角色授予、全局授予对任何租户生效、租户专属授予只对该租户生效、未知主体为空、主体与权限区分大小写、通配授予原样返回。

## 依赖方向

依赖 `Security.Abstractions` 与 xUnit 核心包。工具链包。

## 状态

P2：可用。
