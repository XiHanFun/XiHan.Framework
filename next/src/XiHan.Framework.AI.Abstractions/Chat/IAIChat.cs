// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Chat;

/// <summary>
/// 聊天入口（作用域服务）：提示词渲染 → 工具绑定 → 护栏 → 解析 Provider → 调用模型。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIChat
{
    /// <summary>
    /// 一次完整应答。提示词不存在 → <see cref="AIErrors.PromptNotFound"/>；工具不存在 → <see cref="AIErrors.ToolNotFound"/>；护栏拦截 → <see cref="AIErrors.GuardrailBlocked"/>；模型不可达 → <see cref="AIErrors.ModelUnavailable"/>；超时 → <see cref="AIErrors.ModelTimeout"/>。
    /// </summary>
    ValueTask<Result<ChatResponse>> CompleteAsync(AIChatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 流式应答：校验、提示词与护栏在返回流之前完成；流里的传输错误以异常抛出。
    /// </summary>
    ValueTask<Result<IAsyncEnumerable<ChatResponseUpdate>>> StreamAsync(AIChatRequest request, CancellationToken cancellationToken);
}
