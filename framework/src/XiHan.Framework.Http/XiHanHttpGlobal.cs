#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHttpGlobal
// Guid:f1d656c2-acae-4cd0-9f32-593ea2452fc5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 22:12:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Http;

/// <summary>
/// 曦寒框架网络请求全局静态类
/// </summary>
public static class XiHanHttpGlobal
{
    internal static IServiceProvider? ServiceProvider { get; set; }
}
