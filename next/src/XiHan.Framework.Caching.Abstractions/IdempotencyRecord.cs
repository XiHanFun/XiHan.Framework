// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 幂等记录：处理中，或已完成的调用结果。
/// </summary>
/// <param name="State"><c>in-progress</c> 或 <c>completed</c>。</param>
/// <param name="ErrorCode">失败结果的错误码。</param>
/// <param name="ErrorMessage">失败结果的消息。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record IdempotencyRecord(string State, string? ErrorCode = null, string? ErrorMessage = null)
{
    /// <summary>处理中状态名。</summary>
    public const string InProgressState = "in-progress";

    /// <summary>已完成状态名。</summary>
    public const string CompletedState = "completed";

    /// <summary>
    /// 处理中标记。
    /// </summary>
    public static IdempotencyRecord InProgress { get; } = new(InProgressState);

    /// <summary>
    /// 是否处理中。
    /// </summary>
    public bool IsInProgress => string.Equals(State, InProgressState, StringComparison.Ordinal);

    /// <summary>
    /// 从调用结果创建已完成记录。
    /// </summary>
    public static IdempotencyRecord From(Result outcome)
        => outcome.IsSuccess ? new(CompletedState) : new(CompletedState, outcome.Error!.Code, outcome.Error.Message);

    /// <summary>
    /// 还原为调用结果。
    /// </summary>
    public Result ToResult()
        => ErrorCode is null ? Result.Success() : Result.Failure(new Error(ErrorCode, ErrorMessage ?? string.Empty));
}
