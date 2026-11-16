// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Capabilities;

/// <summary>
/// 回放清单读取的一致性向量
/// </summary>
/// <remarks>
/// <c>manifest/manifests.json</c>：哪些清单读得出、哪些必须拒绝（RFC-0002 解析规则 2，ADR-0235）
/// </remarks>
public sealed class ManifestVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ManifestVectors _vectors = SpecVectors.Load<ManifestVectors>("manifest/manifests.json");

    [Fact]
    public void Read_AcceptsEveryValidManifest()
    {
        Assert.NotEmpty(_vectors.Valid);
        var failures = _vectors.Valid
            .Select(static vector => (vector.Name, Result: ManifestReader.Read(Text(vector))))
            .Where(static pair => pair.Result.IsFailure)
            .Select(static pair => $"{pair.Name}: {pair.Result.Error!.Message}")
            .ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Read_RejectsEveryInvalidManifest()
    {
        Assert.NotEmpty(_vectors.Invalid);
        var accepted = _vectors.Invalid
            .Where(static vector => ManifestReader.Read(Text(vector)) is not { IsFailure: true, Error.Code: KernelErrors.InvalidManifest })
            .Select(static vector => $"{vector.Name} ({vector.Reason})")
            .ToList();
        Assert.True(accepted.Count == 0, "accepted: " + string.Join(Environment.NewLine, accepted));
    }

    [Fact]
    public void Write_ReadsBackToTheSameManifest()
    {
        foreach (var vector in _vectors.Valid)
        {
            var written = ManifestReader.Write(ManifestReader.Read(Text(vector)).ValueOrThrow());
            Assert.Equal(written, ManifestReader.Write(ManifestReader.Read(written).ValueOrThrow()));
        }
    }

    /// <summary>
    /// 一份清单的原文：给了 <c>text</c> 时用它，否则把 <c>manifest</c> 写成 JSON
    /// </summary>
    private static string Text(ManifestCase vector)
        => vector.Text ?? vector.Manifest.GetRawText();

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record ManifestVectors(ManifestCase[] Valid, ManifestCase[] Invalid);

    /// <summary>
    /// 一份清单：JSON 值或原文，以及不合法的原因
    /// </summary>
    private sealed record ManifestCase(string Name, JsonElement Manifest, string? Text, string? Reason);
}
