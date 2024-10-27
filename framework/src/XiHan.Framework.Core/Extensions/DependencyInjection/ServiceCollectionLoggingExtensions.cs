#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionLoggingExtensions
// Guid:88d375fa-986c-4179-af80-267ef410f49b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:16:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Logging;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 获取初始化日志
/// </summary>
public static class ServiceCollectionLoggingExtensions
{
    /// <summary>
    /// 获取初始化日志工厂
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static ILogger<T> GetInitLogger<T>(this IServiceCollection services)
    {
        return services.GetSingletonInstance<IInitLoggerFactory>().Create<T>();
    }
}