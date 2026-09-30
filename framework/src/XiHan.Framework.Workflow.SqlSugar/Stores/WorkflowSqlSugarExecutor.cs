// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Workflow.SqlSugar.Options;

namespace XiHan.Framework.Workflow.SqlSugar.Stores;

/// <summary>
/// 工作流存储的数据库执行器
/// </summary>
/// <remarks>
/// 每次操作在一个新开的事务型工作单元内执行并在返回前提交，不加入调用方的工作单元；
/// 操作固定作用在 <see cref="ConfigId"/> 指向的连接上，与当前租户上下文无关。
/// </remarks>
public sealed class WorkflowSqlSugarExecutor
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="coreOptions">数据访问配置</param>
    /// <param name="options">工作流 SqlSugar 存储配置</param>
    public WorkflowSqlSugarExecutor(
        ISqlSugarClientResolver clientResolver,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanSqlSugarCoreOptions> coreOptions,
        IOptions<XiHanWorkflowSqlSugarOptions> options)
    {
        _clientResolver = clientResolver;
        _unitOfWorkManager = unitOfWorkManager;

        var configuredConfigId = options.Value.ConfigId;
        ConfigId = string.IsNullOrWhiteSpace(configuredConfigId)
            ? coreOptions.Value.DefaultConfigId
            : configuredConfigId.Trim();
    }

    /// <summary>
    /// 工作流数据表所在连接的配置标识
    /// </summary>
    public string ConfigId { get; }

    /// <summary>
    /// 在独立的事务型工作单元内执行一次数据库操作并提交
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="operation">数据库操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<ISqlSugarClient, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        using var unitOfWork = _unitOfWorkManager.Begin(
            new XiHanUnitOfWorkOptions(isTransactional: true),
            requiresNew: true);

        var client = _clientResolver.GetClient(ConfigId);
        var result = await operation(client);

        await unitOfWork.CompleteAsync(cancellationToken);
        return result;
    }
}
