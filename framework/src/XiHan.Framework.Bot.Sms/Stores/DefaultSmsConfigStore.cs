// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Options;

namespace XiHan.Framework.Bot.Sms.Stores;

/// <summary>
/// 默认短信配置存储（恒未配置）
/// </summary>
/// <remarks>
/// 短信凭证（云厂商 AccessKeySecret）不宜放配置文件，故默认实现不绑定选项、恒返回 null；
/// 须由应用层注册数据库 store 实现覆盖（本实现以 TryAdd 注册），并在实现内完成凭证解密。
/// </remarks>
public class DefaultSmsConfigStore : ISmsConfigStore
{
    /// <inheritdoc />
    public Task<SmsChannelConfig?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SmsChannelConfig?>(null);
    }
}
