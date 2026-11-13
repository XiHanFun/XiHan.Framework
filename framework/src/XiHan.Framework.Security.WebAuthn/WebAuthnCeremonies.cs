// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 通行密钥注册与认证仪式的选项生成与响应校验
/// </summary>
/// <remarks>
/// 无状态、线程安全：挑战由应用保存到仪式结束（一次性、有时限），凭据由应用存储。核对顺序与失败的 <c>reason</c> 见 RFC-0008「WebAuthn」，第一处不符即返回
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WebAuthnCeremonies
{
    /// <summary>
    /// 凭据类型
    /// </summary>
    private const string PublicKeyType = "public-key";

    /// <summary>
    /// 注册仪式的客户端数据类型
    /// </summary>
    private const string CreateCeremonyType = "webauthn.create";

    /// <summary>
    /// 认证仪式的客户端数据类型
    /// </summary>
    private const string GetCeremonyType = "webauthn.get";

    /// <summary>
    /// 证明格式 none
    /// </summary>
    private const string NoneAttestation = "none";

    /// <summary>
    /// 用户句柄的最大字节数
    /// </summary>
    private const int MaximumUserHandleLength = 64;

    /// <summary>
    /// 凭据 ID 的最大字节数
    /// </summary>
    private const int MaximumCredentialIdLength = 1023;

    /// <summary>
    /// 接受的算法，按偏好排序
    /// </summary>
    private static readonly WebAuthnCredentialParameter[] _parameters =
    [
        new(PublicKeyType, CoseKey.Es256),
        new(PublicKeyType, CoseKey.Ps256),
        new(PublicKeyType, CoseKey.Rs256),
    ];

    /// <summary>
    /// RP ID 的 SHA-256
    /// </summary>
    private readonly byte[] _rpIdHash;

    /// <summary>
    /// 以选项构造
    /// </summary>
    /// <param name="options">依赖方选项</param>
    /// <exception cref="ArgumentException">某个来源的主机既不是 RP ID 也不是它的子域</exception>
    public WebAuthnCeremonies(WebAuthnOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        WebAuthnOptions.Validate(options);
        Options = options;
        _rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.RelyingPartyId));
    }

    /// <summary>
    /// 依赖方选项
    /// </summary>
    public WebAuthnOptions Options { get; }

    /// <summary>
    /// 生成注册仪式的选项
    /// </summary>
    /// <param name="user">要注册凭据的用户</param>
    /// <param name="excludeCredentials">该用户已有的凭据</param>
    /// <returns>选项；应用保存其中的挑战</returns>
    /// <exception cref="ArgumentException">用户句柄不是 1 到 64 字节的 base64url，或账号名为空</exception>
    public WebAuthnCreationOptions CreateRegistrationOptions(WebAuthnUserEntity user, IReadOnlyList<WebAuthnCredentialDescriptor> excludeCredentials)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(excludeCredentials);
        if (!Base64UrlText.TryDecode(user.Id, out var handle) || handle.Length > MaximumUserHandleLength)
        {
            throw new ArgumentException("The user handle must be 1 to 64 bytes encoded as base64url.", nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Name))
        {
            throw new ArgumentException("The user name must not be empty.", nameof(user));
        }

        var residentKey = Options.ResidentKey;
        return new WebAuthnCreationOptions(
            new WebAuthnRelyingParty(Options.RelyingPartyId, Options.RelyingPartyName ?? Options.RelyingPartyId),
            user,
            NewChallenge(),
            _parameters,
            TimeoutMilliseconds(),
            excludeCredentials,
            new WebAuthnAuthenticatorSelection(ToJson(residentKey), residentKey == WebAuthnRequirement.Required, ToJson(Options.UserVerification)),
            NoneAttestation);
    }

    /// <summary>
    /// 生成认证仪式的选项
    /// </summary>
    /// <param name="allowCredentials">可用的凭据；为空即请求可发现凭据（免用户名登录）</param>
    /// <returns>选项；应用保存其中的挑战</returns>
    public WebAuthnRequestOptions CreateAuthenticationOptions(IReadOnlyList<WebAuthnCredentialDescriptor> allowCredentials)
    {
        ArgumentNullException.ThrowIfNull(allowCredentials);
        return new WebAuthnRequestOptions(NewChallenge(), TimeoutMilliseconds(), Options.RelyingPartyId, allowCredentials, ToJson(Options.UserVerification));
    }

    /// <summary>
    /// 核对注册响应
    /// </summary>
    /// <param name="response">浏览器交回的响应</param>
    /// <param name="expectedChallenge">本次仪式保存的挑战（base64url）</param>
    /// <returns>核对通过的凭据；解析不了为 <c>SECURITY_VALIDATION_006</c>，算法不支持为 <c>SECURITY_VALIDATION_007</c>，核对不通过为 <c>SECURITY_DENIED_005</c></returns>
    /// <exception cref="ArgumentException"><paramref name="expectedChallenge"/> 不是 base64url</exception>
    public Result<WebAuthnRegisteredCredential> VerifyRegistration(WebAuthnRegistrationResponse response, string expectedChallenge)
    {
        ArgumentNullException.ThrowIfNull(response);
        var challenge = DecodeChallenge(expectedChallenge);
        if (!TryReadId(response.Type, response.Id, response.RawId, out var rawId) || response.Response is not { } attestationResponse)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.Id);
        }

        if (!Base64UrlText.TryDecode(attestationResponse.ClientDataJson, out var clientDataJson) || !ClientData.TryParse(clientDataJson, out var clientData) || clientData is null)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.ClientData);
        }

        if (CheckClientData(clientData, CreateCeremonyType, challenge) is { } clientFailure)
        {
            return clientFailure;
        }

        if (!Base64UrlText.TryDecode(attestationResponse.AttestationObject, out var encoded) || !AttestationObject.TryParse(encoded, out var attestation) || attestation is null)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.AttestationObject);
        }

        if (!AuthenticatorData.TryParse(attestation.AuthenticatorData, out var authenticatorData) || authenticatorData is null)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.AuthenticatorData);
        }

        if (CheckAuthenticatorData(authenticatorData) is { } dataFailure)
        {
            return dataFailure;
        }

        if (!authenticatorData.HasAttestedCredential || authenticatorData.CredentialId is not { } credentialId || authenticatorData.CredentialPublicKey is not { } publicKey)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.AttestedCredential);
        }

        if (credentialId.Length > MaximumCredentialIdLength || credentialId.Length == 0 || !credentialId.AsSpan().SequenceEqual(rawId))
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.CredentialId);
        }

        var key = CoseKey.Read(publicKey);
        if (!key.TryGetValue(out var coseKey))
        {
            return key.Error!;
        }

        // 证明声明不校验（依赖方不要求证明）；none 格式按规范必须是空映射
        if (string.Equals(attestation.Format, NoneAttestation, StringComparison.Ordinal) && !attestation.StatementEmpty)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.Attestation);
        }

        var transports = attestationResponse.Transports is { } reported
            ? [.. reported.Where(static t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal)]
            : Array.Empty<string>();
        return new WebAuthnRegisteredCredential(
            Base64Url.EncodeToString(credentialId),
            Base64Url.EncodeToString(publicKey),
            coseKey.Algorithm,
            authenticatorData.SignCount,
            authenticatorData.Aaguid,
            authenticatorData.UserVerified,
            authenticatorData.BackupEligible,
            authenticatorData.BackedUp,
            transports,
            attestation.Format);
    }

    /// <summary>
    /// 核对认证响应
    /// </summary>
    /// <param name="response">浏览器交回的响应</param>
    /// <param name="expectedChallenge">本次仪式保存的挑战（base64url）</param>
    /// <param name="credential">按响应的凭据 ID 找到的存储凭据</param>
    /// <param name="expectedUserHandle">
    /// 登录前已经认出的用户（base64url）：先填账号、或作为第二因素的流程传这个用户，凭据必须属于他；
    /// 免用户名（可发现凭据）的流程传 <see langword="null"/>，此时响应必须带 <c>userHandle</c>，登录的用户取结果的 <see cref="WebAuthnVerifiedAssertion.UserHandle"/>（ADR-0179）
    /// </param>
    /// <returns>核对通过的结果；解析不了为 <c>SECURITY_VALIDATION_006</c>，核对不通过为 <c>SECURITY_DENIED_005</c>，签名计数没有增加为 <c>SECURITY_DENIED_006</c></returns>
    /// <exception cref="ArgumentException"><paramref name="expectedChallenge"/>、存储的凭据 ID、存储的用户句柄或 <paramref name="expectedUserHandle"/> 不是 base64url</exception>
    public Result<WebAuthnVerifiedAssertion> VerifyAuthentication(WebAuthnAuthenticationResponse response, string expectedChallenge, WebAuthnStoredCredential credential, string? expectedUserHandle)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(credential);
        var challenge = DecodeChallenge(expectedChallenge);
        if (!Base64UrlText.TryDecode(credential.CredentialId, out var storedId))
        {
            throw new ArgumentException("The stored credential ID must be base64url.", nameof(credential));
        }

        if (string.IsNullOrEmpty(credential.UserHandle) || !Base64UrlText.TryDecode(credential.UserHandle, out var ownerHandle))
        {
            throw new ArgumentException("The stored credential must record its owner's user handle as base64url.", nameof(credential));
        }

        byte[]? expectedOwner = null;
        if (expectedUserHandle is not null && !Base64UrlText.TryDecode(expectedUserHandle, out expectedOwner))
        {
            throw new ArgumentException("The expected user handle must be base64url.", nameof(expectedUserHandle));
        }

        if (!TryReadId(response.Type, response.Id, response.RawId, out var rawId) || response.Response is not { } assertionResponse)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.Id);
        }

        if (!rawId.AsSpan().SequenceEqual(storedId))
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.Credential);
        }

        if (!Base64UrlText.TryDecode(assertionResponse.ClientDataJson, out var clientDataJson) || !ClientData.TryParse(clientDataJson, out var clientData) || clientData is null)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.ClientData);
        }

        if (CheckClientData(clientData, GetCeremonyType, challenge) is { } clientFailure)
        {
            return clientFailure;
        }

        if (!Base64UrlText.TryDecode(assertionResponse.AuthenticatorData, out var authenticatorBytes) || !AuthenticatorData.TryParse(authenticatorBytes, out var authenticatorData) || authenticatorData is null)
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.AuthenticatorData);
        }

        if (CheckAuthenticatorData(authenticatorData) is { } dataFailure)
        {
            return dataFailure;
        }

        if (!Base64UrlText.TryDecode(credential.PublicKey, out var publicKey))
        {
            return WebAuthnFailures.Malformed(WebAuthnReasons.PublicKey);
        }

        var key = CoseKey.Read(publicKey);
        if (!key.TryGetValue(out var coseKey))
        {
            return key.Error!;
        }

        // 签名覆盖认证器数据与客户端数据的哈希：客户端数据里的挑战与来源由此受签名保护
        var signed = new byte[authenticatorBytes.Length + SHA256.HashSizeInBytes];
        authenticatorBytes.CopyTo(signed, 0);
        SHA256.HashData(clientDataJson, signed.AsSpan(authenticatorBytes.Length));
        if (!Base64UrlText.TryDecode(assertionResponse.Signature, out var signature) || !coseKey.Verify(signed, signature))
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.Signature);
        }

        // 凭据属于谁（WebAuthn §7.2 第 6 步）：登录前已认出用户时，凭据必须属于他，否则攻击者能用自己账号下的凭据签受害者的挑战；
        // 没有认出用户（免用户名）时响应必须带句柄。响应带了句柄就必须等于存储的所属用户，防止拿别人的句柄冒充（ADR-0179）
        // 放在验签之后：签名不对的响应只得到 signature，没有私钥的人试不出凭据属于谁（ADR-0216）
        if (expectedOwner is not null && !expectedOwner.AsSpan().SequenceEqual(ownerHandle))
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.UserHandle);
        }

        if (assertionResponse.UserHandle is { Length: > 0 } receivedHandle)
        {
            if (!Base64UrlText.TryDecode(receivedHandle, out var actualHandle) || !actualHandle.AsSpan().SequenceEqual(ownerHandle))
            {
                return WebAuthnFailures.Denied(WebAuthnReasons.UserHandle);
            }
        }
        else if (expectedOwner is null)
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.UserHandle);
        }

        // 计数在签名之后判定：签名不对的响应不该泄露存储的计数；都为 0 是不计数的认证器
        var received = authenticatorData.SignCount;
        if ((credential.SignCount != 0 || received != 0) && received <= credential.SignCount)
        {
            return WebAuthnFailures.CounterRegressed(credential.SignCount, received);
        }

        return new WebAuthnVerifiedAssertion(Base64Url.EncodeToString(rawId), received, authenticatorData.UserVerified, authenticatorData.BackedUp, Base64Url.EncodeToString(ownerHandle));
    }

    /// <summary>
    /// 要求程度在 JSON 里的写法
    /// </summary>
    private static string ToJson(WebAuthnRequirement requirement) => requirement switch
    {
        WebAuthnRequirement.Preferred => "preferred",
        WebAuthnRequirement.Discouraged => "discouraged",
        _ => "required",
    };

    /// <summary>
    /// 核对凭据类型并解出 rawId
    /// </summary>
    private static bool TryReadId(string? type, string? id, string? rawIdText, out byte[] rawId)
    {
        rawId = [];
        return string.Equals(type, PublicKeyType, StringComparison.Ordinal)
            && Base64UrlText.TryDecode(id, out var idBytes)
            && Base64UrlText.TryDecode(rawIdText, out rawId)
            && idBytes.AsSpan().SequenceEqual(rawId);
    }

    /// <summary>
    /// 解出应用保存的挑战
    /// </summary>
    private static byte[] DecodeChallenge(string expectedChallenge)
    {
        ArgumentNullException.ThrowIfNull(expectedChallenge);
        return Base64UrlText.TryDecode(expectedChallenge, out var challenge)
            ? challenge
            : throw new ArgumentException("The expected challenge must be base64url.", nameof(expectedChallenge));
    }

    /// <summary>
    /// 新挑战
    /// </summary>
    private string NewChallenge() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(Options.ChallengeSize));

    /// <summary>
    /// 超时的毫秒数
    /// </summary>
    private long TimeoutMilliseconds() => (long)Options.Timeout.TotalMilliseconds;

    /// <summary>
    /// 核对客户端数据：类型、挑战、来源、跨源
    /// </summary>
    private Error? CheckClientData(ClientData clientData, string expectedType, byte[] challenge)
    {
        if (!string.Equals(clientData.Type, expectedType, StringComparison.Ordinal))
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.Type);
        }

        // 挑战是一次性的秘密，定长比较不按前缀泄露
        if (!CryptographicOperations.FixedTimeEquals(clientData.Challenge, challenge))
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.Challenge);
        }

        if (!Options.Origins.Contains(clientData.Origin, StringComparer.Ordinal))
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.Origin);
        }

        return clientData.CrossOrigin ? WebAuthnFailures.Denied(WebAuthnReasons.CrossOrigin) : null;
    }

    /// <summary>
    /// 核对认证器数据：RP ID、用户在场与验证、备份标志
    /// </summary>
    private Error? CheckAuthenticatorData(AuthenticatorData data)
    {
        if (!CryptographicOperations.FixedTimeEquals(data.RpIdHash, _rpIdHash))
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.RelyingPartyId);
        }

        if (!data.UserPresent)
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.UserPresent);
        }

        if (Options.UserVerification == WebAuthnRequirement.Required && !data.UserVerified)
        {
            return WebAuthnFailures.Denied(WebAuthnReasons.UserVerified);
        }

        return data.BackedUp && !data.BackupEligible ? WebAuthnFailures.Malformed(WebAuthnReasons.Flags) : null;
    }
}
