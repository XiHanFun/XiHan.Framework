// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Text;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 响应字段里的 base64url
/// </summary>
internal static class Base64UrlText
{
    /// <summary>
    /// 解码非空的 base64url（有无填充都认）
    /// </summary>
    /// <param name="value">字段值</param>
    /// <param name="bytes">解出的字节</param>
    /// <returns>值为空、空白或不是 base64url 时为假</returns>
    public static bool TryDecode(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            bytes = Base64Url.DecodeFromChars(value.TrimEnd('='));
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
