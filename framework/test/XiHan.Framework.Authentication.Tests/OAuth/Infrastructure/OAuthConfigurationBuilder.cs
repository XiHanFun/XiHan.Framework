// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.OAuth;

namespace XiHan.Framework.Authentication.Tests.OAuth.Infrastructure;

/// <summary>
/// 把提供商配置摊平成配置项字典
/// </summary>
public static class OAuthConfigurationBuilder
{
    private const string SectionPrefix = "XiHan:Authentication:OAuth";

    /// <summary>
    /// 生成启用状态下的提供商配置项
    /// </summary>
    /// <param name="providers">提供商配置</param>
    /// <returns>配置项字典</returns>
    public static Dictionary<string, string?> Build(params OAuthProviderConfig[] providers)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{SectionPrefix}:Enabled"] = "true"
        };

        for (var index = 0; index < providers.Length; index++)
        {
            var provider = providers[index];
            var prefix = $"{SectionPrefix}:Providers:{index}:";

            Set(settings, prefix + nameof(OAuthProviderConfig.Name), provider.Name);
            Set(settings, prefix + nameof(OAuthProviderConfig.Provider), provider.Provider);
            Set(settings, prefix + nameof(OAuthProviderConfig.DisplayName), provider.DisplayName);
            Set(settings, prefix + nameof(OAuthProviderConfig.Mode), provider.Mode.ToString());
            Set(settings, prefix + nameof(OAuthProviderConfig.ClientId), provider.ClientId);
            Set(settings, prefix + nameof(OAuthProviderConfig.ClientSecret), provider.ClientSecret);
            Set(settings, prefix + nameof(OAuthProviderConfig.AgentId), provider.AgentId);
            Set(settings, prefix + nameof(OAuthProviderConfig.CorpId), provider.CorpId);
            Set(settings, prefix + nameof(OAuthProviderConfig.CallbackPath), provider.CallbackPath);
            Set(settings, prefix + nameof(OAuthProviderConfig.AuthorizationEndpoint), provider.AuthorizationEndpoint);

            if (provider.LoadMemberProfile)
            {
                Set(settings, prefix + nameof(OAuthProviderConfig.LoadMemberProfile), "true");
            }

            for (var scopeIndex = 0; scopeIndex < provider.Scopes.Length; scopeIndex++)
            {
                Set(settings, $"{prefix}{nameof(OAuthProviderConfig.Scopes)}:{scopeIndex}", provider.Scopes[scopeIndex]);
            }

            foreach (var parameter in provider.AuthorizationParameters)
            {
                Set(settings, $"{prefix}{nameof(OAuthProviderConfig.AuthorizationParameters)}:{parameter.Key}", parameter.Value);
            }
        }

        return settings;
    }

    private static void Set(Dictionary<string, string?> settings, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            settings[key] = value;
        }
    }
}
