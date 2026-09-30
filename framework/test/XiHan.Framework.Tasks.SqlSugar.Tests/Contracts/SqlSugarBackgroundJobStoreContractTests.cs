// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ProviderContractTests;
using XiHan.Framework.ProviderContractTests.Tasks;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

namespace XiHan.Framework.Tasks.SqlSugar.Tests.Contracts;

/// <summary>
/// SqlSugar 后台作业存储在 SQLite 上的提供方契约测试
/// </summary>
public class SqlSugarBackgroundJobStoreSqliteContractTests : BackgroundJobStoreContract
{
    /// <summary>
    /// 创建 SQLite 夹具
    /// </summary>
    /// <returns>夹具</returns>
    protected override Task<IProviderContractFixture<IBackgroundJobStore>> CreateFixtureAsync()
    {
        return Task.FromResult<IProviderContractFixture<IBackgroundJobStore>>(SqlSugarBackgroundJobContractFixture.CreateSqlite());
    }
}

/// <summary>
/// SqlSugar 后台作业存储在真实 MySQL 上的提供方契约测试
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时全部跳过。
/// </remarks>
[Collection(MySqlTestCollection.Name)]
public class SqlSugarBackgroundJobStoreMySqlContractTests : BackgroundJobStoreContract
{
    /// <summary>
    /// 创建 MySQL 夹具，未配置时跳过
    /// </summary>
    /// <returns>夹具</returns>
    protected override Task<IProviderContractFixture<IBackgroundJobStore>> CreateFixtureAsync()
    {
        Assert.SkipUnless(MySqlTestEnvironment.IsConfigured, MySqlTestEnvironment.SkipReason);
        return Task.FromResult<IProviderContractFixture<IBackgroundJobStore>>(
            SqlSugarBackgroundJobContractFixture.CreateMySql(MySqlTestEnvironment.ConnectionString!));
    }
}
