// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.ProviderContractTests;
using XiHan.Framework.ProviderContractTests.EventBus;

namespace XiHan.Framework.EventBus.SqlSugar.Tests.Contracts;

/// <summary>
/// SqlSugar 发件箱在 SQLite 上的提供方契约测试
/// </summary>
public class SqlSugarOutboxSqliteContractTests : OutboxContract
{
    /// <summary>
    /// 创建 SQLite 夹具
    /// </summary>
    /// <returns>夹具</returns>
    protected override Task<IProviderContractFixture<IEventOutbox>> CreateFixtureAsync()
    {
        return Task.FromResult<IProviderContractFixture<IEventOutbox>>(SqlSugarOutboxContractFixture.CreateSqlite());
    }
}

/// <summary>
/// SqlSugar 发件箱在真实 MySQL 上的提供方契约测试
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时全部跳过。
/// </remarks>
[Collection(MySqlTestCollection.Name)]
public class SqlSugarOutboxMySqlContractTests : OutboxContract
{
    /// <summary>
    /// 创建 MySQL 夹具，未配置时跳过
    /// </summary>
    /// <returns>夹具</returns>
    protected override Task<IProviderContractFixture<IEventOutbox>> CreateFixtureAsync()
    {
        Assert.SkipUnless(MySqlTestEnvironment.IsConfigured, MySqlTestEnvironment.SkipReason);
        return Task.FromResult<IProviderContractFixture<IEventOutbox>>(
            SqlSugarOutboxContractFixture.CreateMySql(MySqlTestEnvironment.ConnectionString!));
    }
}
