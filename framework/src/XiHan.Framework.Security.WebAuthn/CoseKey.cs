// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Formats.Cbor;
using System.Security.Cryptography;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 凭据的 COSE 公钥
/// </summary>
/// <remarks>
/// 只认 ES256（EC2、P-256）、PS256 与 RS256（RSA，模长至少 2048 位）
/// </remarks>
internal sealed class CoseKey
{
    /// <summary>
    /// ES256：ECDSA、P-256、SHA-256
    /// </summary>
    public const int Es256 = -7;

    /// <summary>
    /// PS256：RSASSA-PSS、SHA-256
    /// </summary>
    public const int Ps256 = -37;

    /// <summary>
    /// RS256：RSASSA-PKCS1-v1_5、SHA-256
    /// </summary>
    public const int Rs256 = -257;

    /// <summary>
    /// 键类型 EC2
    /// </summary>
    private const long KeyTypeEc2 = 2;

    /// <summary>
    /// 键类型 RSA
    /// </summary>
    private const long KeyTypeRsa = 3;

    /// <summary>
    /// 曲线 P-256
    /// </summary>
    private const long CurveP256 = 1;

    /// <summary>
    /// RSA 模长的下限（字节）
    /// </summary>
    private const int MinimumRsaModulusLength = 256;

    /// <summary>
    /// RSA 模长的上限（字节）
    /// </summary>
    /// <remarks>
    /// 4096 位。验签的开销随模长增长，不设上限时几万位的模数让每次认证都很慢（ADR-0216）
    /// </remarks>
    private const int MaximumRsaModulusLength = 512;

    /// <summary>
    /// EC 公钥参数
    /// </summary>
    /// <remarks>
    /// RSA 键为空
    /// </remarks>
    private readonly ECParameters? _ec;

    /// <summary>
    /// RSA 公钥参数
    /// </summary>
    /// <remarks>
    /// EC 键为空
    /// </remarks>
    private readonly RSAParameters? _rsa;

    /// <summary>
    /// 构造
    /// </summary>
    private CoseKey(int algorithm, ECParameters? ec, RSAParameters? rsa)
    {
        Algorithm = algorithm;
        _ec = ec;
        _rsa = rsa;
    }

    /// <summary>
    /// COSE 算法编号
    /// </summary>
    public int Algorithm { get; }

    /// <summary>
    /// 从编码的 COSE 键读出
    /// </summary>
    /// <param name="encoded">COSE 键的 CBOR 字节</param>
    /// <returns>结构不合法为 <c>SECURITY_VALIDATION_006</c>（<c>public_key</c>），算法不支持为 <c>SECURITY_VALIDATION_007</c></returns>
    public static Result<CoseKey> Read(ReadOnlyMemory<byte> encoded)
    {
        long? keyType = null;
        long? algorithm = null;
        long? curve = null;
        byte[]? minus1 = null;
        byte[]? minus2 = null;
        byte[]? minus3 = null;
        try
        {
            var reader = new CborReader(encoded, CborConformanceMode.Strict);
            reader.ReadStartMap();
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                // COSE 也允许文本标签，这里用不上，连值一起跳过
                if (reader.PeekState() == CborReaderState.TextString)
                {
                    reader.SkipValue();
                    reader.SkipValue();
                    continue;
                }

                var label = reader.ReadInt64();
                switch (label)
                {
                    case 1:
                        keyType = reader.ReadInt64();
                        break;
                    case 3:
                        algorithm = reader.ReadInt64();
                        break;
                    case -1:
                        // EC2 / OKP 的 -1 是曲线编号，RSA 的 -1 是模
                        if (reader.PeekState() == CborReaderState.ByteString)
                        {
                            minus1 = reader.ReadByteString();
                        }
                        else
                        {
                            curve = reader.ReadInt64();
                        }

                        break;
                    case -2:
                        minus2 = reader.ReadByteString();
                        break;
                    case -3:
                        minus3 = reader.ReadByteString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();
            if (reader.BytesRemaining != 0)
            {
                return Malformed();
            }
        }
        catch (Exception exception) when (exception is CborContentException or InvalidOperationException or OverflowException)
        {
            return Malformed();
        }

        if (algorithm is not { } alg || keyType is not { } kty)
        {
            return Malformed();
        }

        // 认识的算法配错了键类型是结构问题；不认识的算法才是不支持
        return (alg, kty) switch
        {
            (Es256, KeyTypeEc2) => Ec(curve, minus2, minus3),
            (Ps256 or Rs256, KeyTypeRsa) => Rsa((int)alg, minus1, minus2),
            (Es256 or Ps256 or Rs256, _) => Malformed(),
            _ => WebAuthnFailures.UnsupportedAlgorithm(alg),
        };
    }

    /// <summary>
    /// 核对签名
    /// </summary>
    /// <param name="data">被签名的数据</param>
    /// <param name="signature">签名</param>
    /// <returns>签名不符或格式不对时为假</returns>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        try
        {
            if (_ec is { } ec)
            {
                using var ecdsa = ECDsa.Create(ec);
                // WebAuthn 的 ES256 签名是 DER 编码的 (r, s)，不是 IEEE P1363 的定长拼接
                return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            }

            if (_rsa is { } parameters)
            {
                using var rsa = RSA.Create(parameters);
                var padding = Algorithm == Ps256 ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, padding);
            }

            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// 结构不合法
    /// </summary>
    private static Error Malformed() => WebAuthnFailures.Malformed(WebAuthnReasons.PublicKey);

    /// <summary>
    /// 组出 P-256 公钥，并确认点在曲线上
    /// </summary>
    private static Result<CoseKey> Ec(long? curve, byte[]? x, byte[]? y)
    {
        if (curve != CurveP256 || x is not { Length: 32 } || y is not { Length: 32 })
        {
            return Malformed();
        }

        var parameters = new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = x, Y = y } };
        try
        {
            // 导入时校验点在曲线上：不在曲线上的点在认证时会让验签出错，这里提前当作公钥不合法
            using var probe = ECDsa.Create(parameters);
        }
        catch (CryptographicException)
        {
            return Malformed();
        }

        return new CoseKey(Es256, parameters, null);
    }

    /// <summary>
    /// 组出 RSA 公钥
    /// </summary>
    private static Result<CoseKey> Rsa(int algorithm, byte[]? modulus, byte[]? exponent)
    {
        if (modulus is not { Length: >= MinimumRsaModulusLength and <= MaximumRsaModulusLength } || exponent is not { Length: > 0 and <= 8 })
        {
            return Malformed();
        }

        var parameters = new RSAParameters { Modulus = modulus, Exponent = exponent };
        try
        {
            using var probe = RSA.Create(parameters);
        }
        catch (CryptographicException)
        {
            return Malformed();
        }

        return new CoseKey(algorithm, null, parameters);
    }
}
