using XiHan.Framework.MultiTenancy.Abstractions.CurrentTenant;

namespace XiHan.Framework.MultiTenancy.CurrentTenant;

/// <summary>
/// 表示当前租户上下文默认实现。
/// </summary>
public sealed class CurrentTenantAccessor : ICurrentTenant
{
    /// <summary>
    /// 使用指定租户标识初始化当前租户上下文。
    /// </summary>
    /// <param name="tenantId">租户标识。</param>
    public CurrentTenantAccessor(string? tenantId = null)
    {
        TenantId = tenantId;
    }

    /// <inheritdoc />
    public string? TenantId { get; }

    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrWhiteSpace(TenantId);
}
