// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Abstractions.Tests.Rag;

/// <summary>
/// 知识向量集合配置测试
/// </summary>
/// <remarks>
/// 集合名与维度都是已落到向量库里的物理事实：集合名改了会连不上既有集合，
/// 维度改了会让写入直接被向量库拒绝，且两者都无法靠重启恢复，只能重建索引。故按字面量锁死。
/// </remarks>
public class KnowledgeVectorOptionsTests
{
    /// <summary>
    /// 默认集合名锁定为 xihan_knowledge
    /// </summary>
    [Fact]
    public void DefaultCollectionName_IsStablePhysicalName()
    {
        Assert.Equal("xihan_knowledge", KnowledgeVectorOptions.DefaultCollectionName);
    }

    /// <summary>
    /// 默认向量维度锁定为 1536
    /// </summary>
    /// <remarks>对应 text-embedding-3-small 的输出维度；与嵌入模型不一致会被向量库直接拒写。</remarks>
    [Fact]
    public void DefaultDimensions_MatchesDefaultEmbeddingModel()
    {
        Assert.Equal(1536, KnowledgeVectorOptions.DefaultDimensions);
    }

    /// <summary>
    /// 新实例的默认值取自公开常量
    /// </summary>
    /// <remarks>
    /// 常量既是实例默认值，也供调用方在建集合时直接引用；
    /// 两者若脱钩，会出现「按常量建集合、按实例默认值写入」的错配。
    /// </remarks>
    [Fact]
    public void Defaults_WhenNewInstance_ComeFromPublicConstants()
    {
        var options = new KnowledgeVectorOptions();

        Assert.Equal(KnowledgeVectorOptions.DefaultCollectionName, options.CollectionName);
        Assert.Equal(KnowledgeVectorOptions.DefaultDimensions, options.Dimensions);
    }

    /// <summary>
    /// 可按部署需要覆盖集合名与维度
    /// </summary>
    /// <param name="collectionName">集合名</param>
    /// <param name="dimensions">向量维度</param>
    /// <remarks>换嵌入模型时必须同时换集合名，这里的成对取值即是该用法的示范。</remarks>
    [Theory]
    [InlineData("xihan_knowledge_bge", 1024)]
    [InlineData("tenant_kb", 768)]
    [InlineData("large_kb", 3072)]
    public void Properties_WhenOverridden_TakeGivenValues(string collectionName, int dimensions)
    {
        var options = new KnowledgeVectorOptions
        {
            CollectionName = collectionName,
            Dimensions = dimensions
        };

        Assert.Equal(collectionName, options.CollectionName);
        Assert.Equal(dimensions, options.Dimensions);
    }

    /// <summary>
    /// 可经 System.Text.Json 往返且值不丢失
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithCustomValues_PreservesValues(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new KnowledgeVectorOptions
        {
            CollectionName = "custom_kb",
            Dimensions = 1024
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<KnowledgeVectorOptions>(json, serializerOptions)!;

        Assert.Equal("custom_kb", restored.CollectionName);
        Assert.Equal(1024, restored.Dimensions);
    }

    /// <summary>
    /// 类型为 sealed
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(KnowledgeVectorOptions).IsSealed);
    }
}
