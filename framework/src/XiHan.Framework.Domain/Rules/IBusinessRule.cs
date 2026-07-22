// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Rules;

/// <summary>
/// 业务规则接口
/// 用于封装和验证业务规则
/// </summary>
public interface IBusinessRule
{
    /// <summary>
    /// 规则描述消息
    /// </summary>
    string Message { get; }

    /// <summary>
    /// 检查规则是否被违反
    /// </summary>
    /// <returns>如果规则被违反返回 true，否则返回 false</returns>
    bool IsBroken();
}
