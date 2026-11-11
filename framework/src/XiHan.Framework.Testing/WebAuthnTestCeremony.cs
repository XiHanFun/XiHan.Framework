// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 测试用认证器一次仪式的输入与篡改项
/// </summary>
/// <remarks>
/// 缺省值产生一份合法的响应；改某一项即可得到对应原因的失败
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnTestCeremony
{
    /// <summary>
    /// 依赖方给的挑战（base64url）
    /// </summary>
    public required string Challenge { get; init; }

    /// <summary>
    /// 写进客户端数据的来源
    /// </summary>
    public required string Origin { get; init; }

    /// <summary>
    /// 算 RP ID 哈希用的依赖方 ID
    /// </summary>
    public required string RelyingPartyId { get; init; }

    /// <summary>
    /// 客户端数据的仪式类型
    /// </summary>
    /// <remarks>
    /// 为空时取正确的类型：注册 <c>webauthn.create</c>，认证 <c>webauthn.get</c>
    /// </remarks>
    public string? Type { get; init; }

    /// <summary>
    /// 是否声称在跨源的 iframe 里发起
    /// </summary>
    public bool CrossOrigin { get; init; }

    /// <summary>
    /// 用户在场标志（UP）
    /// </summary>
    public bool UserPresent { get; init; } = true;

    /// <summary>
    /// 用户已验证标志（UV）
    /// </summary>
    public bool UserVerified { get; init; } = true;

    /// <summary>
    /// 可备份标志（BE）
    /// </summary>
    public bool BackupEligible { get; init; } = true;

    /// <summary>
    /// 已备份标志（BS）
    /// </summary>
    public bool BackedUp { get; init; } = true;

    /// <summary>
    /// 注册时的证明格式
    /// </summary>
    /// <remarks>
    /// <c>none</c> 写空的证明声明；其他格式写一份不可校验的声明（依赖方本就不校验）
    /// </remarks>
    public string AttestationFormat { get; init; } = "none";

    /// <summary>
    /// 认证时是否把签名改坏
    /// </summary>
    public bool CorruptSignature { get; init; }

    /// <summary>
    /// 认证时交回的用户句柄（base64url）
    /// </summary>
    /// <remarks>
    /// 为空即不交回
    /// </remarks>
    public string? UserHandle { get; init; }
}
