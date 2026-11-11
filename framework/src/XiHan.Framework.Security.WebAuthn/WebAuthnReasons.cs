// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 核对失败时错误参数 <c>reason</c> 的取值
/// </summary>
/// <remarks>
/// 与 RFC-0008「WebAuthn」里括号中的原因一一对应，一致性向量按它们断言
/// </remarks>
internal static class WebAuthnReasons
{
    /// <summary>
    /// 凭据类型不是 public-key，或 id 与 rawId 不同、解不开
    /// </summary>
    public const string Id = "id";

    /// <summary>
    /// 客户端数据不是 JSON 对象或缺字段
    /// </summary>
    public const string ClientData = "client_data";

    /// <summary>
    /// 客户端数据的仪式类型不符
    /// </summary>
    public const string Type = "type";

    /// <summary>
    /// 挑战不符
    /// </summary>
    public const string Challenge = "challenge";

    /// <summary>
    /// 来源不在允许的列表里
    /// </summary>
    public const string Origin = "origin";

    /// <summary>
    /// 跨源的 iframe 里发起
    /// </summary>
    public const string CrossOrigin = "cross_origin";

    /// <summary>
    /// 证明对象不是合法的 CBOR 映射或缺字段
    /// </summary>
    public const string AttestationObject = "attestation_object";

    /// <summary>
    /// 认证器数据的长度或结构不合法
    /// </summary>
    public const string AuthenticatorData = "authenticator_data";

    /// <summary>
    /// RP ID 哈希不符
    /// </summary>
    public const string RelyingPartyId = "rp_id";

    /// <summary>
    /// 用户不在场
    /// </summary>
    public const string UserPresent = "user_present";

    /// <summary>
    /// 要求用户验证而没有做
    /// </summary>
    public const string UserVerified = "user_verified";

    /// <summary>
    /// 标志位自相矛盾：不可备份却已备份
    /// </summary>
    public const string Flags = "flags";

    /// <summary>
    /// 注册响应没有证明凭据数据
    /// </summary>
    public const string AttestedCredential = "attested_credential";

    /// <summary>
    /// 凭据 ID 的长度不合法或与 rawId 不同
    /// </summary>
    public const string CredentialId = "credential_id";

    /// <summary>
    /// 公钥不是合法的 COSE 键
    /// </summary>
    public const string PublicKey = "public_key";

    /// <summary>
    /// none 证明的声明不为空
    /// </summary>
    public const string Attestation = "attestation";

    /// <summary>
    /// 响应的凭据不是存储的那个
    /// </summary>
    public const string Credential = "credential";

    /// <summary>
    /// 凭据不属于期望的用户
    /// </summary>
    /// <remarks>
    /// 凭据的所属用户不是登录前认出的用户、响应的句柄与所属用户不同，或免用户名的流程里响应没带句柄（ADR-0179）
    /// </remarks>
    public const string UserHandle = "user_handle";

    /// <summary>
    /// 签名不符
    /// </summary>
    public const string Signature = "signature";
}
