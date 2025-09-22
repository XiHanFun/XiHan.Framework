#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateInheritanceValidator
// Guid:9b1967a8-388d-4819-b61f-f67de17cfdaa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:11:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板继承验证器
/// </summary>
public interface ITemplateInheritanceValidator
{
    /// <summary>
    /// 验证继承语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult ValidateInheritance(string templateSource);

    /// <summary>
    /// 验证继承链
    /// </summary>
    /// <param name="inheritanceChain">继承链</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult ValidateInheritanceChain(IList<string> inheritanceChain);

    /// <summary>
    /// 检测循环继承
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="inheritanceChain">继承链</param>
    /// <returns>是否存在循环继承</returns>
    bool DetectCircularInheritance(string templateName, IList<string> inheritanceChain);
}
