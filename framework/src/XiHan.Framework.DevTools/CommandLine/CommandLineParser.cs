#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandLineParser
// Guid:23dcf7b3-306d-4fd2-9f64-0c72bc2b2034
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DevTools.CommandLine.Arguments;

namespace XiHan.Framework.DevTools.CommandLine;

/// <summary>
/// 命令行参数解析器
/// </summary>
public class CommandLineParser
{
    private readonly ParseOptions _options;

    /// <summary>
    /// 创建命令行解析器
    /// </summary>
    /// <param name="options">解析配置</param>
    public CommandLineParser(ParseOptions? options = null)
    {
        _options = options ?? new ParseOptions();
    }

    /// <summary>
    /// 解析命令行参数
    /// </summary>
    /// <param name="args">命令行参数数组</param>
    /// <returns>解析结果</returns>
    public ParsedArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var result = new ParsedArguments();
        var tokens = Tokenize(args);
        var position = 0;

        while (position < tokens.Count)
        {
            var token = tokens[position];

            switch (token.Type)
            {
                case TokenType.StopParsing:
                    // 遇到 -- 停止解析，后续所有参数作为普通参数
                    position++;
                    while (position < tokens.Count)
                    {
                        result.Remaining.Add(tokens[position].Value);
                        position++;
                    }
                    break;

                case TokenType.LongOption:
                    position = ParseLongOption(tokens, position, result);
                    break;

                case TokenType.ShortOption:
                    position = ParseShortOption(tokens, position, result);
                    break;

                case TokenType.CombinedShortOptions:
                    position = ParseCombinedShortOptions(tokens, position, result);
                    break;

                case TokenType.KeyValue:
                    position = ParseKeyValue(tokens, position, result);
                    break;

                case TokenType.Value:
                case TokenType.Command:
                    result.Arguments.Add(token.Value);
                    position++;
                    break;

                default:
                    throw new ArgumentParseException($"未知的token类型: {token.Type}");
            }
        }

        return result;
    }

    /// <summary>
    /// 解析为强类型对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="args">命令行参数</param>
    /// <returns>解析结果</returns>
    public T Parse<T>(string[] args) where T : new()
    {
        var parsedArgs = Parse(args);
        return CommandLineBinder.Bind<T>(parsedArgs);
    }

    /// <summary>
    /// 尝试解析命令行参数
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <param name="result">解析结果</param>
    /// <returns>是否解析成功</returns>
    public bool TryParse(string[] args, out ParsedArguments result)
    {
        try
        {
            result = Parse(args);
            return true;
        }
        catch
        {
            result = new ParsedArguments();
            return false;
        }
    }

    /// <summary>
    /// 尝试解析为强类型对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="args">命令行参数</param>
    /// <param name="result">解析结果</param>
    /// <returns>是否解析成功</returns>
    public bool TryParse<T>(string[] args, out T result) where T : new()
    {
        try
        {
            result = Parse<T>(args);
            return true;
        }
        catch
        {
            result = new T();
            return false;
        }
    }

    /// <summary>
    /// 解析长选项
    /// </summary>
    /// <param name="tokens">token列表</param>
    /// <param name="position">当前位置</param>
    /// <param name="result">解析结果</param>
    /// <returns>新位置</returns>
    private static int ParseLongOption(List<ArgumentToken> tokens, int position, ParsedArguments result)
    {
        var token = tokens[position];
        var optionName = token.Value;

        // 检查是否有值
        if (position + 1 < tokens.Count)
        {
            var nextToken = tokens[position + 1];
            if (nextToken.Type == TokenType.Value)
            {
                result.AddOption(optionName, nextToken.Value);
                return position + 2;
            }
        }

        // 布尔开关
        result.AddOption(optionName);
        return position + 1;
    }

    /// <summary>
    /// 解析短选项
    /// </summary>
    /// <param name="tokens">token列表</param>
    /// <param name="position">当前位置</param>
    /// <param name="result">解析结果</param>
    /// <returns>新位置</returns>
    private static int ParseShortOption(List<ArgumentToken> tokens, int position, ParsedArguments result)
    {
        var token = tokens[position];
        var optionName = token.Value;

        // 检查是否有值
        if (position + 1 < tokens.Count)
        {
            var nextToken = tokens[position + 1];
            if (nextToken.Type == TokenType.Value)
            {
                result.AddOption(optionName, nextToken.Value);
                return position + 2;
            }
        }

        // 布尔开关
        result.AddOption(optionName);
        return position + 1;
    }

    /// <summary>
    /// 解析组合短选项
    /// </summary>
    /// <param name="tokens">token列表</param>
    /// <param name="position">当前位置</param>
    /// <param name="result">解析结果</param>
    /// <returns>新位置</returns>
    private static int ParseCombinedShortOptions(List<ArgumentToken> tokens, int position, ParsedArguments result)
    {
        var token = tokens[position];
        var options = token.Value;

        // 将每个字符作为独立的布尔选项
        foreach (var option in options)
        {
            result.AddOption(option.ToString());
        }

        return position + 1;
    }

    /// <summary>
    /// 将参数数组转换为token列表
    /// </summary>
    /// <param name="args">参数数组</param>
    /// <returns>token列表</returns>
    private List<ArgumentToken> Tokenize(string[] args)
    {
        var tokens = new List<ArgumentToken>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (string.IsNullOrEmpty(arg))
            {
                continue;
            }

            // 停止解析标记
            if (arg == _options.StopParsingMarker)
            {
                tokens.Add(new ArgumentToken(TokenType.StopParsing, arg, i));
                continue;
            }

            // 检查键值对格式 (key=value, key:value)
            if (ContainsValueSeparator(arg, out _))
            {
                tokens.Add(new ArgumentToken(TokenType.KeyValue, arg, i));
                continue;
            }

            // 长选项 (--option)
            if (arg.StartsWith("--") && arg.Length > 2)
            {
                tokens.Add(new ArgumentToken(TokenType.LongOption, arg[2..], i));
                continue;
            }

            // 短选项或组合短选项 (-o, -abc)
            if (arg.StartsWith('-') && arg.Length > 1)
            {
                if (arg.Length == 2)
                {
                    tokens.Add(new ArgumentToken(TokenType.ShortOption, arg[1..], i));
                }
                else if (_options.EnablePosixStyle)
                {
                    tokens.Add(new ArgumentToken(TokenType.CombinedShortOptions, arg[1..], i));
                }
                else
                {
                    tokens.Add(new ArgumentToken(TokenType.ShortOption, arg[1..], i));
                }
                continue;
            }

            // 普通值
            tokens.Add(new ArgumentToken(TokenType.Value, arg, i));
        }

        return tokens;
    }

    /// <summary>
    /// 检查是否包含值分隔符
    /// </summary>
    /// <param name="arg">参数</param>
    /// <param name="kvp">键值对</param>
    /// <returns>是否包含分隔符</returns>
    private bool ContainsValueSeparator(string arg, out (string key, string value) kvp)
    {
        kvp = ("", "");

        foreach (var separator in _options.ValueSeparators)
        {
            var index = arg.IndexOf(separator);
            if (index > 0 && index < arg.Length - 1)
            {
                kvp = (arg[..index], arg[(index + 1)..]);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 解析键值对
    /// </summary>
    /// <param name="tokens">token列表</param>
    /// <param name="position">当前位置</param>
    /// <param name="result">解析结果</param>
    /// <returns>新位置</returns>
    private int ParseKeyValue(List<ArgumentToken> tokens, int position, ParsedArguments result)
    {
        var token = tokens[position];

        if (ContainsValueSeparator(token.Value, out var kvp))
        {
            var key = kvp.key;
            var value = kvp.value;

            // 处理 --key=value 格式
            if (key.StartsWith("--") && key.Length > 2)
            {
                key = key[2..];
            }
            // 处理 -k=value 格式
            else if (key.StartsWith('-') && key.Length > 1)
            {
                key = key[1..];
            }

            result.AddOption(key, value);
        }

        return position + 1;
    }
}
