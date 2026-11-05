# XiHan.Framework.AI.Conformance

## 关于

`XiHan.Framework.AI.Conformance` 是 AI 提供程序的一致性测试套件，给写提供程序的人用：继承抽象测试类并实现夹具，就能验证自己的知识库存储或模型提供程序与框架语义一致。

## 主要特性

* `KnowledgeStoreConformance`：验证 `IKnowledgeStore`
* `AIProviderConformance`：验证聊天与嵌入提供程序
* 确定性的哈希嵌入，测试不依赖外部模型

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.AI.Conformance --prerelease
```

### 使用

在提供程序的测试项目里引用本包与 xUnit v3，继承基类并返回夹具：

```csharp
public sealed class VectorDataKnowledgeStoreConformanceTests : KnowledgeStoreConformance
{
    protected override async Task<KnowledgeStoreHost> StartAsync()
    {
        var host = await TestHost.StartAsync(static b => b
                .UseFeature<HashingEmbeddingFeature>()
                .UseFeature<VectorDataKnowledgeFeature>(),
            [new("XiHan:AI:Providers:hash:Kind", "hashing"),
             new("XiHan:AI:Providers:hash:EmbeddingDimensions", "64"),
             new("XiHan:AI:Knowledge:Dimensions", "64")],
            services => services.AddSingleton<VectorStore>(new TestVectorStore()));
        return new KnowledgeStoreHost(host.Services.GetRequiredService<IKnowledgeStore>(), 64, host);
    }
}
```

`TestVectorStore` 是测试项目里自写的进程内 `VectorStore` 替身；验证真实连接器时换成它的 `VectorStore`。

## 主要类型

此库提供的主要类型：

* `KnowledgeStoreConformance`、`KnowledgeStoreHost`
* `AIProviderConformance`、`AIProviderHost`
* `HashingEmbeddingFeature`、`HashingEmbeddingGenerator`：`Kind = hashing` 的确定性嵌入

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)
* [一致性套件文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 抽象包：[XiHan.Framework.AI.Abstractions](https://www.nuget.org/packages/XiHan.Framework.AI.Abstractions)
* 向量知识库提供程序：[XiHan.Framework.AI.VectorData](https://www.nuget.org/packages/XiHan.Framework.AI.VectorData)

## 反馈与贡献

`XiHan.Framework.AI.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
