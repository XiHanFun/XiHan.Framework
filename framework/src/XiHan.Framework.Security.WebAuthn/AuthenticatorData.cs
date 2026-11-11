// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using System.Formats.Cbor;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 解出的认证器数据
/// </summary>
/// <remarks>
/// 布局：RP ID 哈希 32 字节、标志 1 字节、签名计数 4 字节（大端）；AT 置位时接 AAGUID 16 字节、凭据 ID 长度 2 字节（大端）、凭据 ID 与 COSE 公钥；ED 置位时再接扩展映射
/// </remarks>
internal sealed class AuthenticatorData
{
    /// <summary>
    /// 固定部分的长度
    /// </summary>
    private const int FixedLength = 37;

    /// <summary>
    /// 用户在场（UP）
    /// </summary>
    private const byte UserPresentFlag = 0x01;

    /// <summary>
    /// 用户已验证（UV）
    /// </summary>
    private const byte UserVerifiedFlag = 0x04;

    /// <summary>
    /// 可备份（BE）
    /// </summary>
    private const byte BackupEligibleFlag = 0x08;

    /// <summary>
    /// 已备份（BS）
    /// </summary>
    private const byte BackedUpFlag = 0x10;

    /// <summary>
    /// 带证明凭据数据（AT）
    /// </summary>
    private const byte AttestedCredentialFlag = 0x40;

    /// <summary>
    /// 带扩展（ED）
    /// </summary>
    private const byte ExtensionsFlag = 0x80;

    /// <summary>
    /// 构造
    /// </summary>
    private AuthenticatorData(byte[] rpIdHash, byte flags, uint signCount, Guid aaguid, byte[]? credentialId, byte[]? credentialPublicKey)
    {
        RpIdHash = rpIdHash;
        Flags = flags;
        SignCount = signCount;
        Aaguid = aaguid;
        CredentialId = credentialId;
        CredentialPublicKey = credentialPublicKey;
    }

    /// <summary>
    /// RP ID 的 SHA-256
    /// </summary>
    public byte[] RpIdHash { get; }

    /// <summary>
    /// 标志字节
    /// </summary>
    public byte Flags { get; }

    /// <summary>
    /// 签名计数
    /// </summary>
    public uint SignCount { get; }

    /// <summary>
    /// 认证器型号
    /// </summary>
    /// <remarks>
    /// 没有证明凭据数据时为全零
    /// </remarks>
    public Guid Aaguid { get; }

    /// <summary>
    /// 凭据 ID
    /// </summary>
    /// <remarks>
    /// 没有证明凭据数据时为空
    /// </remarks>
    public byte[]? CredentialId { get; }

    /// <summary>
    /// 编码的 COSE 公钥
    /// </summary>
    /// <remarks>
    /// 没有证明凭据数据时为空
    /// </remarks>
    public byte[]? CredentialPublicKey { get; }

    /// <summary>
    /// 用户在场
    /// </summary>
    public bool UserPresent => (Flags & UserPresentFlag) != 0;

    /// <summary>
    /// 用户已验证
    /// </summary>
    public bool UserVerified => (Flags & UserVerifiedFlag) != 0;

    /// <summary>
    /// 凭据可在设备间同步
    /// </summary>
    public bool BackupEligible => (Flags & BackupEligibleFlag) != 0;

    /// <summary>
    /// 凭据已同步备份
    /// </summary>
    public bool BackedUp => (Flags & BackedUpFlag) != 0;

    /// <summary>
    /// 带证明凭据数据
    /// </summary>
    public bool HasAttestedCredential => (Flags & AttestedCredentialFlag) != 0;

    /// <summary>
    /// 从字节解出
    /// </summary>
    /// <param name="data">认证器数据</param>
    /// <param name="result">解出的数据</param>
    /// <returns>长度不够、CBOR 不合法或有多余字节时为假</returns>
    public static bool TryParse(ReadOnlyMemory<byte> data, out AuthenticatorData? result)
    {
        result = null;
        if (data.Length < FixedLength)
        {
            return false;
        }

        var span = data.Span;
        var rpIdHash = span[..32].ToArray();
        var flags = span[32];
        var signCount = BinaryPrimitives.ReadUInt32BigEndian(span[33..FixedLength]);
        var rest = data[FixedLength..];
        var aaguid = Guid.Empty;
        byte[]? credentialId = null;
        byte[]? publicKey = null;
        try
        {
            if ((flags & AttestedCredentialFlag) != 0)
            {
                if (rest.Length < 18)
                {
                    return false;
                }

                aaguid = new Guid(rest.Span[..16], bigEndian: true);
                var idLength = BinaryPrimitives.ReadUInt16BigEndian(rest.Span[16..18]);
                if (rest.Length < 18 + idLength)
                {
                    return false;
                }

                credentialId = rest.Span.Slice(18, idLength).ToArray();
                rest = rest[(18 + idLength)..];
                // COSE 公钥是一个 CBOR 项，长度由它自己的编码决定；后面可能还跟着扩展映射
                var reader = new CborReader(rest, CborConformanceMode.Strict, allowMultipleRootLevelValues: true);
                publicKey = reader.ReadEncodedValue().ToArray();
                rest = rest[(rest.Length - reader.BytesRemaining)..];
            }

            if ((flags & ExtensionsFlag) != 0)
            {
                var reader = new CborReader(rest, CborConformanceMode.Strict, allowMultipleRootLevelValues: true);
                if (reader.PeekState() != CborReaderState.StartMap)
                {
                    return false;
                }

                reader.SkipValue();
                rest = rest[(rest.Length - reader.BytesRemaining)..];
            }
        }
        catch (Exception exception) when (exception is CborContentException or InvalidOperationException)
        {
            return false;
        }

        // 标志没声明的尾部字节一律视为结构不合法
        if (!rest.IsEmpty)
        {
            return false;
        }

        result = new AuthenticatorData(rpIdHash, flags, signCount, aaguid, credentialId, publicKey);
        return true;
    }
}
