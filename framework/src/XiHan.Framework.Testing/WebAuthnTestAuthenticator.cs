// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.WebAuthn;

namespace XiHan.Framework.Testing;

/// <summary>
/// 测试用的通行密钥认证器
/// </summary>
/// <remarks>
/// 在内存里持有一把私钥，按 WebAuthn 的格式生成注册与认证响应，给 <see cref="WebAuthnCeremonies"/> 与应用的测试用；签名计数取 <see cref="SignCount"/> 原值，不自动递增
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WebAuthnTestAuthenticator : IDisposable
{
    /// <summary>
    /// ES256 的 COSE 编号
    /// </summary>
    private const int Es256 = -7;

    /// <summary>
    /// PS256 的 COSE 编号
    /// </summary>
    private const int Ps256 = -37;

    /// <summary>
    /// RS256 的 COSE 编号
    /// </summary>
    private const int Rs256 = -257;

    /// <summary>
    /// ES256 的私钥
    /// </summary>
    /// <remarks>
    /// RSA 认证器为空
    /// </remarks>
    private readonly ECDsa? _ecdsa;

    /// <summary>
    /// RSA 的私钥
    /// </summary>
    /// <remarks>
    /// EC 认证器为空
    /// </remarks>
    private readonly RSA? _rsa;

    /// <summary>
    /// 凭据 ID 的字节
    /// </summary>
    private readonly byte[] _credentialId;

    /// <summary>
    /// 编码的 COSE 公钥
    /// </summary>
    private readonly byte[] _publicKey;

    /// <summary>
    /// 以 ES256 构造
    /// </summary>
    public WebAuthnTestAuthenticator()
        : this(Es256)
    {
    }

    /// <summary>
    /// 以指定算法构造
    /// </summary>
    /// <param name="algorithm">COSE 算法编号：-7（ES256）、-37（PS256）或 -257（RS256）</param>
    /// <exception cref="ArgumentOutOfRangeException">算法不在这三种里</exception>
    public WebAuthnTestAuthenticator(int algorithm)
    {
        Algorithm = algorithm;
        _credentialId = RandomNumberGenerator.GetBytes(16);
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        switch (algorithm)
        {
            case Es256:
                _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var point = _ecdsa.ExportParameters(false).Q;
                var x = point.X ?? throw new CryptographicException("The exported EC key has no X coordinate.");
                var y = point.Y ?? throw new CryptographicException("The exported EC key has no Y coordinate.");
                // CTAP2 规范序：1、3、-1、-2、-3
                writer.WriteStartMap(5);
                writer.WriteInt32(1);
                writer.WriteInt32(2);
                writer.WriteInt32(3);
                writer.WriteInt32(Es256);
                writer.WriteInt32(-1);
                writer.WriteInt32(1);
                writer.WriteInt32(-2);
                writer.WriteByteString(x);
                writer.WriteInt32(-3);
                writer.WriteByteString(y);
                writer.WriteEndMap();
                break;
            case Ps256 or Rs256:
                _rsa = RSA.Create(2048);
                var parameters = _rsa.ExportParameters(false);
                var modulus = parameters.Modulus ?? throw new CryptographicException("The exported RSA key has no modulus.");
                var exponent = parameters.Exponent ?? throw new CryptographicException("The exported RSA key has no exponent.");
                writer.WriteStartMap(4);
                writer.WriteInt32(1);
                writer.WriteInt32(3);
                writer.WriteInt32(3);
                writer.WriteInt32(algorithm);
                writer.WriteInt32(-1);
                writer.WriteByteString(modulus);
                writer.WriteInt32(-2);
                writer.WriteByteString(exponent);
                writer.WriteEndMap();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "The test authenticator supports ES256 (-7), PS256 (-37) and RS256 (-257).");
        }

        _publicKey = writer.Encode();
    }

    /// <summary>
    /// COSE 算法编号
    /// </summary>
    public int Algorithm { get; }

    /// <summary>
    /// 凭据 ID（base64url）
    /// </summary>
    public string CredentialId => Base64Url.EncodeToString(_credentialId);

    /// <summary>
    /// COSE 公钥（base64url），与注册核对得到的相同
    /// </summary>
    public string PublicKey => Base64Url.EncodeToString(_publicKey);

    /// <summary>
    /// 写进认证器数据的签名计数
    /// </summary>
    public uint SignCount { get; set; }

    /// <summary>
    /// 生成注册响应
    /// </summary>
    /// <param name="ceremony">仪式的输入与篡改项</param>
    public WebAuthnRegistrationResponse Register(WebAuthnTestCeremony ceremony)
    {
        ArgumentNullException.ThrowIfNull(ceremony);
        var clientData = ClientData(ceremony, "webauthn.create");
        var credential = new byte[16 + 2 + _credentialId.Length + _publicKey.Length];
        // AAGUID 留全零：none 证明本就不透露型号
        BinaryPrimitives.WriteUInt16BigEndian(credential.AsSpan(16, 2), (ushort)_credentialId.Length);
        _credentialId.CopyTo(credential, 18);
        _publicKey.CopyTo(credential, 18 + _credentialId.Length);
        var authenticatorData = AuthenticatorData(ceremony, attested: true, credential);

        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(ceremony.AttestationFormat);
        writer.WriteTextString("attStmt");
        if (string.Equals(ceremony.AttestationFormat, "none", StringComparison.Ordinal))
        {
            writer.WriteStartMap(0);
        }
        else
        {
            // 依赖方不校验证明声明，这里写一份形似 packed 的占位
            writer.WriteStartMap(2);
            writer.WriteTextString("alg");
            writer.WriteInt32(Algorithm);
            writer.WriteTextString("sig");
            writer.WriteByteString(RandomNumberGenerator.GetBytes(64));
        }

        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authenticatorData);
        writer.WriteEndMap();

        var id = CredentialId;
        return new WebAuthnRegistrationResponse(id, id, "public-key", new WebAuthnAttestationResponse(Base64Url.EncodeToString(clientData), Base64Url.EncodeToString(writer.Encode()), ["internal"]));
    }

    /// <summary>
    /// 生成认证响应
    /// </summary>
    /// <param name="ceremony">仪式的输入与篡改项</param>
    public WebAuthnAuthenticationResponse Authenticate(WebAuthnTestCeremony ceremony)
    {
        ArgumentNullException.ThrowIfNull(ceremony);
        var clientData = ClientData(ceremony, "webauthn.get");
        var authenticatorData = AuthenticatorData(ceremony, attested: false, []);
        var signed = new byte[authenticatorData.Length + SHA256.HashSizeInBytes];
        authenticatorData.CopyTo(signed, 0);
        SHA256.HashData(clientData, signed.AsSpan(authenticatorData.Length));
        // ES256 签名用 DER 编码，与浏览器交回的一致
        var signature = (_ecdsa, _rsa) switch
        {
            ({ } ecdsa, _) => ecdsa.SignData(signed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence),
            (_, { } rsa) => rsa.SignData(signed, HashAlgorithmName.SHA256, Algorithm == Ps256 ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1),
            _ => throw new ObjectDisposedException(nameof(WebAuthnTestAuthenticator)),
        };
        if (ceremony.CorruptSignature)
        {
            signature[^1] ^= 0x01;
        }

        var id = CredentialId;
        return new WebAuthnAuthenticationResponse(id, id, "public-key", new WebAuthnAssertionResponse(Base64Url.EncodeToString(clientData), Base64Url.EncodeToString(authenticatorData), Base64Url.EncodeToString(signature), ceremony.UserHandle));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ecdsa?.Dispose();
        _rsa?.Dispose();
    }

    /// <summary>
    /// 客户端数据的 UTF-8 字节
    /// </summary>
    private static byte[] ClientData(WebAuthnTestCeremony ceremony, string type)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("type", ceremony.Type ?? type);
            json.WriteString("challenge", ceremony.Challenge);
            json.WriteString("origin", ceremony.Origin);
            json.WriteBoolean("crossOrigin", ceremony.CrossOrigin);
            json.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// 认证器数据：RP ID 哈希、标志、计数，注册时接证明凭据数据
    /// </summary>
    private byte[] AuthenticatorData(WebAuthnTestCeremony ceremony, bool attested, byte[] credential)
    {
        var data = new byte[37 + credential.Length];
        SHA256.HashData(Encoding.UTF8.GetBytes(ceremony.RelyingPartyId), data.AsSpan(0, 32));
        byte flags = 0;
        flags |= ceremony.UserPresent ? (byte)0x01 : (byte)0;
        flags |= ceremony.UserVerified ? (byte)0x04 : (byte)0;
        flags |= ceremony.BackupEligible ? (byte)0x08 : (byte)0;
        flags |= ceremony.BackedUp ? (byte)0x10 : (byte)0;
        flags |= attested ? (byte)0x40 : (byte)0;
        data[32] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(33, 4), SignCount);
        credential.CopyTo(data, 37);
        return data;
    }
}
