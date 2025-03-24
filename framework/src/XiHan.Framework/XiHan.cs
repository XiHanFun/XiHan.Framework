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

namespace XiHan.Framework;

/// <summary>
/// 曦寒项目信息
/// </summary>
public static class XiHan
{
    /// <summary>
    /// Logo
    /// </summary>
    public static string Logo => $@"
██╗  ██╗██╗██╗  ██╗ █████╗ ███╗   ██╗
╚██╗██╔╝██║██║  ██║██╔══██╗████╗  ██║
 ╚███╔╝ ██║███████║███████║██╔██╗ ██║
 ██╔██╗ ██║██╔══██║██╔══██║██║╚██╗██║
██╔╝ ██╗██║██║  ██║██║  ██║██║ ╚████║
╚═╝  ╚═╝╚═╝╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝";

    /// <summary>
    /// 版本
    /// </summary>
    public static string Version => $@"v{Assembly.GetAssembly(typeof(XiHan))?.GetName().Version?.ToString()}" ?? "v1.0.0";

    /// <summary>
    /// 版权
    /// </summary>
    public static string Copyright => $@"Copyright (C)2021-Present ZhaiFanhua All Rights Reserved.";

    /// <summary>
    /// 文档
    /// </summary>
    public static string Doc => $@"https://docs.xihanfun.com";

    /// <summary>
    /// 组织
    /// </summary>
    public static string Org => $@"https://github.com/XiHanFun";

    /// <summary>
    /// 仓库
    /// </summary>
    public static string Rep => $@"https://github.com/XiHanFun/XiHan.Framework";

    /// <summary>
    /// 寄语
    /// </summary>
    public static string SendWord => $@"
碧落降恩承淑颜，共挚崎缘挽曦寒。
迁般故事终成忆，谨此葳蕤换思短。
              —— 致她
";

    /// <summary>
    /// 标语
    /// </summary>
    public static string Tagline => $@"快速、轻量、高效、用心的开发框架和组件库。基于 DotNet 和 Vue 构建。";

    /// <summary>
    /// 欢迎使用曦寒
    /// </summary>
    public static void SayHello()
    {
        Console.WriteLine(Logo);
        Console.WriteLine(Version);
        Console.WriteLine(Copyright);
        Console.WriteLine(Doc);
        Console.WriteLine(Org);
        Console.WriteLine(Rep);
        Console.WriteLine(SendWord);
        Console.WriteLine(Tagline);
        Console.WriteLine();
    }
}
