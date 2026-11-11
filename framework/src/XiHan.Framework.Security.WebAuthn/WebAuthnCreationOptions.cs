// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 注册仪式的选项（<c>PublicKeyCredentialCreationOptionsJSON</c>）
/// </summary>
/// <remarks>
/// 按 camelCase 序列化后浏览器用 <c>PublicKeyCredential.parseCreationOptionsFromJSON</c> 还原；应用保存 <see cref="Challenge"/> 到仪式结束
/// </remarks>
/// <param name="Rp">依赖方</param>
/// <param name="User">用户</param>
/// <param name="Challenge">挑战（base64url）</param>
/// <param name="PubKeyCredParams">接受的算法，按偏好排序</param>
/// <param name="Timeout">超时（毫秒）</param>
/// <param name="ExcludeList">该用户已有的凭据，同一认证器不再注册；JSON 名 <c>excludeCredentials</c></param>
/// <param name="AuthenticatorSelection">对认证器的要求</param>
/// <param name="Attestation">证明的传递方式，恒为 <c>none</c></param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnCreationOptions(
    WebAuthnRelyingParty Rp,
    WebAuthnUserEntity User,
    string Challenge,
    IReadOnlyList<WebAuthnCredentialParameter> PubKeyCredParams,
    long Timeout,
    [property: JsonPropertyName("excludeCredentials")] IReadOnlyList<WebAuthnCredentialDescriptor> ExcludeList,
    WebAuthnAuthenticatorSelection AuthenticatorSelection,
    string Attestation);
