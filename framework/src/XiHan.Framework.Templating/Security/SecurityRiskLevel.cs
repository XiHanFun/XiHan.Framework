#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SecurityRiskLevel
// Guid:02d03184-8fc4-438a-bc28-8bc599cafe35
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:03:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 安全风险级别
/// </summary>
public enum SecurityRiskLevel
{
    /// <summary>
    /// 低风险
    /// </summary>
    Low = 0,

    /// <summary>
    /// 中风险
    /// </summary>
    Medium = 1,

    /// <summary>
    /// 高风险
    /// </summary>
    High = 2,

    /// <summary>
    /// 严重风险
    /// </summary>
    Critical = 3
}
