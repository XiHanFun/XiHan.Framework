// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Chat;

/// <summary>
/// 聊天入口（作用域服务）。
/// </summary>
/// <remarks>
/// 提示词渲染 → 工具绑定 → 护栏 → 解析 Provider → 调用模型。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIChat
{
    /// <summary>
    /// 一次完整应答。
    /// </summary>
    /// <remarks>
    /// 提示词不存在 → <see cref="AIErrors.PromptNotFound"/>；工具不存在 → <see cref="AIErrors.ToolNotFound"/>；护栏拦截 → <see cref="AIErrors.GuardrailBlocked"/>；模型不可达 → <see cref="AIErrors.ModelUnavailable"/>；超时 → <see cref="AIErrors.ModelTimeout"/>。
    /// Provider 的超时令牌（<c>TimeoutSeconds</c>，覆盖 Provider 的整次调用含它的函数调用循环）已触发即超时，不论调用方是否也取消、Provider 是抛出还是正常返回；
    /// 只有调用方取消时抛出 <see cref="OperationCanceledException"/>（RFC-0022「超时与取消」）。
    /// </remarks>
    ValueTask<Result<ChatResponse>> CompleteAsync(AIChatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 流式应答。
    /// </summary>
    /// <remarks>
    /// 校验、提示词与护栏在返回流之前完成；流里的传输错误以异常抛出。
    /// Provider 的时限按等每个更新（含第一个）计时，调用方处理更新的时间不算：超时令牌已触发即抛出 <see cref="TimeoutException"/>，不论调用方是否也取消；
    /// 只有调用方取消时抛出 <see cref="OperationCanceledException"/>（RFC-0022「超时与取消」）。
    /// </remarks>
    ValueTask<Result<IAsyncEnumerable<ChatResponseUpdate>>> StreamAsync(AIChatRequest request, CancellationToken cancellationToken);
}
