#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBusinessRule
// Guid:e2ea1df8-ad36-4f91-a192-6d3b3ac6817a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 15:50:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
