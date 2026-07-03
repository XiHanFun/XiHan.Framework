#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISmsGatewayResolver
// Guid:40be0989-fe75-4f40-98f8-7e43746d9c0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Sms.Options;

namespace XiHan.Framework.Bot.Sms.Abstractions;

/// <summary>
/// 短信网关解析器
/// </summary>
/// <remarks>
/// 每次解析读取 <see cref="ISmsConfigStore"/> 的当前生效配置，
/// 按配置指纹缓存已构建的网关客户端——改配置即重建（热生效），无需缓存失效事件。
/// 无可用配置时返回 <c>null</c>，调用方须 fail-closed（拒绝发送并记 Failed），杜绝静默假成功。
/// </remarks>
public interface ISmsGatewayResolver
{
    /// <summary>
    /// 解析当前生效的短信网关客户端
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>网关客户端；无可用（启用）配置时返回 null</returns>
    Task<ISmsGatewayClient?> ResolveAsync(CancellationToken cancellationToken = default);
}
