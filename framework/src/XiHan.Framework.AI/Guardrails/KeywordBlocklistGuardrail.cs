// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using XiHan.Framework.AI.Abstractions.Guardrails;

namespace XiHan.Framework.AI.Guardrails;

/// <summary>
/// 默认护栏：敏感词/正则黑名单 + 提示注入启发式（薄自包含，零外部依赖，第一道防线）
/// </summary>
/// <remarks>只检 <see cref="ChatRole.User"/> 消息；关键词大小写不敏感子串匹配，注入用正则。命中即拦截。</remarks>
public sealed class KeywordBlocklistGuardrail : IAiGuardrail
{
    // 内置提示注入启发式（中英；仅第一道防线，易被混淆/多语言绕过）
    private static readonly string[] BuiltInInjectionPatterns =
    [
        @"ignore\s+(all\s+)?(previous|above|prior)\s+instructions",
        @"disregard\s+(the\s+)?(above|previous|prior)",
        @"forget\s+(everything|all|the\s+above)",
        @"you\s+are\s+now\s+",
        @"reveal\s+your\s+(system\s+)?(prompt|instructions)",
        @"忽略(以上|之前|前面|上述).{0,4}(指令|提示|规则)",
        @"忽略(你|您)(之前|前面|上面)的?(所有)?(指令|设定|提示)",
        @"泄露(你的|您的)?(系统)?(提示词|指令|设定)"
    ];

    private readonly AiGuardrailOptions _options;
    private readonly Regex[] _injectionRegexes;

    /// <summary>
    /// 构造函数
    /// </summary>
    public KeywordBlocklistGuardrail(IOptions<AiGuardrailOptions> options)
    {
        _options = options.Value;

        var patterns = new List<string>(_options.InjectionPatterns);
        if (_options.UseBuiltInInjectionHeuristics)
        {
            patterns.AddRange(BuiltInInjectionPatterns);
        }

        _injectionRegexes = patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .ToArray();
    }

    /// <inheritdoc />
    public string Name => "keyword_blocklist";

    /// <inheritdoc />
    public ValueTask<GuardrailResult> InspectInputAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User)
            {
                continue;
            }

            var text = message.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (var keyword in _options.BlockedKeywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult(GuardrailResult.Block($"命中敏感词：{keyword}"));
                }
            }

            foreach (var regex in _injectionRegexes)
            {
                if (regex.IsMatch(text))
                {
                    return ValueTask.FromResult(GuardrailResult.Block("疑似提示注入"));
                }
            }
        }

        return ValueTask.FromResult(GuardrailResult.Allow());
    }
}
