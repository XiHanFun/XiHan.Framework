#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HybridAuthorizationPolicyName
// Guid:a4da28d4-7dc7-4f3b-a307-cf0d99fbf932
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 混合授权策略名称帮助类
/// </summary>
internal static class HybridAuthorizationPolicyName
{
    /// <summary>
    /// 策略前缀
    /// </summary>
    public const string Prefix = "xihan.hybrid:";

    /// <summary>
    /// 构建策略名称
    /// </summary>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="abacPolicyCode">ABAC 策略编码</param>
    /// <returns></returns>
    public static string Build(string? permissionCode, string? abacPolicyCode)
    {
        var permission = permissionCode?.Trim() ?? string.Empty;
        var abac = abacPolicyCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(permission) && string.IsNullOrWhiteSpace(abac))
        {
            throw new ArgumentException("至少需要配置一个策略参数");
        }

        var encodedPermission = Uri.EscapeDataString(permission);
        var encodedAbac = Uri.EscapeDataString(abac);
        return $"{Prefix}p={encodedPermission};a={encodedAbac}";
    }

    /// <summary>
    /// 解析策略名称
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="abacPolicyCode">ABAC 策略编码</param>
    /// <returns></returns>
    public static bool TryParse(string policyName, out string permissionCode, out string abacPolicyCode)
    {
        permissionCode = string.Empty;
        abacPolicyCode = string.Empty;

        if (!policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = policyName[Prefix.Length..];
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var segments = payload.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var rawValue = segment[(separatorIndex + 1)..].Trim();
            var value = Uri.UnescapeDataString(rawValue);
            if (key.Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                permissionCode = value;
            }
            else if (key.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                abacPolicyCode = value;
            }
        }

        return !string.IsNullOrWhiteSpace(permissionCode) || !string.IsNullOrWhiteSpace(abacPolicyCode);
    }
}
