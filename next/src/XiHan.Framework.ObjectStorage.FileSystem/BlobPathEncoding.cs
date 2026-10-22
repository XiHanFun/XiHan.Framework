// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 键的段与文件名之间的可逆编码。键区分大小写而常见文件系统（Windows、macOS 默认）不区分，所以大写字母、非 ASCII、<c>%</c>、
/// Windows 保留字符、保留设备名与结尾的点都按 UTF-8 字节百分号编码；小写字母、数字与 <c>._-</c> 原样保留，常见的键在目录里仍可读。
/// </summary>
internal static class BlobPathEncoding
{
    /// <summary>
    /// Windows 保留的设备名（不区分大小写）；作为文件名会失败，必须编码。
    /// </summary>
    private static readonly HashSet<string> _reservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    };

    /// <summary>
    /// 键 → 相对路径（各段编码后以目录分隔符连接）。
    /// </summary>
    public static string ToRelativePath(BlobKey key) => string.Join(Path.DirectorySeparatorChar, key.Value.Split(BlobKey.Separator).Select(EncodeSegment));

    /// <summary>
    /// 相对路径 → 键文本（各段解码后以 <c>/</c> 连接）。
    /// </summary>
    public static string ToKey(string relativePath) => string.Join(BlobKey.Separator, relativePath.Split(Path.DirectorySeparatorChar).Select(DecodeSegment));

    /// <summary>
    /// 编码一段。
    /// </summary>
    public static string EncodeSegment(string segment)
    {
        // 保留设备名整体编码首字符即可打破匹配；结尾的点在 Windows 上会被去掉，也要编码
        var escapeFirst = _reservedNames.Contains(segment) || _reservedNames.Contains(Path.GetFileNameWithoutExtension(segment));
        var builder = new StringBuilder(segment.Length + 8);
        Span<byte> utf8 = stackalloc byte[4];
        for (var index = 0; index < segment.Length; index++)
        {
            var character = segment[index];
            var last = index == segment.Length - 1;
            if (IsSafe(character) && !(escapeFirst && index == 0) && !(last && character == '.'))
            {
                builder.Append(character);
                continue;
            }

            // 代理对合成一个 Rune 再取 UTF-8 字节
            var rune = char.IsHighSurrogate(character) && index + 1 < segment.Length && char.IsLowSurrogate(segment[index + 1])
                ? new Rune(character, segment[++index])
                : new Rune(character);
            var length = rune.EncodeToUtf8(utf8);
            for (var offset = 0; offset < length; offset++)
            {
                builder.Append('%').Append(utf8[offset].ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 解码一段：<c>%XX</c> 序列还原为 UTF-8 字节；不是本编码产生的名字（外部放入的文件）原样返回。
    /// </summary>
    public static string DecodeSegment(string name)
    {
        if (!name.Contains('%', StringComparison.Ordinal))
        {
            return name;
        }

        var bytes = new List<byte>(name.Length);
        for (var index = 0; index < name.Length; index++)
        {
            if (name[index] == '%' && index + 2 < name.Length + 0 && index + 2 <= name.Length - 1 && byte.TryParse(name.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                bytes.Add(value);
                index += 2;
                continue;
            }

            bytes.AddRange(Encoding.UTF8.GetBytes(name[index].ToString()));
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    /// <summary>
    /// 原样保留的字符：小写字母、数字、<c>.</c>、<c>_</c>、<c>-</c>。
    /// </summary>
    private static bool IsSafe(char character) => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '.' or '_' or '-';
}
