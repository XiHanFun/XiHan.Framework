// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Auditing.SqlSugar.Entities;
using XiHan.Framework.Auditing.SqlSugar.Mapping;
using XiHan.Framework.Auditing.Writers;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Auditing.SqlSugar.Writers;

/// <summary>
/// 异常日志 SqlSugar 写入器
/// </summary>
public class SqlSugarExceptionLogWriter : IExceptionLogWriter
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    /// <param name="currentTenant">当前租户</param>
    public SqlSugarExceptionLogWriter(
        ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator,
        ICurrentTenant currentTenant)
    {
        _clientResolver = clientResolver;
        _idGenerator = idGenerator;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 写入异常日志
    /// </summary>
    /// <param name="record">异常日志记录</param>
    /// <param name="cancellationToken">取消令牌，仅在写入前检查，不传递给数据库</param>
    public async Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = AuditingLogMapper.ToEntity(record, _idGenerator.NextId(), DateTimeOffset.UtcNow);

        using (_currentTenant.Change(record.TenantId))
        {
            var client = _clientResolver.GetClientForEntity<SysExceptionLog>();

            await client.Insertable(entity).SplitTable().ExecuteCommandAsync();
        }
    }
}
