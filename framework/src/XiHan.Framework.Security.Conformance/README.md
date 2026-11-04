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

在完整的应用宿主上给不可达的存储时，不要让整个宿主的库不可达（删库、断开默认连接）：宿主里别的子系统的后台循环（工作流轮询与领导权、发件箱、调度、翻译加载等）会如实报存储不可达，例如 `WORKFLOW_UNAVAILABLE_001`。用只装被测特性的最小宿主（框架 `Security.Database` 的测试就是这样），或只让被测存储用的连接不可达。

## 依赖方向

依赖 `Security.Abstractions` 与 xUnit 核心包。工具链包。

## 状态

P2：可用。
