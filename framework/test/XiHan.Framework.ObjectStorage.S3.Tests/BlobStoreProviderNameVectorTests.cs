// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.ObjectStorage.FileSystem;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 回放 <c>store-definitions.json</c> 的 <c>providers</c>：内置 Provider 的名字
/// </summary>
/// <remarks>
/// 规范 RFC-0012「命名存储实例」（ADR-0050、ADR-0277）。两个名字分在文件系统与 S3 两个 Provider 包里，本项目同时引用它们；文件的其余部分（实例名的文法、定义的相等）由 <c>ObjectStorage.Abstractions.Tests</c> 的 <c>BlobStoreDefinitionVectorTests</c> 回放
/// </remarks>
public sealed class BlobStoreProviderNameVectorTests
{
    [Fact]
    public void TheBuiltInProviderNames_AreTheOnesInTheVectors()
    {
        string[] builtIn = [FileSystemBlobStoreProvider.ProviderName, S3BlobStoreProvider.ProviderName];
        var vectors = SpecVectors.Read<StoreDefinitionVectors>("objectstorage", "store-definitions.json");

        Assert.Equal(vectors.Providers.Order(StringComparer.Ordinal), builtIn.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <remarks>
    /// 这个类只回放 <c>providers</c>，其余两项由 <c>BlobStoreDefinitionVectorTests</c> 按它的模型回放，这里原样留着
    /// </remarks>
    /// <param name="Names">实例名的用例</param>
    /// <param name="Providers">内置的 Provider 名</param>
    /// <param name="Same">两个定义是不是同一个定义的用例</param>
    private sealed record StoreDefinitionVectors(JsonElement Names, string[] Providers, JsonElement Same);
}
