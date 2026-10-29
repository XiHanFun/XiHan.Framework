// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 配置键的排序。
/// </summary>
/// <remarks>
/// 逐段比较，两段都是整数时按数值，否则忽略大小写按序数；与 <c>Microsoft.Extensions.Configuration</c> 的子键顺序一致。
/// </remarks>
internal sealed class ConfigurationKeyOrder : IComparer<string>
{
    /// <summary>
    /// 共享实例。
    /// </summary>
    public static ConfigurationKeyOrder Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(string? x, string? y)
    {
        var xs = (x ?? string.Empty).Split(ConfigurationPath.KeyDelimiter, StringSplitOptions.RemoveEmptyEntries);
        var ys = (y ?? string.Empty).Split(ConfigurationPath.KeyDelimiter, StringSplitOptions.RemoveEmptyEntries);
        var length = Math.Min(xs.Length, ys.Length);
        for (var i = 0; i < length; i++)
        {
            var result = int.TryParse(xs[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var xn) && int.TryParse(ys[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var yn)
                ? xn.CompareTo(yn)
                : string.Compare(xs[i], ys[i], StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }
        }

        return xs.Length.CompareTo(ys.Length);
    }
}
