#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOpenApiSecurityMiddleware
// Guid:fb5111ef-1775-4ed3-bc0a-b76de69f5f0f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 23:36:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Security.Cryptography;
using XiHan.Framework.Utils.Security.Cryptography;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Security.OpenApi;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// OpenApi 安全中间件（签名、内容签名、加密、防重放）
/// </summary>
public class XiHanOpenApiSecurityMiddleware(
    RequestDelegate next,
    ILogger<XiHanOpenApiSecurityMiddleware> logger,
    IOptionsMonitor<XiHanOpenApiSecurityOptions> optionsMonitor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string> SupportedSignatureAlgorithms =
    [
        "HMACSHA1",
        "HMACSHA256",
        "HMACSHA512",
        "RSASHA256",
        "SM2"
    ];

    private static readonly HashSet<string> SupportedContentSignatureAlgorithms =
    [
        "SHA256",
        "SHA512",
        "MD5"
    ];

    private static readonly HashSet<string> SupportedEncryptionAlgorithms =
    [
        "NONE",
        "AES",
        "AES-CBC",
        "BLOWFISH"
    ];

    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <param name="clientStore"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context, IOpenApiSecurityClientStore clientStore)
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.IsEnabled || ShouldSkipByPath(context.Request.Path, options))
        {
            await next(context);
            return;
        }

        if (context.GetEndpoint()?.Metadata.GetMetadata<IgnoreApiSecurityAttribute>() is not null)
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var headers = context.Request.Headers;
        var hasSecurityHeaders = HasAnySecurityHeaders(headers);
        if (!hasSecurityHeaders && options.AllowUnsignedRequests)
        {
            await next(context);
            return;
        }

        if (!hasSecurityHeaders)
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status401Unauthorized, "缺少安全请求头");
            return;
        }

        var accessKey = headers[OpenApiSecurityConstants.AccessKeyHeaderName].FirstOrDefault()?.Trim();
        var timestampRaw = headers[OpenApiSecurityConstants.TimestampHeaderName].FirstOrDefault()?.Trim();
        var nonce = headers[OpenApiSecurityConstants.NonceHeaderName].FirstOrDefault()?.Trim();
        var signature = headers[OpenApiSecurityConstants.SignatureHeaderName].FirstOrDefault()?.Trim();
        var contentSignHeader = headers[OpenApiSecurityConstants.ContentSignHeaderName].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(accessKey) ||
            string.IsNullOrWhiteSpace(timestampRaw) ||
            string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(signature))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status401Unauthorized, "安全请求头不完整");
            return;
        }

        if (!long.TryParse(timestampRaw, out var timestampUnix))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status400BadRequest, "时间戳格式错误");
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
        var now = DateTimeOffset.UtcNow;
        if (Math.Abs((now - requestTime).TotalSeconds) > Math.Max(1, options.TimestampToleranceSeconds))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status401Unauthorized, "请求时间窗已失效");
            return;
        }

        var client = await clientStore.FindByAccessKeyAsync(accessKey, context.RequestAborted);
        if (client is null || !client.IsEnabled)
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status401Unauthorized, "AccessKey 无效或已禁用");
            return;
        }

        if (!IsClientIpAllowed(client, context.Connection.RemoteIpAddress?.ToString()))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status403Forbidden, "请求 IP 不在白名单内");
            return;
        }

        if (!await TryAcquireNonceAsync(context, options, accessKey, nonce))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status409Conflict, "检测到重复请求");
            return;
        }

        var signatureAlgorithm = ResolveAlgorithm(
            headers[OpenApiSecurityConstants.SignatureAlgorithmHeaderName].FirstOrDefault(),
            client.SignatureAlgorithm,
            options.DefaultSignatureAlgorithm);
        var contentSignAlgorithm = ResolveAlgorithm(
            headers[OpenApiSecurityConstants.ContentSignAlgorithmHeaderName].FirstOrDefault(),
            client.ContentSignatureAlgorithm,
            options.DefaultContentSignatureAlgorithm);
        var encryptionAlgorithm = ResolveAlgorithm(
            headers[OpenApiSecurityConstants.EncryptAlgorithmHeaderName].FirstOrDefault(),
            client.EncryptionAlgorithm,
            options.DefaultEncryptionAlgorithm);

        if (!SupportedSignatureAlgorithms.Contains(signatureAlgorithm))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status400BadRequest, $"不支持的签名算法: {signatureAlgorithm}");
            return;
        }

        if (!SupportedContentSignatureAlgorithms.Contains(contentSignAlgorithm))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status400BadRequest, $"不支持的内容签名算法: {contentSignAlgorithm}");
            return;
        }

        if (!SupportedEncryptionAlgorithms.Contains(encryptionAlgorithm))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status400BadRequest, $"不支持的加密算法: {encryptionAlgorithm}");
            return;
        }

        string requestBody;
        try
        {
            requestBody = await ReadRequestBodyAsync(context.Request, options, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "读取请求体失败");
            await WriteSecurityErrorAsync(context, StatusCodes.Status400BadRequest, "请求体读取失败");
            return;
        }

        var encryptedIvHeader = headers[OpenApiSecurityConstants.EncryptIvHeaderName].FirstOrDefault();
        var plaintextBody = requestBody;
        var isRequestEncrypted = !IsNoEncryption(encryptionAlgorithm) && !string.IsNullOrWhiteSpace(requestBody);
        if (isRequestEncrypted)
        {
            try
            {
                plaintextBody = DecryptPayload(
                    requestBody,
                    encryptionAlgorithm,
                    client,
                    nonce,
                    encryptedIvHeader,
                    out var resolvedIvBase64);

                ReplaceRequestBody(context.Request, plaintextBody);
                context.Items[XiHanWebApiConstants.RequestBodyItemKey] = plaintextBody;
                if (!string.IsNullOrWhiteSpace(resolvedIvBase64))
                {
                    context.Response.Headers[OpenApiSecurityConstants.EncryptIvHeaderName] = resolvedIvBase64;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "请求体解密失败");
                await WriteSecurityErrorAsync(context, StatusCodes.Status400BadRequest, "请求体解密失败");
                return;
            }
        }

        var requestContentForSign = plaintextBody ?? string.Empty;
        var computedContentSign = ComputeContentSignature(requestContentForSign, contentSignAlgorithm);
        if (options.RequireContentSignature && string.IsNullOrWhiteSpace(contentSignHeader))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status401Unauthorized, "缺少内容签名");
            return;
        }

        if (!string.IsNullOrWhiteSpace(contentSignHeader) &&
            !string.Equals(computedContentSign, contentSignHeader, StringComparison.OrdinalIgnoreCase))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status401Unauthorized, "内容签名校验失败");
            return;
        }

        var contentSignForCanonical = string.IsNullOrWhiteSpace(contentSignHeader)
            ? computedContentSign
            : contentSignHeader;

        var canonicalRequest = BuildCanonicalRequest(
            context.Request.Method,
            context.Request.Path.ToString(),
            BuildCanonicalQuery(context.Request.Query),
            contentSignForCanonical,
            timestampRaw,
            nonce);
        if (!VerifySignature(signatureAlgorithm, canonicalRequest, signature, client))
        {
            await WriteSecurityErrorAsync(context, StatusCodes.Status401Unauthorized, "请求签名校验失败");
            return;
        }

        context.Items[OpenApiSecurityConstants.SecurityClientContextKey] = client;

        var shouldEncryptResponse = ShouldEncryptResponse(
            options,
            client,
            isRequestEncrypted,
            headers[OpenApiSecurityConstants.EncryptResponseHeaderName].FirstOrDefault(),
            encryptionAlgorithm);

        if (!shouldEncryptResponse)
        {
            await next(context);
            return;
        }

        await InvokeAndEncryptResponseAsync(context, encryptionAlgorithm, contentSignAlgorithm, client);
    }

    private async Task InvokeAndEncryptResponseAsync(
        HttpContext context,
        string encryptionAlgorithm,
        string contentSignAlgorithm,
        OpenApiSecurityClient client)
    {
        var originBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        await next(context);

        context.Response.Body = originBody;
        responseBuffer.Position = 0;
        if (!ShouldEncryptResponseBody(context.Response, responseBuffer.Length))
        {
            await responseBuffer.CopyToAsync(originBody, context.RequestAborted);
            return;
        }

        var responseText = await ReadStreamAsStringAsync(responseBuffer, context.RequestAborted);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            await responseBuffer.CopyToAsync(originBody, context.RequestAborted);
            return;
        }

        var responseContentSign = ComputeContentSignature(responseText, contentSignAlgorithm);
        string encryptedData;
        string? ivBase64 = null;
        if (IsAesEncryption(encryptionAlgorithm))
        {
            var ivBytes = RandomNumberGenerator.GetBytes(16);
            ivBase64 = Convert.ToBase64String(ivBytes);
            encryptedData = EncryptPayload(responseText, encryptionAlgorithm, client, ivBytes);
        }
        else
        {
            encryptedData = EncryptPayload(responseText, encryptionAlgorithm, client, null);
        }

        var envelope = new SecurePayloadEnvelope
        {
            Alg = encryptionAlgorithm,
            Data = encryptedData,
            Iv = ivBase64,
            ContentSign = responseContentSign
        };
        var encryptedJson = JsonSerializer.Serialize(envelope, JsonOptions);

        context.Response.Headers[OpenApiSecurityConstants.SecureResponseHeaderName] = "1";
        context.Response.Headers[OpenApiSecurityConstants.ContentSignHeaderName] = responseContentSign;
        context.Response.Headers[OpenApiSecurityConstants.EncryptAlgorithmHeaderName] = encryptionAlgorithm;
        if (!string.IsNullOrWhiteSpace(ivBase64))
        {
            context.Response.Headers[OpenApiSecurityConstants.EncryptIvHeaderName] = ivBase64;
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = Encoding.UTF8.GetByteCount(encryptedJson);
        await context.Response.WriteAsync(encryptedJson, context.RequestAborted);
    }

    private static bool ShouldSkipByPath(PathString requestPath, XiHanOpenApiSecurityOptions options)
    {
        var path = requestPath.ToString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var normalizedPath = path.Trim();
        if (options.IgnoredPathPrefixes.Any(prefix =>
                !string.IsNullOrWhiteSpace(prefix) &&
                normalizedPath.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (options.ProtectedPathPrefixes.Count == 0)
        {
            return false;
        }

        return !options.ProtectedPathPrefixes.Any(prefix =>
            !string.IsNullOrWhiteSpace(prefix) &&
            normalizedPath.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAnySecurityHeaders(IHeaderDictionary headers)
    {
        return headers.ContainsKey(OpenApiSecurityConstants.AccessKeyHeaderName) ||
               headers.ContainsKey(OpenApiSecurityConstants.TimestampHeaderName) ||
               headers.ContainsKey(OpenApiSecurityConstants.NonceHeaderName) ||
               headers.ContainsKey(OpenApiSecurityConstants.SignatureHeaderName) ||
               headers.ContainsKey(OpenApiSecurityConstants.ContentSignHeaderName) ||
               headers.ContainsKey(OpenApiSecurityConstants.EncryptAlgorithmHeaderName);
    }

    private static bool ShouldEncryptResponse(
        XiHanOpenApiSecurityOptions options,
        OpenApiSecurityClient client,
        bool isRequestEncrypted,
        string? encryptResponseHeader,
        string encryptionAlgorithm)
    {
        if (!options.EnableResponseEncryption || !client.AllowResponseEncryption || IsNoEncryption(encryptionAlgorithm))
        {
            return false;
        }

        if (IsTruthy(encryptResponseHeader))
        {
            return true;
        }

        return isRequestEncrypted && options.EncryptResponseByDefaultWhenRequestEncrypted;
    }

    private static bool ShouldEncryptResponseBody(HttpResponse response, long responseBodyLength)
    {
        if (response.StatusCode == StatusCodes.Status204NoContent || responseBodyLength <= 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.ContentType))
        {
            return false;
        }

        return response.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCanonicalRequest(
        string method,
        string path,
        string canonicalQuery,
        string contentSign,
        string timestamp,
        string nonce)
    {
        return string.Join(
            '\n',
            method.ToUpperInvariant(),
            path,
            canonicalQuery,
            contentSign,
            timestamp,
            nonce);
    }

    private static string BuildCanonicalQuery(IQueryCollection query)
    {
        if (query.Count == 0)
        {
            return string.Empty;
        }

        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var (key, values) in query.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (values.Count == 0)
            {
                pairs.Add(new KeyValuePair<string, string>(key, string.Empty));
                continue;
            }

            foreach (var value in values.OrderBy(item => item, StringComparer.Ordinal))
            {
                pairs.Add(new KeyValuePair<string, string>(key, value ?? string.Empty));
            }
        }

        return string.Join("&", pairs.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static string ResolveAlgorithm(string? headerAlgorithm, string? clientAlgorithm, string defaultAlgorithm)
    {
        return NormalizeAlgorithm(
            string.IsNullOrWhiteSpace(headerAlgorithm)
                ? (string.IsNullOrWhiteSpace(clientAlgorithm) ? defaultAlgorithm : clientAlgorithm)
                : headerAlgorithm);
    }

    private static string NormalizeAlgorithm(string algorithm)
    {
        return string.IsNullOrWhiteSpace(algorithm)
            ? string.Empty
            : algorithm.Trim().ToUpperInvariant();
    }

    private static bool VerifySignature(string signatureAlgorithm, string canonicalRequest, string signature, OpenApiSecurityClient client)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        switch (NormalizeAlgorithm(signatureAlgorithm))
        {
            case "HMACSHA1":
            case "HMACSHA256":
            case "HMACSHA512":
                {
                    if (string.IsNullOrWhiteSpace(client.SecretKey))
                    {
                        return false;
                    }

                    var hashBytes = HmacHelper.ComputeHmacBytes(
                        NormalizeAlgorithm(signatureAlgorithm),
                        Encoding.UTF8.GetBytes(client.SecretKey),
                        Encoding.UTF8.GetBytes(canonicalRequest));
                    var expectedHex = Convert.ToHexString(hashBytes);
                    if (string.Equals(signature, expectedHex, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var expectedBase64 = Convert.ToBase64String(hashBytes);
                    return string.Equals(signature, expectedBase64, StringComparison.Ordinal);
                }
            case "RSASHA256":
                return !string.IsNullOrWhiteSpace(client.PublicKey) &&
                       RsaHelper.VerifyData(
                           canonicalRequest,
                           signature,
                           client.PublicKey,
                           HashAlgorithmName.SHA256,
                           RSASignaturePadding.Pkcs1);
            case "SM2":
                return !string.IsNullOrWhiteSpace(client.Sm2PublicKey) &&
                       Sm2Helper.VerifyData(canonicalRequest, signature, client.Sm2PublicKey);
            default:
                return false;
        }
    }

    private static string ComputeContentSignature(string content, string contentSignAlgorithm)
    {
        var normalized = NormalizeAlgorithm(contentSignAlgorithm);
        return normalized switch
        {
            "SHA512" => HashHelper.Sha512(content).ToLowerInvariant(),
            "MD5" => HashHelper.Md5(content).ToLowerInvariant(),
            _ => HashHelper.Sha256(content).ToLowerInvariant()
        };
    }

    private static bool IsClientIpAllowed(OpenApiSecurityClient client, string? remoteIp)
    {
        var rules = FlattenIpRules(client.IpWhitelist);
        if (rules.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(remoteIp))
        {
            return false;
        }

        foreach (var rule in rules)
        {
            if (rule == "*")
            {
                return true;
            }

            if (rule.EndsWith('*'))
            {
                var prefix = rule[..^1];
                if (remoteIp.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(remoteIp, rule, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> FlattenIpRules(IReadOnlyCollection<string> rules)
    {
        var result = new List<string>();
        foreach (var raw in rules)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var segments = raw
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    result.Add(segment);
                }
            }
        }

        return result;
    }

    private static async Task<bool> TryAcquireNonceAsync(
        HttpContext context,
        XiHanOpenApiSecurityOptions options,
        string accessKey,
        string nonce)
    {
        if (!options.EnableReplayProtection)
        {
            return true;
        }

        var distributedCache = context.RequestServices.GetService<IDistributedCache>();
        if (distributedCache is null)
        {
            return true;
        }

        var nonceKey = $"openapi:nonce:{accessKey}:{nonce}";
        var cancellationToken = context.RequestAborted;
        var exists = await distributedCache.GetAsync(nonceKey, cancellationToken);
        if (exists is not null)
        {
            return false;
        }

        var cacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, options.NonceExpireSeconds))
        };
        await distributedCache.SetAsync(
            nonceKey,
            [1],
            cacheEntryOptions,
            cancellationToken);

        return true;
    }

    private static async Task<string> ReadRequestBodyAsync(
        HttpRequest request,
        XiHanOpenApiSecurityOptions options,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is null or <= 0)
        {
            return string.Empty;
        }

        if (request.ContentLength > options.MaxRequestBodySize)
        {
            throw new InvalidOperationException("请求体超过限制大小");
        }

        request.EnableBuffering();
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        return body;
    }

    private static async Task<string> ReadStreamAsStringAsync(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static void ReplaceRequestBody(HttpRequest request, string plaintextBody)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintextBody);
        request.Body = new MemoryStream(bytes);
        request.ContentLength = bytes.Length;
    }

    private static string DecryptPayload(
        string requestBody,
        string encryptionAlgorithm,
        OpenApiSecurityClient client,
        string nonce,
        string? ivHeader,
        out string? resolvedIvBase64)
    {
        var hasEnvelope = TryParseEncryptedEnvelope(requestBody, out var encryptedData, out var envelopeIv, out var envelopeAlg);
        var resolvedAlgorithm = !string.IsNullOrWhiteSpace(envelopeAlg)
            ? NormalizeAlgorithm(envelopeAlg)
            : NormalizeAlgorithm(encryptionAlgorithm);

        if (!SupportedEncryptionAlgorithms.Contains(resolvedAlgorithm) || IsNoEncryption(resolvedAlgorithm))
        {
            throw new InvalidOperationException($"不支持的加密算法: {resolvedAlgorithm}");
        }

        if (resolvedAlgorithm == "BLOWFISH")
        {
            resolvedIvBase64 = null;
            return BlowfishHelper.Decrypt(
                hasEnvelope ? encryptedData : requestBody.Trim(),
                ResolveEncryptKey(client));
        }

        var ivBase64 = !string.IsNullOrWhiteSpace(ivHeader) ? ivHeader : envelopeIv;
        var ivBytes = ResolveIvBytes(ivBase64, nonce);
        resolvedIvBase64 = Convert.ToBase64String(ivBytes);
        var cipherBytes = Convert.FromBase64String(hasEnvelope ? encryptedData : requestBody.Trim().Trim('"'));
        var keyBytes = ResolveAesKeyBytes(client);
        var plaintextBytes = AesHelper.DecryptBytes(cipherBytes, keyBytes, ivBytes);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static string EncryptPayload(
        string plaintext,
        string encryptionAlgorithm,
        OpenApiSecurityClient client,
        byte[]? ivBytes)
    {
        var normalized = NormalizeAlgorithm(encryptionAlgorithm);
        if (normalized == "BLOWFISH")
        {
            return BlowfishHelper.Encrypt(plaintext, ResolveEncryptKey(client));
        }

        if (ivBytes is null || ivBytes.Length != 16)
        {
            throw new InvalidOperationException("AES 加密需要 16 字节 IV");
        }

        var keyBytes = ResolveAesKeyBytes(client);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = AesHelper.EncryptBytes(plaintextBytes, keyBytes, ivBytes);
        return Convert.ToBase64String(cipherBytes);
    }

    private static byte[] ResolveAesKeyBytes(OpenApiSecurityClient client)
    {
        var key = ResolveEncryptKey(client);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length is 16 or 24 or 32)
        {
            return keyBytes;
        }

        return SHA256.HashData(keyBytes);
    }

    private static string ResolveEncryptKey(OpenApiSecurityClient client)
    {
        return string.IsNullOrWhiteSpace(client.EncryptKey)
            ? client.SecretKey
            : client.EncryptKey;
    }

    private static byte[] ResolveIvBytes(string? ivBase64, string nonce)
    {
        if (!string.IsNullOrWhiteSpace(ivBase64))
        {
            var parsed = Convert.FromBase64String(ivBase64);
            if (parsed.Length != 16)
            {
                throw new InvalidOperationException("IV 长度必须为 16 字节");
            }

            return parsed;
        }

        var nonceHash = SHA256.HashData(Encoding.UTF8.GetBytes(nonce));
        return nonceHash[..16];
    }

    private static bool TryParseEncryptedEnvelope(
        string requestBody,
        out string encryptedData,
        out string? iv,
        out string? algorithm)
    {
        encryptedData = string.Empty;
        iv = null;
        algorithm = null;

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            encryptedData = dataElement.GetString() ?? string.Empty;
            if (document.RootElement.TryGetProperty("iv", out var ivElement) &&
                ivElement.ValueKind == JsonValueKind.String)
            {
                iv = ivElement.GetString();
            }

            if (document.RootElement.TryGetProperty("alg", out var algElement) &&
                algElement.ValueKind == JsonValueKind.String)
            {
                algorithm = algElement.GetString();
            }

            return !string.IsNullOrWhiteSpace(encryptedData);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNoEncryption(string algorithm)
    {
        return string.IsNullOrWhiteSpace(algorithm) ||
               string.Equals(NormalizeAlgorithm(algorithm), "NONE", StringComparison.Ordinal);
    }

    private static bool IsAesEncryption(string algorithm)
    {
        var normalized = NormalizeAlgorithm(algorithm);
        return normalized is "AES" or "AES-CBC";
    }

    private static bool IsTruthy(string? headerValue)
    {
        return !string.IsNullOrWhiteSpace(headerValue) &&
               (headerValue.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                headerValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                headerValue.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteSecurityErrorAsync(HttpContext context, int statusCode, string message)
    {
        var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString() ?? context.TraceIdentifier;
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = ApiResponse.Fail(message, traceId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }

    private sealed class SecurePayloadEnvelope
    {
        /// <summary>
        /// 加密算法
        /// </summary>
        public string Alg { get; set; } = string.Empty;

        /// <summary>
        /// 密文
        /// </summary>
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// IV(Base64)
        /// </summary>
        public string? Iv { get; set; }

        /// <summary>
        /// 明文内容签名
        /// </summary>
        public string ContentSign { get; set; } = string.Empty;
    }
}
