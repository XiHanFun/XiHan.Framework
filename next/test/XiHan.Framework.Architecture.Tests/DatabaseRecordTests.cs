// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 数据库 Provider 记录的列约定（ADR-0036）：<c>*.Database</c> 包里实现 <c>IEntity&lt;&gt;</c> / <c>IAggregateRoot&lt;&gt;</c> 的类，每个字符串列都要显式声明长度——
/// <c>[StringLength(n)]</c> 或长文本的 <c>[LongText]</c>（DataAnnotations 的 <c>[MaxLength]</c> 构造函数带 <c>RequiresUnreferencedCode</c>，正式包不能用）。
/// SqlSugar 的 CodeFirst 对没有长度的字符串在 PostgreSQL 建成 <c>varchar(255)</c>，超长写入整批失败而 SQLite 不校验长度，一致性套件发现不了。
/// </summary>
public sealed partial class DatabaseRecordTests
{
    /// <summary>
    /// 公开的字符串自动属性声明行。
    /// </summary>
    [GeneratedRegex(@"^\s*public string\??\s+(?<name>[A-Za-z0-9_]+)\s*\{")]
    private static partial Regex StringColumn();

    [Fact]
    public void DatabaseRecords_DeclareEveryStringColumnLength()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects.Where(static p => p.PackageId.EndsWith(".Database", StringComparison.Ordinal)))
        {
            foreach (var file in Workspace.SourceFiles(project.Directory))
            {
                var lines = File.ReadAllLines(file);
                if (!lines.Any(static l => l.Contains(" IEntity<", StringComparison.Ordinal) || l.Contains(" IAggregateRoot<", StringComparison.Ordinal)))
                {
                    continue;
                }

                for (var index = 0; index < lines.Length; index++)
                {
                    var match = StringColumn().Match(lines[index]);
                    if (!match.Success || !HasLengthAttribute(lines, index))
                    {
                        if (match.Success)
                        {
                            failures.Add($"'{Workspace.Relative(file)}:{index + 1}' string column '{match.Groups["name"].Value}' must declare [StringLength(n)] or [LongText].");
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 属性声明之前的特性行（跳过文档注释与空行）里是否有 <c>[StringLength(</c> 或 <c>[LongText]</c>。
    /// </summary>
    private static bool HasLengthAttribute(string[] lines, int propertyIndex)
    {
        for (var index = propertyIndex - 1; index >= 0; index--)
        {
            var line = lines[index].TrimStart();
            if (line.StartsWith('[') && (line.Contains("StringLength(", StringComparison.Ordinal) || line.Contains("LongText", StringComparison.Ordinal)))
            {
                return true;
            }

            if (line.StartsWith('[') || line.StartsWith("///", StringComparison.Ordinal) || line.Length == 0)
            {
                continue;
            }

            return false;
        }

        return false;
    }
}
