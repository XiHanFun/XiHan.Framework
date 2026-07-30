// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 固定文本本地化字符串
/// </summary>
public sealed class FixedLocalizableString : ILocalizableString
{
    /// <summary>
    /// 初始化固定文本本地化字符串
    /// </summary>
    /// <param name="value">固定文本</param>
    public FixedLocalizableString(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 固定文本
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 本地化（固定文本直接返回原值）
    /// </summary>
    /// <param name="stringLocalizerFactory"></param>
    /// <returns></returns>
    public LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory)
    {
        ArgumentNullException.ThrowIfNull(stringLocalizerFactory);
        return new LocalizedString(Value, Value, resourceNotFound: false);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
