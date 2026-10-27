// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 源码门禁：版权头、XML 文档 <c>&lt;summary&gt;</c> 的三行写法、Ring 0/1 零反射、每个正式包的必备文件。
/// </summary>
public sealed class SourceHygieneTests
{
    /// <summary>
    /// 标准版权头第一行。
    /// </summary>
    private const string HeaderLine1 = "// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.";

    /// <summary>
    /// 标准版权头第二行。
    /// </summary>
    private const string HeaderLine2 = "// See the LICENSE file in the project root for full license text.";

    /// <summary>
    /// 源码中出现即视为使用反射的文本片段。
    /// </summary>
    private static readonly string[] _reflectionTokens =
    [
        "using System.Reflection",
        "Activator.",
        "Assembly.Load",
        "Type.GetType(",
        ".GetMethod(",
        ".GetMethods(",
        ".GetProperty(",
        ".GetProperties(",
        ".GetField(",
        ".GetFields(",
        ".GetConstructor(",
        ".GetConstructors(",
        "MakeGenericType(",
        "MakeGenericMethod(",
    ];

    /// <summary>
    /// 每个正式包目录必须存在的文件。
    /// </summary>
    private static readonly string[] _requiredFiles = ["README.md", "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"];

    [Fact]
    public void SourceFiles_StartWithCopyrightHeader()
    {
        var failures = new List<string>();

        foreach (var file in Workspace.SourceFiles(Workspace.SourceRoot).Concat(Workspace.SourceFiles(Workspace.TestRoot)))
        {
            var lines = File.ReadLines(file).Take(2).ToList();
            if (lines.Count < 2 || lines[0] != HeaderLine1 || lines[1] != HeaderLine2)
            {
                failures.Add(Workspace.Relative(file));
            }
        }

        Assert.True(failures.Count == 0, "Files missing the standard copyright header:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void XmlSummaries_UseTheThreeLineForm()
    {
        var failures = new List<string>();

        // <summary> 一律三行：开标签、文案、闭标签各占一行；单行 /// <summary>文案</summary> 与把文案写在开标签或闭标签同一行的都不收。正式包、测试、示例与基准一视同仁
        foreach (var directory in new[] { Workspace.SourceRoot, Workspace.TestRoot, Path.Combine(Workspace.Root, "samples"), Path.Combine(Workspace.Root, "benchmarks") }.Where(Directory.Exists))
        {
            foreach (var file in Workspace.SourceFiles(directory))
            {
                var number = 0;
                foreach (var line in File.ReadLines(file))
                {
                    number++;
                    var trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("///", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var opensWithText = trimmed.StartsWith("/// <summary>", StringComparison.Ordinal) && trimmed.Length > "/// <summary>".Length;
                    var closesAfterText = trimmed.EndsWith("</summary>", StringComparison.Ordinal) && trimmed != "/// </summary>";
                    if (opensWithText || closesAfterText)
                    {
                        failures.Add($"'{Workspace.Relative(file)}:{number}' must write <summary> on three lines: '/// <summary>', '/// text', '/// </summary>'.");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PrimitivesAndKernel_DoNotUseReflection()
    {
        var failures = new List<string>();

        // 以文本匹配而不是编译后扫描：注释里出现这些片段同样算违规，避免被注释掉的反射代码留在仓库里。
        foreach (var project in Workspace.SourceProjects.Where(static p => PackageGrammar.Ring(p.PackageId) <= 1))
        {
            foreach (var file in Workspace.SourceFiles(project.Directory))
            {
                var text = File.ReadAllText(file);
                foreach (var token in _reflectionTokens)
                {
                    if (text.Contains(token, StringComparison.Ordinal))
                    {
                        failures.Add($"'{Workspace.Relative(file)}' uses '{token}'.");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EverySourceProject_HasReadmeAndPublicApiFiles()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects)
        {
            foreach (var required in _requiredFiles)
            {
                if (!File.Exists(Path.Combine(project.Directory, required)))
                {
                    failures.Add($"'{project.PackageId}' is missing '{required}'.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
