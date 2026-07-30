// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
