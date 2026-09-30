// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Auditing.SqlSugar.Entities;

namespace XiHan.Framework.Auditing.SqlSugar.Mapping;

/// <summary>
/// 审计日志记录到实体的映射
/// </summary>
public static class AuditingLogMapper
{
    /// <summary>
    /// 把访问日志记录转换为实体
    /// </summary>
    /// <param name="record">访问日志记录</param>
    /// <param name="basicId">主键</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>访问日志实体</returns>
    public static SysAccessLog ToEntity(AccessLogRecord record, long basicId, DateTimeOffset createdTime)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SysAccessLog(basicId)
        {
            CreatedTime = createdTime,
            TraceId = Clamp(record.TraceId, 64),
            UserId = record.UserId,
            UserName = Clamp(record.UserName, 128),
            SessionId = Clamp(record.SessionId, 64),
            ResourceName = Clamp(record.ResourceName, 256),
            Method = Clamp(record.Method, 16),
            Path = Clamp(record.Path, 512),
            QueryString = Clamp(record.QueryString, 2048),
            RequestBody = record.RequestBody,
            StatusCode = record.StatusCode,
            RemoteIp = Clamp(record.RemoteIp, 64),
            UserAgent = Clamp(record.UserAgent, 512),
            Referer = Clamp(record.Referer, 512),
            ElapsedMilliseconds = record.ElapsedMilliseconds,
            ResponseSize = record.ResponseSize,
            ErrorMessage = record.ErrorMessage,
            TenantId = record.TenantId
        };
    }

    /// <summary>
    /// 把接口日志记录转换为实体
    /// </summary>
    /// <param name="record">接口日志记录</param>
    /// <param name="basicId">主键</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>接口日志实体</returns>
    public static SysApiLog ToEntity(ApiLogRecord record, long basicId, DateTimeOffset createdTime)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SysApiLog(basicId)
        {
            CreatedTime = createdTime,
            TraceId = Clamp(record.TraceId, 64),
            UserId = record.UserId,
            UserName = Clamp(record.UserName, 128),
            ClientId = Clamp(record.ClientId, 128),
            AppId = Clamp(record.AppId, 128),
            IsSignatureValid = record.IsSignatureValid,
            SignatureAlgorithm = Clamp(record.SignatureAlgorithm, 64),
            Method = Clamp(record.Method, 16),
            Path = Clamp(record.Path, 512),
            ApiName = Clamp(record.ApiName, 256),
            ControllerName = Clamp(record.ControllerName, 256),
            ActionName = Clamp(record.ActionName, 256),
            RequestParams = record.RequestParams,
            RequestBody = record.RequestBody,
            ResponseBody = record.ResponseBody,
            StatusCode = record.StatusCode,
            RemoteIp = Clamp(record.RemoteIp, 64),
            UserAgent = Clamp(record.UserAgent, 512),
            Referer = Clamp(record.Referer, 512),
            ElapsedMilliseconds = record.ElapsedMilliseconds,
            RequestSize = record.RequestSize,
            ResponseSize = record.ResponseSize,
            IsSuccess = record.IsSuccess,
            ErrorMessage = record.ErrorMessage,
            TenantId = record.TenantId
        };
    }

    /// <summary>
    /// 把异常日志记录转换为实体
    /// </summary>
    /// <param name="record">异常日志记录</param>
    /// <param name="basicId">主键</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>异常日志实体</returns>
    public static SysExceptionLog ToEntity(ExceptionLogRecord record, long basicId, DateTimeOffset createdTime)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SysExceptionLog(basicId)
        {
            CreatedTime = createdTime,
            TraceId = Clamp(record.TraceId, 64),
            UserId = record.UserId,
            UserName = Clamp(record.UserName, 128),
            Path = Clamp(record.Path, 512),
            Method = Clamp(record.Method, 16),
            ControllerName = Clamp(record.ControllerName, 256),
            ActionName = Clamp(record.ActionName, 256),
            StatusCode = record.StatusCode,
            ExceptionType = Clamp(record.ExceptionType, 512),
            ExceptionMessage = record.ExceptionMessage,
            ExceptionStackTrace = record.ExceptionStackTrace,
            RequestHeaders = record.RequestHeaders,
            RequestParams = record.RequestParams,
            RequestBody = record.RequestBody,
            RemoteIp = Clamp(record.RemoteIp, 64),
            UserAgent = Clamp(record.UserAgent, 512),
            TenantId = record.TenantId
        };
    }

    /// <summary>
    /// 把登录日志记录转换为实体
    /// </summary>
    /// <param name="record">登录日志记录</param>
    /// <param name="basicId">主键</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>登录日志实体</returns>
    public static SysLoginLog ToEntity(LoginLogRecord record, long basicId, DateTimeOffset createdTime)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SysLoginLog(basicId)
        {
            CreatedTime = createdTime,
            TraceId = Clamp(record.TraceId, 64),
            UserId = record.UserId,
            UserName = Clamp(record.UserName, 128),
            SessionId = Clamp(record.SessionId, 64),
            LoginResult = record.LoginResult,
            Message = Clamp(record.Message, 512),
            LoginIp = Clamp(record.LoginIp, 64),
            UserAgent = Clamp(record.UserAgent, 512),
            DeviceId = Clamp(record.DeviceId, 128),
            LoginTime = record.LoginTime,
            TenantId = record.TenantId
        };
    }

    /// <summary>
    /// 把操作日志记录转换为实体
    /// </summary>
    /// <param name="record">操作日志记录</param>
    /// <param name="basicId">主键</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>操作日志实体</returns>
    public static SysOperationLog ToEntity(OperationLogRecord record, long basicId, DateTimeOffset createdTime)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SysOperationLog(basicId)
        {
            CreatedTime = createdTime,
            TraceId = Clamp(record.TraceId, 64),
            SessionId = Clamp(record.SessionId, 64),
            UserId = record.UserId,
            UserName = Clamp(record.UserName, 128),
            ControllerName = Clamp(record.ControllerName, 256),
            ActionName = Clamp(record.ActionName, 256),
            Method = Clamp(record.Method, 16),
            Path = Clamp(record.Path, 512),
            RequestParams = record.RequestParams,
            ResponseResult = record.ResponseResult,
            StatusCode = record.StatusCode,
            ElapsedMilliseconds = record.ElapsedMilliseconds,
            RemoteIp = Clamp(record.RemoteIp, 64),
            UserAgent = Clamp(record.UserAgent, 512),
            ErrorMessage = record.ErrorMessage,
            TenantId = record.TenantId
        };
    }

    /// <summary>
    /// 把实体差异日志记录转换为实体
    /// </summary>
    /// <param name="record">实体差异日志记录</param>
    /// <param name="basicId">主键</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>实体差异日志实体</returns>
    public static SysDiffLog ToEntity(EntityDiffLogRecord record, long basicId, DateTimeOffset createdTime)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SysDiffLog(basicId)
        {
            CreatedTime = createdTime,
            AuditType = Clamp(record.AuditType, 32),
            OperationType = Clamp(record.OperationType, 16),
            EntityType = Clamp(record.EntityType, 256),
            EntityId = Clamp(record.EntityId, 256),
            BeforeData = record.BeforeData,
            AfterData = record.AfterData,
            ChangedFields = record.ChangedFields,
            RequestPath = Clamp(record.RequestPath, 512),
            RequestMethod = Clamp(record.RequestMethod, 16),
            OperationIp = Clamp(record.OperationIp, 64),
            RequestId = Clamp(record.RequestId, 64),
            UserId = record.UserId,
            UserName = Clamp(record.UserName, 128),
            TenantId = record.TenantId
        };
    }

    /// <summary>
    /// 按实体列宽截断字符串，切点落在代理对中间时再退一位
    /// </summary>
    /// <param name="value">原值</param>
    /// <param name="maxLength">列宽</param>
    /// <returns>长度不超过列宽、且不含半个字符的原值或截断后的值</returns>
    [return: NotNullIfNotNull(nameof(value))]
    private static string? Clamp(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        var cut = maxLength;
        if (cut > 0 && char.IsHighSurrogate(value[cut - 1]))
        {
            cut--;
        }

        return value[..cut];
    }
}
