#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISupportsSavingChanges
// Guid:61fbba93-936f-4e0d-afbd-6d00183d9ad9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 20:55:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow.Abstracts;

/// <summary>
/// 支持保存更改
/// </summary>
public interface ISupportsSavingChanges
{
    /// <summary>
    /// 保存更改
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
