// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 正则锚点门禁：文法按整串匹配
/// </summary>
/// <remarks>
/// 规则见 RFC-0001「文法的匹配」（ADR-0236）：.NET 的 <c>$</c> 在末尾的换行之前也算匹配，<c>xihan.tenant\n</c> 这样的值会通过校验，其他语言不会。
/// 正式包里 <see cref="GeneratedRegexAttribute"/> 的模式在字符类之外不得出现未转义的 <c>$</c>，以 <c>\z</c> 收尾；按行匹配（<see cref="RegexOptions.Multiline"/>）的除外
/// </remarks>
public sealed class RegexAnchorTests
{
    [Fact]
    public void GeneratedRegexes_AnchorAtTheTrueEndOfInput()
    {
        var checkedPatterns = 0;
        var failures = new List<string>();
        foreach (var assembly in NamingTests.LoadSourceAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (var attribute in method.GetCustomAttributesData().Where(static a => a.AttributeType == typeof(GeneratedRegexAttribute)))
                    {
                        var pattern = (string)attribute.ConstructorArguments[0].Value!;
                        var options = attribute.ConstructorArguments.Count > 1 && attribute.ConstructorArguments[1].Value is int value ? (RegexOptions)value : RegexOptions.None;
                        checkedPatterns++;
                        if ((options & RegexOptions.Multiline) == 0 && HasDollarAnchor(pattern))
                        {
                            failures.Add($"'{type.FullName}.{method.Name}' anchors with '$' in '{pattern}'; end the pattern with '\\z' so a trailing newline does not match.");
                        }
                    }
                }
            }
        }

        Assert.True(checkedPatterns > 0, "No generated regex was found; the scan is broken.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("^a$", true)]
    [InlineData("(a|b)$|^c", true)]
    [InlineData("^a\\z", false)]
    [InlineData("^a\\$", false)]
    [InlineData("^[^\"$]+\\z", false)]
    [InlineData("^[]$]\\z", false)]
    [InlineData("^[\\]$]\\z", false)]
    public void HasDollarAnchor_IgnoresEscapesAndCharacterClasses(string pattern, bool expected)
        => Assert.Equal(expected, HasDollarAnchor(pattern));

    /// <summary>
    /// 模式在字符类之外是否有未转义的 <c>$</c>
    /// </summary>
    private static bool HasDollarAnchor(string pattern)
    {
        var inClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\')
            {
                // 转义连同下一个字符跳过
                i++;
                continue;
            }

            if (inClass)
            {
                inClass = c != ']';
            }
            else if (c == '[')
            {
                inClass = true;

                // 紧跟的 ^ 与 ] 属于字符类本身，不结束它
                if (i + 1 < pattern.Length && pattern[i + 1] == '^')
                {
                    i++;
                }

                if (i + 1 < pattern.Length && pattern[i + 1] == ']')
                {
                    i++;
                }
            }
            else if (c == '$')
            {
                return true;
            }
        }

        return false;
    }
}
