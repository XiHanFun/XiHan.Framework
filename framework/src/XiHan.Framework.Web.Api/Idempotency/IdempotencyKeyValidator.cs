// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 幂等键校验
/// </summary>
public static class IdempotencyKeyValidator
{
    /// <summary>
    /// 判断幂等键是否有效：非空、不超长、仅包含可见 ASCII 字符
    /// </summary>
    /// <param name="key">幂等键</param>
    /// <param name="maxLength">最大长度</param>
    /// <returns>是否有效</returns>
    public static bool IsValid(string? key, int maxLength)
    {
        if (string.IsNullOrEmpty(key) || key.Length > maxLength)
        {
            return false;
        }

        foreach (var ch in key)
        {
            if (ch is < '!' or > '~')
            {
                return false;
            }
        }

        return true;
    }
}
