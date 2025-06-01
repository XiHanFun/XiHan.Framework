#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUnitOfWorkAccessor
// Guid:d2dd5579-7960-4fde-8f58-f51074704437
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 5:09:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元访问器接口
/// </summary>
public interface IUnitOfWorkAccessor
{
    /// <summary>
    /// 获取工作单元
    /// </summary>
    IUnitOfWork? UnitOfWork { get; }

    /// <summary>
    /// 设置工作单元
    /// </summary>
    /// <param name="unitOfWork"></param>
    void SetUnitOfWork(IUnitOfWork? unitOfWork);
}
