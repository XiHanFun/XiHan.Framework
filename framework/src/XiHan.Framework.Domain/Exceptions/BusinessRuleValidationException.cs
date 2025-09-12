#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BusinessRuleValidationException
// Guid:def12f9d-8e3a-4b5c-9a2e-1234567890gh
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 15:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Rules;

namespace XiHan.Framework.Domain.Exceptions;

/// <summary>
/// 业务规则验证异常
/// 当业务规则验证失败时抛出
/// </summary>
public class BusinessRuleValidationException : DomainException
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="brokenRule">被违反的业务规则</param>
    public BusinessRuleValidationException(IBusinessRule brokenRule) 
        : base(brokenRule.Message)
    {
        BrokenRule = brokenRule;
        Details = brokenRule.Message;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="brokenRule">被违反的业务规则</param>
    /// <param name="message">自定义异常消息</param>
    public BusinessRuleValidationException(IBusinessRule brokenRule, string message) 
        : base(message)
    {
        BrokenRule = brokenRule;
        Details = brokenRule.Message;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    public BusinessRuleValidationException(string message) 
        : base(message)
    {
        BrokenRule = null!;
        Details = message;
    }

    /// <summary>
    /// 被违反的业务规则
    /// </summary>
    public IBusinessRule? BrokenRule { get; }

    /// <summary>
    /// 重写 ToString 方法
    /// </summary>
    /// <returns>异常的字符串表示</returns>
    public override string ToString()
    {
        return BrokenRule is not null 
            ? $"Business Rule Broken: {BrokenRule.GetType().Name}\n{base.ToString()}"
            : $"Business Rule Validation Failed\n{base.ToString()}";
    }
}
