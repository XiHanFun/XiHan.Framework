// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// Provider 共用的装配：选项、派发器、直接发布者与运行时。Provider 的特性先注册 <see cref="IMessageBroker"/> 再调用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingRegistration
{
    /// <summary>
    /// 注册消息子系统的公共服务。
    /// </summary>
    public static MessagingOptions Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = MessagingOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        context.Services.GetMessageSubscriptions();
        context.Services.TryAddSingleton<MessageDispatcher>();
        context.Services.TryAddSingleton<MessagingRuntime>();
        context.Services.TryAddScoped<IMessagePublisher, DirectPublisher>();
        return options;
    }
}
