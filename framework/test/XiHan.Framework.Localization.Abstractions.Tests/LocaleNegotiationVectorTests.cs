// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 回放 <c>locale-negotiation.json</c>：区域的协商与文案的回退链
/// </summary>
/// <remarks>
/// <c>localization/locale-negotiation.json</c>（RFC-0010「协商与宿主」「解析」，ADR-0277）。协商经 <see cref="LocalizationOptions.Negotiate"/> 回放，只有一个候选的用例再经 <see cref="LocalizationOptions.Resolve"/> 从 Baggage <c>xihan.locale</c> 走一遍；回退链经 <see cref="LocalizationOptions.FallbackChain"/> 回放。选项照用例的启用列表与默认区域建，并过一遍「默认区域必须在启用列表里」的校验
/// </remarks>
public sealed class LocaleNegotiationVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly NegotiationVectors _vectors = ConformanceVectors.Load<NegotiationVectors>("locale-negotiation.json");

    [Fact]
    public void Negotiate_PicksTheLocaleOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Negotiation);
        var failures = new List<string>();
        foreach (var vector in _vectors.Negotiation)
        {
            var options = Options(vector.Enabled, vector.Default);
            var negotiated = options.Negotiate(vector.Candidates);
            if (negotiated != vector.Locale)
            {
                failures.Add($"{vector.Name}: negotiated '{negotiated}', the vector has '{vector.Locale}'");
            }

            // 结果总是启用列表里的写法
            if (!vector.Enabled.Contains(negotiated, StringComparer.Ordinal))
            {
                failures.Add($"{vector.Name}: negotiated '{negotiated}', which is not written as in the enabled list");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Resolve_NegotiatesTheLocaleInTheBaggage()
    {
        var failures = new List<string>();
        var replayed = 0;
        foreach (var vector in _vectors.Negotiation)
        {
            // Baggage 的 xihan.locale 只带一个值：没有候选就是没有这个键；不合 Baggage 取值规则的候选进不了上下文，不在这里回放
            if (vector.Candidates.Length > 1 || (vector.Candidates is [var only] && !BaggageKeys.IsAcceptableValue(only)))
            {
                continue;
            }

            replayed++;
            var baggage = vector.Candidates is [var candidate] ? new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Locale] = candidate } : null;
            var resolved = Options(vector.Enabled, vector.Default).Resolve(TestInvocationContext.Create(baggage: baggage));
            if (resolved != vector.Locale)
            {
                failures.Add($"{vector.Name}: resolved '{resolved}', the vector has '{vector.Locale}'");
            }
        }

        Assert.True(replayed > 0, "No case of the vectors could be replayed through the baggage.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void FallbackChain_IsTheChainOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Fallback);
        var failures = new List<string>();
        foreach (var vector in _vectors.Fallback)
        {
            var chain = Options(vector.Enabled, vector.Default).FallbackChain(vector.Locale);
            if (!chain.SequenceEqual(vector.Chain, StringComparer.Ordinal))
            {
                failures.Add($"{vector.Name}: the chain is [{string.Join(", ", chain)}], the vector has [{string.Join(", ", vector.Chain)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverEveryStepOfTheNegotiation_AndEveryLinkOfTheChain()
    {
        // 防止向量换了写法后某一步一条都没核对还照样通过：精确命中、只按语言命中、退到默认区域、没有候选、带不合文法的候选
        Assert.Contains(_vectors.Negotiation, static vector => vector.Candidates.Any(candidate => vector.Enabled.Contains(candidate, StringComparer.OrdinalIgnoreCase)));
        Assert.Contains(_vectors.Negotiation, static vector => vector.Candidates.Length > 0 && vector.Candidates.All(LocaleName.IsValid) && !Exact(vector) && SameLanguage(vector) && vector.Locale != vector.Default);
        Assert.Contains(_vectors.Negotiation, static vector => vector.Candidates.Length > 0 && vector.Candidates.All(LocaleName.IsValid) && !Exact(vector) && !SameLanguage(vector) && vector.Locale == vector.Default);
        Assert.Contains(_vectors.Negotiation, static vector => vector.Candidates.Length == 0);
        Assert.Contains(_vectors.Negotiation, static vector => vector.Candidates.Any(static candidate => !LocaleName.IsValid(candidate)));

        // 精确命中先于按语言命中：前面的候选只能按语言命中、后面的候选精确命中的用例，一趟扫描的实现会在这里出错
        Assert.Contains(_vectors.Negotiation, static vector => vector.Candidates.Length > 1
            && !vector.Enabled.Contains(vector.Candidates[0], StringComparer.OrdinalIgnoreCase)
            && vector.Enabled.Any(enabled => LocaleName.IsValid(vector.Candidates[0]) && LocaleName.LanguageOf(enabled) == LocaleName.LanguageOf(vector.Candidates[0]))
            && vector.Candidates.Skip(1).Any(candidate => vector.Enabled.Contains(candidate, StringComparer.OrdinalIgnoreCase)));

        // 回退链：请求区域不在启用列表里、默认区域不是 en-US、en-US 不在启用列表里、各级重合
        Assert.Contains(_vectors.Fallback, static vector => !vector.Enabled.Contains(vector.Locale, StringComparer.OrdinalIgnoreCase));
        Assert.Contains(_vectors.Fallback, static vector => vector.Default != SupportedLocales.Default);
        Assert.Contains(_vectors.Fallback, static vector => !vector.Enabled.Contains(SupportedLocales.Default, StringComparer.Ordinal));
        Assert.Contains(_vectors.Fallback, static vector => vector.Chain.Length == 1);
    }

    /// <summary>
    /// 照用例的启用列表与默认区域建选项
    /// </summary>
    /// <param name="enabled">启用列表，按配置的次序</param>
    /// <param name="defaultLocale">默认区域</param>
    private static LocalizationOptions Options(string[] enabled, string defaultLocale)
        => new LocalizationOptions { Locales = enabled, DefaultLocale = defaultLocale }.Validated();

    /// <summary>
    /// 用例里是否有候选精确命中启用列表
    /// </summary>
    /// <param name="vector">用例</param>
    private static bool Exact(NegotiationCase vector)
        => vector.Candidates.Any(candidate => vector.Enabled.Contains(candidate, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 用例里是否有候选与启用列表里的某个区域同语言
    /// </summary>
    /// <param name="vector">用例</param>
    private static bool SameLanguage(NegotiationCase vector)
        => vector.Candidates.Any(candidate => vector.Enabled.Any(enabled => LocaleName.LanguageOf(enabled) == LocaleName.LanguageOf(candidate)));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Negotiation">协商的用例</param>
    /// <param name="Fallback">回退链的用例</param>
    private sealed record NegotiationVectors(NegotiationCase[] Negotiation, FallbackCase[] Fallback);

    /// <summary>
    /// 一个协商的用例
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Enabled">启用列表，按配置的次序</param>
    /// <param name="Default">默认区域</param>
    /// <param name="Candidates">调用方给出的候选区域，按偏好的次序</param>
    /// <param name="Locale">协商出的区域</param>
    private sealed record NegotiationCase(string Name, string[] Enabled, string Default, string[] Candidates, string Locale);

    /// <summary>
    /// 一个回退链的用例
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Enabled">启用列表，按配置的次序</param>
    /// <param name="Default">默认区域</param>
    /// <param name="Locale">取文案时请求的区域</param>
    /// <param name="Chain">依次查找的区域</param>
    private sealed record FallbackCase(string Name, string[] Enabled, string Default, string Locale, string[] Chain);
}
