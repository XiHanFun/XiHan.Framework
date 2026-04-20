namespace XiHan.Framework.MultiTenancy.Abstractions.CurrentTenant;

/// <summary>
/// 表示当前租户上下文抽象。
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// 获取当前租户标识。
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// 获取当前上下文是否位于租户作用域内。
    /// </summary>
    bool IsAvailable { get; }
}
