// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 认证响应里认证器的部分（<c>AuthenticatorAssertionResponseJSON</c>）
/// </summary>
/// <param name="ClientDataJson">客户端数据（base64url），JSON 名 <c>clientDataJSON</c></param>
/// <param name="AuthenticatorData">认证器数据（base64url）</param>
/// <param name="Signature">签名（base64url）</param>
/// <param name="UserHandle">用户句柄（base64url）；可发现凭据登录时认证器交回注册时的句柄，指名凭据时可能为空</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnAssertionResponse(
    [property: JsonPropertyName("clientDataJSON")] string ClientDataJson,
    string AuthenticatorData,
    string Signature,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UserHandle);
