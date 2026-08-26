// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Uow.Attributes;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 带事务标注的应用服务
/// </summary>
[DynamicApi(Name = "transactional-sample")]
public class TransactionalAppService : IApplicationService
{
    /// <summary>
    /// 新增条目
    /// </summary>
    /// <param name="name">条目名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>条目名称</returns>
    [UnitOfWork(true)]
    public Task<string> CreateItemAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(name);
    }

    /// <summary>
    /// 查询条目
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>条目名称</returns>
    public Task<string> GetItemAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}
