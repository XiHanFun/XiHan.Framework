// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 指名的一个凭据（<c>PublicKeyCredentialDescriptorJSON</c>）
/// </summary>
/// <remarks>
/// 注册时列在 <c>excludeCredentials</c> 里免得同一认证器重复注册，认证时列在 <c>allowCredentials</c> 里限定可用的凭据
/// </remarks>
/// <param name="Type">凭据类型，恒为 <c>public-key</c></param>
/// <param name="Id">凭据 ID（base64url）</param>
/// <param name="Transports">注册时认证器报告的传输方式（<c>internal</c>、<c>hybrid</c>、<c>usb</c> 等）；没有即不写</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnCredentialDescriptor(
    string Type,
    string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Transports);
