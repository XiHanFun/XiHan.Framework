// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 命令行记号的解析：第一个非选项记号是命令名，其余非选项记号是位置参数；<c>--name value</c>、<c>--name=value</c> 是选项，
/// 后面不跟值的 <c>--flag</c> 记为 <c>true</c>；<c>--</c> 之后全是位置参数。不做类型转换，值一律是字符串。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineParser
{
    /// <summary>
    /// 选项前缀。
    /// </summary>
    private const string OptionPrefix = "--";

    /// <summary>
    /// 解析。
    /// </summary>
    public static ParsedCommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? command = null;
        var arguments = new List<string>();
        var options = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var optionsEnded = false;
        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (!optionsEnded && string.Equals(token, OptionPrefix, StringComparison.Ordinal))
            {
                optionsEnded = true;
                continue;
            }

            if (!optionsEnded && IsOption(token))
            {
                var body = token[OptionPrefix.Length..];
                var separator = body.IndexOf('=', StringComparison.Ordinal);
                string name;
                string value;
                if (separator > 0)
                {
                    name = body[..separator];
                    value = body[(separator + 1)..];
                }
                else if (index + 1 < args.Count && !IsOption(args[index + 1]) && !string.Equals(args[index + 1], OptionPrefix, StringComparison.Ordinal))
                {
                    // 下一个记号不是选项就是本选项的值：标志后面要放位置参数时用 --flag=true 或把标志放到最后
                    name = body;
                    value = args[++index];
                }
                else
                {
                    name = body;
                    value = "true";
                }

                if (!options.TryGetValue(name, out var values))
                {
                    values = [];
                    options[name] = values;
                }

                values.Add(value);
                continue;
            }

            if (command is null)
            {
                command = token;
            }
            else
            {
                arguments.Add(token);
            }
        }

        return new ParsedCommandLine(command, arguments, options.ToDictionary(static o => o.Key, static o => (IReadOnlyList<string>)o.Value, StringComparer.Ordinal));
    }

    /// <summary>
    /// 是否为选项记号（<c>--</c> 加至少一个字符）。
    /// </summary>
    private static bool IsOption(string token) => token.StartsWith(OptionPrefix, StringComparison.Ordinal) && token.Length > OptionPrefix.Length;
}
