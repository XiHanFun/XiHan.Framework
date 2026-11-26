// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// CI 的测试分片表与测试项目、工作流对得上
/// </summary>
/// <remarks>
/// 分片表是 <c>test/shards.json</c>：CI 把测试拆成几个并行作业，每个作业只起自己那一片要的服务、只设自己那一片的门控环境变量。门控用例在变量没设时跳过而不是失败，所以测试项目放错分片不会让 CI 变红，只会让用例不再运行；这里把这种情况变成失败
/// </remarks>
public sealed partial class TestShardTests
{
    /// <summary>
    /// 不列在分片表里的测试项目所属的分片
    /// </summary>
    private const string CoreShard = "core";

    [Fact]
    public void Shards_ListExistingProjectsUnderUniqueNames()
    {
        var shards = LoadShards();
        var known = Workspace.TestProjects.Select(static p => p.PackageId).ToHashSet(StringComparer.Ordinal);

        var duplicated = shards.GroupBy(static s => s.Name, StringComparer.Ordinal).Where(static g => g.Count() > 1).Select(static g => g.Key).ToList();
        Assert.True(duplicated.Count == 0, "Shard names that appear more than once: " + string.Join(", ", duplicated));
        Assert.DoesNotContain(shards, static s => s.Name == CoreShard);

        var missing = shards.SelectMany(s => s.Projects.Where(p => !known.Contains(p)).Select(p => $"{s.Name}: {p}")).ToList();
        Assert.True(missing.Count == 0, "Names listed in 'test/shards.json' that are not test projects under 'test': " + string.Join(", ", missing));

        var empty = shards.Where(static s => s.Projects.Count == 0).Select(static s => s.Name).ToList();
        Assert.True(empty.Count == 0, "Shards without test projects: " + string.Join(", ", empty));
    }

    [Fact]
    public void GatedProjects_RunInAShardThatProvidesWhatTheyNeed()
    {
        var shards = LoadShards();
        var provided = shards.SelectMany(static s => s.Variables.Concat(s.Tools)).ToHashSet(StringComparer.Ordinal);

        var misplaced = new List<string>();
        foreach (var project in Workspace.TestProjects)
        {
            var needs = Workspace.SourceFiles(project.Directory)
                .Select(File.ReadAllText)
                .SelectMany(static text => GateVariable().Matches(text).Select(static m => m.Value).Concat(LaunchedTool().Matches(text).Select(static m => m.Groups["tool"].Value)))
                .Where(provided.Contains)
                .ToHashSet(StringComparer.Ordinal);

            // 没有哪个分片提供的变量（如 Kafka、Nacos、不认条件写入的 S3）在 CI 上本来就不跑，不在这里管
            var own = shards.Where(s => s.Projects.Contains(project.PackageId, StringComparer.Ordinal)).ToList();
            misplaced.AddRange(needs
                .Where(need => !own.Any(s => s.Variables.Contains(need, StringComparer.Ordinal) || s.Tools.Contains(need, StringComparer.Ordinal)))
                .Order(StringComparer.Ordinal)
                .Select(need => $"{project.PackageId} needs '{need}'"));
        }

        Assert.True(misplaced.Count == 0, "Test projects that use a gate variable or a toolchain but are not in a shard of 'test/shards.json' that provides it, so the cases would be skipped on CI: " + string.Join("; ", misplaced));
    }

    [Fact]
    public void Workflow_RunsEveryShardAndSetsItsVariables()
    {
        var workflow = File.ReadAllText(Path.Combine(Workspace.Root, "..", ".github", "workflows", "ci.yml"));

        var absent = new List<string>();
        foreach (var shard in LoadShards().Select(static s => (s.Name, s.Variables)).Append((CoreShard, [])))
        {
            if (!workflow.Contains($"shard: {shard.Name}\n", StringComparison.Ordinal) && !workflow.Contains($"shard: {shard.Name}\r\n", StringComparison.Ordinal))
            {
                absent.Add($"no job runs shard '{shard.Name}'");
            }

            absent.AddRange(shard.Variables.Where(variable => !workflow.Contains($"{variable}: ", StringComparison.Ordinal)).Select(variable => $"'{variable}' of shard '{shard.Name}' is not set"));
        }

        Assert.True(absent.Count == 0, "'.github/workflows/ci.yml' does not match 'test/shards.json': " + string.Join("; ", absent));
    }

    [Fact]
    public void WorkflowFilters_NameTestClassesThatExist()
    {
        // ci.yml 把用例多的一片按 xUnit 的类与方法过滤器拆成几个作业（ADR-0327）：类改了名而过滤器没跟上，排除它的那份照样跑它，选它的那份才失败，这里提前报出来
        var workflow = File.ReadAllText(Path.Combine(Workspace.Root, "..", ".github", "workflows", "ci.yml"));
        var classes = Workspace.TestProjects
            .SelectMany(static p => Workspace.SourceFiles(p.Directory))
            .Select(File.ReadAllText)
            .SelectMany(static text => FileNamespace().Match(text) is { Success: true } scope
                ? DeclaredClass().Matches(text).Select(m => scope.Groups["name"].Value + "." + m.Groups["name"].Value)
                : [])
            .ToHashSet(StringComparer.Ordinal);

        var named = XunitFilter().Matches(workflow)
            .Select(static m => m.Groups["kind"].Value == "class" ? m.Groups["name"].Value : m.Groups["name"].Value[..m.Groups["name"].Value.LastIndexOf('.')])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var unknown = named.Where(name => !classes.Contains(name)).ToList();

        Assert.NotEmpty(named);
        Assert.True(unknown.Count == 0, "'.github/workflows/ci.yml' filters test classes that no test project declares: " + string.Join(", ", unknown));
    }

    /// <summary>
    /// 读分片表
    /// </summary>
    private static List<Shard> LoadShards()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(Workspace.TestRoot, "shards.json")));
        return table.RootElement.GetProperty("shards").EnumerateArray()
            .Select(static s => new Shard(s.GetProperty("name").GetString()!, Strings(s, "variables"), Strings(s, "tools"), Strings(s, "projects")))
            .ToList();
    }

    /// <summary>
    /// 读分片的一个字符串数组成员
    /// </summary>
    private static List<string> Strings(JsonElement shard, string name) => shard.GetProperty(name).EnumerateArray().Select(static v => v.GetString()!).ToList();

    /// <summary>
    /// 测试源码里出现的门控环境变量名
    /// </summary>
    [GeneratedRegex("XIHAN_TEST_[A-Z0-9_]+")]
    private static partial Regex GateVariable();

    /// <summary>
    /// 测试源码里按名字启动的外部工具
    /// </summary>
    [GeneratedRegex("ProcessStartInfo\\(\"(?<tool>[a-z]+)\"")]
    private static partial Regex LaunchedTool();

    /// <summary>
    /// 工作流里传给测试程序的 xUnit 类与方法过滤器
    /// </summary>
    /// <remarks>
    /// 方法过滤器的名字是类的全名加方法名（可以以 <c>*</c> 结尾），取最后一个点之前的部分即类名
    /// </remarks>
    [GeneratedRegex(@"--filter-(?:not-)?(?<kind>class|method) (?<name>[A-Za-z0-9_.]+)\*?")]
    private static partial Regex XunitFilter();

    /// <summary>
    /// 源文件的文件范围命名空间
    /// </summary>
    [GeneratedRegex(@"^namespace (?<name>[A-Za-z0-9_.]+);", RegexOptions.Multiline)]
    private static partial Regex FileNamespace();

    /// <summary>
    /// 源文件里声明的类名
    /// </summary>
    [GeneratedRegex(@"\bclass (?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex DeclaredClass();

    /// <summary>
    /// 分片表的一行
    /// </summary>
    /// <param name="Name">分片名</param>
    /// <param name="Variables">这一片的作业设置的门控环境变量</param>
    /// <param name="Tools">这一片的作业安装的工具链</param>
    /// <param name="Projects">这一片的测试项目</param>
    private sealed record Shard(string Name, List<string> Variables, List<string> Tools, List<string> Projects);
}
