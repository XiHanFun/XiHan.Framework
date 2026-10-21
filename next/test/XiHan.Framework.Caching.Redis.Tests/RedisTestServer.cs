// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using StackExchange.Redis;

namespace XiHan.Framework.Caching.Redis.Tests;

/// <summary>
/// 真实 Redis 来自环境变量 <c>XIHAN_TEST_REDIS</c>（如 <c>localhost:6379</c>）；未设置时跳过需要它的用例。
/// </summary>
internal static class RedisTestServer
{
    /// <summary>
    /// 指向测试 Redis 的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_REDIS";

    /// <summary>
    /// 进程内共享的连接，首次使用时建立。
    /// </summary>
    private static readonly Lazy<IConnectionMultiplexer> _connection = new(() => ConnectionMultiplexer.Connect(Configuration!));

    /// <summary>
    /// 环境变量里的连接字符串；未设置为 <see langword="null"/>。
    /// </summary>
    public static string? Configuration => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>
    /// 返回连接字符串；未设置时跳过当前用例。
    /// </summary>
    public static string RequireConfiguration()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Configuration), $"Set the environment variable '{EnvironmentVariable}' to a Redis instance to run this test.");
        return Configuration!;
    }

    /// <summary>
    /// 取得共享连接；未设置连接字符串时跳过当前用例。
    /// </summary>
    public static IConnectionMultiplexer Connect()
    {
        RequireConfiguration();
        return _connection.Value;
    }

    /// <summary>
    /// 带随机键前缀的选项，使各用例写入的键互不干扰。
    /// </summary>
    public static RedisCachingOptions Options(string configuration) => new()
    {
        Configuration = configuration,
        KeyPrefix = $"xihan-test:{Guid.NewGuid():N}:",
    };
}
