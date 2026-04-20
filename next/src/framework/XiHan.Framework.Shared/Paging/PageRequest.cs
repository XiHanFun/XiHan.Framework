namespace XiHan.Framework.Shared.Paging;

/// <summary>
/// 表示通用分页请求。
/// </summary>
public sealed class PageRequest
{
    /// <summary>
    /// 页码，从 1 开始。
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 每页大小。
    /// </summary>
    public int PageSize { get; init; } = 20;
}
