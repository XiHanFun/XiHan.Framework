// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 构造函数
    /// </summary>
    public UnitOfWorkFailedEventArgs(IUnitOfWork unitOfWork, Exception? exception, bool isRolledback)
        : base(unitOfWork)
    {
        Exception = exception;
        IsRolledback = isRolledback;
    }

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
}
