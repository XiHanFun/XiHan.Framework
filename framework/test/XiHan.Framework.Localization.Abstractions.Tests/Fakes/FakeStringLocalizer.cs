// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;
using System.Globalization;

namespace XiHan.Framework.Localization.Abstractions.Tests.Fakes;

/// <summary>
/// 手写的字符串本地化器替身
/// </summary>
/// <remarks>
/// 命中词条时返回 ResourceNotFound=false；未命中时按 Microsoft 约定返回资源键自身且 ResourceNotFound=true，
/// 与本仓 XiHanJsonStringLocalizer 的缺失语义保持一致，便于验证回退分支。
/// </remarks>
public sealed class FakeStringLocalizer : IStringLocalizer
{
    private readonly Dictionary<string, string> _entries;

    /// <summary>
    /// 初始化字符串本地化器替身
    /// </summary>
    /// <param name="entries">预置词条，键为资源键，值为本地化文本（可含复合格式占位符）</param>
    public FakeStringLocalizer(IDictionary<string, string>? entries = null)
    {
        _entries = entries is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(entries, StringComparer.Ordinal);
    }

    /// <summary>
    /// 记录被请求过的资源键（按调用顺序）
    /// </summary>
    public List<string> RequestedNames { get; } = [];

    /// <summary>
    /// 记录带参重载收到的格式化参数（按调用顺序）
    /// </summary>
    public List<object[]> RequestedArguments { get; } = [];

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="name">资源键</param>
    /// <returns>本地化结果</returns>
    public LocalizedString this[string name]
    {
        get
        {
            RequestedNames.Add(name);
            return _entries.TryGetValue(name, out var value)
                ? new LocalizedString(name, value, resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);
        }
    }

    /// <summary>
    /// 获取带格式化参数的本地化字符串
    /// </summary>
    /// <param name="name">资源键</param>
    /// <param name="arguments">格式化参数</param>
    /// <returns>本地化结果</returns>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            RequestedNames.Add(name);
            RequestedArguments.Add(arguments);
            return _entries.TryGetValue(name, out var value)
                ? new LocalizedString(name, string.Format(CultureInfo.InvariantCulture, value, arguments), resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);
        }
    }

    /// <summary>
    /// 返回全部预置词条
    /// </summary>
    /// <param name="includeParentCultures">是否包含父区域，替身忽略该参数</param>
    /// <returns>全部词条</returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return _entries.Select(entry => new LocalizedString(entry.Key, entry.Value, resourceNotFound: false));
    }
}
