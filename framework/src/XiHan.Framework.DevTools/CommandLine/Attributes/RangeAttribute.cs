#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RangeAttribute
// Guid:86071e74-8976-463a-9902-2a61ccd53653
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:03:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DevTools.CommandLine.Validators;

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 范围验证属性
/// </summary>
public class RangeAttribute : ValidationAttribute
{
    /// <summary>
    /// 创建范围验证属性
    /// </summary>
    /// <param name="minimum">最小值</param>
    /// <param name="maximum">最大值</param>
    public RangeAttribute(object minimum, object maximum) : base(typeof(RangeValidator))
    {
        Minimum = minimum;
        Maximum = maximum;
        Parameters = [minimum, maximum];
    }

    /// <summary>
    /// 最小值
    /// </summary>
    public object Minimum { get; }

    /// <summary>
    /// 最大值
    /// </summary>
    public object Maximum { get; }
}
