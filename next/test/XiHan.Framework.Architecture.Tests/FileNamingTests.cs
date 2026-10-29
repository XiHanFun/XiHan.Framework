// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 文件命名门禁（代码规范 §1.3）。
/// </summary>
/// <remarks>
/// 正式包、测试与示例里每个源文件至多声明一个顶层类型，文件名等于类型名，泛型类型加 <c>OfT</c> 后缀；嵌套类型留在外层类型的文件里。
/// </remarks>
public sealed partial class FileNamingTests
{
    /// <summary>
    /// 顶层类型声明：修饰符之后是类型种类与名称，名称后可跟类型参数列表。
    /// </summary>
    [GeneratedRegex(@"^(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|ref|unsafe|new)\s+)*(?:class|struct|record(?:\s+struct|\s+class)?|interface|enum|delegate\s+[\w<>\[\],\s?]+?)\s+(?<name>\w+)\s*(?<generic><)?")]
    private static partial Regex TypeDeclaration();

    /// <summary>
    /// 字符串与字符字面量。
    /// </summary>
    /// <remarks>
    /// 统计花括号前先去掉，免得字面量里的括号打乱配对。
    /// </remarks>
    [GeneratedRegex(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'")]
    private static partial Regex Literal();

    [Fact]
    public void SourceFiles_DeclareOneTopLevelType_NamedLikeTheFile()
    {
        var failures = new List<string>();

        foreach (var root in new[] { Workspace.SourceRoot, Workspace.TestRoot, Path.Combine(Workspace.Root, "samples") })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Workspace.SourceFiles(root))
            {
                // 生成的文件（示例里提交的 .g.cs 部署产物等）不是手写代码，不受命名约束
                if (file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var types = TopLevelTypes(file);
                // 没有类型的文件（全局 using 等）不受约束
                if (types.Count == 0)
                {
                    continue;
                }

                if (types.Count > 1)
                {
                    failures.Add($"'{Workspace.Relative(file)}' declares {types.Count} top-level types ({string.Join(", ", types.Select(static t => t.Name))}); keep one type per file.");
                    continue;
                }

                var (name, generic) = types[0];
                var expected = generic ? name + "OfT" : name;
                if (!string.Equals(Path.GetFileNameWithoutExtension(file), expected, StringComparison.Ordinal))
                {
                    failures.Add($"'{Workspace.Relative(file)}' declares '{name}'; the file must be named '{expected}.cs'.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 按花括号深度为 0 的行找出文件里的顶层类型声明。
    /// </summary>
    /// <remarks>
    /// 文件级命名空间不增加深度，块级命名空间里的类型按深度 1 识别。
    /// </remarks>
    private static List<(string Name, bool Generic)> TopLevelTypes(string file)
    {
        var types = new List<(string, bool)>();
        var depth = 0;
        var blockNamespace = false;
        foreach (var rawLine in File.ReadLines(file))
        {
            var line = Literal().Replace(rawLine.Trim(), string.Empty);
            var commentStart = line.IndexOf("//", StringComparison.Ordinal);
            if (commentStart >= 0)
            {
                line = line[..commentStart];
            }

            if (line.Length == 0)
            {
                continue;
            }

            // 块级命名空间把类型推到深度 1；记住它以便把那一层当作顶层
            if (line.StartsWith("namespace ", StringComparison.Ordinal) && line.Contains('{'))
            {
                blockNamespace = true;
            }

            var topLevel = depth == (blockNamespace ? 1 : 0);
            if (topLevel && !line.StartsWith('[') && TypeDeclaration().Match(line) is { Success: true } match)
            {
                types.Add((match.Groups["name"].Value, match.Groups["generic"].Success));
            }

            depth += line.Count('{') - line.Count('}');
        }

        return types;
    }
}
