#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryOperatorSupportAttribute
// Guid:77f496e1-2a36-4271-a1ff-dae6b570dc38
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 13:05:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Attributes;

/// <summary>
/// 支持的查询操作符属性
/// </summary>
/// <remarks>
/// 用于限制某个属性可以使用的查询操作符，防止不合法的查询
/// 例如：字符串字段可能只支持 Equal、Contains 等操作，不支持 GreaterThan
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class QueryOperatorSupportAttribute : Attribute
{
    /// <summary>
    /// 支持的查询操作符
    /// </summary>
    /// <param name="supportedOperators">支持的查询操作符</param>
    public QueryOperatorSupportAttribute(params QueryOperator[] supportedOperators)
    {
        SupportedOperators = supportedOperators ?? [];
    }

    /// <summary>
    /// 支持的查询操作符
    /// </summary>
    public QueryOperator[] SupportedOperators { get; }

    /// <summary>
    /// 检查指定操作符是否被支持
    /// </summary>
    public bool IsSupported(QueryOperator @operator) => SupportedOperators.Contains(@operator);

    /// <summary>
    /// 获取所有支持的操作符描述（用于错误提示）
    /// </summary>
    public string GetSupportedOperatorsDescription() => string.Join(", ", SupportedOperators);
}
