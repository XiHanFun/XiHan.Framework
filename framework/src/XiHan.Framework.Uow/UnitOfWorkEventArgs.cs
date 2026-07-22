// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元事件参数
/// </summary>
public class UnitOfWorkEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="unitOfWork"></param>
    public UnitOfWorkEventArgs(IUnitOfWork unitOfWork)
    {
        Guard.NotNull(unitOfWork, nameof(unitOfWork));

        UnitOfWork = unitOfWork;
    }

    /// <summary>
    /// 工作单元
    /// </summary>
    public IUnitOfWork UnitOfWork { get; }
}
