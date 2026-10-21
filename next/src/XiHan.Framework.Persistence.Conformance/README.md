# XiHan.Framework.Persistence.Conformance

持久化一致性套件。任何提供 `persistence.relational@1` 的 Provider 都必须继承 `RepositoryConformance` 并通过全部用例；这是"换 Provider 只改配置与包引用"的保证。

```csharp
public sealed class InMemoryRepositoryConformanceTests : RepositoryConformance
{
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
```

套件覆盖：读己之写与提交可见性、标识重复、审计字段与并发戳、软删除与物理删除、租户必填与隔离、过滤排序分页与计数、失败结果与异常回滚、Transaction 阶段之外使用仓储抛错、标识生成器。

## 依赖方向

依赖 `Persistence.Abstractions`、`Testing` 与 xUnit 核心包。工具链包，可引用任何环。

## 状态

P2：可用。
