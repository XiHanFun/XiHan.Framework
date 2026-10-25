// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 源码门禁：版权头、Ring 0/1 零反射、每个正式包的必备文件。
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
