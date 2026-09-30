// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 测试用客户端解析器，始终返回同一个客户端并记录解析时的租户
/// </summary>
internal sealed class StubClientResolver : ISqlSugarClientResolver
{
    /// <summary>
    /// 主库的连接配置标识
    /// </summary>
    public const string MainConfigId = "Default";

    private readonly ISqlSugarClient _client;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">客户端</param>
    /// <param name="currentTenant">当前租户</param>
    public StubClientResolver(ISqlSugarClient client, ICurrentTenant currentTenant)
    {
        _client = client;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 每次解析当前客户端时的租户标识
    /// </summary>
    public List<long?> ObservedTenantIds { get; } = [];

    /// <summary>
    /// 获取当前客户端并记录当时的租户
    /// </summary>
    /// <returns>客户端</returns>
    public ISqlSugarClient GetCurrentClient()
    {
        ObservedTenantIds.Add(_currentTenant.Id);
        return _client;
    }

    /// <summary>
    /// 获取实体对应的客户端
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>客户端</returns>
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        return GetCurrentClient();
    }

    /// <summary>
    /// 按连接配置标识获取客户端
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>客户端</returns>
    public ISqlSugarClient GetClient(string configId)
    {
        return _client;
    }

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return [MainConfigId];
    }

    /// <summary>
    /// 获取当前布局的全部连接配置标识
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return [MainConfigId];
    }

    /// <summary>
    /// 获取当前工作单元已登记的连接配置标识
    /// </summary>
    /// <returns>空集合</returns>
    public IReadOnlyList<string> GetEnlistedConfigIds()
    {
        return [];
    }

    /// <summary>
    /// 获取所有库的客户端
    /// </summary>
    /// <returns>客户端集合</returns>
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return [_client];
    }

    /// <summary>
    /// 获取底层多租户接口
    /// </summary>
    /// <returns>不返回，始终抛出</returns>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public ITenant AsTenant()
    {
        throw new NotSupportedException("测试桩不支持多租户切换。");
    }
}
