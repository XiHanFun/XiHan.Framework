#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanThreadingModule
// Guid:62981b36-b74b-4b1b-8da2-d63cd2f25cfe
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 5:57:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Threading;

/// <summary>
/// XiHanThreadingModule
/// </summary>
public class XiHanThreadingModule : XiHanModule
{
    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        _ = context.Services.AddSingleton<ICancellationTokenProvider>(NullCancellationTokenProvider.Instance);
        _ = context.Services.AddSingleton(typeof(IAmbientScopeProvider<>), typeof(AmbientDataContextAmbientScopeProvider<>));
    }
}
