#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleHelper
// Guid:824ca05d-f5be-49a9-96f9-8a6502e5b064
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-30 上午 12:12:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 控制台输出帮助类
/// </summary>
public static class ConsoleHelper
{
    private static readonly object _objLock = new();

    /// <summary>
    /// 在控制台输出
    /// </summary>
    /// <param name="inputStr">打印文本</param>
    /// <param name="frontColor">前置颜色</param>
    private static void WriteColorLine(string? inputStr, ConsoleColor frontColor)
    {
        lock (_objLock)
        {
            ConsoleColor currentForeColor = Console.ForegroundColor;
            Console.ForegroundColor = frontColor;
            Console.WriteLine(inputStr);
            Console.ForegroundColor = currentForeColor;
        }
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Info(string? inputStr, ConsoleColor frontColor = ConsoleColor.White)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Success(string? inputStr, ConsoleColor frontColor = ConsoleColor.Green)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Handle(string? inputStr, ConsoleColor frontColor = ConsoleColor.Cyan)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Warning(string? inputStr, ConsoleColor frontColor = ConsoleColor.Yellow)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 错误、删除信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Error(string? inputStr, ConsoleColor frontColor = ConsoleColor.Magenta)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 危险、异常信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Danger(string? inputStr, ConsoleColor frontColor = ConsoleColor.Red)
    {
        WriteColorLine(inputStr, frontColor);
    }
}
