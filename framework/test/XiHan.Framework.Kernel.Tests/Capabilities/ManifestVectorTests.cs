// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Kernel.Tests.Capabilities;

/// <summary>
/// 回放清单读取的一致性向量
/// </summary>
/// <remarks>
/// <c>manifest/manifests.json</c>：哪些清单读得出、写出的全文，哪些必须拒绝、报什么错误（RFC-0002 解析规则 2 与「写出」，ADR-0235、ADR-0236）
/// </remarks>
public sealed class ManifestVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ManifestVectors _vectors = SpecVectors.Read<ManifestVectors>("manifest", "manifests.json");

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
    public void Read_RejectsEveryInvalidManifestWithTheExpectedError()
    {
        Assert.NotEmpty(_vectors.Invalid);
        var failures = new List<string>();
        foreach (var vector in _vectors.Invalid)
        {
            var result = ManifestReader.Read(Text(vector));
            if (result.IsSuccess)
            {
                failures.Add($"{vector.Name}: accepted");
                continue;
            }

            // 码、消息键与参数逐字相等（RFC-0002 解析规则 2，ADR-0236）
            var error = result.Error!;
            var arguments = (error.Details ?? new Dictionary<string, object?>()).ToDictionary(static p => p.Key, static p => p.Value?.ToString());
            if (error.Code != vector.Error!.Code || error.MessageKey != vector.Error.MessageKey || !arguments.OrderBy(static p => p.Key, StringComparer.Ordinal).SequenceEqual(vector.Error.Arguments.OrderBy(static p => p.Key, StringComparer.Ordinal)))
            {
                failures.Add($"{vector.Name}: {error.Code} {error.MessageKey} {string.Join(", ", arguments)}, expected {vector.Error.Code} {vector.Error.MessageKey} {string.Join(", ", vector.Error.Arguments)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Write_MatchesTheWrittenTextExactly()
    {
        foreach (var vector in _vectors.Valid)
        {
            var written = ManifestReader.Write(ManifestReader.Read(Text(vector)).ValueOrThrow());
            Assert.Equal(vector.Written, written);
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
    /// 一份清单：JSON 值或原文，合法时再写出的全文，不合法时的原因与期望的错误
    /// </summary>
    private sealed record ManifestCase(string Name, JsonElement Manifest = default, string? Text = null, string? Reason = null, string? Written = null, ManifestError? Error = null);

    /// <summary>
    /// 期望的错误：码、消息键与参数
    /// </summary>
    private sealed record ManifestError(string Code, string MessageKey, Dictionary<string, string?> Arguments);
}
