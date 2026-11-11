// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 核对失败的错误
/// </summary>
internal static class WebAuthnFailures
{
    /// <summary>
    /// 响应解析不了（<c>SECURITY_VALIDATION_006</c>）
    /// </summary>
    /// <param name="reason">机读原因，取自 <see cref="WebAuthnReasons"/></param>
    public static Error Malformed(string reason)
        => Error.Validation(SecurityErrors.WebAuthnResponseMalformed, SecurityMessages.WebAuthnResponseMalformed, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 核对不通过（<c>SECURITY_DENIED_005</c>）
    /// </summary>
    /// <param name="reason">机读原因，取自 <see cref="WebAuthnReasons"/></param>
    public static Error Denied(string reason)
        => Error.Denied(SecurityErrors.WebAuthnVerificationFailed, SecurityMessages.WebAuthnVerificationFailed, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 公钥算法不支持（<c>SECURITY_VALIDATION_007</c>）
    /// </summary>
    /// <param name="algorithm">COSE 算法编号</param>
    public static Error UnsupportedAlgorithm(long algorithm)
        => Error.Validation(SecurityErrors.WebAuthnAlgorithmUnsupported, SecurityMessages.WebAuthnAlgorithmUnsupported, MessageArguments.Of(("algorithm", algorithm)));

    /// <summary>
    /// 签名计数没有增加（<c>SECURITY_DENIED_006</c>）
    /// </summary>
    /// <param name="stored">存储的计数</param>
    /// <param name="received">收到的计数</param>
    public static Error CounterRegressed(uint stored, uint received)
        => Error.Denied(SecurityErrors.WebAuthnCounterRegressed, SecurityMessages.WebAuthnCounterRegressed, MessageArguments.Of(("stored", stored), ("received", received)));
}
