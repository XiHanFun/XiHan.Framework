#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExceptionNotifier
// Guid:5ad89f58-b901-474c-935b-815543636d1f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 1:09:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Exceptions.Handling.Abstracts;
using XiHan.Framework.Core.Extensions.Logging;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Exceptions.Handling;

/// <summary>
/// 异常通知器
/// </summary>
public class ExceptionNotifier : IExceptionNotifier, ITransientDependency
{
    /// <summary>
    /// 日志
    /// </summary>
    public ILogger<ExceptionNotifier> Logger { get; set; }

    /// <summary>
    /// 服务作用域工厂
    /// </summary>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    public ExceptionNotifier(IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
        Logger = NullLogger<ExceptionNotifier>.Instance;
    }

    /// <summary>
    /// 通知，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual async Task NotifyAsync([NotNull] ExceptionNotificationContext context)
    {
        CheckHelper.NotNull(context, nameof(context));

        using var scope = ServiceScopeFactory.CreateScope();
        var exceptionSubscribers = scope.ServiceProvider.GetServices<IExceptionSubscriber>();

        foreach (var exceptionSubscriber in exceptionSubscribers)
        {
            try
            {
                await exceptionSubscriber.HandleAsync(context);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("抛出{AssemblyQualifiedName}类型异常。", exceptionSubscriber.GetType().AssemblyQualifiedName);
                Logger.LogException(ex, LogLevel.Warning);
            }
        }
    }
}