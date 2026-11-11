// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.WebAuthn.Tests;

/// <summary>
/// 回放 spec/conformance/vectors/security/webauthn.json
/// </summary>
/// <remarks>
/// 向量是规范的一部分，测试只回放；设 <c>XIHAN_UPDATE_WEBAUTHN_VECTORS=1</c> 时先用测试认证器重新生成并写回规范目录
/// </remarks>
public sealed class WebAuthnVectorTests
{
    /// <summary>
    /// 重新生成向量的环境变量
    /// </summary>
    private const string UpdateVariable = "XIHAN_UPDATE_WEBAUTHN_VECTORS";

    /// <summary>
    /// 向量的依赖方
    /// </summary>
    private const string RelyingPartyId = "example.com";

    /// <summary>
    /// 向量的来源
    /// </summary>
    private const string Origin = "https://example.com";

    /// <summary>
    /// 向量文件的序列化选项：camelCase、缩进、不写空值
    /// </summary>
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// 向量文件的路径
    /// </summary>
    private static string VectorPath => Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "security", "webauthn.json");

    [Fact]
    public void Vectors_ReplayAgainstTheReferenceImplementation()
    {
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VectorPath)!);
            File.WriteAllText(VectorPath, JsonSerializer.Serialize(Generate(), _json) + "\n");
        }

        var file = JsonSerializer.Deserialize<VectorFile>(File.ReadAllText(VectorPath), _json)!;
        var ceremonies = new WebAuthnCeremonies(new WebAuthnOptions
        {
            RelyingPartyId = file.RelyingParty.Id,
            Origins = file.RelyingParty.Origins,
            UserVerification = Enum.Parse<WebAuthnRequirement>(file.RelyingParty.UserVerification, ignoreCase: true),
        });
        Assert.NotEmpty(file.Registration);
        Assert.NotEmpty(file.Authentication);

        foreach (var vector in file.Registration)
        {
            var result = ceremonies.VerifyRegistration(vector.Response, vector.Challenge);
            if (vector.Error is { } error)
            {
                AssertError(vector.Name, result, error);
                continue;
            }

            var expected = vector.Expected!;
            Assert.True(result.IsSuccess, $"{vector.Name}: {result.Error?.Code}");
            var credential = result.ValueOrThrow();
            Assert.Equal(
                (expected.CredentialId, expected.PublicKey, expected.Algorithm, expected.SignCount, expected.UserVerified, expected.BackupEligible, expected.BackedUp, expected.AttestationFormat),
                (credential.CredentialId, credential.PublicKey, credential.Algorithm, credential.SignCount, credential.UserVerified, credential.BackupEligible, credential.BackedUp, credential.AttestationFormat));
        }

        foreach (var vector in file.Authentication)
        {
            var result = ceremonies.VerifyAuthentication(vector.Response, vector.Challenge, vector.Credential, vector.ExpectedUserHandle);
            if (vector.Error is { } error)
            {
                AssertError(vector.Name, result, error);
                continue;
            }

            var expected = vector.Expected!;
            Assert.True(result.IsSuccess, $"{vector.Name}: {result.Error?.Code}");
            var verified = result.ValueOrThrow();
            Assert.Equal((expected.SignCount, expected.UserVerified, expected.BackedUp), (verified.SignCount, verified.UserVerified, verified.BackedUp));
        }
    }

    /// <summary>
    /// 断言向量期望的错误
    /// </summary>
    private static void AssertError<T>(string name, Result<T> result, ExpectedError error)
    {
        Assert.True(result.IsFailure, $"{name}: expected {error.Code}.");
        Assert.Equal((name, error.Code), (name, result.Error!.Code));
        if (error.Reason is not null)
        {
            Assert.Equal((name, error.Reason), (name, result.Error.Details?["reason"] as string));
        }

        if (error.Algorithm is { } algorithm)
        {
            Assert.Equal((name, algorithm), (name, Convert.ToInt64(result.Error.Details?["algorithm"], System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// 用测试认证器生成向量
    /// </summary>
    private static VectorFile Generate()
    {
        var registration = new List<RegistrationVector>();
        var authentication = new List<AuthenticationVector>();
        var handle = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("user-0001"));
        WebAuthnTestCeremony Ceremony(string challenge) => new() { Challenge = challenge, Origin = Origin, RelyingPartyId = RelyingPartyId };
        string Challenge() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        using var es256 = new WebAuthnTestAuthenticator(-7);
        using var ps256 = new WebAuthnTestAuthenticator(-37);
        using var rs256 = new WebAuthnTestAuthenticator(-257);
        using var other = new WebAuthnTestAuthenticator(-7);

        // 成功的注册：三种算法与一个不校验声明的 packed 格式
        foreach (var (name, authenticator, format) in new[] { ("es256-none", es256, "none"), ("ps256-none", ps256, "none"), ("rs256-none", rs256, "none"), ("es256-packed-unverified", es256, "packed") })
        {
            var challenge = Challenge();
            registration.Add(new RegistrationVector(name, challenge, authenticator.Register(Ceremony(challenge) with { AttestationFormat = format }), new ExpectedRegistration(authenticator.CredentialId, authenticator.PublicKey, authenticator.Algorithm, 0, true, true, true, format), null));
        }

        // 失败的注册：每条只改一处
        void Failed(string name, Func<string, WebAuthnRegistrationResponse> build, string code, string? reason, long? algorithm = null)
        {
            var challenge = Challenge();
            registration.Add(new RegistrationVector(name, challenge, build(challenge), null, new ExpectedError(code, reason, algorithm)));
        }

        Failed("challenge-mismatch", _ => es256.Register(Ceremony(Challenge())), SecurityErrors.WebAuthnVerificationFailed, "challenge");
        Failed("type-mismatch", c => es256.Register(Ceremony(c) with { Type = "webauthn.get" }), SecurityErrors.WebAuthnVerificationFailed, "type");
        Failed("origin-not-allowed", c => es256.Register(Ceremony(c) with { Origin = "https://evil.example" }), SecurityErrors.WebAuthnVerificationFailed, "origin");
        Failed("cross-origin", c => es256.Register(Ceremony(c) with { CrossOrigin = true }), SecurityErrors.WebAuthnVerificationFailed, "cross_origin");
        Failed("rp-id-mismatch", c => es256.Register(Ceremony(c) with { RelyingPartyId = "evil.example" }), SecurityErrors.WebAuthnVerificationFailed, "rp_id");
        Failed("user-not-present", c => es256.Register(Ceremony(c) with { UserPresent = false }), SecurityErrors.WebAuthnVerificationFailed, "user_present");
        Failed("user-not-verified", c => es256.Register(Ceremony(c) with { UserVerified = false }), SecurityErrors.WebAuthnVerificationFailed, "user_verified");
        Failed("backed-up-without-eligibility", c => es256.Register(Ceremony(c) with { BackupEligible = false, BackedUp = true }), SecurityErrors.WebAuthnResponseMalformed, "flags");
        Failed("type-not-public-key", c => es256.Register(Ceremony(c)) with { Type = "password" }, SecurityErrors.WebAuthnResponseMalformed, "id");
        Failed("raw-id-mismatch", c => es256.Register(Ceremony(c)) with { RawId = other.CredentialId }, SecurityErrors.WebAuthnResponseMalformed, "id");
        Failed("client-data-not-object", c => WithClientData(es256.Register(Ceremony(c)), "[]"), SecurityErrors.WebAuthnResponseMalformed, "client_data");
        Failed("attestation-object-not-cbor", c => Crafted(c, "none", true, Encoding.ASCII.GetBytes("not cbor"), raw: true), SecurityErrors.WebAuthnResponseMalformed, "attestation_object");
        Failed("authenticator-data-truncated", c => Crafted(c, "none", true, new byte[36]), SecurityErrors.WebAuthnResponseMalformed, "authenticator_data");
        Failed("none-statement-not-empty", c => Crafted(c, "none", false, AttestedData(Ec2Key())), SecurityErrors.WebAuthnResponseMalformed, "attestation");
        Failed("eddsa-public-key", c => Crafted(c, "none", true, AttestedData(OkpKey())), SecurityErrors.WebAuthnAlgorithmUnsupported, null, -8);

        // 认证：以 es256 注册得到的凭据为准
        var stored = new WebAuthnStoredCredential(es256.CredentialId, es256.PublicKey, 0, handle);
        void Authenticated(string name, WebAuthnTestAuthenticator authenticator, WebAuthnStoredCredential credential, uint signCount, Func<WebAuthnTestCeremony, WebAuthnTestCeremony> tamper, ExpectedAssertion? expected, ExpectedError? error)
        {
            var challenge = Challenge();
            authenticator.SignCount = signCount;
            authentication.Add(new AuthenticationVector(name, challenge, credential, null, authenticator.Authenticate(tamper(Ceremony(challenge) with { UserHandle = handle })), expected, error));
        }

        Authenticated("es256-uncounted", es256, stored, 0, static c => c, new ExpectedAssertion(0, true, true), null);
        Authenticated("es256-counter-increases", es256, stored with { SignCount = 5 }, 6, static c => c, new ExpectedAssertion(6, true, true), null);
        Authenticated("ps256", ps256, new WebAuthnStoredCredential(ps256.CredentialId, ps256.PublicKey, 0, handle), 0, static c => c, new ExpectedAssertion(0, true, true), null);
        Authenticated("rs256", rs256, new WebAuthnStoredCredential(rs256.CredentialId, rs256.PublicKey, 0, handle), 0, static c => c, new ExpectedAssertion(0, true, true), null);
        Authenticated("challenge-mismatch", es256, stored, 0, c => c with { Challenge = Challenge() }, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "challenge", null));
        Authenticated("type-mismatch", es256, stored, 0, static c => c with { Type = "webauthn.create" }, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "type", null));
        Authenticated("origin-not-allowed", es256, stored, 0, static c => c with { Origin = "https://evil.example" }, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "origin", null));
        Authenticated("rp-id-mismatch", es256, stored, 0, static c => c with { RelyingPartyId = "evil.example" }, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "rp_id", null));
        Authenticated("user-not-verified", es256, stored, 0, static c => c with { UserVerified = false }, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "user_verified", null));
        Authenticated("signature-corrupted", es256, stored, 0, static c => c with { CorruptSignature = true }, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "signature", null));
        Authenticated("user-handle-mismatch", es256, stored, 0, c => c with { UserHandle = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("user-0002")) }, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "user_handle", null));
        Authenticated("other-credential", other, stored, 0, static c => c, null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "credential", null));
        Authenticated("counter-regressed", es256, stored with { SignCount = 6 }, 6, static c => c, null, new ExpectedError(SecurityErrors.WebAuthnCounterRegressed, null, null));

        // 凭据属于谁（ADR-0179）：先认出用户的流程传期望用户，凭据不属于他即拒绝；免用户名的流程响应必须带句柄
        es256.SignCount = 0;
        var victim = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("user-0003"));
        var ownedChallenge = Challenge();
        authentication.Add(new AuthenticationVector("credential-of-another-user", ownedChallenge, stored, victim, es256.Authenticate(Ceremony(ownedChallenge)), null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "user_handle", null)));
        var identifiedChallenge = Challenge();
        authentication.Add(new AuthenticationVector("identified-user-without-handle", identifiedChallenge, stored, handle, es256.Authenticate(Ceremony(identifiedChallenge)), new ExpectedAssertion(0, true, true), null));
        var usernamelessChallenge = Challenge();
        authentication.Add(new AuthenticationVector("usernameless-without-handle", usernamelessChallenge, stored, null, es256.Authenticate(Ceremony(usernamelessChallenge)), null, new ExpectedError(SecurityErrors.WebAuthnVerificationFailed, "user_handle", null)));

        return new VectorFile(new VectorRelyingParty(RelyingPartyId, [Origin], "required"), registration, authentication);
    }

    /// <summary>
    /// 换掉注册响应的客户端数据
    /// </summary>
    private static WebAuthnRegistrationResponse WithClientData(WebAuthnRegistrationResponse response, string json)
        => response with { Response = response.Response with { ClientDataJson = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json)) } };

    /// <summary>
    /// 手工组一份测试认证器造不出的注册响应
    /// </summary>
    /// <remarks>
    /// 坏 CBOR、截断的认证器数据、非空的 none 声明、EdDSA 公钥
    /// </remarks>
    private static WebAuthnRegistrationResponse Crafted(string challenge, string format, bool emptyStatement, byte[] authenticatorData, bool raw = false)
    {
        var clientData = Encoding.UTF8.GetBytes($$"""{"type":"webauthn.create","challenge":"{{challenge}}","origin":"{{Origin}}","crossOrigin":false}""");
        byte[] attestation;
        if (raw)
        {
            attestation = authenticatorData;
        }
        else
        {
            var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
            writer.WriteStartMap(3);
            writer.WriteTextString("fmt");
            writer.WriteTextString(format);
            writer.WriteTextString("attStmt");
            writer.WriteStartMap(emptyStatement ? 0 : 1);
            if (!emptyStatement)
            {
                writer.WriteTextString("x");
                writer.WriteInt32(1);
            }

            writer.WriteEndMap();
            writer.WriteTextString("authData");
            writer.WriteByteString(authenticatorData);
            writer.WriteEndMap();
            attestation = writer.Encode();
        }

        var id = Base64Url.EncodeToString(CraftedCredentialId);
        return new WebAuthnRegistrationResponse(id, id, "public-key", new WebAuthnAttestationResponse(Base64Url.EncodeToString(clientData), Base64Url.EncodeToString(attestation), null));
    }

    /// <summary>
    /// 手工组的凭据 ID
    /// </summary>
    private static byte[] CraftedCredentialId { get; } = Encoding.ASCII.GetBytes("crafted-credential");

    /// <summary>
    /// 带证明凭据数据的认证器数据：UP、UV、BE、BS、AT 都置位
    /// </summary>
    private static byte[] AttestedData(byte[] cose)
    {
        var data = new byte[37 + 18 + CraftedCredentialId.Length + cose.Length];
        SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId), data.AsSpan(0, 32));
        data[32] = 0x01 | 0x04 | 0x08 | 0x10 | 0x40;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(37 + 16, 2), (ushort)CraftedCredentialId.Length);
        CraftedCredentialId.CopyTo(data, 37 + 18);
        cose.CopyTo(data, 37 + 18 + CraftedCredentialId.Length);
        return data;
    }

    /// <summary>
    /// 一把合法的 ES256 COSE 公钥
    /// </summary>
    private static byte[] Ec2Key()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var point = ecdsa.ExportParameters(false).Q;
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        writer.WriteInt32(-7);
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(point.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(point.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>
    /// 一把 EdDSA（Ed25519）的 COSE 公钥：结构合法，算法不支持
    /// </summary>
    private static byte[] OkpKey()
    {
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(4);
        writer.WriteInt32(1);
        writer.WriteInt32(1);
        writer.WriteInt32(3);
        writer.WriteInt32(-8);
        writer.WriteInt32(-1);
        writer.WriteInt32(6);
        writer.WriteInt32(-2);
        writer.WriteByteString(RandomNumberGenerator.GetBytes(32));
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>
    /// 从测试输出目录向上找框架工作区根（以 <c>XiHan.Framework.slnx</c> 为标记）
    /// </summary>
    private static string LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate 'XiHan.Framework.slnx'.");
    }

    /// <summary>
    /// 向量文件
    /// </summary>
    /// <param name="RelyingParty">依赖方</param>
    /// <param name="Registration">注册样例</param>
    /// <param name="Authentication">认证样例</param>
    private sealed record VectorFile(VectorRelyingParty RelyingParty, IReadOnlyList<RegistrationVector> Registration, IReadOnlyList<AuthenticationVector> Authentication);

    /// <summary>
    /// 向量的依赖方
    /// </summary>
    /// <param name="Id">依赖方 ID</param>
    /// <param name="Origins">允许的来源</param>
    /// <param name="UserVerification">用户验证要求</param>
    private sealed record VectorRelyingParty(string Id, IReadOnlyList<string> Origins, string UserVerification);

    /// <summary>
    /// 一条注册样例
    /// </summary>
    /// <param name="Name">名称</param>
    /// <param name="Challenge">依赖方保存的挑战</param>
    /// <param name="Response">浏览器交回的响应</param>
    /// <param name="Expected">成功时的凭据</param>
    /// <param name="Error">失败时的错误</param>
    private sealed record RegistrationVector(string Name, string Challenge, WebAuthnRegistrationResponse Response, ExpectedRegistration? Expected, ExpectedError? Error);

    /// <summary>
    /// 一条认证样例
    /// </summary>
    /// <param name="Name">名称</param>
    /// <param name="Challenge">依赖方保存的挑战</param>
    /// <param name="Credential">存储的凭据</param>
    /// <param name="ExpectedUserHandle">登录前已认出的用户；免用户名的流程为空（ADR-0179）</param>
    /// <param name="Response">浏览器交回的响应</param>
    /// <param name="Expected">成功时的结果</param>
    /// <param name="Error">失败时的错误</param>
    private sealed record AuthenticationVector(string Name, string Challenge, WebAuthnStoredCredential Credential, string? ExpectedUserHandle, WebAuthnAuthenticationResponse Response, ExpectedAssertion? Expected, ExpectedError? Error);

    /// <summary>
    /// 注册成功时的期望
    /// </summary>
    /// <param name="CredentialId">凭据 ID</param>
    /// <param name="PublicKey">COSE 公钥</param>
    /// <param name="Algorithm">算法</param>
    /// <param name="SignCount">签名计数</param>
    /// <param name="UserVerified">用户已验证</param>
    /// <param name="BackupEligible">可备份</param>
    /// <param name="BackedUp">已备份</param>
    /// <param name="AttestationFormat">证明格式</param>
    private sealed record ExpectedRegistration(string CredentialId, string PublicKey, int Algorithm, uint SignCount, bool UserVerified, bool BackupEligible, bool BackedUp, string AttestationFormat);

    /// <summary>
    /// 认证成功时的期望
    /// </summary>
    /// <param name="SignCount">新的签名计数</param>
    /// <param name="UserVerified">用户已验证</param>
    /// <param name="BackedUp">已备份</param>
    private sealed record ExpectedAssertion(uint SignCount, bool UserVerified, bool BackedUp);

    /// <summary>
    /// 失败时的期望
    /// </summary>
    /// <param name="Code">错误码</param>
    /// <param name="Reason">参数 <c>reason</c></param>
    /// <param name="Algorithm">参数 <c>algorithm</c></param>
    private sealed record ExpectedError(string Code, string? Reason, long? Algorithm);
}
