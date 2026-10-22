// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Xml;
using System.Xml.Linq;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 定位 next 工作区并枚举正式包。所有架构测试都以文件系统为事实源，不依赖已编译的程序集。
/// </summary>
internal static class Workspace
{
    /// <summary>
    /// 正式包 ID 的公共前缀。
    /// </summary>
    public const string PackagePrefix = "XiHan.Framework.";

    /// <summary>
    /// next 工作区根目录。
    /// </summary>
    public static string Root { get; } = Locate();

    /// <summary>
    /// 正式包所在目录。
    /// </summary>
    public static string SourceRoot => Path.Combine(Root, "src");

    /// <summary>
    /// 测试项目所在目录。
    /// </summary>
    public static string TestRoot => Path.Combine(Root, "test");

    /// <summary>
    /// 规范所在目录。
    /// </summary>
    public static string SpecRoot => Path.Combine(Root, "spec");

    /// <summary>
    /// 全部正式包，按包 ID 排序。
    /// </summary>
    public static IReadOnlyList<SourceProject> SourceProjects { get; } = LoadSourceProjects();

    /// <summary>
    /// 枚举目录下的全部 C# 源文件，跳过构建输出。
    /// </summary>
    public static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Where(static f => !IsBuildOutput(f));

    /// <summary>
    /// 安全地加载 XML 文件：禁止 DTD 与外部解析，避免项目文件被当作攻击面。
    /// </summary>
    public static XDocument LoadXml(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }

    /// <summary>
    /// 相对于工作区根的路径，统一使用正斜杠以便跨平台比较与输出。
    /// </summary>
    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    /// <summary>
    /// 从测试输出目录向上查找 next 工作区根（以 <c>XiHan.Framework.Next.slnx</c> 为标记）。
    /// </summary>
    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate 'XiHan.Framework.Next.slnx'; the architecture tests must run inside the next workspace.");
    }

    /// <summary>
    /// 枚举 <c>src</c> 下每个目录中与目录同名的项目文件；没有同名项目文件的目录不算正式包。
    /// </summary>
    private static List<SourceProject> LoadSourceProjects()
    {
        var projects = new List<SourceProject>();
        foreach (var directory in Directory.EnumerateDirectories(SourceRoot))
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
    /// 路径是否位于 <c>obj</c> 或 <c>bin</c> 目录内。
    /// </summary>
    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }
}
