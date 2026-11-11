// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using StackExchange.Redis;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 真实 Redis 来自环境变量 <c>XIHAN_TEST_REDIS</c>
/// </summary>
/// <remarks>
/// 如 <c>localhost:6379</c>，与 <c>Caching.Redis</c> 的测试同一个变量；未设置时跳过需要它的用例。每个用例用自己的键前缀，结束时删掉前缀下的全部键
/// </remarks>
internal static class RedisTestServer
{
    /// <summary>
    /// 指向测试 Redis 的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_REDIS";

    /// <summary>
    /// 进程内共享的连接，首次使用时建立
    /// </summary>
    /// <remarks>
    /// 用例用它准备与核对流上的数据，被测的宿主各自建连接
    /// </remarks>
    private static readonly Lazy<IConnectionMultiplexer> _connection = new(() => ConnectionMultiplexer.Connect(Configuration!));

    /// <summary>
    /// 环境变量里的连接字符串
    /// </summary>
    /// <remarks>
    /// 未设置为 <see langword="null"/>
    /// </remarks>
    public static string? Configuration => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>
    /// 是否设置了测试 Redis
    /// </summary>
    public static bool IsAvailable => !string.IsNullOrWhiteSpace(Configuration);

    /// <summary>
    /// 返回连接字符串
    /// </summary>
    /// <remarks>
    /// 未设置时跳过当前用例
    /// </remarks>
    public static string RequireConfiguration()
    {
        Assert.SkipWhen(!IsAvailable, $"Set the environment variable '{EnvironmentVariable}' to a Redis instance to run this test.");
        return Configuration!;
    }

    /// <summary>
    /// 取得共享连接上的数据库
    /// </summary>
    /// <remarks>
    /// 未设置连接字符串时跳过当前用例
    /// </remarks>
    public static IDatabase Database()
    {
        RequireConfiguration();
        return _connection.Value.GetDatabase();
    }

    /// <summary>
    /// 本用例独有的键前缀
    /// </summary>
    public static string NewPrefix() => $"xihan-test:{Guid.NewGuid():N}:";

    /// <summary>
    /// 删掉前缀下的全部键
    /// </summary>
    /// <remarks>
    /// 未设置测试 Redis 时什么也不做
    /// </remarks>
    public static async Task DeleteKeysAsync(string prefix)
    {
        if (!IsAvailable)
        {
            return;
        }

        var connection = _connection.Value;
        var database = connection.GetDatabase();
        var keys = new List<RedisKey>();
        foreach (var endPoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endPoint);
            if (server.IsReplica)
            {
                continue;
            }

            await foreach (var key in server.KeysAsync(database.Database, prefix + "*"))
            {
                keys.Add(key);
            }
        }

        if (keys.Count > 0)
        {
            await database.KeyDeleteAsync([.. keys]);
        }
    }
}
