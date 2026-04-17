#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISplitTableLocator
// Guid:1c8f5d30-4a6b-4d1a-a8c9-5e4b2a1f0b3d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.SqlSugar.SplitTables;

/// <summary>
/// 分表定位器
/// </summary>
/// <remarks>
/// 借助分布式 ID（雪花）可以反推时间戳的能力，精准定位单条/多条记录的归属分片，
/// 避免全分片扫描。也提供基于时间范围的分表定位，供批量查询使用。
/// </remarks>
public interface ISplitTableLocator
{
    /// <summary>
    /// 根据雪花 ID 反推时间（UTC）
    /// </summary>
    /// <param name="id">分布式 ID</param>
    /// <returns>ID 内嵌的生成时间</returns>
    DateTime ExtractTime(long id);

    /// <summary>
    /// 根据多个 ID 获取时间范围（最小与最大时间）
    /// </summary>
    /// <param name="ids">分布式 ID 列表</param>
    /// <returns>时间范围，列表为空返回 null</returns>
    (DateTime Begin, DateTime End)? ExtractTimeRange(IEnumerable<long> ids);
}
