#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorTemplateProvider
// Guid:2f3a4b5c-7d8e-9f0a-1b2c-3d4e5f6a7b8c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// 错误模板提供者
/// </summary>
internal static class ErrorTemplateProvider
{
    /// <summary>
    /// 根据编程语言获取错误模板数组
    /// </summary>
    public static string[] GetTemplates(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.CSharp => CSharpErrorTemplates.Templates,
            ProgrammingLanguage.Java => JavaErrorTemplates.Templates,
            ProgrammingLanguage.Php => PhpErrorTemplates.Templates,
            ProgrammingLanguage.Go => GoErrorTemplates.Templates,
            ProgrammingLanguage.Python => PythonErrorTemplates.Templates,
            ProgrammingLanguage.NodeJs => NodeJsErrorTemplates.Templates,
            ProgrammingLanguage.Ruby => RubyErrorTemplates.Templates,
            ProgrammingLanguage.Rust => RustErrorTemplates.Templates,
            _ => CSharpErrorTemplates.Templates
        };
    }

    /// <summary>
    /// 获取随机错误模板
    /// </summary>
    public static string GetRandomTemplate(ProgrammingLanguage language)
    {
        var templates = GetTemplates(language);
        var random = Random.Shared;
        return templates[random.Next(templates.Length)];
    }
}
