#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkEventArgs
// Guid:c11620d4-c4d0-4230-8e92-644820cfe654
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:11:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元事件参数
/// </summary>
public class UnitOfWorkEventArgs
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public IUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="unitOfWork"></param>
    public UnitOfWorkEventArgs([NotNull] IUnitOfWork unitOfWork)
    {
        _ = CheckHelper.NotNull(unitOfWork, nameof(unitOfWork));

        UnitOfWork = unitOfWork;
    }
}
