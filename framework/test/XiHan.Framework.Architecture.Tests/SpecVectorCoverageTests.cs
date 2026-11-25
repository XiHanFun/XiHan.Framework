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
/// 档表是规范的 <c>conformance/profiles.json</c>（RFC-0024「一致性档」）。参考实现认领 <see cref="_unclaimed"/> 之外的全部档。回放以测试源码里 <c>SpecVectors.Read&lt;T&gt;("&lt;目录&gt;", "&lt;文件&gt;")</c> 与 <c>SpecVectors.PathOf("&lt;目录&gt;", "&lt;文件&gt;")</c> 的调用为准，目录与文件名都要是字面量，按（目录, 文件）成对识别（ADR-0300）；按 <c>JsonDocument</c> 自己读的测试也经 <c>PathOf</c> 定位。认领的档里新增向量而没人回放、缺口有了回放、缺口不在认领的档里、调用处的目录下没有那个文件，都在这里失败（ADR-0242、ADR-0243）
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
    /// 路径相对 <c>conformance/vectors</c>，值是理由；与规范 README 生态一节列的缺口一致。现在没有缺口：最后一个 <c>primitives/wire-encoding.json</c> 由内核的 <c>ResultJson</c> 回放（ADR-0244）
    /// </remarks>
    private static readonly Dictionary<string, string> _gaps = new(StringComparer.Ordinal);

    [Fact]
    public void EveryVectorOfTheClaimedProfiles_IsReplayedOrAGap()
    {
        var files = VectorFiles();
        Assert.True(files.Count >= 20, $"Only {files.Count} vector files were found under '{VectorsRoot}'; is the spec submodule checked out?");

        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(Workspace.SpecRoot, "conformance", "profiles.json")));
        var profiles = table.RootElement.GetProperty("profiles").EnumerateArray().ToList();
        var ids = profiles.Select(static p => p.GetProperty("id").GetString()!).ToList();
        var unknown = _unclaimed.Where(id => !ids.Contains(id, StringComparer.Ordinal)).ToList();
        Assert.True(unknown.Count == 0, "Unclaimed profiles that the spec does not define: " + string.Join(", ", unknown));

        var required = profiles
            .Where(p => !_unclaimed.Contains(p.GetProperty("id").GetString(), StringComparer.Ordinal))
            .SelectMany(static p => p.GetProperty("vectors").EnumerateArray().Select(static v => v.GetString()!))
            .ToHashSet(StringComparer.Ordinal);

        // 只认目录下真有的文件：目录写错的调用由 VectorCalls_NameAFileOfThatDirectory 报，这里不算回放
        var replayed = VectorCalls().Select(static call => call.Vector).Where(files.Contains).ToHashSet(StringComparer.Ordinal);

        var missing = required.Where(file => !replayed.Contains(file) && !_gaps.ContainsKey(file)).Order(StringComparer.Ordinal).ToList();
        var outside = _gaps.Keys.Where(file => !required.Contains(file)).Order(StringComparer.Ordinal).ToList();
        var replayedGaps = _gaps.Keys.Where(replayed.Contains).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, "Vector files of the claimed profiles that no test replays and that are not listed as gaps: " + string.Join(", ", missing));
        Assert.True(outside.Count == 0, "Gaps outside the claimed profiles: " + string.Join(", ", outside));
        Assert.True(replayedGaps.Count == 0, "Gaps that a test replays; remove them from the gaps: " + string.Join(", ", replayedGaps));
    }

    [Fact]
    public void VectorCalls_NameAFileOfThatDirectory()
    {
        var files = VectorFiles();
        Assert.True(files.Count >= 20, $"Only {files.Count} vector files were found under '{VectorsRoot}'; is the spec submodule checked out?");

        // 文件名只在别的目录里有时说出在哪：多半是目录写错了，按文件名识别时这种调用照样算作回放
        var failures = VectorCalls()
            .Where(call => !files.Contains(call.Vector))
            .Select(call =>
            {
                var elsewhere = files.Where(file => string.Equals(Path.GetFileName(file), Path.GetFileName(call.Vector), StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
                var hint = elsewhere.Count == 0 ? "no vector has that file name" : "the file is at " + string.Join(", ", elsewhere.Select(static file => $"'{file}'"));
                return $"'{call.Source}' reads '{call.Vector}', which does not exist; {hint}.";
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 向量根目录
    /// </summary>
    private static string VectorsRoot => Path.Combine(Workspace.SpecRoot, "conformance", "vectors");

    /// <summary>
    /// 全部向量文件，路径相对向量根目录、用正斜杠
    /// </summary>
    private static HashSet<string> VectorFiles()
        => Directory.EnumerateFiles(VectorsRoot, "*.json", SearchOption.AllDirectories)
            .Select(static file => Path.GetRelativePath(VectorsRoot, file).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 测试源码里以字面量给出目录与文件名的向量调用
    /// </summary>
    /// <remarks>
    /// 本文件的说明里也写着调用的样子，不算
    /// </remarks>
    private static List<VectorCall> VectorCalls()
    {
        var self = Path.GetFullPath(Path.Combine(Workspace.TestRoot, "XiHan.Framework.Architecture.Tests", "SpecVectorCoverageTests.cs"));
        return Workspace.SourceFiles(Workspace.TestRoot)
            .Where(file => !string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase))
            .SelectMany(static file => VectorCallPattern().Matches(File.ReadAllText(file)).Select(m => new VectorCall(Workspace.Relative(file), m.Groups["area"].Value + "/" + m.Groups["file"].Value)))
            .ToList();
    }

    /// <summary>
    /// <c>SpecVectors.Read&lt;T&gt;</c> 或 <c>SpecVectors.PathOf</c> 以两个字面量开头的调用
    /// </summary>
    /// <remarks>
    /// 第二个字面量之后只能是右括号或逗号（带源生成上下文的重载）；参数是变量或常量的调用认不出，那样的回放要另有一处字面量调用，否则按没有回放失败
    /// </remarks>
    [GeneratedRegex("""\bSpecVectors\s*\.\s*(?:Read\s*<[^()]*?>|PathOf)\s*\(\s*"(?<area>[^"\\]*)"\s*,\s*"(?<file>[^"\\]*)"\s*[,)]""")]
    private static partial Regex VectorCallPattern();

    /// <summary>
    /// 一处向量调用：所在的源文件与（目录, 文件）
    /// </summary>
    /// <param name="Source">源文件，路径相对工作区根</param>
    /// <param name="Vector">调用读的向量，路径相对向量根目录：<c>&lt;目录&gt;/&lt;文件&gt;</c></param>
    private sealed record VectorCall(string Source, string Vector);
}
