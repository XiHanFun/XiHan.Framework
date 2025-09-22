#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateSecurityChecker
// Guid:9g4i8i3e-7h0j-8f3g-4i9i-8e3h0j7g4i9i
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 模板安全检查器接口
/// </summary>
public interface ITemplateSecurityChecker
{
    /// <summary>
    /// 检查模板安全性
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    TemplateSecurityResult CheckSecurity(string templateSource, TemplateSecurityOptions? options = null);

    /// <summary>
    /// 异步检查模板安全性
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">安全选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>安全检查结果</returns>
    Task<TemplateSecurityResult> CheckSecurityAsync(string templateSource, TemplateSecurityOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查表达式安全性
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    TemplateSecurityResult CheckExpression(string expression, TemplateSecurityOptions? options = null);

    /// <summary>
    /// 验证上下文安全性
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    TemplateSecurityResult ValidateContext(ITemplateContext context, TemplateSecurityOptions? options = null);
}
