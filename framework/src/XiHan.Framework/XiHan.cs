#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHan
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5b5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Text;

namespace XiHan.Framework;

/// <summary>
/// 曦寒项目信息
/// </summary>
public static class XiHan
{
    /// <summary>
    /// 曦寒框架标志
    /// </summary>
    public static string Logo => @"
   _  __ ______  _____    _   __
  | |/ //  _/ / / /   |  / | / /
  |   / / // /_/ / /| | /  |/ /
 /   |_/ // __  / ___ |/ /|  /
/_/|_/___/_/ /_/_/  |_/_/ |_/";

    /// <summary>
    /// 曦寒框架版本
    /// </summary>
    public static string Version => Assembly.GetAssembly(typeof(XiHan))?.GetName().Version?.ToString() ?? string.Empty;

    /// <summary>
    /// 曦寒框架版权
    /// </summary>
    public static string Copyright => @"Copyright (C)2021-Present ZhaiFanhua All Rights Reserved.";

    /// <summary>
    /// 曦寒框架组织
    /// </summary>
    public static string Org => @"https://github.com/XiHanFun";

    /// <summary>
    /// 曦寒框架仓库
    /// </summary>
    public static string Rep => @"https://github.com/XiHanFun/XiHan.Framework";

    /// <summary>
    /// 曦寒框架文档
    /// </summary>
    public static string Doc => @"https://docs.xihanfun.com";

    /// <summary>
    /// 曦寒框架寄语
    /// </summary>
    public static string SendWord => @"
碧落降恩承淑颜，共挚崎缘挽曦寒。
迁般故事终成忆，谨此葳蕤换思短。
              —— 致她
";

    /// <summary>
    /// 曦寒框架标语
    /// </summary>
    public static string Tagline => @"快速、轻量、高效、用心的开发框架和组件库。基于 DotNet 和 Vue 构建。";

    /// <summary>
    /// 入口程序版本
    /// </summary>
    public static string EntryAssemblyVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty;

    /// <summary>
    /// XiHan
    /// </summary>
    public static string SayHello()
    {
        var sb = new StringBuilder();

        sb.AppendLine("欢迎使用曦寒框架");
        sb.AppendLine(Logo);
        sb.AppendLine($"v{Version}");
        sb.AppendLine(Copyright);
        sb.AppendLine(Org);
        sb.AppendLine(Rep);
        sb.AppendLine(Doc);
        sb.AppendLine(SendWord);
        sb.AppendLine(Tagline);
        sb.AppendLine();
        sb.AppendLine($"项目版本:{EntryAssemblyVersion}");

        return sb.ToString();
    }
}
