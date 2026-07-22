// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Models;

/// <summary>
/// 调度聚合结果（一次调度的整体成败 + 各提供者明细）
/// </summary>
/// <remarks>
/// 由 <c>BotDispatcher</c> 在管道执行完成后构建并经 <c>IBotClient</c> 返回给调用方；
/// 各提供者明细为 <see cref="BotResult"/>（其 <see cref="BotResult.Provider"/> 标识来源提供者）。
/// </remarks>
public sealed class BotDispatchResult
{
    /// <summary>
    /// 是否成功（至少有一个提供者结果且全部成功；被跳过视为未成功）
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 是否被管道跳过（如环境过滤）
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// 失败说明（无提供者/存在失败时汇总；成功为 null）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 各提供者结果明细
    /// </summary>
    public IReadOnlyList<BotResult> Results { get; init; } = [];

    /// <summary>
    /// 无可用提供者结果
    /// </summary>
    /// <param name="errorMessage">失败说明</param>
    public static BotDispatchResult NoProvider(string errorMessage)
    {
        return new BotDispatchResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// 按提供者明细构建聚合结果
    /// </summary>
    /// <param name="results">各提供者结果</param>
    /// <param name="isSkipped">是否被管道跳过</param>
    public static BotDispatchResult From(IReadOnlyList<BotResult> results, bool isSkipped)
    {
        var snapshot = results?.ToArray() ?? [];
        if (isSkipped)
        {
            return new BotDispatchResult
            {
                IsSuccess = false,
                IsSkipped = true,
                Results = snapshot,
                ErrorMessage = "Bot dispatch skipped."
            };
        }

        var failures = snapshot.Where(result => !result.IsSuccess).ToArray();
        var isSuccess = snapshot.Length > 0 && failures.Length == 0;
        return new BotDispatchResult
        {
            IsSuccess = isSuccess,
            Results = snapshot,
            ErrorMessage = isSuccess
                ? null
                : snapshot.Length == 0
                    ? "Bot dispatch finished with no results."
                    : string.Join("；", failures.Select(result => $"{result.Provider}:{result.Message}"))
        };
    }
}
