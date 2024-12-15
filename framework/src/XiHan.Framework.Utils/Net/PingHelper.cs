#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PingHelper
// Guid:4b03ace8-6bee-43e7-af3a-ee7783ec0f57
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 5:34:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net.NetworkInformation;
using System.Text;

namespace XiHan.Framework.Utils.Net;

/// <summary>
/// Ping 辅助类
/// </summary>
/// <remarks>
/// 提供对 IP 或域名的 Ping 操作，支持自定义超时、缓冲区大小、TTL 等参数。
/// </remarks>
public static class PingHelper
{
    /// <summary>
    /// 执行 Ping 操作
    /// </summary>
    /// <param name="host">目标 IP 或域名</param>
    /// <param name="timeout">超时时间（毫秒），默认 4000ms</param>
    /// <param name="bufferSize">缓冲区大小，默认 32 字节</param>
    /// <param name="ttl">TTL（生存时间），默认 128</param>
    /// <param name="pingCount">Ping 次数，默认 4 次</param>
    /// <returns>Ping 结果字符串</returns>
    public static string Ping(string host, int timeout = 4000, int bufferSize = 32, int ttl = 128, int pingCount = 4)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("目标主机不能为空", nameof(host));
        }

        using var ping = new Ping();
        var buffer = Encoding.ASCII.GetBytes(new string('a', bufferSize));
        var options = new PingOptions(ttl, true);

        var result = new StringBuilder();
        _ = result.AppendLine($"开始 Ping {host}，共 {pingCount} 次：");

        for (var i = 1; i <= pingCount; i++)
        {
            try
            {
                var reply = ping.Send(host, timeout, buffer, options);
                _ = reply.Status == IPStatus.Success
                    ? result.AppendLine($"第 {i} 次：成功，地址：{reply.Address}，往返时间：{reply.RoundtripTime}ms，TTL：{reply.Options?.Ttl}")
                    : result.AppendLine($"第 {i} 次：失败，状态：{reply.Status}");
            }
            catch (PingException ex)
            {
                _ = result.AppendLine($"第 {i} 次：Ping 异常，错误信息：{ex.Message}");
            }
            catch (Exception ex)
            {
                _ = result.AppendLine($"第 {i} 次：未知错误，错误信息：{ex.Message}");
            }
        }

        _ = result.AppendLine("Ping 操作结束。");
        return result.ToString();
    }

    /// <summary>
    /// 快速测试目标是否可达
    /// </summary>
    /// <param name="host">目标 IP 或域名</param>
    /// <param name="timeout">超时时间（毫秒），默认 1000ms</param>
    /// <returns>是否可达</returns>
    public static bool IsHostReachable(string host, int timeout = 1000)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host, timeout);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}
