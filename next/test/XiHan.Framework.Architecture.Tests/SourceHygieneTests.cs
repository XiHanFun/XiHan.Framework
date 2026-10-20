// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 源码门禁：版权头、Ring 0/1 零反射、每个正式包的必备文件。
/// </summary>
public sealed class SourceHygieneTests
{
    private const string HeaderLine1 = "// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.";
    private const string HeaderLine2 = "// See the LICENSE file in the project root for full license text.";

    private static readonly string[] ReflectionTokens =
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

    private static readonly string[] RequiredFiles = ["README.md", "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"];

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

        Assert.True(failures.Count == 0, "缺少标准版权头：" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PrimitivesAndKernel_DoNotUseReflection()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects.Where(static p => PackageGrammar.Ring(p.PackageId) <= 1))
        {
            foreach (var file in Workspace.SourceFiles(project.Directory))
            {
                var text = File.ReadAllText(file);
                foreach (var token in ReflectionTokens)
                {
                    if (text.Contains(token, StringComparison.Ordinal))
                    {
                        failures.Add($"{Workspace.Relative(file)} 使用了 {token}");
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
            foreach (var required in RequiredFiles)
            {
                if (!File.Exists(Path.Combine(project.Directory, required)))
                {
                    failures.Add($"{project.PackageId} 缺少 {required}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
