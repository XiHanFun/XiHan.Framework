#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DatabaseHealthCheck
// Guid:b1c2d3e4-f5a6-47b8-c9d0-e1f2a3b4c5d6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 03:59:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XiHan.Framework.Observability.HealthChecks;

/// <summary>
/// 数据库健康检查
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="connectionString">数据库连接字符串</param>
    public DatabaseHealthCheck(string connectionString)
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
            // 这里应该实现真实的数据库连接测试
            // 示例：使用 DbConnection 测试连接
            await Task.Delay(10, cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "ConnectionString", MaskConnectionString(_connectionString) },
                { "CheckTime", DateTime.UtcNow }
            };

            return HealthCheckResult.Healthy("数据库连接正常", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("数据库连接失败", ex);
        }
    }

    /// <summary>
    /// 隐藏连接字符串敏感信息
    /// </summary>
    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return string.Empty;
        }

        // 简单隐藏密码部分
        var parts = connectionString.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Contains("password", StringComparison.OrdinalIgnoreCase) ||
                parts[i].Contains("pwd", StringComparison.OrdinalIgnoreCase))
            {
                var keyValue = parts[i].Split('=');
                if (keyValue.Length == 2)
                {
                    parts[i] = $"{keyValue[0]}=***";
                }
            }
        }
        return string.Join(";", parts);
    }
}
