// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// 默认 OpenApi 客户端存储（基于配置）
/// </summary>
public class DefaultOpenApiSecurityClientStore : IOpenApiSecurityClientStore
{
    private readonly IOptionsMonitor<XiHanOpenApiSecurityOptions> _optionsMonitor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="optionsMonitor"></param>
    public DefaultOpenApiSecurityClientStore(IOptionsMonitor<XiHanOpenApiSecurityOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public Task<OpenApiSecurityClient?> FindByAccessKeyAsync(string accessKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            return Task.FromResult<OpenApiSecurityClient?>(null);
        }

        var normalizedAccessKey = accessKey.Trim();
        var options = _optionsMonitor.CurrentValue;
        var configuredClient = options.Clients.FirstOrDefault(item =>
            string.Equals(item.AccessKey, normalizedAccessKey, StringComparison.OrdinalIgnoreCase));

        if (configuredClient is null)
        {
            return Task.FromResult<OpenApiSecurityClient?>(null);
        }

        var client = new OpenApiSecurityClient
        {
            AccessKey = configuredClient.AccessKey,
            SecretKey = configuredClient.SecretKey,
            EncryptKey = configuredClient.EncryptKey,
            PublicKey = configuredClient.PublicKey,
            Sm2PublicKey = configuredClient.Sm2PublicKey,
            SignatureAlgorithm = configuredClient.SignatureAlgorithm,
            ContentSignatureAlgorithm = configuredClient.ContentSignatureAlgorithm,
            EncryptionAlgorithm = configuredClient.EncryptionAlgorithm,
            AllowResponseEncryption = configuredClient.AllowResponseEncryption,
            IpWhitelist = [.. configuredClient.IpWhitelist],
            IsEnabled = configuredClient.IsEnabled
        };

        return Task.FromResult<OpenApiSecurityClient?>(client);
    }
}
