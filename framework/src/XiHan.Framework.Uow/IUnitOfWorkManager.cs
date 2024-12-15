#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUnitOfWorkManager
// Guid:d34e3c01-4431-4759-88ee-f6650f15d79e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 6:13:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元管理器接口
/// </summary>
public interface IUnitOfWorkManager
{
    /// <summary>
    /// 当前工作单元
    /// </summary>
    IUnitOfWork? Current { get; }

    /// <summary>
    /// 开始一个新的工作单元
    /// </summary>
    /// <param name="options"></param>
    /// <param name="requiresNew"></param>
    /// <returns></returns>
    IUnitOfWork Begin([NotNull] XiHanUnitOfWorkOptions options, bool requiresNew = false);

    /// <summary>
    /// 尝试开始一个新的工作单元
    /// </summary>
    /// <param name="reservationName"></param>
    /// <param name="requiresNew"></param>
    /// <returns></returns>
    IUnitOfWork Reserve([NotNull] string reservationName, bool requiresNew = false);

    /// <summary>
    /// 开始一个预留的工作单元
    /// </summary>
    /// <param name="reservationName"></param>
    /// <param name="options"></param>
    void BeginReserved([NotNull] string reservationName, [NotNull] XiHanUnitOfWorkOptions options);

    /// <summary>
    /// 尝试开始一个预留的工作单元
    /// </summary>
    /// <param name="reservationName"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    bool TryBeginReserved([NotNull] string reservationName, [NotNull] XiHanUnitOfWorkOptions options);
}
