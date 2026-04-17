#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SplitTableLocator
// Guid:7ed34e89-1e2b-4a6c-9d47-8b3f0c6a7e12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DistributedIds;

namespace XiHan.Framework.Data.SqlSugar.SplitTables;

/// <summary>
/// 分表定位器默认实现（基于 <see cref="IDistributedIdGenerator{TKey}"/>）
/// </summary>
public sealed class SplitTableLocator : ISplitTableLocator
{
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="idGenerator">分布式 ID 生成器（需可反推时间）</param>
    public SplitTableLocator(IDistributedIdGenerator<long> idGenerator)
    {
        _idGenerator = idGenerator;
    }

    /// <inheritdoc />
    public DateTime ExtractTime(long id)
    {
        return _idGenerator.ExtractTime(id);
    }

    /// <inheritdoc />
    public (DateTime Begin, DateTime End)? ExtractTimeRange(IEnumerable<long> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        DateTime? begin = null;
        DateTime? end = null;
        foreach (var id in ids)
        {
            var time = _idGenerator.ExtractTime(id);
            if (begin is null || time < begin)
            {
                begin = time;
            }

            if (end is null || time > end)
            {
                end = time;
            }
        }

        if (begin is null || end is null)
        {
            return null;
        }

        return (begin.Value, end.Value);
    }
}
