// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 启动 Banner 的文本
/// </summary>
/// <remarks>
/// 版式沿用 4.x：空行、字符画 Logo（按列彩虹渐变）、框架名与版本、标语、空行、寄语、空行，之后是应用、环境、运行时与操作系统各一行。Logo 与寄语是固定的作品，不是界面文案，不进语言文件；标语与各行的标签按默认区域翻译
/// </remarks>
internal static class StartupBanner
{
    /// <summary>
    /// 字符画 Logo
    /// </summary>
    internal const string Logo = """
           _  __ ______  _____    _   __
          | |/ //  _/ / / /   |  / | / /
          |   / / // /_/ / /| | /  |/ /
         /   |_/ // __  / ___ |/ /|  /
        /_/|_/___/_/ /_/_/  |_/_/ |_/
        """;

    /// <summary>
    /// 寄语
    /// </summary>
    internal const string Dedication = """
        碧落降恩承淑颜，共挚崎缘挽曦寒。
        迁般故事终成忆，谨此葳蕤换思短。
                      —— 致她
        """;

    /// <summary>
    /// 彩虹渐变的色相跨度（度）
    /// </summary>
    /// <remarks>
    /// 停在 300° 的品红，不绕回起点的红色，首尾两列才分得开
    /// </remarks>
    private const double HueSpan = 300;

    /// <summary>
    /// 恢复默认颜色的 SGR 序列
    /// </summary>
    private const string Reset = "\u001b[0m";

    /// <summary>
    /// 生成 Banner 文本
    /// </summary>
    /// <param name="applicationName">应用名；为空时不打印应用一行</param>
    /// <param name="applicationVersion">应用版本；为空时应用一行只有应用名</param>
    /// <param name="environmentName">环境名；为空时不打印环境一行</param>
    /// <param name="resolver">按默认区域解析标签的解析器；为空时退回当前作用域</param>
    /// <param name="color">Logo 是否带 24 位色</param>
    internal static string Render(string? applicationName, string? applicationVersion, string? environmentName, IMessageResolver? resolver, bool color)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        AppendLogo(builder, color);
        builder.Append(FrameworkInfo.Name).Append(" v").AppendLine(FrameworkInfo.Version);
        builder.AppendLine(Localize(resolver, KernelMessages.BannerTagline, null));
        builder.AppendLine();
        foreach (var line in Lines(Dedication))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
        if (!string.IsNullOrEmpty(applicationName))
        {
            var application = string.IsNullOrEmpty(applicationVersion) ? applicationName : $"{applicationName} v{applicationVersion}";
            builder.AppendLine(Localize(resolver, KernelMessages.BannerApplication, MessageArguments.Of(("application", application))));
        }

        if (!string.IsNullOrEmpty(environmentName))
        {
            builder.AppendLine(Localize(resolver, KernelMessages.BannerEnvironment, MessageArguments.Of(("environment", environmentName))));
        }

        builder.AppendLine(Localize(resolver, KernelMessages.BannerRuntime, MessageArguments.Of(("runtime", RuntimeInformation.FrameworkDescription), ("architecture", RuntimeInformation.ProcessArchitecture.ToString()))));
        builder.AppendLine(Localize(resolver, KernelMessages.BannerOperatingSystem, MessageArguments.Of(("os", RuntimeInformation.OSDescription))));
        return builder.ToString();
    }

    /// <summary>
    /// 按列写 Logo，带颜色时每个非空格字符按所在列取色
    /// </summary>
    private static void AppendLogo(StringBuilder builder, bool color)
    {
        var lines = Lines(Logo);
        if (!color)
        {
            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }

            return;
        }

        // 色相按全图最长的行归一化，同一列在每一行颜色相同，渐变是竖直的色带
        var width = lines.Max(static line => line.Length);
        foreach (var line in lines)
        {
            (int R, int G, int B)? current = null;
            for (var column = 0; column < line.Length; column++)
            {
                var character = line[column];
                // 空格不着色，也不打断当前颜色，序列只在颜色变化时写
                if (character != ' ')
                {
                    var rgb = Rainbow((double)column / Math.Max(1, width - 1));
                    if (rgb != current)
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"\u001b[38;2;{rgb.R};{rgb.G};{rgb.B}m");
                        current = rgb;
                    }
                }

                builder.Append(character);
            }

            // 每行末尾复位，后面的文字与其他进程输出不会被染色
            builder.Append(Reset).AppendLine();
        }
    }

    /// <summary>
    /// 渐变进度对应的颜色
    /// </summary>
    /// <param name="progress">0 到 1 的进度</param>
    private static (int R, int G, int B) Rainbow(double progress)
    {
        var hue = (int)(Math.Clamp(progress, 0, 1) * HueSpan);
        // HSV 转 RGB，饱和度与明度都取 1
        var h = hue / 60.0;
        var x = 1 - Math.Abs((h % 2) - 1);
        var (r, g, b) = h switch
        {
            < 1 => (1.0, x, 0.0),
            < 2 => (x, 1.0, 0.0),
            < 3 => (0.0, 1.0, x),
            < 4 => (0.0, x, 1.0),
            < 5 => (x, 0.0, 1.0),
            _ => (1.0, 0.0, x),
        };
        return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    /// <summary>
    /// 按默认区域取文案
    /// </summary>
    /// <remarks>
    /// Banner 在特性启动之前打印，本地化特性还没有设置进程默认作用域：有解析器时以空区域（即默认区域）直接解析，没有解析器或找不到键时退回 <c>key.L()</c>
    /// </remarks>
    private static string Localize(IMessageResolver? resolver, string key, IReadOnlyDictionary<string, object?>? arguments)
        => resolver is not null && resolver.TryResolve(key, null, arguments, out var text) ? text : key.L(arguments);

    /// <summary>
    /// 把多行常量拆成行
    /// </summary>
    /// <remarks>
    /// 原始字符串的换行随源文件而定，这里统一去掉 <c>\r</c>，再由 <see cref="StringBuilder.AppendLine()"/> 按当前平台换行
    /// </remarks>
    private static string[] Lines(string text) => text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
}
