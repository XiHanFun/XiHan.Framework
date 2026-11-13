// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 注册核对通过的凭据
/// </summary>
/// <remarks>
/// 应用把 <see cref="CredentialId"/>、<see cref="PublicKey"/> 与 <see cref="SignCount"/> 存下，认证时组成 <see cref="WebAuthnStoredCredential"/>
/// </remarks>
/// <param name="CredentialId">凭据 ID（base64url）</param>
/// <param name="PublicKey">COSE 公钥（base64url）</param>
/// <param name="Algorithm">COSE 算法编号</param>
/// <param name="SignCount">签名计数；不计数的认证器恒为 0</param>
/// <param name="Aaguid">认证器自报的型号；按 <c>none</c> 证明时多为全零，没有经过证明，不能拿来限制型号（ADR-0216）</param>
/// <param name="UserVerified">本次是否做过用户验证</param>
/// <param name="BackupEligible">凭据能否在设备间同步（BE）</param>
/// <param name="BackedUp">凭据是否已同步备份（BS）</param>
/// <param name="Transports">认证器的传输方式</param>
/// <param name="AttestationFormat">证明格式；证明声明不校验</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnRegisteredCredential(
    string CredentialId,
    string PublicKey,
    int Algorithm,
    uint SignCount,
    Guid Aaguid,
    bool UserVerified,
    bool BackupEligible,
    bool BackedUp,
    IReadOnlyList<string> Transports,
    string AttestationFormat);
