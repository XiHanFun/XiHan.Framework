// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 启动装配了哈希嵌入 Provider 与 VectorData 知识库 Provider 的宿主。
/// </summary>
internal static class VectorDataTestHost
{
    /// <summary>
    /// 嵌入维度。
    /// </summary>
    public const int Dimensions = 64;

    /// <summary>
    /// 启动。
    /// </summary>
    /// <remarks>
    /// <paramref name="store"/> 为空时不注册向量存储。
    /// </remarks>
    public static Task<TestHost> StartAsync(VectorStore? store, params (string Key, string Value)[] extra)
    {
        (string Key, string Value)[] configuration =
        [
            ($"{AIOptions.ConfigurationSection}:Providers:hash:Kind", HashingEmbeddingGeneratorFactory.KindName),
            ($"{AIOptions.ConfigurationSection}:Providers:hash:EmbeddingDimensions", Dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ($"{AIOptions.ConfigurationSection}:Knowledge:Dimensions", Dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ($"{AIOptions.ConfigurationSection}:Knowledge:ChunkSize", "60"),
            ($"{AIOptions.ConfigurationSection}:Knowledge:ChunkOverlap", "0"),
        ];
        return TestHost.StartAsync(
            static b => b.UseFeature<HashingEmbeddingFeature>().UseFeature<VectorDataKnowledgeFeature>(),
            configuration.Concat(extra).Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)),
            services =>
            {
                if (store is not null)
                {
                    services.AddSingleton(store);
                }
            });
    }
}
