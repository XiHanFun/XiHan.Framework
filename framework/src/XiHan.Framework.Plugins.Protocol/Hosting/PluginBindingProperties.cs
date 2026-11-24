// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 下发给插件的绑定属性的文法与上限
/// </summary>
/// <remarks>
/// 规则出自 <c>spec/plugin/lifecycle.md</c> §2（ADR-0285）：属性名是 1 到 <see cref="MaxNameLength"/> 个 ASCII 字母、数字、<c>.</c>、<c>_</c>、<c>-</c> 且不以 <c>.</c> 开头，一个绑定最多 <see cref="MaxCount"/> 个属性，属性名与属性值合计不超过 <see cref="MaxBytes"/> 个 UTF-8 字节。只管随 <c>Configure</c> 下发的绑定，内核自己解析绑定不受这里约束
/// </remarks>
internal static class PluginBindingProperties
{
    /// <summary>
    /// 一个绑定最多带的属性数
    /// </summary>
    public const int MaxCount = 64;

    /// <summary>
    /// 属性名的最大字符数
    /// </summary>
    /// <remarks>
    /// 与 Kubernetes Secret 的键相同：Service Binding 目录里的文件名就是 Secret 的键
    /// </remarks>
    public const int MaxNameLength = 253;

    /// <summary>
    /// 一个绑定的全部属性名与属性值按 UTF-8 编码后的字节数上限
    /// </summary>
    public const int MaxBytes = 262144;

    /// <summary>
    /// 属性名是否合文法
    /// </summary>
    /// <remarks>
    /// 逐字符判断，不用正则：<c>$</c> 会放过结尾的换行，这里要按整串匹配
    /// </remarks>
    public static bool IsValidName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is 0 or > MaxNameLength || name[0] == '.')
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 找出一个绑定的属性里第一处不能下发的地方
    /// </summary>
    /// <remarks>
    /// 先核对数量、再核对属性名、最后核对字节数：三项都是对整组属性的判断，结果与字典的枚举顺序无关。有值时是写进错误 <c>reason</c> 的固定英文文本，只带绑定名与上限的数字，不带属性名与任何值；都合规时为无值
    /// </remarks>
    /// <param name="binding">清单里的绑定名</param>
    /// <param name="properties">解析出的属性</param>
    public static Maybe<string> FindViolation(string binding, IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(properties);
        if (properties.Count > MaxCount)
        {
            return Maybe.Some(string.Create(CultureInfo.InvariantCulture, $"binding '{binding}' has more than {MaxCount} properties"));
        }

        if (properties.Keys.Any(static name => !IsValidName(name)))
        {
            return Maybe.Some($"binding '{binding}' has a property name that is not valid");
        }

        // 名字已经核对过是 ASCII，字符数就是字节数；值按 UTF-8 计，与线上写出的字节数一致（非 ASCII 的值按码元计会少算）
        long bytes = 0;
        foreach (var (name, value) in properties)
        {
            bytes += name.Length + Encoding.UTF8.GetByteCount(value);
            if (bytes > MaxBytes)
            {
                return Maybe.Some(string.Create(CultureInfo.InvariantCulture, $"the properties of binding '{binding}' exceed {MaxBytes} bytes"));
            }
        }

        return default;
    }
}
