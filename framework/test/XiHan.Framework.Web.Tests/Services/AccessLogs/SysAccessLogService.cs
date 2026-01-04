#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SysAccessLogService
// Guid:g1c2d3e4-f5a6-7890-abcd-ef1234567902
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/28 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using DocumentFormat.OpenXml.Vml.Office;
using Mapster;
using XiHan.BasicApp.Core;
using XiHan.BasicApp.Rbac.Entities;
using XiHan.BasicApp.Rbac.Repositories.AccessLogs;
using XiHan.BasicApp.Rbac.Services.AccessLogs.Dtos;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace XiHan.BasicApp.Rbac.Services.AccessLogs;

/// <summary>
/// 系统访问日志服务实现
/// </summary>
public class SysAccessLogService : CrudApplicationServiceBase<SysAccessLog, AccessLogDto, XiHanBasicAppIdType, CreateAccessLogDto, CreateAccessLogDto>, ISysAccessLogService
{
    private readonly ISysAccessLogRepository _accessLogRepository;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SysAccessLogService(ISysAccessLogRepository accessLogRepository) : base(accessLogRepository)
    {
        _accessLogRepository = accessLogRepository;
    }

    #region 业务特定方法

    /// <summary>
    /// 根据用户ID获取访问日志列表
    /// </summary>
    [DynamicApi(Version = "1")]
    [DynamicApi(Version = "2")]
    [MapToApiVersion("3")]
    public async Task<List<AccessLogDto>> GetByUserIdAsync(XiHanBasicAppIdType userId)
    {
        var logs = await _accessLogRepository.GetByUserIdAsync(userId);
        return logs.Adapt<List<AccessLogDto>>();
    }

    /// <summary>
    /// 根据资源路径获取访问日志列表
    /// </summary>
    [DynamicApi(Version = "1")]
    public async Task<List<AccessLogDto>> GetByResourcePathAsync(string resourcePath)
    {
        var logs = await _accessLogRepository.GetByResourcePathAsync(resourcePath);
        return logs.Adapt<List<AccessLogDto>>();
    }

    /// <summary>
    /// 根据租户ID获取访问日志列表
    /// </summary>
    [DynamicApi(Version = "3")]
    public async Task<List<AccessLogDto>> GetByTenantIdAsync(XiHanBasicAppIdType tenantId)
    {
        var logs = await _accessLogRepository.GetByTenantIdAsync(tenantId);
        return logs.Adapt<List<AccessLogDto>>();
    }

    /// <summary>
    /// 根据时间范围获取访问日志列表
    /// </summary>
    public async Task<List<AccessLogDto>> GetByTimeRangeAsync(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var logs = await _accessLogRepository.GetByTimeRangeAsync(startTime, endTime);
        return logs.Adapt<List<AccessLogDto>>();
    }

    #endregion 业务特定方法

    #region 映射方法实现

    /// <summary>
    /// 映射实体到DTO
    /// </summary>
    protected override Task<AccessLogDto> MapToEntityDtoAsync(SysAccessLog entity)
    {
        return Task.FromResult(entity.Adapt<AccessLogDto>());
    }

    /// <summary>
    /// 映射 CreateAccessLogDto 到实体
    /// </summary>
    protected override Task<SysAccessLog> MapToEntityAsync(CreateAccessLogDto createDto)
    {
        return Task.FromResult(createDto.Adapt<SysAccessLog>());
    }

    protected override Task MapToEntityAsync(CreateAccessLogDto updateDto, SysAccessLog entity)
    {
        throw new NotImplementedException();
    }

    protected override Task<SysAccessLog> MapToEntityAsync(AccessLogDto dto)
    {
        throw new NotImplementedException();
    }

    protected override Task MapToEntityAsync(AccessLogDto dto, SysAccessLog entity)
    {
        throw new NotImplementedException();
    }

    #endregion 映射方法实现
}
