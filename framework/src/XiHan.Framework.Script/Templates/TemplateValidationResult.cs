// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
