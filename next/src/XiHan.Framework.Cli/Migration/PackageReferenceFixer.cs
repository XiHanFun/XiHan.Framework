// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-10（项目文件）：<c>PackageReference</c> 里的 4.x 包按映射表改名或删除，与 5.0 同名的只改版本，指向 4.x 框架项目的 <c>ProjectReference</c> 改成 <c>PackageReference</c>；同一目标只保留一条。
/// </summary>
internal sealed partial class PackageReferenceFixer : V4Fixer
{
    /// <inheritdoc />
    public override string Id => "V4-10";

    /// <inheritdoc />
    public override bool Handles(MigrationFile file) => file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string Apply(MigrationFile file, string text, MigrationSession session)
    {
        var lines = text.Split('\n');
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in PackageReference().Matches(text))
        {
            present.Add(match.Groups["id"].Value);
        }

        var builder = new StringBuilder(text.Length);
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var package = PackageReference().Match(line);
            var project = package.Success ? Match.Empty : ProjectReference().Match(line);
            var id = package.Success ? package.Groups["id"].Value : project.Success ? project.Groups["id"].Value : null;
            var targets = id is null ? null : V4Packages.Map(id);
            // 指向 5.0 项目的 ProjectReference（源码模式）不动：映射结果就是自己
            if (id is null || targets is null || (project.Success && targets.Count == 1 && string.Equals(targets[0], id, StringComparison.OrdinalIgnoreCase)))
            {
                Append(builder, line, index, lines.Length);
                continue;
            }

            var indent = line[..(line.Length - line.TrimStart().Length)];
            var carriage = line.EndsWith('\r') ? "\r" : string.Empty;
            var emitted = 0;
            foreach (var target in targets)
            {
                string replaced;
                if (package.Success)
                {
                    // 同名：只把版本改成 5.0；换名：目标已在文件里（原有或前面的行刚生成）就不再加
                    if (!string.Equals(target, id, StringComparison.OrdinalIgnoreCase) && !present.Add(target))
                    {
                        continue;
                    }

                    replaced = VersionAttribute().Replace(line.Replace($"Include=\"{id}\"", $"Include=\"{target}\"", StringComparison.Ordinal), $"Version=\"{FrameworkInfo.Version}\"");
                }
                else
                {
                    if (!present.Add(target))
                    {
                        continue;
                    }

                    replaced = $"{indent}<PackageReference Include=\"{target}\" Version=\"{FrameworkInfo.Version}\" />{carriage}";
                }

                if (!string.Equals(replaced, line, StringComparison.Ordinal))
                {
                    changed = true;
                    session.Changed(Id, file, index + 1);
                }

                emitted++;
                Append(builder, replaced, index, lines.Length);
            }

            // 删除或因重复被并掉的行
            if (emitted == 0)
            {
                changed = true;
                session.Changed(Id, file, index + 1);
            }
        }

        return changed ? builder.ToString() : text;
    }

    /// <summary>
    /// 按原换行拼回：除最后一行外每行后面补换行（<c>Split('\n')</c> 保留了 <c>\r</c>）。
    /// </summary>
    private static void Append(StringBuilder builder, string line, int index, int count)
    {
        builder.Append(line);
        if (index < count - 1)
        {
            builder.Append('\n');
        }
    }

    /// <summary>
    /// 单行的 <c>&lt;PackageReference Include="XiHan.Framework.…" … /&gt;</c>。
    /// </summary>
    [GeneratedRegex("""<PackageReference\s+Include="(?<id>XiHan\.Framework\.[^"]+)"[^>]*/>""")]
    private static partial Regex PackageReference();

    /// <summary>
    /// 单行的 <c>&lt;ProjectReference Include="…XiHan.Framework.….csproj" /&gt;</c>。
    /// </summary>
    [GeneratedRegex("""<ProjectReference\s+Include="[^"]*?(?<id>XiHan\.Framework\.[^"\\/]+)\.csproj"[^>]*/>""")]
    private static partial Regex ProjectReference();

    /// <summary>
    /// <c>Version="…"</c> 属性。
    /// </summary>
    [GeneratedRegex("Version=\"[^\"]*\"")]
    private static partial Regex VersionAttribute();
}
