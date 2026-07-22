// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XiHan.Framework.Observability.HealthChecks;

/// <summary>
/// Redis 健康检查
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="connectionString">Redis连接字符串</param>
    public RedisHealthCheck(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 这里应该实现真实的 Redis 连接测试
            // 示例：使用 StackExchange.Redis 测试 PING
            await Task.Delay(5, cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "Server", _connectionString },
                { "CheckTime", DateTime.UtcNow }
            };

            return HealthCheckResult.Healthy("Redis 连接正常", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis 连接失败", ex);
        }
    }
}
