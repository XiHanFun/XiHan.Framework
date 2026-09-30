// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Policies;
using XiHan.Framework.Authorization.SqlSugar.Roles;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 授权存储测试夹具，提供一个临时 SQLite 库与各存储实例
/// </summary>
internal sealed class AuthorizationTestContext : IDisposable
{
    private readonly string _databaseFile;

    /// <summary>
    /// 构造函数，建出本包全部实体的表
    /// </summary>
    public AuthorizationTestContext()
    {
        _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_authz_{Guid.NewGuid():N}.db");

        Client = new SqlSugarClient(new ConnectionConfig
        {
            // 关闭连接池，用例结束后驱动不再持有临时库文件句柄
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        Client.CodeFirst.InitTables(EntityTypes);
        Resolver = new StubClientResolver(Client);
    }

    /// <summary>
    /// 本包程序集中全部标注了 SugarTable 的实体类型
    /// </summary>
    public static Type[] EntityTypes { get; } =
        [.. typeof(SysAuthzPermission).Assembly.GetTypes().Where(type => type.GetCustomAttribute<SugarTable>() is not null)];

    /// <summary>
    /// 临时库的客户端
    /// </summary>
    public SqlSugarClient Client { get; }

    /// <summary>
    /// 只放行当前客户端的解析器
    /// </summary>
    public StubClientResolver Resolver { get; }

    /// <summary>
    /// 主键生成器
    /// </summary>
    public IDistributedIdGenerator<long> IdGenerator { get; } = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload();

    /// <summary>
    /// 可写的当前租户，默认无租户上下文
    /// </summary>
    public StubCurrentTenant CurrentTenant { get; } = new();

    /// <summary>
    /// 创建权限存储
    /// </summary>
    /// <returns>权限存储</returns>
    public SqlSugarPermissionStore CreatePermissionStore()
    {
        return new SqlSugarPermissionStore(Resolver, CurrentTenant, IdGenerator);
    }

    /// <summary>
    /// 创建角色存储
    /// </summary>
    /// <returns>角色存储</returns>
    public SqlSugarRoleStore CreateRoleStore()
    {
        return new SqlSugarRoleStore(Resolver, CurrentTenant, IdGenerator);
    }

    /// <summary>
    /// 创建权限检查器
    /// </summary>
    /// <returns>权限检查器</returns>
    public SqlSugarPermissionChecker CreatePermissionChecker()
    {
        return new SqlSugarPermissionChecker(Resolver, CurrentTenant);
    }

    /// <summary>
    /// 创建策略存储
    /// </summary>
    /// <returns>策略存储</returns>
    public SqlSugarPolicyStore CreatePolicyStore()
    {
        return new SqlSugarPolicyStore(Resolver, IdGenerator);
    }

    /// <summary>
    /// 释放客户端并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        Client.Dispose();

        if (File.Exists(_databaseFile))
        {
            File.Delete(_databaseFile);
        }
    }
}

/// <summary>
/// 测试用当前租户，租户标识可直接赋值
/// </summary>
internal sealed class StubCurrentTenant : ICurrentTenant
{
    /// <summary>
    /// 是否有租户上下文
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 租户标识，为空表示平台态
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string? Name => null;

    /// <summary>
    /// 切换租户，释放时恢复原值
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>恢复原租户的释放对象</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        var previous = Id;
        Id = id;
        return new RestoreTenant(this, previous);
    }

    /// <summary>
    /// 释放时恢复原租户
    /// </summary>
    private sealed class RestoreTenant : IDisposable
    {
        private readonly StubCurrentTenant _owner;
        private readonly long? _previous;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">当前租户桩</param>
        /// <param name="previous">原租户标识</param>
        public RestoreTenant(StubCurrentTenant owner, long? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        /// <summary>
        /// 恢复原租户
        /// </summary>
        public void Dispose()
        {
            _owner.Id = _previous;
        }
    }
}

/// <summary>
/// 测试用客户端解析器，只允许经当前客户端访问
/// </summary>
internal sealed class StubClientResolver : ISqlSugarClientResolver
{
    private readonly ISqlSugarClient _client;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">当前客户端</param>
    public StubClientResolver(ISqlSugarClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 获取当前客户端
    /// </summary>
    /// <returns>当前客户端</returns>
    public ISqlSugarClient GetCurrentClient()
    {
        return _client;
    }

    /// <summary>
    /// 按实体路由，授权存储不应调用
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>不返回</returns>
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        throw new NotSupportedException("授权存储应统一经 GetCurrentClient 取客户端。");
    }

    /// <summary>
    /// 按连接标识取客户端，授权存储不应调用
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>不返回</returns>
    public ISqlSugarClient GetClient(string configId)
    {
        throw new NotSupportedException("授权存储应统一经 GetCurrentClient 取客户端。");
    }

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    /// <returns>连接配置标识</returns>
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取当前布局的连接配置标识
    /// </summary>
    /// <returns>连接配置标识</returns>
    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取全部客户端
    /// </summary>
    /// <returns>客户端集合</returns>
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return [_client];
    }

    /// <summary>
    /// 获取底层多租户接口
    /// </summary>
    /// <returns>不返回</returns>
    public ITenant AsTenant()
    {
        throw new NotSupportedException("测试桩不支持多租户切换。");
    }
}
