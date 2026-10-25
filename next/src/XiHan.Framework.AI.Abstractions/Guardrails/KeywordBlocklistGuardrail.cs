// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 内置护栏：<c>User</c> 消息命中禁用关键词或提示注入模式即拦截。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KeywordBlocklistGuardrail : IAIGuardrail
{
    /// <summary>
    /// 护栏名。
    /// </summary>
    public const string GuardrailName = "keyword-blocklist";

    /// <summary>
    /// 内置的提示注入模式（中英文）。
    /// </summary>
    private static readonly string[] _builtInInjectionPatterns =
    [
        @"ignore\s+(all\s+)?(previous|above|prior)\s+instructions",
        @"disregard\s+(the\s+)?(above|previous|prior)",
        @"forget\s+(everything|all|the\s+above)",
        @"you\s+are\s+now\s+",
        @"reveal\s+your\s+(system\s+)?(prompt|instructions)",
        @"忽略(以上|之前|前面|上述).{0,4}(指令|提示|规则)",
        @"忽略(你|您)(之前|前面|上面)的?(所有)?(指令|设定|提示)",
        @"泄露(你的|您的)?(系统)?(提示词|指令|设定)",
    ];

    /// <summary>
    /// 禁用关键词。
    /// </summary>
    private readonly IReadOnlyList<string> _keywords;

    /// <summary>
    /// 注入模式。
    /// </summary>
    private readonly Regex[] _patterns;

    /// <summary>
    /// 创建。
    /// </summary>
    public KeywordBlocklistGuardrail(AIGuardrailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _keywords = options.BlockedKeywords.Where(static keyword => !string.IsNullOrWhiteSpace(keyword)).ToList();
        IEnumerable<string> patterns = options.InjectionPatterns.Where(static pattern => !string.IsNullOrWhiteSpace(pattern));
        if (options.UseBuiltInInjectionHeuristics)
        {
            patterns = patterns.Concat(_builtInInjectionPatterns);
        }

        _patterns = patterns.Select(static pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))).ToArray();
    }

    /// <inheritdoc />
    public string Name => GuardrailName;

    /// <inheritdoc />
    public ValueTask<Result<GuardrailVerdict>> InspectAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User || string.IsNullOrEmpty(message.Text))
            {
                continue;
            }

            foreach (var keyword in _keywords)
            {
                if (message.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult(Result.Success(GuardrailVerdict.Block(AIMessages.GuardrailKeyword, MessageArguments.Of(("keyword", keyword)))));
                }
            }

            foreach (var pattern in _patterns)
            {
                if (pattern.IsMatch(message.Text))
                {
                    return ValueTask.FromResult(Result.Success(GuardrailVerdict.Block(AIMessages.GuardrailInjection)));
                }
            }
        }

        return ValueTask.FromResult(Result.Success(GuardrailVerdict.Allow()));
    }
}
