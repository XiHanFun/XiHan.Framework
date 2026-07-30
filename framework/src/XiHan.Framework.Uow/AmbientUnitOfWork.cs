// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Uow;

/// <summary>
/// 环境工作单元
/// </summary>
[ExposeServices(typeof(IAmbientUnitOfWork), typeof(IUnitOfWorkAccessor))]
public class AmbientUnitOfWork : IAmbientUnitOfWork, ISingletonDependency
{
    private readonly AsyncLocal<IUnitOfWork?> _currentUow;

    /// <summary>
    /// 构造函数
    /// </summary>
    public AmbientUnitOfWork()
    {
        _currentUow = new AsyncLocal<IUnitOfWork?>();
    }

    /// <summary>
    /// 获取当前工作单元
    /// </summary>
    public IUnitOfWork? UnitOfWork => _currentUow.Value;

    /// <summary>
    /// 设置当前工作单元
    /// </summary>
    /// <param name="unitOfWork"></param>
    public void SetUnitOfWork(IUnitOfWork? unitOfWork)
    {
        _currentUow.Value = unitOfWork;
    }

    /// <summary>
    /// 获取当前工作单元
    /// 如果当前工作单元已被保留、已释放或已完成，则返回 null
    /// </summary>
    /// <returns></returns>
    public IUnitOfWork? GetCurrentByChecking()
    {
        var uow = UnitOfWork;

        // 跳过保留的工作单元
        while (uow != null && (uow.IsReserved || uow.IsDisposed || uow.IsCompleted))
        {
            uow = uow.Outer;
        }

        return uow;
    }
}
