#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorUtilities
// Guid:1c2d3e4f-6a7b-8c9d-0e1f-2a3b4c5d6e7f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Constants;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Utilities;

/// <summary>
/// 错误工具类
/// </summary>
internal static class ErrorUtilities
{
    /// <summary>
    /// 获取随机错误信息
    /// </summary>
    public static ErrorInfo GetRandomError()
    {
        var random = Random.Shared;
        var languageType = random.Next(0, 8); // 0-7: 八种语言

        var language = languageType switch
        {
            0 => ProgrammingLanguage.CSharp,
            1 => ProgrammingLanguage.Java,
            2 => ProgrammingLanguage.Php,
            3 => ProgrammingLanguage.Go,
            4 => ProgrammingLanguage.Python,
            5 => ProgrammingLanguage.NodeJs,
            6 => ProgrammingLanguage.Ruby,
            _ => ProgrammingLanguage.Rust
        };

        return GetErrorByLanguage(language);
    }

    /// <summary>
    /// 根据语言获取错误信息
    /// </summary>
    public static ErrorInfo GetErrorByLanguage(ProgrammingLanguage language)
    {
        var template = ErrorTemplateProvider.GetRandomTemplate(language);
        return ParseError(language, template);
    }

    /// <summary>
    /// 解析错误信息，提取消息和堆栈跟踪
    /// </summary>
    public static ErrorInfo ParseError(ProgrammingLanguage language, string fullError)
    {
        var lines = fullError.Split('\n');
        var message = lines[0];
        var stackTrace = string.Join("\\n", lines.Skip(1));

        // 提取异常类型
        var exceptionType = ExtractExceptionType(message, language);

        return new ErrorInfo
        {
            Language = language.ToString(),
            Message = message,
            StackTrace = stackTrace,
            ExceptionType = exceptionType
        };
    }

    /// <summary>
    /// 提取异常类型
    /// </summary>
    private static string ExtractExceptionType(string message, ProgrammingLanguage language)
    {
        var colonIndex = message.IndexOf(':');
        if (colonIndex <= 0) return "UnknownException";

        var exceptionType = message[..colonIndex].Trim();

        return language switch
        {
            ProgrammingLanguage.CSharp => exceptionType.Contains('.') ? exceptionType : $"System.{exceptionType}",
            ProgrammingLanguage.Java => exceptionType.Contains('.') ? exceptionType : $"java.lang.{exceptionType}",
            ProgrammingLanguage.Php => exceptionType.Replace("Fatal error:", "").Replace("Warning:", "").Trim(),
            ProgrammingLanguage.Python => exceptionType,
            ProgrammingLanguage.NodeJs => exceptionType,
            ProgrammingLanguage.Ruby => exceptionType,
            _ => exceptionType
        };
    }

    /// <summary>
    /// 生成追踪ID
    /// </summary>
    public static string GenerateTraceId()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    /// <summary>
    /// 生成主机名
    /// </summary>
    public static string GenerateHostname()
    {
        var random = Random.Shared;
        var prefix = ErrorConstants.HostnamePrefixes[random.Next(ErrorConstants.HostnamePrefixes.Length)];
        var number = random.Next(1, 999);
        var domain = ErrorConstants.HostnameDomains[random.Next(ErrorConstants.HostnameDomains.Length)];

        return $"{prefix}-{number:D3}.{domain}.com";
    }

    /// <summary>
    /// 获取随机HTTP方法
    /// </summary>
    public static string GetRandomHttpMethod()
    {
        var random = Random.Shared;
        return ErrorConstants.HttpMethods[random.Next(ErrorConstants.HttpMethods.Length)];
    }

    /// <summary>
    /// 转义JSON字符串
    /// </summary>
    public static string EscapeJson(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f");
    }

    /// <summary>
    /// 转义XML字符串
    /// </summary>
    public static string EscapeXml(string input)
    {
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// 转义HTML字符串
    /// </summary>
    public static string EscapeHtml(string input)
    {
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
