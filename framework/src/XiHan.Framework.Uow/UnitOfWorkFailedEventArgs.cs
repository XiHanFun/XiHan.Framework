#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkFailedEventArgs
// Guid:317f1c67-cb76-467e-b5ef-f29e5a112264
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:12:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元失败事件参数
/// </summary>
/// <remarks>
/// 用作 <see cref="IUnitOfWork.Failed"/> 事件的事件参数
/// </remarks>
public class UnitOfWorkFailedEventArgs : UnitOfWorkEventArgs
{
    /// <summary>
    /// 导致失败的异常。仅当 <see cref="IUnitOfWork.CompleteAsync"/> 期间发生错误时才设置
    /// 如果没有异常，但未调用 <see cref="IUnitOfWork.CompleteAsync"/>，则可以为 null
    /// 如果 UOW 期间发生另一个异常，则可以为 null
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// 如果工作单元被手动回滚，则为真
    /// </summary>
    public bool IsRolledback { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public UnitOfWorkFailedEventArgs([NotNull] IUnitOfWork unitOfWork, Exception? exception, bool isRolledback)
        : base(unitOfWork)
    {
        Exception = exception;
        IsRolledback = isRolledback;
    }
}
