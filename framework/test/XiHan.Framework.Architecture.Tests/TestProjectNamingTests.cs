// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// test 下的项目含测试当且仅当名字以 .Tests 结尾
/// </summary>
/// <remarks>
/// <c>test/Directory.Build.props</c> 只按项目名的 <c>.Tests</c> 后缀设测试项目的属性、包与 <c>using Xunit</c>（ADR-0300），<c>New-TestShard.ps1</c> 也只把这样的项目列进 CI 分片的筛选器。名字写错的测试项目不当测试运行，用例悄悄不跑；这里以项目文件与源码为准核对两个方向，不以 <c>.Tests</c> 结尾的类库只能是 <see cref="_libraries"/> 列出的（ADR-0307）。
/// 「含测试」照仓库的写法认：源码里有 xUnit 的测试方法（行首的 <c>[Fact]</c> / <c>[Theory]</c>），或有类从 <c>src</c> 下的一致性套件派生（套件的测试方法写在 <c>*.Conformance</c> 包里，派生类继承它们）。<c>test</c> 下的项目文件不写 xUnit 的引用，由 <c>test/Directory.Build.props</c> 按名字加，所以项目文件不能用来判定
/// </remarks>
public sealed partial class TestProjectNamingTests
{
    /// <summary>
    /// test/Directory.Build.props 判定测试项目的条件
    /// </summary>
    private const string TestProjectCondition = $"$(MSBuildProjectName.EndsWith('{Workspace.TestProjectSuffix}'))";

    /// <summary>
    /// 不以 .Tests 结尾、给测试项目引用的类库
    /// </summary>
    /// <remarks>
    /// 新加这样的类库要改这里：名单之外的项目名字不以 <c>.Tests</c> 结尾就失败；名单里的项目必须存在、不含测试、名字不以 <c>.Tests</c> 结尾
    /// </remarks>
    private static readonly string[] _libraries = ["XiHan.Framework.Testing.Workspace"];

    [Fact]
    public void DirectoryProps_MarksTestProjectsByTheNameSuffixAlone()
    {
        var props = LoadProps();
        var other = props.Descendants()
            .Select(static e => (string?)e.Attribute("Condition"))
            .OfType<string>()
            .Where(static c => c.Contains("MSBuildProjectName", StringComparison.Ordinal) && !string.Equals(c, TestProjectCondition, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var marked = props.Descendants("IsTestProject").Select(static e => (string?)e.Parent?.Attribute("Condition")).ToList();

        // New-TestShard.ps1 从这些条件里取后缀，条件不止一种写法时它无从取起
        Assert.True(other.Count == 0, $"'test/Directory.Build.props' tests the project name with conditions other than '{TestProjectCondition}': {string.Join(", ", other)}.");
        Assert.True(marked.Count == 1 && string.Equals(marked[0], TestProjectCondition, StringComparison.Ordinal), $"'test/Directory.Build.props' should set 'IsTestProject' once, under the condition '{TestProjectCondition}'.");
        Assert.NotEmpty(TestPackages());
    }

    [Fact]
    public void ProjectFiles_SitInADirectoryOfTheirName()
    {
        // New-TestShard.ps1 与架构测试只认 test/<名>/<名>.csproj：放在别处或文件名与目录不同的项目不进 CI 分片，用例不跑
        var misplaced = Workspace.ProjectFiles(Workspace.TestRoot)
            .Select(Workspace.Relative)
            .Where(static path => path.Split('/') is not ["test", var directory, var file] || !string.Equals(file, directory + ".csproj", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(misplaced.Count == 0, "Project files under 'test' that are not at 'test/<name>/<name>.csproj', so 'New-TestShard.ps1' never lists them: " + string.Join(", ", misplaced));
    }

    [Fact]
    public void ProjectsUnderTest_ContainTestsExactlyWhenNamedAsTestProjects()
    {
        var suites = ConformanceSuites();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var project in Workspace.TestDirectoryProjects)
        {
            var name = Path.GetFileNameWithoutExtension(project.ProjectPath);
            names.Add(name);
            var tests = FindTests(project.Directory, suites);
            var listed = _libraries.Contains(name, StringComparer.Ordinal);

            if (Workspace.IsTestProjectName(name))
            {
                if (tests.Count == 0)
                {
                    failures.Add($"'{name}' ends with '{Workspace.TestProjectSuffix}', so 'test/Directory.Build.props' makes it a test project, but it contains no tests: no source file declares a '[Fact]' or '[Theory]' method and no class derives from a conformance suite. Name a library for test projects without the suffix and add it to '_libraries' of '{nameof(TestProjectNamingTests)}'.");
                }

                if (listed)
                {
                    failures.Add($"'{name}' is in '_libraries' but ends with '{Workspace.TestProjectSuffix}'; remove it from the list.");
                }
            }
            else if (tests.Count > 0)
            {
                // 含测试的项目先报名字：它即使在名单里也不该是类库
                failures.Add($"'{name}' contains tests but its name does not end with '{Workspace.TestProjectSuffix}', so 'test/Directory.Build.props' does not make it a test project and neither 'dotnet test' nor the CI shards run them ({string.Join("; ", tests.Take(3))}). Rename it to end with '{Workspace.TestProjectSuffix}'.");
            }
            else if (!listed)
            {
                failures.Add($"'{name}' does not end with '{Workspace.TestProjectSuffix}' and is not in '_libraries' of '{nameof(TestProjectNamingTests)}'. Name a test project with the suffix; add a library for test projects to the list.");
            }
        }

        // 名单只放还在的类库：类库改名或删掉时名单跟着改，不留一条放行任何项目的空位
        failures.AddRange(_libraries.Where(library => !names.Contains(library)).Select(static library => $"'{library}' is in '_libraries' but there is no such project under 'test'; remove it from the list."));

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ProjectFiles_LeaveTestProjectSettingsToTheDirectoryProps()
    {
        // 测试项目只由名字决定：项目文件自己设 IsTestProject 或引用测试包，会让 dotnet test 与 CI 分片对它的看法分开
        var packages = TestPackages();
        var failures = new List<string>();

        foreach (var file in Workspace.ProjectFiles(Workspace.TestRoot))
        {
            var document = Workspace.LoadXml(file);
            var path = Workspace.Relative(file);
            if (document.Descendants("IsTestProject").Any())
            {
                failures.Add($"'{path}' sets 'IsTestProject'; 'test/Directory.Build.props' decides it from the name suffix '{Workspace.TestProjectSuffix}', so remove it.");
            }

            failures.AddRange(document.Descendants("PackageReference")
                .Select(static e => (string?)e.Attribute("Include") ?? (string?)e.Attribute("Update"))
                .OfType<string>()
                .Where(packages.Contains)
                .Select(package => $"'{path}' references '{package}', which 'test/Directory.Build.props' adds to every project whose name ends with '{Workspace.TestProjectSuffix}'; remove the reference."));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Recognition_FollowsHowTheRepositoryWritesTests()
    {
        string[] methods =
        [
            "    [Fact]",
            "[Theory]",
            "[Fact(Skip = \"Needs a server.\")]",
            "[Xunit.Fact]",
            "[global::Xunit.TheoryAttribute]",
            "[Trait(\"Category\", \"Slow\"), Fact]",
            "[Fact, Trait(\"Category\", \"Slow\")]",
        ];
        string[] others =
        [
            "// [Fact] in a comment",
            "var value = table[Fact];",
            "[InlineData(\"a\", Fact)]",
            "[MemberData(nameof(Theory))]",
            "[FactOfTheDay]",
            "[Facts]",
        ];
        var suites = new HashSet<string>(StringComparer.Ordinal) { "CacheStoreConformance", "IdGeneratorConformance" };
        string[] derived =
        [
            "public sealed class InMemoryCacheStoreTests : CacheStoreConformance",
            "public sealed class SnowflakeTests(ITestOutputHelper output) : IdGeneratorConformance<long>, IAsyncLifetime",
            "internal sealed class Store\n    : CacheStoreConformance\n{",
        ];
        string[] underived =
        [
            "public sealed class CacheStoreConformanceTests : IDisposable",
            "public sealed class Options { public CacheStoreConformance? Suite { get; init; } }",
        ];

        string[] sources =
        [
            "public abstract class BrokerConformance\n{\n    [Fact]\n    public async Task Publishes() { }\n}",
            "public abstract class StreamConformance : BrokerConformance\n{\n}",
            "public abstract class ReplayConformance\n    : StreamConformance, IAsyncLifetime\n{\n}",
            "public abstract class BrokerFixture\n{\n}",
            "public sealed class BrokerProbe\n{\n    [Fact]\n    public void Runs() { }\n}",
        ];

        var missed = methods.Where(static code => !TestMethodAttribute().IsMatch(code)).Concat(derived.Where(code => !DerivedSuites(code, suites).Any())).ToList();
        var flagged = others.Where(static code => TestMethodAttribute().IsMatch(code)).Concat(underived.Where(code => DerivedSuites(code, suites).Any())).ToList();
        var found = ConformanceSuites();

        Assert.True(missed.Count == 0, "Ways of writing tests that are not recognized: " + string.Join(" | ", missed));
        Assert.True(flagged.Count == 0, "Code that is wrongly taken for tests: " + string.Join(" | ", flagged));
        Assert.Equal(["BrokerConformance", "ReplayConformance", "StreamConformance"], ConformanceSuites(sources).Order(StringComparer.Ordinal));
        Assert.True(found.Contains("CacheStoreConformance") && found.Contains("IdGeneratorConformance") && found.Contains("MessageStreamConformance"), "The conformance suites under 'src' were not found, which suggests the scan is broken: " + string.Join(", ", found.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// 读 test/Directory.Build.props
    /// </summary>
    private static XDocument LoadProps() => Workspace.LoadXml(Path.Combine(Workspace.TestRoot, "Directory.Build.props"));

    /// <summary>
    /// test/Directory.Build.props 只给测试项目加的包
    /// </summary>
    /// <remarks>
    /// 包 ID 不分大小写
    /// </remarks>
    private static HashSet<string> TestPackages() =>
        LoadProps().Descendants("ItemGroup")
            .Where(static group => string.Equals((string?)group.Attribute("Condition"), TestProjectCondition, StringComparison.Ordinal))
            .Elements("PackageReference")
            .Select(static e => (string?)e.Attribute("Include"))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// src 下全部一致性套件的类名
    /// </summary>
    private static HashSet<string> ConformanceSuites() => ConformanceSuites(Workspace.SourceFiles(Workspace.SourceRoot).Select(File.ReadAllText));

    /// <summary>
    /// 一组源文件里的一致性套件的类名
    /// </summary>
    /// <remarks>
    /// 套件是声明了测试方法的源文件里的抽象类（一个文件一个类型），以及从套件派生的抽象类：派生的一层（如 <c>MessageStreamConformance</c> 从 <c>MessageBrokerConformance</c> 派生）即使自己不再声明测试方法，也继承了用例
    /// </remarks>
    /// <param name="sources">源文件的内容</param>
    private static HashSet<string> ConformanceSuites(IEnumerable<string> sources)
    {
        var declarations = sources
            .SelectMany(static text => AbstractClass().Matches(text).Select(m => (Name: m.Groups["name"].Value, Bases: Identifiers(m.Groups["bases"].Value), DeclaresTests: TestMethodAttribute().IsMatch(text))))
            .ToList();
        var suites = declarations.Where(static d => d.DeclaresTests).Select(static d => d.Name).ToHashSet(StringComparer.Ordinal);

        // 一层一层往下找派生的抽象类，直到没有新的
        while (true)
        {
            var added = declarations.Where(d => !suites.Contains(d.Name) && d.Bases.Any(suites.Contains)).Select(static d => d.Name).ToList();
            if (added.Count == 0)
            {
                return suites;
            }

            suites.UnionWith(added);
        }
    }

    /// <summary>
    /// 找出目录下的测试：声明测试方法的文件与从套件派生的类
    /// </summary>
    /// <param name="directory">项目目录</param>
    /// <param name="suites">一致性套件的类名</param>
    private static List<string> FindTests(string directory, IReadOnlySet<string> suites)
    {
        var tests = new List<string>();
        foreach (var file in Workspace.SourceFiles(directory).Order(StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file);
            if (TestMethodAttribute().IsMatch(text))
            {
                tests.Add($"'{Workspace.Relative(file)}' declares test methods");
            }

            tests.AddRange(DerivedSuites(text, suites).Distinct(StringComparer.Ordinal).Select(suite => $"'{Workspace.Relative(file)}' derives from the conformance suite '{suite}'"));
        }

        return tests;
    }

    /// <summary>
    /// 源码里的类从哪些套件派生
    /// </summary>
    /// <param name="text">源码</param>
    /// <param name="suites">一致性套件的类名</param>
    private static IEnumerable<string> DerivedSuites(string text, IReadOnlySet<string> suites) =>
        ClassBaseList().Matches(text).SelectMany(static m => Identifiers(m.Groups["bases"].Value)).Where(suites.Contains);

    /// <summary>
    /// 基类列表里的标识符
    /// </summary>
    /// <param name="bases">基类列表的原文</param>
    private static List<string> Identifiers(string bases) => Identifier().Matches(bases).Select(static m => m.Value).ToList();

    /// <summary>
    /// xUnit 的测试方法特性
    /// </summary>
    /// <remarks>
    /// 行首的 <c>[Fact]</c> / <c>[Theory]</c>，可带命名空间、<c>Attribute</c> 后缀与参数，或与别的特性写在同一对方括号里；注释里与索引器里的不算
    /// </remarks>
    [GeneratedRegex(@"^[ \t]*\[(?:[ \t]*[\w.:]+(?:\([^()\r\n]*\))?[ \t]*,)*[ \t]*(?:global::)?(?:Xunit\.)?(?:Fact|Theory)(?:Attribute)?[ \t]*[\](,]", RegexOptions.Multiline)]
    private static partial Regex TestMethodAttribute();

    /// <summary>
    /// 类的声明与它的基类列表
    /// </summary>
    [GeneratedRegex(@"\bclass\s+\w+[^{;:]*:(?<bases>[^{;]*)")]
    private static partial Regex ClassBaseList();

    /// <summary>
    /// 抽象类的声明与它可能有的基类列表
    /// </summary>
    [GeneratedRegex(@"\babstract\s+(?:partial\s+)?class\s+(?<name>\w+)[^{;:]*(?::(?<bases>[^{;]*))?")]
    private static partial Regex AbstractClass();

    /// <summary>
    /// 标识符
    /// </summary>
    [GeneratedRegex(@"\b[A-Za-z_]\w*")]
    private static partial Regex Identifier();
}
