# XiHan.Framework.AI.Conformance

AI 一致性套件（工具链，RFC-0022）：Provider 继承抽象测试类并实现夹具，通过全部用例。

| 类型 | 用途 |
| --- | --- |
| `KnowledgeStoreConformance` + `KnowledgeStoreHost` | `IKnowledgeStore`：写入与按相似度排序、`top`、文档与元数据过滤、同 ID 重写入替换、按文档删除、按块序号裁剪、集合隔离、空集合搜索 |
| `AIProviderConformance` + `AIProviderHost` | `IAIProviderCatalog` 上的聊天与嵌入 Provider：应答、嵌入维度与批量条数、缓存与 `Invalidate`、未知名字 `AI_NOTFOUND_001`；默认名——`StartAsync` 的夹具里被测 Provider 是聊天与嵌入的默认，`StartWithoutDefaultAsync` 的夹具没有默认（不写名字即 `AI_NOTFOUND_001`），能把两种默认分开的目录覆写 `StartWithSeparateDefaultsAsync`（`AIProviderHost.EmbeddingProviderName` 给出默认嵌入 Provider），不覆写时该例跳过 |
| `HashingEmbeddingGenerator`、`HashingEmbeddingGeneratorFactory`、`HashingEmbeddingFeature` | 确定性的词袋哈希嵌入（`Kind = hashing`）与提供 `ai.embedding@1` 的特性，让知识库 Provider 的测试不依赖外部模型 |

下面是 `AI.VectorData` 自己的一致性测试的写法。`TestVectorStore` 是测试项目里写的进程内 `VectorStore` 替身（`test/XiHan.Framework.AI.VectorData.Tests/TestVectorStore.cs`），仓库不引用任何向量库连接器；验证真实连接器时换成它的 `VectorStore`，并在测试项目里引用连接器的包。

```csharp
public sealed class VectorDataKnowledgeStoreConformanceTests : KnowledgeStoreConformance
{
    protected override async Task<KnowledgeStoreHost> StartAsync()
    {
        var host = await TestHost.StartAsync(static b => b.UseFeature<HashingEmbeddingFeature>().UseFeature<VectorDataKnowledgeFeature>(),
            [new("XiHan:AI:Providers:hash:Kind", "hashing"), new("XiHan:AI:Providers:hash:EmbeddingDimensions", "64"), new("XiHan:AI:Knowledge:Dimensions", "64")],
            services => services.AddSingleton<VectorStore>(new TestVectorStore()));
        return new KnowledgeStoreHost(host.Services.GetRequiredService<IKnowledgeStore>(), 64, host);
    }
}
```

## 依赖方向

依赖 `XiHan.Framework.AI.Abstractions` 与 xUnit v3。被各 Provider 的测试项目引用。

## 状态

5.x P0：可用（Preview）。
