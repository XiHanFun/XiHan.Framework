#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkManager
// Guid:41f01972-e1a2-45f2-ad00-958296777702
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 05:06:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Uow.Extensions;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元管理器
/// </summary>
public class UnitOfWorkManager : IUnitOfWorkManager, ISingletonDependency
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IAmbientUnitOfWork _ambientUnitOfWork;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">范围服务工厂</param>
    /// <param name="ambientUnitOfWork">环境工作单元</param>
    public UnitOfWorkManager(IServiceScopeFactory serviceScopeFactory, IAmbientUnitOfWork ambientUnitOfWork)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _ambientUnitOfWork = ambientUnitOfWork;
    }

    /// <summary>
    /// 当前工作单元
    /// </summary>
    public IUnitOfWork? Current => _ambientUnitOfWork.GetCurrentByChecking();

    /// <summary>
    /// 开始一个新的工作单元
    /// </summary>
    /// <param name="options">工作单元选项</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元实例</returns>
    public virtual IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false)
    {
        Guard.NotNull(options, nameof(options));

        var currentUow = Current;
        if (currentUow != null && !requiresNew)
        {
            return new ChildUnitOfWork(currentUow);
        }

        var unitOfWork = CreateNewUnitOfWork();
        unitOfWork.Initialize(options);

        return unitOfWork;
    }

    /// <summary>
    /// 预留一个工作单元
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元实例</returns>
    public virtual IUnitOfWork Reserve(string reservationName, bool requiresNew = false)
    {
        Guard.NotNull(reservationName, nameof(reservationName));

        if (!requiresNew &&
            _ambientUnitOfWork.UnitOfWork != null &&
            _ambientUnitOfWork.UnitOfWork.IsReservedFor(reservationName))
        {
            return new ChildUnitOfWork(_ambientUnitOfWork.UnitOfWork);
        }

        var unitOfWork = CreateNewUnitOfWork();
        unitOfWork.Reserve(reservationName);

        return unitOfWork;
    }

    /// <summary>
    /// 开始一个预留的工作单元
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    public virtual void BeginReserved(string reservationName, XiHanUnitOfWorkOptions options)
    {
        if (!TryBeginReserved(reservationName, options))
        {
            throw new XiHanException($"找不到名为 '{reservationName}' 的预留工作单元。");
        }
    }

    /// <summary>
    /// 尝试开始一个预留的工作单元
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    /// <returns>是否成功开始</returns>
    public virtual bool TryBeginReserved(string reservationName, XiHanUnitOfWorkOptions options)
    {
        Guard.NotNullOrWhiteSpace(reservationName, nameof(reservationName));

        var uow = Current;

        // 查找预留的工作单元
        while (uow != null && !uow.IsReservedFor(reservationName))
        {
            uow = uow.Outer;
        }

        if (uow == null)
        {
            return false;
        }

        uow.Initialize(options);

        return true;
    }

    /// <summary>
    /// 创建新的工作单元
    /// </summary>
    /// <returns>工作单元实例</returns>
    protected virtual IUnitOfWork CreateNewUnitOfWork()
    {
        var scope = _serviceScopeFactory.CreateScope();
        try
        {
            var outerUow = _ambientUnitOfWork.UnitOfWork;

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            unitOfWork.SetOuter(outerUow);

            _ambientUnitOfWork.SetUnitOfWork(unitOfWork);

            unitOfWork.Disposed += (sender, args) =>
            {
                _ambientUnitOfWork.SetUnitOfWork(outerUow);
                scope.Dispose();
            };

            return unitOfWork;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
