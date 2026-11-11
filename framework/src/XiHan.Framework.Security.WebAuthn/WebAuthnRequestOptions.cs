// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 认证仪式的选项（<c>PublicKeyCredentialRequestOptionsJSON</c>）
/// </summary>
/// <remarks>
/// 按 camelCase 序列化后浏览器用 <c>PublicKeyCredential.parseRequestOptionsFromJSON</c> 还原；应用保存 <see cref="Challenge"/> 到仪式结束
/// </remarks>
/// <param name="Challenge">挑战（base64url）</param>
/// <param name="Timeout">超时（毫秒）</param>
/// <param name="RpId">依赖方 ID</param>
/// <param name="AllowList">可用的凭据；为空即请求可发现凭据（免用户名登录）；JSON 名 <c>allowCredentials</c></param>
/// <param name="UserVerification">用户验证：<c>required</c>、<c>preferred</c> 或 <c>discouraged</c></param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnRequestOptions(
    string Challenge,
    long Timeout,
    string RpId,
    [property: JsonPropertyName("allowCredentials")] IReadOnlyList<WebAuthnCredentialDescriptor> AllowList,
    string UserVerification);
