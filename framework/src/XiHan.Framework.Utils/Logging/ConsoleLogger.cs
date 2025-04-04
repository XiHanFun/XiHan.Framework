#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleLogger
// Guid:824ca05d-f5be-49a9-96f9-8a6502e5b064
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-30 上午 12:12:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 简单的控制台日志输出类
/// </summary>
public static class ConsoleLogger
{
    private static readonly Lock ObjLock = new();

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
    public static void Handle(string? inputStr, ConsoleColor frontColor = ConsoleColor.Blue)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Warn(string? inputStr, ConsoleColor frontColor = ConsoleColor.Yellow)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="inputStr"></param>
    /// <param name="frontColor"></param>
    public static void Error(string? inputStr, ConsoleColor frontColor = ConsoleColor.Red)
    {
        WriteColorLine(inputStr, frontColor);
    }

    /// <summary>
    /// 在控制台输出
    /// </summary>
    /// <param name="inputStr">打印文本</param>
    /// <param name="frontColor">前置颜色</param>
    private static void WriteColorLine(string? inputStr, ConsoleColor frontColor)
    {
        lock (ObjLock)
        {
            var currentForeColor = Console.ForegroundColor;
            Console.ForegroundColor = frontColor;
            Console.WriteLine(inputStr);
            Console.ForegroundColor = currentForeColor;
        }
    }
}
