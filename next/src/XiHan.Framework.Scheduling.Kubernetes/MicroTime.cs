// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Scheduling.Kubernetes;

/// <summary>
/// Kubernetes MicroTime（RFC 3339，微秒，UTC）的读写。
/// </summary>
internal static class MicroTime
{
    /// <summary>
    /// 写出格式。
    /// </summary>
    private const string Pattern = "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'";

    /// <summary>
    /// 格式化为 UTC 文本。
    /// </summary>
    public static string Format(DateTimeOffset time) => time.UtcDateTime.ToString(Pattern, CultureInfo.InvariantCulture);

    /// <summary>
    /// 解析；空或不合法为无值。
    /// </summary>
    public static DateTimeOffset? Parse(string? text)
        => !string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : null;
}
