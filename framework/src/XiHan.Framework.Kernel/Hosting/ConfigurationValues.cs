// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 从配置逐键读取标量值
/// </summary>
/// <remarks>
/// <para>各包的选项类用它读时长、整数、布尔与枚举，不用 <c>Bind</c>（零反射、AOT 友好），也不各写一份解析：同一种写法在哪个包里都得到同一个结论、同一种报错。</para>
/// <para>键不存在才算缺失，返回 <see langword="null"/>，调用方回落到缺省值；写了值（含空串）就必须合法，不按缺省处理，不合法抛出 <see cref="ArgumentException"/>。</para>
/// <para>数字按不变文化解析。整数接受前后空白与正负号，与 <c>Microsoft.Extensions.Configuration</c> 的绑定一致；时长是秒数，可带小数，必须是有限的数且装得进 <see cref="TimeSpan"/>。</para>
/// <para>消息是 <c>'&lt;配置键&gt;' must be …, but was '&lt;原值&gt;'.</c>，配置键是完整路径：传进来的是配置节时取它的路径加上键。配置错误给运维看，不本地化；这些键都不是机密，原值照写。</para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConfigurationValues
{
    /// <summary>
    /// 读以秒计的正时长
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <returns>时长；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是有限的正数，或超出 <see cref="TimeSpan"/> 的范围</exception>
    public static TimeSpan? ReadSeconds(IConfiguration configuration, string key) => ReadSeconds(configuration, key, allowZero: false);

    /// <summary>
    /// 读以秒计的时长，可以允许零
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <param name="allowZero">是否允许 0（如「0 表示关闭」的键）</param>
    /// <returns>时长；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是有限的数、为负、不允许零时为零，或超出 <see cref="TimeSpan"/> 的范围</exception>
    public static TimeSpan? ReadSeconds(IConfiguration configuration, string key, bool allowZero)
    {
        if (Raw(configuration, key) is not { } raw)
        {
            return null;
        }

        // 上界也在这里判：TimeSpan.FromSeconds 对装不下的数抛 OverflowException，消息里没有配置键
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && double.IsFinite(seconds)
            && (allowZero ? seconds >= 0 : seconds > 0)
            && seconds <= TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.FromSeconds(seconds)
            : throw Invalid(configuration, key, allowZero ? "a non-negative number of seconds" : "a positive number of seconds", raw);
    }

    /// <summary>
    /// 读不小于下界的 32 位整数
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <param name="minimum">下界（含）</param>
    /// <returns>整数；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是整数或小于下界</exception>
    public static int? ReadInt32(IConfiguration configuration, string key, int minimum) => ReadInt32(configuration, key, minimum, int.MaxValue);

    /// <summary>
    /// 读落在区间内的 32 位整数
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <param name="minimum">下界（含）</param>
    /// <param name="maximum">上界（含）</param>
    /// <returns>整数；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是整数或不在区间内</exception>
    public static int? ReadInt32(IConfiguration configuration, string key, int minimum, int maximum)
        => ReadInt64(configuration, key, minimum, maximum, int.MaxValue) is { } value ? (int)value : null;

    /// <summary>
    /// 读不小于下界的 64 位整数
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <param name="minimum">下界（含）</param>
    /// <returns>整数；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是整数或小于下界</exception>
    public static long? ReadInt64(IConfiguration configuration, string key, long minimum) => ReadInt64(configuration, key, minimum, long.MaxValue);

    /// <summary>
    /// 读落在区间内的 64 位整数
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <param name="minimum">下界（含）</param>
    /// <param name="maximum">上界（含）</param>
    /// <returns>整数；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是整数或不在区间内</exception>
    public static long? ReadInt64(IConfiguration configuration, string key, long minimum, long maximum)
        => ReadInt64(configuration, key, minimum, maximum, long.MaxValue);

    /// <summary>
    /// 读布尔值
    /// </summary>
    /// <remarks>
    /// 只认 <c>true</c> 与 <c>false</c>，不分大小写
    /// </remarks>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <returns>布尔值；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是 <c>true</c> 或 <c>false</c></exception>
    public static bool? ReadBoolean(IConfiguration configuration, string key)
    {
        if (Raw(configuration, key) is not { } raw)
        {
            return null;
        }

        return bool.TryParse(raw, out var value) ? value : throw Invalid(configuration, key, "'true' or 'false'", raw);
    }

    /// <summary>
    /// 读枚举值
    /// </summary>
    /// <remarks>
    /// 按成员名匹配，不分大小写；不接受数字写法：<c>1</c> 换一个版本可能就是另一个成员
    /// </remarks>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">相对 <paramref name="configuration"/> 的键</param>
    /// <returns>枚举值；键不存在为 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">不是这个枚举的成员名</exception>
    public static TEnum? ReadEnum<TEnum>(IConfiguration configuration, string key)
        where TEnum : struct, Enum
    {
        if (Raw(configuration, key) is not { } raw)
        {
            return null;
        }

        // Enum.TryParse 也收数字文本，先排除；未定义的数值本来就过不了 IsDefined
        return !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value)
            && Enum.IsDefined(value)
            ? value
            : throw Invalid(configuration, key, "one of " + string.Join(", ", Enum.GetNames<TEnum>().Select(static name => $"'{name}'")), raw);
    }

    /// <summary>
    /// 读整数并核对区间
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">键</param>
    /// <param name="minimum">下界（含）</param>
    /// <param name="maximum">上界（含）</param>
    /// <param name="unbounded">这个宽度的最大值：上界等于它时消息里不写上界</param>
    private static long? ReadInt64(IConfiguration configuration, string key, long minimum, long maximum, long unbounded)
    {
        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "The minimum must not be greater than the maximum.");
        }

        if (Raw(configuration, key) is not { } raw)
        {
            return null;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum)
        {
            return value;
        }

        var expected = maximum < unbounded
            ? $"an integer between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}"
            : minimum switch
            {
                1 => "a positive integer",
                0 => "a non-negative integer",
                _ => $"an integer not less than {minimum.ToString(CultureInfo.InvariantCulture)}",
            };
        throw Invalid(configuration, key, expected, raw);
    }

    /// <summary>
    /// 取键的原值
    /// </summary>
    /// <remarks>
    /// 键不存在为 <see langword="null"/>
    /// </remarks>
    private static string? Raw(IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(key);
        return configuration[key];
    }

    /// <summary>
    /// 值不合法的异常
    /// </summary>
    /// <param name="configuration">配置或配置节</param>
    /// <param name="key">键</param>
    /// <param name="expected">应当是什么（接在 <c>must be</c> 之后）</param>
    /// <param name="raw">原值</param>
    private static ArgumentException Invalid(IConfiguration configuration, string key, string expected, string raw)
    {
        // 传进来的是配置节时补上它的路径：报错里是运维在配置文件里写的那个完整键
        var path = configuration is IConfigurationSection section ? $"{section.Path}:{key}" : key;
        return new ArgumentException($"'{path}' must be {expected}, but was '{raw}'.");
    }
}
