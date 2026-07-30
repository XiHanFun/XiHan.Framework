// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Options;
using XiHan.Framework.Bot.Sms.Enums;

namespace XiHan.Framework.Bot.Sms.Messaging;

/// <summary>
/// 短信网关解析器实现
/// </summary>
/// <remarks>
/// 每次解析都读 <see cref="ISmsConfigStore"/> 当前生效配置（store 实现负责租户过滤与凭证解密），
/// 网关客户端按配置指纹缓存复用——指纹变化即重建，改配置即热生效。
/// </remarks>
public sealed class SmsGatewayResolver : ISmsGatewayResolver
{
    private readonly ISmsConfigStore _configStore;

    private readonly ConcurrentDictionary<long, CachedClient> _cache = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configStore">短信配置存储</param>
    public SmsGatewayResolver(ISmsConfigStore configStore)
    {
        _configStore = configStore;
    }

    /// <inheritdoc />
    public async Task<ISmsGatewayClient?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetAsync(cancellationToken);
        if (config is null || !config.IsEnabled)
        {
            return null;
        }

        var fingerprint = Fingerprint(config);
        var cached = _cache.AddOrUpdate(
            config.ConfigId,
            _ => new CachedClient(fingerprint, Build(config)),
            (_, existing) => existing.Fingerprint == fingerprint ? existing : new CachedClient(fingerprint, Build(config)));
        return cached.Client;
    }

    private static string Fingerprint(SmsChannelConfig config)
    {
        return string.Join('|',
            config.ConfigId, (int)config.Provider, config.AccessKeyId, config.AccessKeySecret,
            config.SdkAppId, config.SignName, config.Region, config.TemplateMap, config.IsEnabled);
    }

    private static ISmsGatewayClient Build(SmsChannelConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.AccessKeySecret))
        {
            throw new InvalidOperationException("短信网关访问密钥为空。");
        }

        return config.Provider switch
        {
            SmsProviderType.Aliyun => new AliyunSmsGatewayClient(
                config.AccessKeyId, config.AccessKeySecret, config.SignName, config.TemplateMap),
            SmsProviderType.TencentCloud => new TencentCloudSmsGatewayClient(
                config.AccessKeyId, config.AccessKeySecret,
                config.SdkAppId ?? throw new InvalidOperationException("腾讯云短信配置缺少应用ID（SmsSdkAppId）。"),
                config.Region ?? throw new InvalidOperationException("腾讯云短信配置缺少地域。"),
                config.SignName, config.TemplateMap),
            _ => throw new InvalidOperationException($"不支持的短信服务商：{config.Provider}。")
        };
    }

    private sealed record CachedClient(string Fingerprint, ISmsGatewayClient Client);
}
