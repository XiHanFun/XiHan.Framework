using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHanWebApp.MultiTenancy;

/// <summary>
/// 当前租户信息
/// </summary>
public class CurrentTenantDto
{
    /// <summary>
    /// 租户标识；平台（未指定租户）为空
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// 租户名称；平台（未指定租户）为空
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// 租户服务（由动态 API 自动暴露为接口）
/// </summary>
/// <remarks>
/// 租户由请求头 <c>X-Tenant-Id</c> 或查询参数 <c>tenant</c> 解析，取值为租户标识或名称；
/// 可用租户在配置节 <c>XiHan:MultiTenancy:DefaultStore:Tenants</c> 中登记。
/// </remarks>
[DynamicApi]
public class TenantAppService(ICurrentTenant currentTenant) : ApplicationServiceBase
{
    /// <summary>
    /// 获取当前请求所属租户
    /// </summary>
    /// <returns>当前租户信息</returns>
    public CurrentTenantDto GetCurrent()
    {
        return new CurrentTenantDto
        {
            Id = currentTenant.Id,
            Name = currentTenant.Name
        };
    }
}
