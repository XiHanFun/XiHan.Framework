// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;
using System.Globalization;

namespace XiHan.Framework.Localization.Tests.TestSupport;

/// <summary>
/// 字典驱动的本地化器替身
/// </summary>
/// <remarks>
/// 用作 <c>XiHanJsonStringLocalizer</c> 的 ResourceManager 兜底位，
/// 命中返回 ResourceNotFound=false，未命中按 IStringLocalizer 标准契约返回「键名 + ResourceNotFound=true」。
/// </remarks>
public sealed class StubStringLocalizer : IStringLocalizer
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="values">兜底文本表，键不区分大小写</param>
    public StubStringLocalizer(IDictionary<string, string>? values = null)
    {
        _values = values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="name">键</param>
    /// <returns>本地化字符串</returns>
    public LocalizedString this[string name] => _values.TryGetValue(name, out var value)
        ? new LocalizedString(name, value, resourceNotFound: false)
        : new LocalizedString(name, name, resourceNotFound: true);

    /// <summary>
    /// 获取带格式化参数的本地化字符串
    /// </summary>
    /// <param name="name">键</param>
    /// <param name="arguments">格式化参数</param>
    /// <returns>本地化字符串</returns>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            if (!_values.TryGetValue(name, out var template))
            {
                return new LocalizedString(name, name, resourceNotFound: true);
            }

            var formatted = arguments.Length == 0
                ? template
                : string.Format(CultureInfo.CurrentCulture, template, arguments);
            return new LocalizedString(name, formatted, resourceNotFound: false);
        }
    }

    /// <summary>
    /// 获取全部本地化字符串
    /// </summary>
    /// <param name="includeParentCultures">是否包含父文化</param>
    /// <returns>本地化字符串集合</returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return _values.Select(pair => new LocalizedString(pair.Key, pair.Value, resourceNotFound: false));
    }
}
