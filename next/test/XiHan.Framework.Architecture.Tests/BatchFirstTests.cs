// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 批量优先门禁（<c>spec/conventions/batch-first.md</c>）：正式包的循环体内不得逐条调用仓储、缓存存储与消息发布的单条方法，处理多个对象时必须用批量方法。
/// </summary>
public sealed partial class BatchFirstTests
{
    /// <summary>
    /// 循环语句的头部：<c>foreach</c> / <c>for</c> / <c>while</c> 后接括号，或 <c>do</c> 后接左花括号。
    /// </summary>
    [GeneratedRegex(@"\b(?:foreach|for|while)\s*\(|\bdo\s*\{")]
    private static partial Regex LoopHeader();

    /// <summary>
    /// 单条方法的调用：仓储的新增 / 更新 / 删除、缓存存储的写入与原子原语、消息的发布；批量方法（<c>AddRangeAsync</c>、<c>SetManyAsync</c> 等）不在其中。
    /// </summary>
    [GeneratedRegex(@"\.(AddAsync|UpdateAsync|RemoveAsync|SetAsync|TryAddAsync|TryRemoveAsync|PublishAsync)\(")]
    private static partial Regex SingleItemCall();

    [Fact]
    public void SourceProjects_DoNotCallSingleItemMethodsInsideLoops()
    {
        var failures = new List<string>();

        // 以文本匹配而不是语义分析：只看循环体（按花括号配对）里是否出现单条方法的调用，名字相同的别的 API 也算违规——批量优先的契约不给循环单条留例外
        foreach (var file in Workspace.SourceFiles(Workspace.SourceRoot))
        {
            var text = File.ReadAllText(file);
            foreach (Match header in LoopHeader().Matches(text))
            {
                if (!TryFindBody(text, header, out var bodyStart, out var bodyEnd))
                {
                    continue;
                }

                foreach (Match call in SingleItemCall().Matches(text[bodyStart..bodyEnd]))
                {
                    var line = text.AsSpan(0, bodyStart + call.Index).Count('\n') + 1;
                    failures.Add($"'{Workspace.Relative(file)}:{line}' calls '{call.Groups[1].Value}' inside a loop; use the batch method instead.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 定位循环体：跳过头部的括号对，再按花括号配对找到循环体的起止；没有花括号体（单语句循环）的不检查。
    /// </summary>
    private static bool TryFindBody(string text, Match header, out int bodyStart, out int bodyEnd)
    {
        bodyStart = header.Index + header.Length;
        bodyEnd = bodyStart;
        if (header.Value.StartsWith("do", StringComparison.Ordinal))
        {
            // do { … }：匹配到的最后一个字符就是左花括号
            bodyStart--;
        }
        else
        {
            // 跳过条件括号（可能嵌套），再跳过空白，期望下一个字符是左花括号
            var depth = 1;
            while (bodyStart < text.Length && depth > 0)
            {
                depth += text[bodyStart] switch { '(' => 1, ')' => -1, _ => 0 };
                bodyStart++;
            }

            while (bodyStart < text.Length && char.IsWhiteSpace(text[bodyStart]))
            {
                bodyStart++;
            }

            if (bodyStart >= text.Length || text[bodyStart] != '{')
            {
                return false;
            }
        }

        var braces = 0;
        for (bodyEnd = bodyStart; bodyEnd < text.Length; bodyEnd++)
        {
            braces += text[bodyEnd] switch { '{' => 1, '}' => -1, _ => 0 };
            if (braces == 0)
            {
                bodyEnd++;
                return true;
            }
        }

        return false;
    }
}
