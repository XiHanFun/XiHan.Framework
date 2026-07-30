// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.OneTimeCode;

/// <summary>
/// 一次性验证码服务
/// </summary>
/// <remarks>
/// 适用于邮箱/短信登录验证码、改绑联系方式验证码等「签发 → 一次性消费」场景：
/// - 验证码以加密安全随机数生成（<see cref="System.Security.Cryptography.RandomNumberGenerator"/>）
/// - 状态存储于 <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>（宿主接入 Redis 时天然支持多实例水平扩展）
/// - 消费即销毁：无论校验成功与否，读取后即删除，杜绝重放与暴力穷举
/// - 可携带负载（payload）：如改绑场景暂存待生效的新邮箱/新手机号，消费成功后取回
/// </remarks>
public interface IOneTimeCodeService
{
    /// <summary>
    /// 签发一次性验证码（同 用途+目标 重复签发将覆盖旧码）
    /// </summary>
    /// <param name="purpose">用途标识（如 auth:email-login，参与存储键隔离）</param>
    /// <param name="target">目标标识（如 租户:邮箱、用户ID:场景，参与存储键隔离）</param>
    /// <param name="payload">随码暂存的负载（消费成功后返回；如改绑场景的新联系方式），可空</param>
    /// <param name="options">签发选项（码长/有效期），缺省取 <see cref="OneTimeCodeOptions"/> 默认值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>签发结果（验证码明文与有效秒数）</returns>
    Task<OneTimeCodeIssueResult> IssueAsync(string purpose, string target, string? payload = null, OneTimeCodeOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 消费一次性验证码（读取后立即销毁，校验失败不可重试同一枚码）
    /// </summary>
    /// <param name="purpose">用途标识（须与签发一致）</param>
    /// <param name="target">目标标识（须与签发一致）</param>
    /// <param name="code">用户提交的验证码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消费结果（是否有效及签发时暂存的负载）</returns>
    Task<OneTimeCodeConsumeResult> TryConsumeAsync(string purpose, string target, string? code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 一次性验证码签发选项
/// </summary>
public sealed class OneTimeCodeOptions
{
    /// <summary>
    /// 验证码长度（纯数字位数），默认 6 位
    /// </summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>
    /// 有效期（秒），默认 600 秒
    /// </summary>
    public int ExpiresInSeconds { get; set; } = 600;
}

/// <summary>
/// 一次性验证码签发结果
/// </summary>
/// <param name="Code">验证码明文（由调用方经邮件/短信等通道下发）</param>
/// <param name="ExpiresInSeconds">有效秒数</param>
public sealed record OneTimeCodeIssueResult(string Code, int ExpiresInSeconds);

/// <summary>
/// 一次性验证码消费结果
/// </summary>
/// <param name="Succeeded">验证码是否有效（不存在/过期/不匹配均为 false）</param>
/// <param name="Payload">签发时暂存的负载（仅 Succeeded 为 true 时有意义）</param>
public sealed record OneTimeCodeConsumeResult(bool Succeeded, string? Payload);
