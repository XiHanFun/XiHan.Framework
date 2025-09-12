#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUnitOfWork
// Guid:6320daef-1f83-41d4-93eb-88828c70ec28
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/13 3:53:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 工作单元接口
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// 保存更改
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存实体并发布领域事件
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
