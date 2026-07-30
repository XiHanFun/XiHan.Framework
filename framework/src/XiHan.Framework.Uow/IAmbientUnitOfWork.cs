// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Uow;

/// <summary>
/// 环境工作单元接口
/// </summary>
public interface IAmbientUnitOfWork : IUnitOfWorkAccessor
{
    /// <summary>
    /// 获取当前工作单元
    /// </summary>
    /// <returns></returns>
    IUnitOfWork? GetCurrentByChecking();
}
