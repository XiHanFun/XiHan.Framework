// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 参考实现认领的一致性档里，每个向量都有测试回放或列为缺口
/// </summary>
/// <remarks>
/// 档表是规范的 <c>conformance/profiles.json</c>（RFC-0024「一致性档」）。参考实现认领 <see cref="_unclaimed"/> 之外的全部档。回放以测试源码里出现文件名的字符串字面量为准（如 <c>"handshake-lines.json"</c>、<c>"manifest/manifests.json"</c>）。认领的档里新增向量而没人回放、缺口有了回放、缺口不在认领的档里，都在这里失败（ADR-0242、ADR-0243）
/// </remarks>
public sealed partial class SpecVectorCoverageTests
{
    /// <summary>
    /// 参考实现不认领的档
    /// </summary>
    /// <remarks>
    /// <c>plugin</c> 是插件一侧：.NET 是宿主，示例插件不经插件 SDK。其余档（含以后新增的子系统档与 Provider 档）都认领
    /// </remarks>
    private static readonly string[] _unclaimed = ["plugin"];

    /// <summary>
    /// 认领的档里没有回放的向量与理由
    /// </summary>
    /// <remarks>
    /// 路径相对 <c>conformance/vectors</c>，值是理由；与规范 README 生态一节列的缺口一致。长期的缺口没有：最后一个 <c>primitives/wire-encoding.json</c> 由内核的 <c>ResultJson</c> 回放（ADR-0244）。现在登记的一项是过渡：<c>caching/cache-keys.json</c> 要求缓存键按 UTF-8 字节计（ADR-0276 决策 1），<c>CacheKey</c> 的改动与回放随 ADR-0274 落地；那边的回放一合进来，本测试就以「缺口有了回放」失败，届时删掉这一项
    /// </remarks>
    private static readonly Dictionary<string, string> _gaps = new(StringComparer.Ordinal)
    {
        ["caching/cache-keys.json"] = "Cache keys are still counted in UTF-16 code units; 'CacheKey' switches to UTF-8 bytes and gains the replay with ADR-0274.",
    };

    [Fact]
    public void EveryVectorOfTheClaimedProfiles_IsReplayedOrAGap()
    {
        var vectors = Path.Combine(Workspace.SpecRoot, "conformance", "vectors");
        var files = Directory.EnumerateFiles(vectors, "*.json", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(vectors, file).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(files.Count >= 20, $"Only {files.Count} vector files were found under '{vectors}'; is the spec submodule checked out?");

        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(Workspace.SpecRoot, "conformance", "profiles.json")));
        var profiles = table.RootElement.GetProperty("profiles").EnumerateArray().ToList();
        var ids = profiles.Select(static p => p.GetProperty("id").GetString()!).ToList();
        var unknown = _unclaimed.Where(id => !ids.Contains(id, StringComparer.Ordinal)).ToList();
        Assert.True(unknown.Count == 0, "Unclaimed profiles that the spec does not define: " + string.Join(", ", unknown));

        var required = profiles
            .Where(p => !_unclaimed.Contains(p.GetProperty("id").GetString(), StringComparer.Ordinal))
            .SelectMany(static p => p.GetProperty("vectors").EnumerateArray().Select(static v => v.GetString()!))
            .ToHashSet(StringComparer.Ordinal);

        // 测试里常只写文件名（目录由夹具拼），所以按文件名匹配，文件名在各目录间必须唯一
        var ambiguous = files.GroupBy(static file => Path.GetFileName(file), StringComparer.Ordinal).Where(static g => g.Count() > 1).Select(static g => g.Key).ToList();
        Assert.True(ambiguous.Count == 0, "Vector file names that appear in more than one directory: " + string.Join(", ", ambiguous));

        // 本文件的缺口表里也写着文件名，不能算作回放
        var self = Path.GetFullPath(Path.Combine(Workspace.TestRoot, "XiHan.Framework.Architecture.Tests", "SpecVectorCoverageTests.cs"));
        var replayed = Workspace.SourceFiles(Workspace.TestRoot)
            .Where(file => !string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase))
            .SelectMany(static file => VectorLiteral().Matches(File.ReadAllText(file)).Select(static m => m.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        var missing = required.Where(file => !replayed.Contains(Path.GetFileName(file)) && !_gaps.ContainsKey(file)).Order(StringComparer.Ordinal).ToList();
        var outside = _gaps.Keys.Where(file => !required.Contains(file)).Order(StringComparer.Ordinal).ToList();
        var replayedGaps = _gaps.Keys.Where(file => replayed.Contains(Path.GetFileName(file))).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, "Vector files of the claimed profiles that no test replays and that are not listed as gaps: " + string.Join(", ", missing));
        Assert.True(outside.Count == 0, "Gaps outside the claimed profiles: " + string.Join(", ", outside));
        Assert.True(replayedGaps.Count == 0, "Gaps that a test replays; remove them from the gaps: " + string.Join(", ", replayedGaps));
    }

    /// <summary>
    /// 测试源码里引用向量文件的字符串字面量，可带一级目录
    /// </summary>
    [GeneratedRegex("\"(?:[a-z-]+/)?(?<name>[a-z0-9-]+\\.json)\"")]
    private static partial Regex VectorLiteral();
}
