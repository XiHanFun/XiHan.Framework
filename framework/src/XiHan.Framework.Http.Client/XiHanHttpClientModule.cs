#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHttpClientModule
// Guid:bd5cd31c-c791-42d9-a48c-56ead4293941
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:35:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Http.Client;

/// <summary>
/// 曦寒框架网络请求客户端模块
/// </summary>
[DependsOn(
    typeof(XiHanHttpModule)
    )]
public class XiHanHttpClientModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
    }
}
