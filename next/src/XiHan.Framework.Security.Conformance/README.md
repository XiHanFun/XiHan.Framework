# XiHan.Framework.Security.Conformance

安全一致性套件。任何 `IPermissionGrantStore` 实现都必须继承 `PermissionGrantStoreConformance` 并通过全部用例。

```csharp
public sealed class InMemoryPermissionGrantStoreConformanceTests : PermissionGrantStoreConformance
{
    protected override ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants)
        => ValueTask.FromResult<IPermissionGrantStore>(new InMemoryPermissionGrantStore(new InMemorySecurityOptions { Grants = grants }));
}
```

套件覆盖：用户与角色授予、全局授予对任何租户生效、租户专属授予只对该租户生效、未知主体为空、主体与权限区分大小写、通配授予原样返回；存储不可达时返回 `SECURITY_UNAVAILABLE_001` 而不是空集合（ADR-0071）。

`GetGrantsAsync` 返回 `Result<IReadOnlyCollection<string>>`：没有授予是成功的空集合，存储故障是失败。访问外部存储的 Provider 覆写 `CreateUnavailableStoreAsync`，给出一个背后存储不可达的实例（连接指向不存在的数据库、服务已停止等）；不覆写时该用例跳过，只有进程内、读取不会失败的存储可以不覆写。

## 依赖方向

依赖 `Security.Abstractions` 与 xUnit 核心包。工具链包。

## 状态

P2：可用。
