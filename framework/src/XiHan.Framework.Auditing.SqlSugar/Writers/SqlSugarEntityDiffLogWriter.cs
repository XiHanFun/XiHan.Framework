// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Auditing.SqlSugar.Entities;
using XiHan.Framework.Auditing.SqlSugar.Mapping;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;

namespace XiHan.Framework.Auditing.SqlSugar.Writers;

/// <summary>
/// 实体差异日志 SqlSugar 写入器
/// </summary>
public class SqlSugarEntityDiffLogWriter : IEntityDiffLogWriter
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    public SqlSugarEntityDiffLogWriter(
        ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator)
    {
        _clientResolver = clientResolver;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 写入实体差异日志
    /// </summary>
    /// <param name="record">实体差异日志记录</param>
    /// <param name="cancellationToken">取消令牌，仅在写入前检查，不传递给数据库</param>
    public async Task WriteAsync(EntityDiffLogRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = AuditingLogMapper.ToEntity(record, _idGenerator.NextId(), DateTimeOffset.UtcNow);
        var client = _clientResolver.GetCurrentClient();

        await client.Insertable(entity).SplitTable().ExecuteCommandAsync();
    }
}
