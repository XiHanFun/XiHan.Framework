#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultSmsConfigStore
// Guid:ac9e72ce-9d90-4230-aa48-7b93cc2ff01f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
