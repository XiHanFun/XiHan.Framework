// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Authentication.Oidc;

namespace XiHan.Framework.Authentication.Tests.Oidc;

public class OidcSigningKeyProviderConcurrencyTests
{
    [Fact]
    public async Task ConcurrentInitialization_UsesSamePersistedKey()
    {
        const int providerCount = 24;
        var directory = Path.Combine(Path.GetTempPath(), $"xihan-oidc-{Guid.NewGuid():N}");
        var keyPath = Path.Combine(directory, "signing.pem");
        using var startBarrier = new Barrier(providerCount);

        try
        {
            var providers = Enumerable.Range(0, providerCount)
                .Select(_ => new OidcSigningKeyProvider(Options.Create(new OidcOptions
                {
                    SigningKeyPath = keyPath,
                    AutoGenerateSigningKey = true
                })))
                .ToArray();

            var keyIds = await Task.WhenAll(providers.Select(provider => Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                return provider.GetSigningCredentials().Key.KeyId;
            })));
            var jwksDocuments = providers.Select(provider => provider.GetJsonWebKeySetJson()).ToArray();

            Assert.Single(keyIds.Distinct());
            Assert.Single(jwksDocuments.Distinct(StringComparer.Ordinal));
            Assert.True(File.Exists(keyPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Restart_ReadsPublishedKey()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xihan-oidc-{Guid.NewGuid():N}");
        var keyPath = Path.Combine(directory, "signing.pem");
        var options = Options.Create(new OidcOptions
        {
            SigningKeyPath = keyPath,
            AutoGenerateSigningKey = true
        });

        try
        {
            var firstKeyId = new OidcSigningKeyProvider(options).GetSigningCredentials().Key.KeyId;
            var restartedProvider = new OidcSigningKeyProvider(options);
            var restartedKeyId = restartedProvider.GetSigningCredentials().Key.KeyId;

            Assert.Equal(firstKeyId, restartedKeyId);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void InvalidExistingKey_FailsExplicitly()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xihan-oidc-{Guid.NewGuid():N}");
        var keyPath = Path.Combine(directory, "signing.pem");
        Directory.CreateDirectory(directory);
        File.WriteAllText(keyPath, "partial pem");

        try
        {
            var provider = new OidcSigningKeyProvider(Options.Create(new OidcOptions
            {
                SigningKeyPath = keyPath,
                AutoGenerateSigningKey = true
            }));

            var exception = Assert.Throws<InvalidOperationException>(() => provider.GetSigningCredentials());
            Assert.Contains("密钥文件无效", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
