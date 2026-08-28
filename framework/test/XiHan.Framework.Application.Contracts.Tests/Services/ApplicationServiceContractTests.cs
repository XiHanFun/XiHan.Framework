// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Application.Contracts.Tests.Services.Fakes;
using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Application.Contracts.Tests.Services;

/// <summary>
/// 应用服务标记接口测试
/// </summary>
/// <remarks>
/// <see cref="IApplicationService"/> 是纯标记接口：动态 API 中间件靠「是否实现它」决定要不要把服务暴露成 REST 端点。
/// 一旦它多出成员，所有既有实现都会编译中断；一旦它不再继承 <see cref="IRemoteService"/>，
/// 上游按 IRemoteService 扫描的装配逻辑会静默漏掉全部应用服务。两条都必须锁死。
/// </remarks>
public class ApplicationServiceContractTests
{
    /// <summary>
    /// 应用服务接口继承远程服务标记接口
    /// </summary>
    [Fact]
    public void ApplicationService_ExtendsRemoteService()
    {
        Assert.True(typeof(IRemoteService).IsAssignableFrom(typeof(IApplicationService)));
        Assert.Contains(typeof(IRemoteService), typeof(IApplicationService).GetInterfaces());
    }

    /// <summary>
    /// 应用服务接口不声明任何成员，保持纯标记语义
    /// </summary>
    [Fact]
    public void ApplicationService_DeclaresNoMembers()
    {
        Assert.Empty(typeof(IApplicationService).GetMembers());
    }

    /// <summary>
    /// 远程服务标记接口同样不声明成员
    /// </summary>
    [Fact]
    public void RemoteService_DeclaresNoMembers()
    {
        Assert.Empty(typeof(IRemoteService).GetMembers());
    }

    /// <summary>
    /// CRUD 与批量 CRUD 契约都归入应用服务，从而都能被动态 API 暴露
    /// </summary>
    [Fact]
    public void CrudContracts_AreApplicationServices()
    {
        Assert.True(typeof(IApplicationService).IsAssignableFrom(ContractTypes.Crud));
        Assert.True(typeof(IApplicationService).IsAssignableFrom(ContractTypes.BatchCrud));
        Assert.True(typeof(IRemoteService).IsAssignableFrom(ContractTypes.BatchCrud));
    }

    /// <summary>
    /// 手写实现同时满足两个契约，说明泛型约束在真实 DTO 基类下可落地
    /// </summary>
    [Fact]
    public void FakeImplementation_SatisfiesBothContracts()
    {
        var service = new FakeBatchCrudApplicationService();

        Assert.IsAssignableFrom<IApplicationService>(service);
        Assert.True(ContractTypes.Crud.IsInstanceOfType(service));
        Assert.True(ContractTypes.BatchCrud.IsInstanceOfType(service));
    }
}
