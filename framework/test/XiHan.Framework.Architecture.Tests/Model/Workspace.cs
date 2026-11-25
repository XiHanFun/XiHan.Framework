// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Xml;
using System.Xml.Linq;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Architecture.Tests.Model;

/// <summary>
/// 定位框架工作区并枚举正式包
/// </summary>
/// <remarks>
/// 所有架构测试都以文件系统为事实源，不依赖已编译的程序集
/// </remarks>
internal static class Workspace
{
    /// <summary>
    /// 正式包 ID 的公共前缀
    /// </summary>
    public const string PackagePrefix = "XiHan.Framework.";

    /// <summary>
    /// 测试项目的名字后缀
    /// </summary>
    /// <remarks>
    /// 与 <c>test/Directory.Build.props</c> 的条件相同：它只给名字以这个后缀结尾的项目设测试项目的属性与包（ADR-0300），<c>TestProjectNamingTests</c> 核对两处一致（ADR-0307）
    /// </remarks>
    public const string TestProjectSuffix = ".Tests";

    /// <summary>
    /// 框架工作区根目录
    /// </summary>
    /// <remarks>
    /// 由 <see cref="FrameworkWorkspace"/> 从测试输出目录向上找，架构测试必须在框架工作区里运行
    /// </remarks>
    public static string Root { get; } = FrameworkWorkspace.Root;

    /// <summary>
    /// 正式包所在目录
    /// </summary>
    public static string SourceRoot => Path.Combine(Root, "src");

    /// <summary>
    /// 测试项目所在目录
    /// </summary>
    public static string TestRoot => Path.Combine(Root, "test");

    /// <summary>
    /// 规范所在目录
    /// </summary>
    public static string SpecRoot => Path.Combine(Root, "spec");

    /// <summary>
    /// 全部正式包，按包 ID 排序
    /// </summary>
    public static IReadOnlyList<SourceProject> SourceProjects { get; } = LoadProjects(SourceRoot);

    /// <summary>
    /// <c>test</c> 下的全部项目（有同名项目文件的目录），按项目名排序
    /// </summary>
    /// <remarks>
    /// 测试项目与给测试项目引用的类库（如 <c>XiHan.Framework.Testing.Workspace</c>）；目录与子命名空间规则对它们同样生效
    /// </remarks>
    public static IReadOnlyList<SourceProject> TestDirectoryProjects { get; } = LoadProjects(TestRoot);

    /// <summary>
    /// 全部测试项目，按项目名排序
    /// </summary>
    /// <remarks>
    /// <see cref="TestDirectoryProjects"/> 里名字以 <see cref="TestProjectSuffix"/> 结尾的，与 <c>test/Directory.Build.props</c> 和 <c>New-TestShard.ps1</c> 的口径相同（ADR-0307）
    /// </remarks>
    public static IReadOnlyList<SourceProject> TestProjects { get; } = TestDirectoryProjects.Where(static p => IsTestProjectName(Path.GetFileNameWithoutExtension(p.ProjectPath))).ToList();

    /// <summary>
    /// 全部示例与基准项目（有同名项目文件的目录），按项目名排序
    /// </summary>
    /// <remarks>
    /// 目录与子命名空间规则对它们同样生效
    /// </remarks>
    public static IReadOnlyList<SourceProject> SampleProjects { get; } = LoadProjects(Path.Combine(Root, "samples")).Concat(LoadProjects(Path.Combine(Root, "benchmarks"))).ToList();

    /// <summary>
    /// 正式包、测试、示例与基准：命名空间门禁检查的全部项目
    /// </summary>
    public static IEnumerable<SourceProject> AllProjects => SourceProjects.Concat(TestDirectoryProjects).Concat(SampleProjects);

    /// <summary>
    /// 项目名是否标明测试项目
    /// </summary>
    /// <param name="projectName">项目名，即项目文件名去掉扩展名（MSBuild 的 <c>MSBuildProjectName</c>）</param>
    public static bool IsTestProjectName(string projectName) => projectName.EndsWith(TestProjectSuffix, StringComparison.Ordinal);

    /// <summary>
    /// 枚举目录下的全部 C# 源文件，跳过构建输出
    /// </summary>
    public static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Where(static f => !IsBuildOutput(f));

    /// <summary>
    /// 枚举目录下任意深度的全部项目文件，跳过构建输出
    /// </summary>
    /// <remarks>
    /// 不像 <see cref="TestDirectoryProjects"/> 那样只认与目录同名的项目文件：放错位置的项目文件也要被看到
    /// </remarks>
    public static IEnumerable<string> ProjectFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories).Where(static f => !IsBuildOutput(f));

    /// <summary>
    /// 安全地加载 XML 文件：禁止 DTD 与外部解析，避免项目文件被当作攻击面
    /// </summary>
    public static XDocument LoadXml(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }

    /// <summary>
    /// 相对于工作区根的路径，统一使用正斜杠以便跨平台比较与输出
    /// </summary>
    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    /// <summary>
    /// 枚举根目录下每个目录中与目录同名的项目文件
    /// </summary>
    /// <remarks>
    /// 没有同名项目文件的目录不算项目
    /// </remarks>
    private static List<SourceProject> LoadProjects(string root)
    {
        var projects = new List<SourceProject>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var csproj = Path.Combine(directory, Path.GetFileName(directory) + ".csproj");
            if (File.Exists(csproj))
            {
                projects.Add(SourceProject.Load(csproj));
            }
        }

        projects.Sort(static (a, b) => string.CompareOrdinal(a.PackageId, b.PackageId));
        return projects;
    }

    /// <summary>
    /// 路径是否位于 <c>obj</c> 或 <c>bin</c> 目录内
    /// </summary>
    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }
}
