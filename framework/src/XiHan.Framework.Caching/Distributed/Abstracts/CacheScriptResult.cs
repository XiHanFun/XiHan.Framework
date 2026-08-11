// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 缓存脚本执行结果
/// </summary>
/// <remarks>
/// 脚本返回值在不同缓存实现里的原生类型各不相同，此处以中立结构承载，
/// 使 <see cref="ICacheSupportsLuaScript"/> 的签名不带任何具体缓存客户端的类型。
/// 具体实现负责把自己的返回值转换为本结构。
/// </remarks>
public sealed class CacheScriptResult
{
    private readonly object? _value;

    private CacheScriptResult(object? value)
    {
        _value = value;
    }

    /// <summary>
    /// 空结果
    /// </summary>
    public static CacheScriptResult Null { get; } = new(null);

    /// <summary>
    /// 是否为空结果
    /// </summary>
    public bool IsNull => _value is null;

    /// <summary>
    /// 是否为数组结果
    /// </summary>
    public bool IsArray => _value is IReadOnlyList<CacheScriptResult>;

    /// <summary>
    /// 由标量值创建
    /// </summary>
    /// <param name="value">标量值</param>
    /// <returns>结果</returns>
    public static CacheScriptResult FromValue(object? value)
    {
        return value is null ? Null : new CacheScriptResult(value);
    }

    /// <summary>
    /// 由数组创建
    /// </summary>
    /// <param name="items">元素集合</param>
    /// <returns>结果</returns>
    public static CacheScriptResult FromArray(IReadOnlyList<CacheScriptResult> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return new CacheScriptResult(items);
    }

    /// <summary>
    /// 取字符串值
    /// </summary>
    /// <returns>字符串值，空结果时为空</returns>
    public string? AsString()
    {
        return _value is null ? null : Convert.ToString(_value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 取整数值
    /// </summary>
    /// <returns>整数值，空结果或不可转换时为空</returns>
    public long? AsInt64()
    {
        if (_value is null)
        {
            return null;
        }

        return _value is long value
            ? value
            : long.TryParse(AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// 取布尔值，按非零为真判定
    /// </summary>
    /// <returns>布尔值，空结果时为空</returns>
    public bool? AsBoolean()
    {
        var value = AsInt64();

        return value.HasValue ? value.Value != 0 : null;
    }

    /// <summary>
    /// 取数组元素
    /// </summary>
    /// <returns>元素集合，非数组结果时为空集合</returns>
    public IReadOnlyList<CacheScriptResult> AsArray()
    {
        return _value as IReadOnlyList<CacheScriptResult> ?? [];
    }
}
