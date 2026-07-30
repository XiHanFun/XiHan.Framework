// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
