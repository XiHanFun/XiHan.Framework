// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Idempotency;

/// <summary>
/// 幂等记录：处理中标记或已完成的调用结果。失败结果保留错误码、已解析文案、消息键与字符串化的参数，回放时还原为可本地化的 <see cref="Error"/>。
/// </summary>
/// <param name="State">记录状态：<see cref="InProgressState"/> 或 <see cref="CompletedState"/>。</param>
/// <param name="ErrorCode">失败时的错误码；成功为 <see langword="null"/>。</param>
/// <param name="ErrorMessage">失败时已解析的文案；键错误未解析时为 <see langword="null"/>。</param>
/// <param name="ErrorMessageKey">失败时的消息键；自由文本错误为 <see langword="null"/>。</param>
/// <param name="ErrorDetails">失败时的模板参数，值已按不变文化转成字符串。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record IdempotencyRecord(
    string State,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? ErrorMessageKey = null,
    IReadOnlyDictionary<string, string>? ErrorDetails = null)
{
    /// <summary>
    /// 处理中状态。
    /// </summary>
    public const string InProgressState = "in-progress";

    /// <summary>
    /// 已完成状态。
    /// </summary>
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
    /// 由调用结果生成已完成记录。
    /// </summary>
    public static IdempotencyRecord From(Result outcome)
    {
        if (outcome.IsSuccess)
        {
            return new(CompletedState);
        }

        var error = outcome.Error!;
        return new(CompletedState, error.Code, error.Text, error.MessageKey, Stringify(error.Details));
    }

    /// <summary>
    /// 还原为调用结果；失败记录还原为带消息键与参数的 <see cref="Error"/>。
    /// </summary>
    public Result ToResult()
    {
        if (ErrorCode is null)
        {
            return Result.Success();
        }

        var details = ErrorDetails is null ? null : ErrorDetails.ToDictionary(static d => d.Key, static d => (object?)d.Value, StringComparer.Ordinal);
        if (ErrorMessageKey is not null)
        {
            // 键错误：按键还原，回放时仍可按调用方区域翻译；已解析的文案一并带回
            var error = new Error(ErrorCode, ErrorMessageKey, details);
            return Result.Failure(string.IsNullOrWhiteSpace(ErrorMessage) ? error : error.WithText(ErrorMessage));
        }

        // 自由文本错误：没有文案的旧记录用错误码占位，保证文本非空契约
        return Result.Failure(Error.Literal(ErrorCode, string.IsNullOrWhiteSpace(ErrorMessage) ? ErrorCode : ErrorMessage, details));
    }

    /// <summary>
    /// 把参数值按不变文化转成字符串，便于源生成序列化且回放时格式化结果与原始一致。
    /// </summary>
    private static Dictionary<string, string>? Stringify(IReadOnlyDictionary<string, object?>? details)
        => details?.ToDictionary(static d => d.Key, static d => Convert.ToString(d.Value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.Ordinal);
}
