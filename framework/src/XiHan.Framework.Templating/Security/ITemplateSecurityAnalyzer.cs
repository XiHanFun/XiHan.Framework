#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateSecurityAnalyzer
// Guid:01fbe3be-6287-424b-9468-ba2101ba7a09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:01:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 模板安全分析器
/// </summary>
public interface ITemplateSecurityAnalyzer
{
    /// <summary>
    /// 分析模板安全风险
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>安全风险报告</returns>
    TemplateSecurityReport AnalyzeSecurity(string templateSource);

    /// <summary>
    /// 检测危险模式
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>危险模式集合</returns>
    ICollection<SecurityThreat> DetectThreats(string templateSource);

    /// <summary>
    /// 验证白名单
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="whitelist">白名单</param>
    /// <returns>是否通过验证</returns>
    bool ValidateWhitelist(string expression, ICollection<string> whitelist);

    /// <summary>
    /// 检查黑名单
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="blacklist">黑名单</param>
    /// <returns>是否存在违规</returns>
    bool CheckBlacklist(string expression, ICollection<string> blacklist);

    /// <summary>
    /// 扫描文件包含
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>文件包含列表</returns>
    ICollection<string> ScanFileIncludes(string templateSource);
}
