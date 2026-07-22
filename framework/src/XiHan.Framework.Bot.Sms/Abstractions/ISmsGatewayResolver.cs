// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
