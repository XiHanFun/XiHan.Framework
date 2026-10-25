// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 一次聊天请求：消息、Provider、提示词模板与参数、要带上的工具名、模型选项。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AIChatRequest
{
    /// <summary>
    /// 消息。
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// Provider 名；空取默认 Provider。
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// 提示词模板名；渲染结果作为首条 <c>System</c> 消息。
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// 提示词参数。
    /// </summary>
    public IReadOnlyDictionary<string, object?>? PromptArguments { get; init; }

    /// <summary>
    /// 要带上的工具名（目录里的名字）；空表示不带工具。
    /// </summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>
    /// 模型选项；其 <c>Tools</c> 会被目录绑定的工具覆盖。
    /// </summary>
    public ChatOptions? Options { get; init; }

    /// <summary>
    /// 跳过护栏；默认 false。
    /// </summary>
    public bool SkipGuardrails { get; init; }
}
