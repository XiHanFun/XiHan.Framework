// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Signing;

/// <summary>
/// 下载时的 <c>Content-Disposition</c>：各签名实现用同一写法，非 ASCII 文件名按 RFC 6266 / RFC 5987 带 <c>filename*</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class BlobContentDisposition
{
    /// <summary>
    /// <c>attachment; filename="&lt;ASCII 回退&gt;"; filename*=UTF-8''&lt;百分号编码&gt;</c>：ASCII 回退把非 ASCII、引号、反斜杠与控制字符换成下划线。
    /// </summary>
    public static string Attachment(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var fallback = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            fallback.Append(ch is < ' ' or > '~' or '"' or '\\' ? '_' : ch);
        }

        return $"attachment; filename=\"{fallback}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
