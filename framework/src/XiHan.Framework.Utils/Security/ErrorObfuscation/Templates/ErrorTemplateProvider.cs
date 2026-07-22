// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
