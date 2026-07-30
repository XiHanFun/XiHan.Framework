// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
