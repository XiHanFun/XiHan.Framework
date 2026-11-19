// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 规范的每个向量文件都有测试回放，或在这里写明参考实现不回放的理由
/// </summary>
/// <remarks>
/// 回放以测试源码里出现文件名的字符串字面量为准（如 <c>"handshake-lines.json"</c>、<c>"manifest/manifests.json"</c>）。规范新增向量而没人回放、豁免了却又有测试回放、豁免的文件规范里已没有，都在这里失败（ADR-0242）
/// </remarks>
public sealed partial class SpecVectorCoverageTests
{
    /// <summary>
    /// 参考实现不回放的向量（相对 <c>conformance/vectors</c>）与理由
    /// </summary>
    private static readonly Dictionary<string, string> _exempted = new(StringComparer.Ordinal)
    {
        ["primitives/wire-encoding.json"] = "The primitives layer has no JSON codec for Result and Error; on the wire .NET carries errors as protobuf Status and Problem Details.",
        ["plugin/callback-status.json"] = "The mapping of callback failures is plugin-side; .NET is the host, and its sample plugins do not use a plugin SDK.",
    };

    [Fact]
    public void EveryVectorFile_IsReplayedOrExempted()
    {
        var directory = Path.Combine(Workspace.SpecRoot, "conformance", "vectors");
        var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(directory, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.True(files.Count >= 20, $"Only {files.Count} vector files were found under '{directory}'; is the spec submodule checked out?");

        // 测试里常只写文件名（目录由夹具拼），所以按文件名匹配，文件名在各目录间必须唯一
        var ambiguous = files.GroupBy(static file => Path.GetFileName(file), StringComparer.Ordinal).Where(static g => g.Count() > 1).Select(static g => g.Key).ToList();
        Assert.True(ambiguous.Count == 0, "Vector file names that appear in more than one directory: " + string.Join(", ", ambiguous));

        // 本文件的豁免表里也写着文件名，不能算作回放
        var self = Path.GetFullPath(Path.Combine(Workspace.TestRoot, "XiHan.Framework.Architecture.Tests", "SpecVectorCoverageTests.cs"));
        var replayed = Workspace.SourceFiles(Workspace.TestRoot)
            .Where(file => !string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase))
            .SelectMany(static file => VectorLiteral().Matches(File.ReadAllText(file)).Select(static m => m.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        var missing = files.Where(file => !replayed.Contains(Path.GetFileName(file)) && !_exempted.ContainsKey(file)).ToList();
        var replayedAndExempted = _exempted.Keys.Where(file => replayed.Contains(Path.GetFileName(file))).Order(StringComparer.Ordinal).ToList();
        var stale = _exempted.Keys.Where(file => !files.Contains(file, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, "Spec vector files that no test replays and that are not exempted: " + string.Join(", ", missing));
        Assert.True(replayedAndExempted.Count == 0, "Exempted vector files that a test replays; remove the exemption: " + string.Join(", ", replayedAndExempted));
        Assert.True(stale.Count == 0, "Exempted vector files that the spec does not have: " + string.Join(", ", stale));
    }

    /// <summary>
    /// 测试源码里引用向量文件的字符串字面量，可带一级目录
    /// </summary>
    [GeneratedRegex("\"(?:[a-z-]+/)?(?<name>[a-z0-9-]+\\.json)\"")]
    private static partial Regex VectorLiteral();
}
