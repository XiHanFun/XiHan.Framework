// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using StackExchange.Redis;

namespace XiHan.Framework.Caching.Redis.Tests;

/// <summary>
/// 真实 Redis 来自环境变量 <c>XIHAN_TEST_REDIS</c>（如 <c>localhost:6379</c>）；未设置时跳过需要它的用例。
/// </summary>
internal static class RedisTestServer
{
    public const string EnvironmentVariable = "XIHAN_TEST_REDIS";

    private static readonly Lazy<IConnectionMultiplexer> Connection = new(() => ConnectionMultiplexer.Connect(Configuration!));

    public static string? Configuration => Environment.GetEnvironmentVariable(EnvironmentVariable);

    public static string RequireConfiguration()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Configuration), $"设置环境变量 {EnvironmentVariable} 指向 Redis 实例后运行。");
        return Configuration!;
    }

    public static IConnectionMultiplexer Connect()
    {
        RequireConfiguration();
        return Connection.Value;
    }

    public static RedisCachingOptions Options(string configuration) => new()
    {
        Configuration = configuration,
        KeyPrefix = $"xihan-test:{Guid.NewGuid():N}:",
    };
}
