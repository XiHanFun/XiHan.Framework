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
    /// XiHan
    /// </summary>
    public static string XiHanInfo => $@"
   _  __ ______  _____    _   __
  | |/ //  _/ / / /   |  / | / /
  |   / / // /_/ / /| | /  |/ /
 /   |_/ // __  / ___ |/ /|  /
/_/|_/___/_/ /_/_/  |_/_/ |_/
v{Assembly.GetAssembly(typeof(XiHan))?.GetName().Version?.ToString()}
Copyright (C)2021-Present ZhaiFanhua All Rights Reserved.
https://docs.xihanfun.com
https://github.com/XiHanFun
https://github.com/XiHanFun/XiHan.Framework

碧落降恩承淑颜，共挚崎缘挽曦寒。
迁般故事终成忆，谨此葳蕤换思短。
—— 致她

快速、轻量、高效、用心的开发框架和组件库。基于 DotNet 和 Vue 构建。
";
}
