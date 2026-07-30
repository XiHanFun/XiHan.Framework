// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Metadata;

/// <summary>
/// 数据库元数据提供器
/// </summary>
public interface IDatabaseMetadataProvider
{
    /// <summary>
    /// 获取连接下所有表元数据（不包含列）
    /// </summary>
    /// <param name="connectionConfigId">连接配置标识，为空表示当前连接</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表元数据列表</returns>
    Task<IReadOnlyList<DatabaseTableMetadata>> GetTablesAsync(string? connectionConfigId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定表的完整元数据（包含列）
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="connectionConfigId">连接配置标识，为空表示当前连接</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表元数据</returns>
    Task<DatabaseTableMetadata?> GetTableAsync(string tableName, string? connectionConfigId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定表的列元数据
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="connectionConfigId">连接配置标识，为空表示当前连接</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>列元数据列表</returns>
    Task<IReadOnlyList<DatabaseColumnMetadata>> GetColumnsAsync(string tableName, string? connectionConfigId = null, CancellationToken cancellationToken = default);
}
