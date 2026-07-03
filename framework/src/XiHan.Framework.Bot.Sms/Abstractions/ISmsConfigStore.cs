#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISmsConfigStore
// Guid:107210f7-07e6-4d0b-ac3b-4982d08502dd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Sms.Options;

namespace XiHan.Framework.Bot.Sms.Abstractions;

/// <summary>
/// 短信配置存储
/// </summary>
/// <remarks>
/// 短信凭证不宜放配置文件，默认实现直接返回 null（未配置）；
/// 应用层须注册数据库实现覆盖（TryAdd 语义），并在实现内完成凭证解密。
/// </remarks>
public interface ISmsConfigStore
{
    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前生效配置（凭证已解密）；null 表示未配置（解析器按未配置处理，fail-closed）</returns>
    Task<SmsChannelConfig?> GetAsync(CancellationToken cancellationToken = default);
}
