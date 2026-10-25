// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 命名门禁：包 ID 文法、命名空间与包 ID 一致、类型名不带前缀、术语表自身合法。
/// </summary>
public sealed partial class NamingTests
{
    [Fact]
    public void SourceProjects_IncludePrimitivesAndKernel()
    {
        var ids = Workspace.SourceProjects.Select(static p => p.PackageId).ToList();

        Assert.Contains("XiHan.Framework.Primitives", ids);
        Assert.Contains("XiHan.Framework.Kernel", ids);
    }

    [Fact]
    public void Vocabulary_IsWellFormed()
    {
        var failures = new List<string>();

        // 术语表每行的能力 ID 前缀、配置节与错误码域都必须能从领域词机械推出；"—" 表示该领域没有对应形态。
        foreach (var entry in Vocabulary.Entries)
        {
            if (!PascalWord().IsMatch(entry.Word))
            {
                failures.Add($"Domain word '{entry.Word}' is not PascalCase.");
            }

            if (entry.CapabilityPrefix != "—" && !string.Equals(entry.CapabilityPrefix, entry.Word.ToLowerInvariant(), StringComparison.Ordinal))
            {
                failures.Add($"The capability ID prefix of '{entry.Word}' should be '{entry.Word.ToLowerInvariant()}', but it is '{entry.CapabilityPrefix}'.");
            }

            if (entry.ConfigSection != "—" && !string.Equals(entry.ConfigSection, "XiHan:" + entry.Word, StringComparison.Ordinal))
            {
                failures.Add($"The configuration section of '{entry.Word}' should be 'XiHan:{entry.Word}', but it is '{entry.ConfigSection}'.");
            }

            if (entry.ErrorDomain != "—" && !string.Equals(entry.ErrorDomain, entry.Word.ToUpperInvariant(), StringComparison.Ordinal))
            {
                failures.Add($"The error code domain of '{entry.Word}' should be '{entry.Word.ToUpperInvariant()}', but it is '{entry.ErrorDomain}'.");
            }
        }

        Assert.True(Vocabulary.Entries.Count >= 20, $"The vocabulary has only {Vocabulary.Entries.Count} entries, which suggests the table failed to parse: '{Vocabulary.FilePath}'.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PackageIds_FollowGrammar()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects)
        {
            if (!PackageGrammar.TryValidate(project.PackageId, Vocabulary.SubsystemWords, out var reason))
            {
                failures.Add($"'{project.PackageId}': {reason}");
            }

            var folder = Path.GetFileName(project.Directory);
            if (!string.Equals(folder, project.PackageId, StringComparison.Ordinal))
            {
                failures.Add($"'{project.PackageId}': directory name '{folder}' must equal the package ID.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Namespaces_MatchFolders()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.AllProjects)
        {
            foreach (var file in Workspace.SourceFiles(project.Directory).Where(static f => !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)))
            {
                // 目录即子命名空间：包根目录的文件在包命名空间，一级子目录的文件在同名子命名空间，不允许更深的目录。
                var relative = Path.GetRelativePath(project.Directory, file).Replace('\\', '/');
                var segments = relative.Split('/');
                var expected = segments.Length switch
                {
                    1 => project.PackageId,
                    2 => project.PackageId + "." + segments[0],
                    _ => null,
                };
                if (expected is null)
                {
                    failures.Add($"'{Workspace.Relative(file)}': source files may sit at the package root or in exactly one area folder.");
                    continue;
                }

                foreach (Match match in TopLevelNamespaceDeclaration().Matches(File.ReadAllText(file)))
                {
                    var ns = match.Groups["ns"].Value;
                    if (!string.Equals(ns, expected, StringComparison.Ordinal))
                    {
                        failures.Add($"'{Workspace.Relative(file)}': namespace '{ns}' must be '{expected}' to match its folder.");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Namespaces_EqualPackageIdWithAtMostOneSegment()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.AllProjects)
        {
            foreach (var file in Workspace.SourceFiles(project.Directory).Where(static f => !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (Match match in TopLevelNamespaceDeclaration().Matches(File.ReadAllText(file)))
                {
                    // 命名空间要么等于包 ID，要么只多一层 PascalCase 子段。
                    var ns = match.Groups["ns"].Value;
                    var ok = string.Equals(ns, project.PackageId, StringComparison.Ordinal)
                        || (ns.StartsWith(project.PackageId + ".", StringComparison.Ordinal)
                            && PascalWord().IsMatch(ns[(project.PackageId.Length + 1)..]));

                    if (!ok)
                    {
                        failures.Add($"'{Workspace.Relative(file)}': namespace '{ns}' must equal '{project.PackageId}' or be exactly one segment below it.");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PublicTypes_DoNotCarryXiHanPrefix()
    {
        var failures = new List<string>();

        foreach (var assembly in LoadSourceAssemblies())
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.Name.StartsWith("XiHan", StringComparison.Ordinal))
                {
                    failures.Add($"'{assembly.GetName().Name}': type name '{type.FullName}' must not carry the 'XiHan' prefix.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 加载全部正式包的程序集；分析器与生成器面向编译器运行，不加载。
    /// </summary>
    internal static IEnumerable<Assembly> LoadSourceAssemblies()
    {
        foreach (var project in Workspace.SourceProjects)
        {
            if (project.PackageId.EndsWith(".Analyzers", StringComparison.Ordinal) || project.PackageId.EndsWith(".Generators", StringComparison.Ordinal))
            {
                continue;
            }

            yield return Assembly.Load(new AssemblyName(project.PackageId));
        }
    }

    /// <summary>
    /// PascalCase 标识符的正则。
    /// </summary>
    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalWord();

    /// <summary>
    /// 匹配列首的文件级或块级 <c>namespace</c> 声明并捕获名称；测试里源码片段中缩进的声明不算。
    /// </summary>
    [GeneratedRegex(@"^namespace\s+(?<ns>[A-Za-z0-9_.]+)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex TopLevelNamespaceDeclaration();
}
