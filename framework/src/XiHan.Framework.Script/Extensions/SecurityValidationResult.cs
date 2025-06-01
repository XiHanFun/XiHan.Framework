#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SecurityValidationResult
// Guid:e55f8af4-fa95-4feb-8fe5-b8f1e57fac52
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/1 6:21:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SecurityValidationResult
// Guid:e55f8af4-fa95-4feb-8fe5-b8f1e57fac52
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/1 6:21:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Extensions;

/// <summary>
/// 安全验证结果
/// </summary>
public class SecurityValidationResult
{
    /// <summary>
    /// 是否安全
    /// </summary>
    public bool IsSecure { get; set; }

    /// <summary>
    /// 安全问题列表
    /// </summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>
    /// 风险级别
    /// </summary>
    public SecurityRiskLevel RiskLevel { get; set; }

    /// <summary>
    /// 格式化安全问题
    /// </summary>
    /// <returns>格式化的问题字符串</returns>
    public string FormatIssues()
    {
        return Issues.Count == 0 ? "无安全问题" : string.Join("\n", Issues.Select((issue, index) => $"{index + 1}. {issue}"));
    }
}
