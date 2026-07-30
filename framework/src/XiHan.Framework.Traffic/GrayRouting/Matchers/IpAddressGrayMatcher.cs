// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.GrayRouting.Matchers;

/// <summary>
/// IP 地址灰度匹配器
/// </summary>
/// <remarks>
/// 根据客户端 IP 判断是否命中灰度，支持精确 IP 与 CIDR 网段（如 "192.168.1.0/24"、"10.0.0.0/8"）
/// </remarks>
public class IpAddressGrayMatcher : IGrayMatcher
{
    /// <summary>
    /// 匹配规则类型
    /// </summary>
    public GrayRuleType RuleType => GrayRuleType.IpAddress;

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        if (string.IsNullOrEmpty(context.ClientIpAddress))
        {
            return false;
        }

        if (rule is not GrayRule grayRule || string.IsNullOrEmpty(grayRule.Configuration))
        {
            return false;
        }

        if (!IPAddress.TryParse(context.ClientIpAddress, out var clientIp))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<IpAddressConfig>(grayRule.Configuration);
            if (config?.IpAddresses == null || config.IpAddresses.Count == 0)
            {
                return false;
            }

            foreach (var entry in config.IpAddresses)
            {
                if (IsIpMatch(clientIp, entry))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 异步判断是否命中灰度规则
    /// </summary>
    public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsMatch(context, rule));
    }

    /// <summary>
    /// 判断单条配置（精确 IP 或 CIDR 网段）是否命中客户端 IP
    /// </summary>
    private static bool IsIpMatch(IPAddress clientIp, string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        entry = entry.Trim();

        // CIDR 网段
        if (entry.Contains('/'))
        {
            return IPNetwork.TryParse(entry, out var network) && network.Contains(clientIp);
        }

        // 精确 IP
        return IPAddress.TryParse(entry, out var target) && target.Equals(clientIp);
    }

    /// <summary>
    /// IP 地址配置
    /// </summary>
    private sealed class IpAddressConfig
    {
        /// <summary>
        /// IP 列表，支持精确 IP 与 CIDR 网段
        /// </summary>
        public List<string> IpAddresses { get; set; } = [];
    }
}
