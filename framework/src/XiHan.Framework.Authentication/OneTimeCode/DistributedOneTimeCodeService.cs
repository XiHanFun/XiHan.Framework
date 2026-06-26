#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedOneTimeCodeService
// Guid:b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/11 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XiHan.Framework.Authentication.OneTimeCode;

/// <summary>
/// 一次性验证码服务实现（分布式缓存后端）
/// </summary>
/// <remarks>
/// 存储依赖标准 <see cref="IDistributedCache"/> 抽象：宿主默认内存实现即可用，
/// 接入 Redis 后自动获得多实例水平扩展与进程重启不丢码能力。
/// 消费语义：读取后无论校验结果立即删除（消费即销毁），同一枚码不可二次尝试。
/// 注：跨实例并发消费的极小竞态窗口（Get 与 Remove 之间）与单实例 TryRemove 语义等价，
/// 如需严格原子消费可在 Redis 后端以 Lua 脚本实现并替换本服务。
/// </remarks>
public sealed class DistributedOneTimeCodeService : IOneTimeCodeService
{
    private const string KeyPrefix = "xihan:auth:otc";

    private readonly IDistributedCache _cache;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="cache">分布式缓存</param>
    public DistributedOneTimeCodeService(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<OneTimeCodeIssueResult> IssueAsync(
        string purpose,
        string target,
        string? payload = null,
        OneTimeCodeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? new OneTimeCodeOptions();
        if (effectiveOptions.CodeLength is < 4 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "验证码长度必须在 4-10 位之间。");
        }

        if (effectiveOptions.ExpiresInSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "验证码有效期必须大于 0 秒。");
        }

        var code = GenerateCode(effectiveOptions.CodeLength);
        var state = new CodeState(code, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state);

        await _cache.SetAsync(
            BuildKey(purpose, target),
            bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(effectiveOptions.ExpiresInSeconds)
            },
            cancellationToken);

        return new OneTimeCodeIssueResult(code, effectiveOptions.ExpiresInSeconds);
    }

    /// <inheritdoc />
    public async Task<OneTimeCodeConsumeResult> TryConsumeAsync(
        string purpose,
        string target,
        string? code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(code))
        {
            return new OneTimeCodeConsumeResult(false, null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(purpose, target);
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return new OneTimeCodeConsumeResult(false, null);
        }

        // 消费即销毁：校验前先删除，杜绝同一枚码的重放与暴力穷举
        await _cache.RemoveAsync(key, cancellationToken);

        CodeState? state;
        try
        {
            state = JsonSerializer.Deserialize<CodeState>(bytes);
        }
        catch (JsonException)
        {
            return new OneTimeCodeConsumeResult(false, null);
        }

        if (state is null || !FixedTimeEquals(state.Code, code.Trim()))
        {
            return new OneTimeCodeConsumeResult(false, null);
        }

        return new OneTimeCodeConsumeResult(true, state.Payload);
    }

    /// <summary>
    /// 生成加密安全的纯数字验证码
    /// </summary>
    private static string GenerateCode(int length)
    {
        var builder = new StringBuilder(length);
        for (var index = 0; index < length; index++)
        {
            builder.Append(RandomNumberGenerator.GetInt32(0, 10));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 恒定时间比较，防止计时侧信道
    /// </summary>
    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string BuildKey(string purpose, string target)
    {
        return $"{KeyPrefix}:{purpose.Trim().ToLowerInvariant()}:{target.Trim().ToLowerInvariant()}";
    }

    /// <summary>
    /// 验证码状态（缓存序列化载体）
    /// </summary>
    /// <param name="Code">验证码明文</param>
    /// <param name="Payload">签发时暂存的负载</param>
    private sealed record CodeState(string Code, string? Payload);
}
