// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 源码门禁。
/// </summary>
/// <remarks>
/// 版权头、XML 文档 <c>&lt;summary&gt;</c> 的三行写法与一句话写法、Ring 0/1 零反射、每个正式包的必备文件。
/// </remarks>
public sealed partial class SourceHygieneTests
{
    /// <summary>
    /// summary 文案的显示宽度上限。
    /// </summary>
    /// <remarks>
    /// 中日韩字符与全角标点算 1，其余（ASCII 等）算 0.5，向上取整：纯中文即 40 字，带类型名的句子不因标识符长而吃亏。
    /// </remarks>
    private const int MaxSummaryWidth = 40;

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
        foreach (var directory in SummaryRoots)
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
    public void XmlSummaries_AreOneConciseSentence()
    {
        var failures = new List<string>();

        // summary 只写一句简明说明：正文一行、显示宽度不超过 MaxSummaryWidth、没有分号与句中的句号。
        // 细节、规则与出处写在 <remarks>，参数、返回与异常写在 <param> / <returns> / <exception>
        foreach (var directory in SummaryRoots)
        {
            foreach (var file in Workspace.SourceFiles(directory))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim() != "/// <summary>")
                    {
                        continue;
                    }

                    var close = i + 1;
                    while (close < lines.Length && lines[close].Trim() != "/// </summary>")
                    {
                        close++;
                    }

                    var location = $"{Workspace.Relative(file)}:{i + 1}";
                    if (close - i - 1 != 1)
                    {
                        failures.Add($"'{location}': the <summary> text must be one line; move details into <remarks>.");
                        continue;
                    }

                    var text = PlainText(lines[i + 1].Trim()["///".Length..]);
                    var width = Width(text);
                    var stop = text.IndexOf('。', StringComparison.Ordinal);
                    if (width > MaxSummaryWidth || text.Contains('；', StringComparison.Ordinal) || (stop >= 0 && stop < text.Length - 1))
                    {
                        failures.Add($"'{location}': the <summary> must be one concise sentence (width {width}, at most {MaxSummaryWidth}, no '；' or inner '。'); move details into <remarks>: {text}");
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

    /// <summary>
    /// 检查 summary 写法的目录。
    /// </summary>
    /// <remarks>
    /// 正式包、测试、示例与基准一视同仁。
    /// </remarks>
    private static IEnumerable<string> SummaryRoots
        => new[] { Workspace.SourceRoot, Workspace.TestRoot, Path.Combine(Workspace.Root, "samples"), Path.Combine(Workspace.Root, "benchmarks") }.Where(Directory.Exists);

    /// <summary>
    /// summary 的纯文本。
    /// </summary>
    /// <remarks>
    /// <c>&lt;see cref&gt;</c> 取最后一段名字，<c>&lt;see langword&gt;</c>、<c>&lt;paramref&gt;</c>、<c>&lt;typeparamref&gt;</c> 取名字，其余标签去掉，实体还原，空白折叠。
    /// </remarks>
    private static string PlainText(string xml)
    {
        var text = SeeCref().Replace(xml, static m =>
        {
            var name = m.Groups["cref"].Value;
            var parameters = name.IndexOf('(', StringComparison.Ordinal);
            name = parameters >= 0 ? name[..parameters] : name;
            name = name[(name.LastIndexOf('.') + 1)..];
            var arity = name.IndexOfAny(['`', '{']);
            return arity >= 0 ? name[..arity] : name;
        });
        text = NamedReference().Replace(text, static m => m.Groups["name"].Value);
        text = AnyTag().Replace(text, string.Empty).Replace("&lt;", "<", StringComparison.Ordinal).Replace("&gt;", ">", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
        return Whitespace().Replace(text, " ").Trim();
    }

    /// <summary>
    /// 显示宽度：中日韩字符与全角标点算 1，其余算 0.5，向上取整。
    /// </summary>
    private static int Width(string text)
    {
        var halves = 0;
        foreach (var ch in text)
        {
            halves += ch is (>= '\u2E80' and <= '\u9FFF') or (>= '\uAC00' and <= '\uD7AF') or (>= '\uF900' and <= '\uFAFF') or (>= '\uFF00' and <= '\uFFEF') ? 2 : 1;
        }

        return (halves + 1) / 2;
    }

    /// <summary>
    /// <c>&lt;see cref="…"/&gt;</c>。
    /// </summary>
    [GeneratedRegex(@"<see\s+cref=""(?:[A-Z]:)?(?<cref>[^""]+)""\s*/>")]
    private static partial Regex SeeCref();

    /// <summary>
    /// <c>&lt;see langword&gt;</c>、<c>&lt;paramref&gt;</c> 与 <c>&lt;typeparamref&gt;</c>。
    /// </summary>
    [GeneratedRegex(@"<(?:see\s+langword|paramref\s+name|typeparamref\s+name)=""(?<name>[^""]+)""\s*/>")]
    private static partial Regex NamedReference();

    /// <summary>
    /// 任意标签。
    /// </summary>
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTag();

    /// <summary>
    /// 连续空白。
    /// </summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
