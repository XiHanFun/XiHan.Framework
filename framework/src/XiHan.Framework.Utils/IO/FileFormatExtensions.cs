// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.IO;

/// <summary>
/// File 扩展方法
/// </summary>
public static class FileFormatExtensions
{
    private static readonly string[] Suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// 格式化文件大小显示为字符串
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string FormatFileSizeToString(this long bytes)
    {
        double last = 1;
        for (var i = 0; i < Suffixes.Length; i++)
        {
            var current = Math.Pow(1024, i + 1);
            var temp = bytes / current;
            if (temp < 1)
            {
                return (bytes / last).ToString("f3") + Suffixes[i];
            }

            last = current;
        }

        return bytes.ToString();
    }
}
