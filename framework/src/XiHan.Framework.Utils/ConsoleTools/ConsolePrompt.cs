#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsolePrompt
// Guid:e0f1g345-d7f4-6051-ae9d-b1fd84c18136
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 控制台交互式提示工具
/// </summary>
public static class ConsolePrompt
{
    /// <summary>
    /// 输入文本提示
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="isRequired">是否必填</param>
    /// <param name="validator">验证函数</param>
    /// <returns>用户输入的文本</returns>
    public static string Input(string message, string? defaultValue = null, bool isRequired = false, Func<string, (bool isValid, string errorMessage)>? validator = null)
    {
        while (true)
        {
            // 显示提示消息
            Console.Write(message);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                ConsoleColorWriter.WriteColoredMessage($" [{defaultValue}]", ConsoleColor.Gray, false);
            }
            Console.Write(": ");

            var input = Console.ReadLine() ?? "";

            // 使用默认值
            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(defaultValue))
            {
                input = defaultValue;
            }

            // 必填校验
            if (isRequired && string.IsNullOrWhiteSpace(input))
            {
                ConsoleColorWriter.WriteError("此字段为必填项，请输入有效值。");
                continue;
            }

            // 自定义验证
            if (validator != null)
            {
                var (isValid, errorMessage) = validator(input);
                if (!isValid)
                {
                    ConsoleColorWriter.WriteError(errorMessage);
                    continue;
                }
            }

            return input;
        }
    }

    /// <summary>
    /// 密码输入提示
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="maskChar">掩码字符</param>
    /// <param name="isRequired">是否必填</param>
    /// <param name="minLength">最小长度</param>
    /// <returns>用户输入的密码</returns>
    public static string Password(string message, char maskChar = '*', bool isRequired = true, int minLength = 0)
    {
        while (true)
        {
            Console.Write($"{message}: ");

            var password = new StringBuilder();
            ConsoleKeyInfo keyInfo;

            do
            {
                keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.Remove(password.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                }
                else if (keyInfo.Key != ConsoleKey.Enter)
                {
                    password.Append(keyInfo.KeyChar);
                    Console.Write(maskChar);
                }
            } while (keyInfo.Key != ConsoleKey.Enter);

            Console.WriteLine();

            var result = password.ToString();

            // 必填校验
            if (isRequired && string.IsNullOrEmpty(result))
            {
                ConsoleColorWriter.WriteError("密码不能为空。");
                continue;
            }

            // 长度校验
            if (result.Length < minLength)
            {
                ConsoleColorWriter.WriteError($"密码长度不能少于 {minLength} 个字符。");
                continue;
            }

            return result;
        }
    }

    /// <summary>
    /// 确认提示（是/否）
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>用户确认结果</returns>
    public static bool Confirm(string message, bool? defaultValue = null)
    {
        var suffix = defaultValue switch
        {
            true => " [Y/n]",
            false => " [y/N]",
            null => " [y/n]"
        };

        while (true)
        {
            Console.Write(message + suffix + ": ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            return input switch
            {
                "y" or "yes" or "是" or "确定" => true,
                "n" or "no" or "否" or "取消" => false,
                _ => defaultValue ?? PromptInvalidChoice()
            };

            static bool PromptInvalidChoice()
            {
                ConsoleColorWriter.WriteError("请输入 y/yes 或 n/no");
                return false; // 这行不会执行，因为会continue循环
            }
        }
    }

    /// <summary>
    /// 单选提示
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="options">选项列表</param>
    /// <param name="defaultIndex">默认选择索引</param>
    /// <returns>选择的选项索引</returns>
    public static int Choose(string message, string[] options, int? defaultIndex = null)
    {
        if (options == null || options.Length == 0)
        {
            throw new ArgumentException("选项列表不能为空", nameof(options));
        }

        while (true)
        {
            Console.WriteLine(message);
            for (var i = 0; i < options.Length; i++)
            {
                var prefix = defaultIndex == i ? ">" : " ";
                ConsoleColorWriter.WriteColoredMessage($"{prefix} {i + 1}. {options[i]}",
                    defaultIndex == i ? ConsoleColor.Yellow : ConsoleColor.White);
            }

            Console.Write("请选择 (1-" + options.Length);
            if (defaultIndex.HasValue)
            {
                Console.Write($", 默认 {defaultIndex.Value + 1}");
            }
            Console.Write("): ");

            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) && defaultIndex.HasValue)
            {
                return defaultIndex.Value;
            }

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Length)
            {
                return choice - 1;
            }

            ConsoleColorWriter.WriteError($"请输入 1 到 {options.Length} 之间的数字。");
        }
    }

    /// <summary>
    /// 多选提示
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="options">选项列表</param>
    /// <param name="defaultSelections">默认选择</param>
    /// <param name="minSelections">最少选择数量</param>
    /// <param name="maxSelections">最多选择数量</param>
    /// <returns>选择的选项索引数组</returns>
    public static int[] MultiChoose(string message, string[] options, int[]? defaultSelections = null, int minSelections = 0, int? maxSelections = null)
    {
        if (options == null || options.Length == 0)
        {
            throw new ArgumentException("选项列表不能为空", nameof(options));
        }

        var selections = new HashSet<int>(defaultSelections ?? []);

        while (true)
        {
            Console.Clear();
            Console.WriteLine(message);
            Console.WriteLine("使用空格切换选择，回车确认:");

            for (var i = 0; i < options.Length; i++)
            {
                var isSelected = selections.Contains(i);
                var checkbox = isSelected ? "☑" : "☐";
                var color = isSelected ? ConsoleColor.Green : ConsoleColor.White;

                Console.Write($"  {checkbox} ");
                ConsoleColorWriter.WriteColoredMessage($"{i + 1}. {options[i]}", color);
            }

            Console.WriteLine();
            Console.WriteLine("操作说明: 输入数字切换选择，输入 'done' 或按回车确认");

            if (selections.Count != 0)
            {
                Console.Write("当前选择: ");
                ConsoleColorWriter.WriteColoredMessage(string.Join(", ", selections.Select(i => options[i])), ConsoleColor.Yellow);
            }

            Console.Write("输入选择: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(input) || input == "done")
            {
                // 验证选择数量
                if (selections.Count < minSelections)
                {
                    ConsoleColorWriter.WriteError($"至少需要选择 {minSelections} 个选项。");
                    Console.WriteLine("按任意键继续...");
                    Console.ReadKey();
                    continue;
                }

                if (maxSelections.HasValue && selections.Count > maxSelections.Value)
                {
                    ConsoleColorWriter.WriteError($"最多只能选择 {maxSelections.Value} 个选项。");
                    Console.WriteLine("按任意键继续...");
                    Console.ReadKey();
                    continue;
                }

                return [.. selections.OrderBy(x => x)];
            }

            // 解析多个选择（支持逗号分隔）
            var choices = input.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);

            foreach (var choice in choices)
            {
                if (int.TryParse(choice, out var index) && index >= 1 && index <= options.Length)
                {
                    var actualIndex = index - 1;
                    if (!selections.Remove(actualIndex))
                    {
                        if (!maxSelections.HasValue || selections.Count < maxSelections.Value)
                        {
                            selections.Add(actualIndex);
                        }
                        else
                        {
                            ConsoleColorWriter.WriteWarn($"已达到最大选择数量 {maxSelections.Value}");
                        }
                    }
                }
                else
                {
                    ConsoleColorWriter.WriteWarn($"无效选择: {choice}");
                }
            }
        }
    }

    /// <summary>
    /// 数字输入提示
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>用户输入的数字</returns>
    public static int Number(string message, int? defaultValue = null, int? min = null, int? max = null)
    {
        while (true)
        {
            var prompt = message;
            if (defaultValue.HasValue)
            {
                prompt += $" [{defaultValue}]";
            }
            if (min.HasValue || max.HasValue)
            {
                prompt += $" ({min?.ToString() ?? "无限制"} - {max?.ToString() ?? "无限制"})";
            }

            Console.Write($"{prompt}: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            if (int.TryParse(input, out var number))
            {
                if (min.HasValue && number < min.Value)
                {
                    ConsoleColorWriter.WriteError($"数值不能小于 {min.Value}");
                    continue;
                }

                if (max.HasValue && number > max.Value)
                {
                    ConsoleColorWriter.WriteError($"数值不能大于 {max.Value}");
                    continue;
                }

                return number;
            }

            ConsoleColorWriter.WriteError("请输入有效的数字。");
        }
    }

    /// <summary>
    /// 小数输入提示
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>用户输入的小数</returns>
    public static double Decimal(string message, double? defaultValue = null, double? min = null, double? max = null)
    {
        while (true)
        {
            var prompt = message;
            if (defaultValue.HasValue)
            {
                prompt += $" [{defaultValue:F2}]";
            }
            if (min.HasValue || max.HasValue)
            {
                prompt += $" ({min?.ToString("F2") ?? "无限制"} - {max?.ToString("F2") ?? "无限制"})";
            }

            Console.Write($"{prompt}: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            if (double.TryParse(input, out var number))
            {
                if (min.HasValue && number < min.Value)
                {
                    ConsoleColorWriter.WriteError($"数值不能小于 {min.Value:F2}");
                    continue;
                }

                if (max.HasValue && number > max.Value)
                {
                    ConsoleColorWriter.WriteError($"数值不能大于 {max.Value:F2}");
                    continue;
                }

                return number;
            }

            ConsoleColorWriter.WriteError("请输入有效的数字。");
        }
    }

    /// <summary>
    /// 日期输入提示
    /// </summary>
    /// <param name="message">提示消息</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="format">日期格式</param>
    /// <returns>用户输入的日期</returns>
    public static DateTime Date(string message, DateTime? defaultValue = null, string format = "yyyy-MM-dd")
    {
        while (true)
        {
            var prompt = message + $" (格式: {format})";
            if (defaultValue.HasValue)
            {
                prompt += $" [{defaultValue.Value.ToString(format)}]";
            }

            Console.Write($"{prompt}: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            if (DateTime.TryParseExact(input, format, null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }

            ConsoleColorWriter.WriteError($"请输入有效的日期，格式: {format}");
        }
    }
}
