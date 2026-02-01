#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateValidationResult
// Guid:84458b8a-7053-4f14-a086-43fcd78dccd7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 08:09:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Templates;

/// <summary>
/// 模板验证结果
/// </summary>
public class TemplateValidationResult
{
    /// <summary>
    /// 是否验证成功
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// 格式化错误信息
    /// </summary>
    /// <returns>格式化的错误信息</returns>
    public string FormatErrors()
    {
        return string.Join(Environment.NewLine, Errors);
    }
}
