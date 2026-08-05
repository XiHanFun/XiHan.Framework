// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;

namespace XiHan.Framework.Authentication.Oidc;

/// <summary>
/// OIDC 签名密钥提供器
/// </summary>
public interface IOidcSigningKeyProvider
{
    /// <summary>
    /// 用于签名的私钥凭据（RS256）
    /// </summary>
    SigningCredentials GetSigningCredentials();

    /// <summary>
    /// 对外发布的公钥集（JWKS），只含公钥部分。
    /// </summary>
    string GetJsonWebKeySetJson();
}

/// <summary>
/// 基于 RSA 私钥文件的签名密钥提供器
/// </summary>
/// <remarks>
/// 密钥在首次解析后常驻：签名与 JWKS 必须来自同一把密钥，且 kid 要在进程生命周期内稳定，
/// 否则客户端按 kid 取到的公钥验不了签。故本类型注册为单例。
/// </remarks>
public sealed class OidcSigningKeyProvider : IOidcSigningKeyProvider
{
    private readonly Lazy<RsaSecurityKey> _privateKey;
    private readonly Lazy<string> _jwksJson;
    private readonly OidcOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OidcSigningKeyProvider(IOptions<OidcOptions> options)
    {
        _options = options.Value;
        _privateKey = new Lazy<RsaSecurityKey>(LoadOrCreateKey, LazyThreadSafetyMode.ExecutionAndPublication);
        _jwksJson = new Lazy<string>(BuildJwks, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public SigningCredentials GetSigningCredentials()
    {
        return new SigningCredentials(_privateKey.Value, SecurityAlgorithms.RsaSha256);
    }

    /// <inheritdoc />
    public string GetJsonWebKeySetJson()
    {
        return _jwksJson.Value;
    }

    /// <summary>
    /// 由公钥指纹推导 kid：同一把密钥在任何实例上得到同一个值，换密钥即换 kid。
    /// </summary>
    private static string DeriveKeyId(RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);
        var material = new byte[(parameters.Modulus?.Length ?? 0) + (parameters.Exponent?.Length ?? 0)];
        parameters.Modulus?.CopyTo(material, 0);
        parameters.Exponent?.CopyTo(material, parameters.Modulus?.Length ?? 0);
        return Base64UrlEncoder.Encode(SHA256.HashData(material));
    }

    /// <summary>
    /// 按「配置内联 PEM → 密钥文件 → 自动生成」的顺序取得私钥。
    /// </summary>
    private RsaSecurityKey LoadOrCreateKey()
    {
        var rsa = RSA.Create();

        if (!string.IsNullOrWhiteSpace(_options.SigningKeyPem))
        {
            rsa.ImportFromPem(_options.SigningKeyPem);
        }
        else
        {
            var path = Path.IsPathRooted(_options.SigningKeyPath)
                ? _options.SigningKeyPath
                : Path.Combine(AppContext.BaseDirectory, _options.SigningKeyPath);

            if (File.Exists(path))
            {
                rsa.ImportFromPem(File.ReadAllText(path));
            }
            else if (_options.AutoGenerateSigningKey)
            {
                rsa = RSA.Create(2048);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    _ = Directory.CreateDirectory(directory);
                }
                File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());
            }
            else
            {
                throw new InvalidOperationException(
                    $"OIDC 签名密钥不存在：{path}。请预置密钥，或开启 {OidcOptions.SectionName}:AutoGenerateSigningKey。");
            }
        }

        return new RsaSecurityKey(rsa)
        {
            KeyId = string.IsNullOrWhiteSpace(_options.KeyId) ? DeriveKeyId(rsa) : _options.KeyId
        };
    }

    /// <summary>
    /// 由私钥导出只含公钥部分的 JWKS。
    /// </summary>
    private string BuildJwks()
    {
        var parameters = _privateKey.Value.Rsa.ExportParameters(false);
        var publicKey = new RsaSecurityKey(parameters) { KeyId = _privateKey.Value.KeyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;

        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new { kty = jwk.Kty, use = jwk.Use, alg = jwk.Alg, kid = jwk.Kid, n = jwk.N, e = jwk.E }
            }
        });
    }
}
