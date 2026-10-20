// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text.RegularExpressions;

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

        foreach (var entry in Vocabulary.Entries)
        {
            if (!PascalWord().IsMatch(entry.Word))
            {
                failures.Add($"领域词 {entry.Word} 不是 PascalCase");
            }

            if (entry.CapabilityPrefix != "—" && !string.Equals(entry.CapabilityPrefix, entry.Word.ToLowerInvariant(), StringComparison.Ordinal))
            {
                failures.Add($"{entry.Word} 的能力 ID 前缀应为 {entry.Word.ToLowerInvariant()}，实际 {entry.CapabilityPrefix}");
            }

            if (entry.ConfigSection != "—" && !string.Equals(entry.ConfigSection, "XiHan:" + entry.Word, StringComparison.Ordinal))
            {
                failures.Add($"{entry.Word} 的配置节应为 XiHan:{entry.Word}，实际 {entry.ConfigSection}");
            }

            if (entry.ErrorDomain != "—" && !string.Equals(entry.ErrorDomain, entry.Word.ToUpperInvariant(), StringComparison.Ordinal))
            {
                failures.Add($"{entry.Word} 的错误码域应为 {entry.Word.ToUpperInvariant()}，实际 {entry.ErrorDomain}");
            }
        }

        Assert.True(Vocabulary.Entries.Count >= 20, $"术语表只有 {Vocabulary.Entries.Count} 条，疑似解析失败：{Vocabulary.FilePath}");
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
                failures.Add($"{project.PackageId}：{reason}");
            }

            var folder = Path.GetFileName(project.Directory);
            if (!string.Equals(folder, project.PackageId, StringComparison.Ordinal))
            {
                failures.Add($"{project.PackageId}：目录名 {folder} 必须等于包 ID");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Namespaces_EqualPackageIdWithAtMostOneSegment()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects)
        {
            foreach (var file in Workspace.SourceFiles(project.Directory))
            {
                foreach (Match match in NamespaceDeclaration().Matches(File.ReadAllText(file)))
                {
                    var ns = match.Groups["ns"].Value;
                    var ok = string.Equals(ns, project.PackageId, StringComparison.Ordinal)
                        || (ns.StartsWith(project.PackageId + ".", StringComparison.Ordinal)
                            && PascalWord().IsMatch(ns[(project.PackageId.Length + 1)..]));

                    if (!ok)
                    {
                        failures.Add($"{Workspace.Relative(file)}：命名空间 {ns} 必须等于 {project.PackageId} 或其下一层");
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
                    failures.Add($"{assembly.GetName().Name}：{type.FullName} 类型名不得带 XiHan 前缀");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

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

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalWord();

    [GeneratedRegex(@"^\s*namespace\s+(?<ns>[A-Za-z0-9_.]+)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();
}
