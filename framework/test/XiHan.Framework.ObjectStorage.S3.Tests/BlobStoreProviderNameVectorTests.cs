// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.ObjectStorage.FileSystem;

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

        Assert.Equal(LoadProviders().Order(StringComparer.Ordinal), builtIn.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 读向量文件里的 <c>providers</c>
    /// </summary>
    private static List<string> LoadProviders()
    {
        var path = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "objectstorage", "store-definitions.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.GetProperty("providers").EnumerateArray().Select(name => name.GetString() ?? throw new InvalidOperationException($"Vector file '{path}' lists a provider name that is not a string."))];
    }

    /// <summary>
    /// 从测试输出目录向上查找框架工作区根（以 <c>XiHan.Framework.slnx</c> 为标记）
    /// </summary>
    private static string LocateRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The framework workspace root (the directory of 'XiHan.Framework.slnx') was not found above the test output directory.");
    }
}
