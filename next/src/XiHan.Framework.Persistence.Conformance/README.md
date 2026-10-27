# XiHan.Framework.Persistence.Conformance

持久化一致性套件。任何提供 `persistence.relational@1` 的 Provider 都必须继承 `RepositoryConformance` 并通过全部用例；这是"换 Provider 只改配置与包引用"的保证。

```csharp
public sealed class InMemoryRepositoryConformanceTests : RepositoryConformance
{
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
```

套件覆盖：读己之写与提交可见性、标识重复、审计字段与并发戳、软删除与物理删除、租户必填与隔离、过滤排序分页与计数（含可空值类型与 null 的相等 / 不等按 `IS NULL` 语义过滤）、失败结果与异常回滚、Transaction 阶段之外使用仓储抛错、标识生成器。

`IdGeneratorConformance<TKey>` 是标识生成器自己的套件（`TKey : IComparable<TKey>`）：实现 `CreateGenerator(TimeProvider clock)`，用例验证顺序生成与 8 线程并发生成不重复（真实时钟）、时钟前进后标识更大（`ConformanceClock`）；`GuidV7IdGenerator` 与 `SnowflakeIdGenerator` 都通过，应用自定义的 `IIdGenerator<TKey>` 也应继承它。

## 依赖方向

依赖 `Persistence.Abstractions`、`Testing` 与 xUnit 核心包。工具链包，可引用任何环。

## 状态

P2：可用。
