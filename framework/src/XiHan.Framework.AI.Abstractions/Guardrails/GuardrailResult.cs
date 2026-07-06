#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GuardrailResult
// Guid:a1b2c3d4-e5f6-4a20-9c20-0a0b0c0d0e20
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 护栏检查结果（放行 / 拦截）
/// </summary>
public sealed class GuardrailResult
{
    private GuardrailResult(bool isBlocked, string? reason)
    {
        IsBlocked = isBlocked;
        Reason = reason;
    }

    /// <summary>
    /// 是否拦截
    /// </summary>
    public bool IsBlocked { get; }

    /// <summary>
    /// 拦截原因（放行时为 null）
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// 放行
    /// </summary>
    public static GuardrailResult Allow()
    {
        return new GuardrailResult(false, null);
    }

    /// <summary>
    /// 拦截
    /// </summary>
    public static GuardrailResult Block(string reason)
    {
        return new GuardrailResult(true, reason);
    }
}
