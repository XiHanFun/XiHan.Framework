#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHttpApplicationBuilder
// Guid:b2c4d6e8-1a2b-3c4d-5e6f-7a8b9c0d1e2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/15 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Http.Builder;

/// <summary>
/// 曦寒框架网络请求应用程序构建器
/// </summary>
public static class XiHanHttpApplicationBuilder
{
    /// <summary>
    /// 初始化曦寒框架网络请求模块
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    public static void InitializeXiHanHttpModule(IServiceProvider serviceProvider)
    {
        // 初始化字符串扩展的HTTP服务
        var httpService = serviceProvider.GetRequiredService<IAdvancedHttpService>();
        StringHttpExtensions.SetHttpService(httpService);
    }
}
