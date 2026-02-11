#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandLineParserFactory
// Guid:2d48e6ec-8705-4558-befd-301ea0b0d629
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:43:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DevTools.CommandLine.Arguments;

namespace XiHan.Framework.DevTools.CommandLine;

/// <summary>
/// 静态解析器工厂
/// </summary>
public static class CommandLineParserFactory
{
    /// <summary>
    /// 使用默认配置解析参数
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>解析结果</returns>
    public static ParsedArguments Parse(string[] args)
    {
        var parser = new CommandLineParser();
        return parser.Parse(args);
    }

    /// <summary>
    /// 解析为强类型对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="args">命令行参数</param>
    /// <returns>解析结果</returns>
    public static T Parse<T>(string[] args) where T : new()
    {
        var parser = new CommandLineParser();
        return parser.Parse<T>(args);
    }

    /// <summary>
    /// 使用自定义配置解析参数
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <param name="options">解析配置</param>
    /// <returns>解析结果</returns>
    public static ParsedArguments Parse(string[] args, ParseOptions options)
    {
        var parser = new CommandLineParser(options);
        return parser.Parse(args);
    }

    /// <summary>
    /// 使用自定义配置解析为强类型对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="args">命令行参数</param>
    /// <param name="options">解析配置</param>
    /// <returns>解析结果</returns>
    public static T Parse<T>(string[] args, ParseOptions options) where T : new()
    {
        var parser = new CommandLineParser(options);
        return parser.Parse<T>(args);
    }

    /// <summary>
    /// 尝试解析参数
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <param name="result">解析结果</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParse(string[] args, out ParsedArguments result)
    {
        var parser = new CommandLineParser();
        return parser.TryParse(args, out result);
    }

    /// <summary>
    /// 尝试解析为强类型对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="args">命令行参数</param>
    /// <param name="result">解析结果</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParse<T>(string[] args, out T result) where T : new()
    {
        var parser = new CommandLineParser();
        return parser.TryParse(args, out result);
    }
}
