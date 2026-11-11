// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 注册响应里认证器的部分（<c>AuthenticatorAttestationResponseJSON</c>）
/// </summary>
/// <param name="ClientDataJson">客户端数据（base64url），JSON 名 <c>clientDataJSON</c></param>
/// <param name="AttestationObject">证明对象（base64url 的 CBOR）</param>
/// <param name="Transports">认证器的传输方式；浏览器不报告时为空</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnAttestationResponse(
    [property: JsonPropertyName("clientDataJSON")] string ClientDataJson,
    string AttestationObject,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Transports);
