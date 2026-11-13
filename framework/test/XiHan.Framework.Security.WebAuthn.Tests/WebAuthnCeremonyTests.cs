// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Text;
using System.Text.Json;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Testing;
using static XiHan.Framework.Security.WebAuthn.Tests.WebAuthnFixture;

namespace XiHan.Framework.Security.WebAuthn.Tests;

public sealed class WebAuthnCeremonyTests
{
    /// <summary>
    /// 一个用户句柄
    /// </summary>
    private static readonly string _userHandle = Base64Url.EncodeToString(Guid.NewGuid().ToByteArray());

    /// <summary>
    /// 浏览器侧的 JSON 约定：camelCase
    /// </summary>
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 支持的三种算法
    /// </summary>
    public static TheoryData<int> Algorithms => new(-7, -37, -257);

    /// <summary>
    /// 注册并核对，返回存储用的凭据
    /// </summary>
    private static WebAuthnStoredCredential Registered(WebAuthnTestAuthenticator authenticator)
    {
        var challenge = NewChallenge();
        var registered = Ceremonies.VerifyRegistration(authenticator.Register(Ceremony(challenge)), challenge).ValueOrThrow();
        return new WebAuthnStoredCredential(registered.CredentialId, registered.PublicKey, registered.SignCount, _userHandle);
    }

    [Fact]
    public void Options_MatchTheWebAuthnJsonShape()
    {
        var creation = Ceremonies.CreateRegistrationOptions(new WebAuthnUserEntity(_userHandle, "alice", "Alice"), [new WebAuthnCredentialDescriptor("public-key", "AAEC", null)]);
        var json = JsonSerializer.SerializeToElement(creation, _web);

        Assert.Equal(("example.com", "example.com"), (json.GetProperty("rp").GetProperty("id").GetString(), json.GetProperty("rp").GetProperty("name").GetString()));
        Assert.Equal(32, Base64Url.DecodeFromChars(json.GetProperty("challenge").GetString()).Length);
        Assert.Equal([-7, -37, -257], json.GetProperty("pubKeyCredParams").EnumerateArray().Select(static p => p.GetProperty("alg").GetInt32()));
        Assert.Equal(300_000, json.GetProperty("timeout").GetInt64());
        Assert.Equal(("required", true, "required"), (json.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString(), json.GetProperty("authenticatorSelection").GetProperty("requireResidentKey").GetBoolean(), json.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString()));
        Assert.Equal("none", json.GetProperty("attestation").GetString());
        // 没有传输方式的凭据不写这个字段：浏览器的 parseCreationOptionsFromJSON 不认 null
        Assert.False(json.GetProperty("excludeCredentials")[0].TryGetProperty("transports", out _));

        var request = Ceremonies.CreateAuthenticationOptions([]);
        Assert.Equal(("example.com", "required", 0), (request.RpId, request.UserVerification, request.AllowList.Count));
        Assert.NotEqual(creation.Challenge, request.Challenge);

        Assert.Throws<ArgumentException>(() => Ceremonies.CreateRegistrationOptions(new WebAuthnUserEntity(Base64Url.EncodeToString(new byte[65]), "alice", "Alice"), []));
        Assert.Throws<ArgumentException>(() => Ceremonies.CreateRegistrationOptions(new WebAuthnUserEntity(_userHandle, " ", "Alice"), []));
    }

    [Theory]
    [MemberData(nameof(Algorithms))]
    public void Registration_ThenAuthentication_Succeed(int algorithm)
    {
        using var authenticator = new WebAuthnTestAuthenticator(algorithm);
        var challenge = NewChallenge();
        var registered = Ceremonies.VerifyRegistration(authenticator.Register(Ceremony(challenge)), challenge).ValueOrThrow();

        Assert.Equal((authenticator.CredentialId, authenticator.PublicKey, algorithm, 0u, Guid.Empty), (registered.CredentialId, registered.PublicKey, registered.Algorithm, registered.SignCount, registered.Aaguid));
        Assert.Equal((true, true, true, "none"), (registered.UserVerified, registered.BackupEligible, registered.BackedUp, registered.AttestationFormat));
        Assert.Equal(["internal"], registered.Transports);

        var stored = new WebAuthnStoredCredential(registered.CredentialId, registered.PublicKey, registered.SignCount, _userHandle);
        var login = NewChallenge();
        var verified = Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(login) with { UserHandle = _userHandle }), login, stored, null).ValueOrThrow();
        Assert.Equal((registered.CredentialId, 0u, true, true, _userHandle), (verified.CredentialId, verified.SignCount, verified.UserVerified, verified.BackedUp, verified.UserHandle));
    }

    [Fact]
    public void Responses_RoundTripThroughWebJson()
    {
        using var authenticator = new WebAuthnTestAuthenticator();
        var challenge = NewChallenge();
        var json = JsonSerializer.Serialize(authenticator.Register(Ceremony(challenge)), _web);

        // 字段名与浏览器的 toJSON() 一致：clientDataJSON 全大写
        Assert.Contains("\"clientDataJSON\":", json, StringComparison.Ordinal);
        var response = JsonSerializer.Deserialize<WebAuthnRegistrationResponse>(json, _web)!;
        Assert.True(Ceremonies.VerifyRegistration(response, challenge).IsSuccess);
    }

    [Fact]
    public void Registration_ChecksTheResponseInOrder()
    {
        using var authenticator = new WebAuthnTestAuthenticator();
        var challenge = NewChallenge();
        WebAuthnRegistrationResponse Register(WebAuthnTestCeremony ceremony) => authenticator.Register(ceremony);

        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(NewChallenge())), challenge), SecurityErrors.WebAuthnVerificationFailed, "challenge");
        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { Type = "webauthn.get" }), challenge), SecurityErrors.WebAuthnVerificationFailed, "type");
        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { Origin = "https://evil.com" }), challenge), SecurityErrors.WebAuthnVerificationFailed, "origin");
        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { CrossOrigin = true }), challenge), SecurityErrors.WebAuthnVerificationFailed, "cross_origin");
        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { RelyingPartyId = "evil.com" }), challenge), SecurityErrors.WebAuthnVerificationFailed, "rp_id");
        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { UserPresent = false }), challenge), SecurityErrors.WebAuthnVerificationFailed, "user_present");
        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { UserVerified = false }), challenge), SecurityErrors.WebAuthnVerificationFailed, "user_verified");
        AssertFailure(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { BackupEligible = false, BackedUp = true }), challenge), SecurityErrors.WebAuthnResponseMalformed, "flags");

        // 另一个来源也在允许的列表里；没要求用户验证时不验证也可以；证明格式不是 none 时照收、不校验声明
        Assert.True(Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { Origin = "https://login.example.com" }), challenge).IsSuccess);
        var lenient = new WebAuthnCeremonies(Options with { UserVerification = WebAuthnRequirement.Preferred });
        Assert.False(lenient.VerifyRegistration(Register(Ceremony(challenge) with { UserVerified = false }), challenge).ValueOrThrow().UserVerified);
        Assert.Equal("packed", Ceremonies.VerifyRegistration(Register(Ceremony(challenge) with { AttestationFormat = "packed" }), challenge).ValueOrThrow().AttestationFormat);

        var valid = Register(Ceremony(challenge));
        AssertFailure(Ceremonies.VerifyRegistration(valid with { Type = "password" }, challenge), SecurityErrors.WebAuthnResponseMalformed, "id");
        AssertFailure(Ceremonies.VerifyRegistration(valid with { RawId = Base64Url.EncodeToString(new byte[16]) }, challenge), SecurityErrors.WebAuthnResponseMalformed, "id");
        AssertFailure(Ceremonies.VerifyRegistration(valid with { Response = valid.Response with { ClientDataJson = Base64Url.EncodeToString("[]"u8) } }, challenge), SecurityErrors.WebAuthnResponseMalformed, "client_data");
        AssertFailure(Ceremonies.VerifyRegistration(valid with { Response = valid.Response with { AttestationObject = "%%%" } }, challenge), SecurityErrors.WebAuthnResponseMalformed, "attestation_object");
        Assert.Throws<ArgumentException>(() => Ceremonies.VerifyRegistration(valid, "%%%"));
    }

    [Fact]
    public void Authentication_ChecksTheResponseInOrder()
    {
        using var authenticator = new WebAuthnTestAuthenticator();
        var stored = Registered(authenticator);
        var challenge = NewChallenge();
        WebAuthnAuthenticationResponse Authenticate(WebAuthnTestCeremony ceremony) => authenticator.Authenticate(ceremony);

        AssertFailure(Ceremonies.VerifyAuthentication(Authenticate(Ceremony(NewChallenge())), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "challenge");
        AssertFailure(Ceremonies.VerifyAuthentication(Authenticate(Ceremony(challenge) with { Type = "webauthn.create" }), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "type");
        AssertFailure(Ceremonies.VerifyAuthentication(Authenticate(Ceremony(challenge) with { RelyingPartyId = "evil.com" }), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "rp_id");
        AssertFailure(Ceremonies.VerifyAuthentication(Authenticate(Ceremony(challenge) with { UserVerified = false }), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "user_verified");
        AssertFailure(Ceremonies.VerifyAuthentication(Authenticate(Ceremony(challenge) with { CorruptSignature = true }), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "signature");
        AssertFailure(Ceremonies.VerifyAuthentication(Authenticate(Ceremony(challenge) with { UserHandle = Base64Url.EncodeToString(Guid.NewGuid().ToByteArray()) }), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "user_handle");

        // 别的凭据的响应、存储的公钥被改坏
        using var other = new WebAuthnTestAuthenticator();
        AssertFailure(Ceremonies.VerifyAuthentication(other.Authenticate(Ceremony(challenge)), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "credential");
        AssertFailure(Ceremonies.VerifyAuthentication(Authenticate(Ceremony(challenge)), challenge, stored with { PublicKey = Base64Url.EncodeToString("\xa0"u8) }, _userHandle), SecurityErrors.WebAuthnResponseMalformed, "public_key");
        // 同一份签名换了客户端数据：签名覆盖客户端数据的哈希
        var valid = Authenticate(Ceremony(challenge));
        var forged = Authenticate(Ceremony(challenge) with { Origin = "https://login.example.com" });
        AssertFailure(Ceremonies.VerifyAuthentication(valid with { Response = valid.Response with { ClientDataJson = forged.Response.ClientDataJson } }, challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "signature");
        Assert.True(Ceremonies.VerifyAuthentication(valid, challenge, stored, _userHandle).IsSuccess);
    }

    /// <summary>
    /// 凭据必须属于登录的那个用户
    /// </summary>
    /// <remarks>
    /// 安全审查 S-04（ADR-0179）：原先只在存储与响应都带句柄时才比对，又没有「期望用户」：知道受害者口令的攻击者用自己账号下的通行密钥签受害者的挑战、省略句柄，就能完成受害者的登录
    /// </remarks>
    [Fact]
    public void IdentifierFirst_ACredentialOfAnotherUser_IsDenied()
    {
        using var attacker = new WebAuthnTestAuthenticator();
        var attackerHandle = Base64Url.EncodeToString(Guid.NewGuid().ToByteArray());
        var attackerCredential = Registered(attacker) with { UserHandle = attackerHandle };
        var victimChallenge = NewChallenge();

        // 应用按响应的凭据 ID 找到的是攻击者自己的凭据：签名、计数都对，只有所属用户不是正在登录的受害者
        var forged = attacker.Authenticate(Ceremony(victimChallenge));
        AssertFailure(Ceremonies.VerifyAuthentication(forged, victimChallenge, attackerCredential, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "user_handle");
        Assert.Equal(attackerHandle, Ceremonies.VerifyAuthentication(forged, victimChallenge, attackerCredential, attackerHandle).ValueOrThrow().UserHandle);
    }

    [Fact]
    public void Usernameless_TheResponseMustCarryTheOwnersHandle()
    {
        using var authenticator = new WebAuthnTestAuthenticator();
        var stored = Registered(authenticator);
        var challenge = NewChallenge();

        // 登录前没认出用户：响应不带句柄就无从确认，带了别人的句柄也不行
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge)), challenge, stored, null), SecurityErrors.WebAuthnVerificationFailed, "user_handle");
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge) with { UserHandle = Base64Url.EncodeToString(Guid.NewGuid().ToByteArray()) }), challenge, stored, null), SecurityErrors.WebAuthnVerificationFailed, "user_handle");
        Assert.Equal(_userHandle, Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge) with { UserHandle = _userHandle }), challenge, stored, null).ValueOrThrow().UserHandle);
    }

    /// <summary>
    /// 所属用户在验签之后判定
    /// </summary>
    /// <remarks>
    /// 安全审查 S-43（ADR-0216）：原先在验签之前判定，没有私钥的人拿任意凭据 ID 与句柄试，就能从 user_handle 与 signature 两种原因分辨凭据属于谁
    /// </remarks>
    [Fact]
    public void Ownership_IsCheckedAfterTheSignature_SoItCannotBeProbedWithoutTheKey()
    {
        using var authenticator = new WebAuthnTestAuthenticator();
        var stored = Registered(authenticator);
        var challenge = NewChallenge();
        var someoneElse = Base64Url.EncodeToString(Guid.NewGuid().ToByteArray());
        var unsigned = Ceremony(challenge) with { CorruptSignature = true };

        // 签名不对：无论猜的是不是所属用户，原因都只是签名
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(unsigned), challenge, stored, someoneElse), SecurityErrors.WebAuthnVerificationFailed, "signature");
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(unsigned), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "signature");
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(unsigned with { UserHandle = someoneElse }), challenge, stored, null), SecurityErrors.WebAuthnVerificationFailed, "signature");
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(unsigned), challenge, stored, null), SecurityErrors.WebAuthnVerificationFailed, "signature");

        // 签名对了才看所属用户
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge)), challenge, stored, someoneElse), SecurityErrors.WebAuthnVerificationFailed, "user_handle");
    }

    /// <summary>
    /// RSA 公钥的模长有上限
    /// </summary>
    /// <remarks>
    /// 安全审查 S-43（ADR-0216）：原先只设下限，几万位的模数让每次验签都很慢
    /// </remarks>
    [Fact]
    public void RsaModulus_LongerThan4096Bits_IsRejected()
    {
        using var authenticator = new WebAuthnTestAuthenticator();
        var stored = Registered(authenticator);
        var challenge = NewChallenge();
        var response = authenticator.Authenticate(Ceremony(challenge));

        AssertFailure(Ceremonies.VerifyAuthentication(response, challenge, stored with { PublicKey = Base64Url.EncodeToString(CoseRsaKey(1024)) }, _userHandle), SecurityErrors.WebAuthnResponseMalformed, "public_key");
        // 4096 位照常读得出公钥，只是这份响应不是它签的
        AssertFailure(Ceremonies.VerifyAuthentication(response, challenge, stored with { PublicKey = Base64Url.EncodeToString(CoseRsaKey(512)) }, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "signature");
    }

    /// <summary>
    /// 手写一个 RS256 的 COSE 公钥
    /// </summary>
    /// <remarks>
    /// 映射 {1: 3, 3: -257, -1: 模数, -2: 65537}；模数最高位置 1、末位为奇数
    /// </remarks>
    private static byte[] CoseRsaKey(int modulusLength)
    {
        var modulus = System.Security.Cryptography.RandomNumberGenerator.GetBytes(modulusLength);
        modulus[0] |= 0x80;
        modulus[^1] |= 0x01;
        byte[] head = [0xA4, 0x01, 0x03, 0x03, 0x39, 0x01, 0x00, 0x20, 0x59, (byte)(modulusLength >> 8), (byte)modulusLength];
        byte[] tail = [0x21, 0x43, 0x01, 0x00, 0x01];
        return [.. head, .. modulus, .. tail];
    }

    [Fact]
    public void SignCount_MustIncrease_UnlessBothAreZero()
    {
        using var authenticator = new WebAuthnTestAuthenticator();
        var stored = Registered(authenticator) with { SignCount = 5 };
        var challenge = NewChallenge();

        authenticator.SignCount = 6;
        Assert.Equal(6u, Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge)), challenge, stored, _userHandle).ValueOrThrow().SignCount);
        authenticator.SignCount = 5;
        var regressed = Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge)), challenge, stored, _userHandle);
        AssertFailure(regressed, SecurityErrors.WebAuthnCounterRegressed, null);
        Assert.Equal((5u, 5u), ((uint)regressed.Error!.Details!["stored"]!, (uint)regressed.Error.Details["received"]!));
        // 计数在签名之后判定：签名不对的响应报签名
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge) with { CorruptSignature = true }), challenge, stored, _userHandle), SecurityErrors.WebAuthnVerificationFailed, "signature");
        authenticator.SignCount = 0;
        Assert.True(Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge)), challenge, stored with { SignCount = 0 }, _userHandle).IsSuccess);
        AssertFailure(Ceremonies.VerifyAuthentication(authenticator.Authenticate(Ceremony(challenge)), challenge, stored, _userHandle), SecurityErrors.WebAuthnCounterRegressed, null);
    }
}
