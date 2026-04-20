namespace XiHan.Framework.Shared.Results;

/// <summary>
/// 表示一个不携带数据载荷的通用操作结果。
/// </summary>
public sealed class OperationResult
{
    /// <summary>
    /// 表示操作是否成功。
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// 表示错误代码。
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 表示错误消息。
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static OperationResult Success() => new() { Succeeded = true };

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    /// <param name="errorCode">错误代码。</param>
    /// <param name="errorMessage">错误消息。</param>
    public static OperationResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
