// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Globalization;
using XiHan.Framework.Localization.Options;

namespace XiHan.Framework.Localization.Middlewares;

/// <summary>
/// 请求文化中间件
/// 按 [自定义请求头(默认 X-Language)] &gt; [Accept-Language] &gt; [默认文化] 的优先级解析请求文化，
/// 仅接受配置中受支持的文化（无效则回退默认文化），并在当前请求范围内设置 <see cref="CultureInfo.CurrentCulture"/> 与 <see cref="CultureInfo.CurrentUICulture"/>。
/// </summary>
public class XiHanRequestCultureMiddleware(RequestDelegate next, IOptionsMonitor<XiHanLocalizationOptions> optionsMonitor)
{
    /// <summary>
    /// HttpContext.Items 中存放解析后文化名的键（便于日志/请求上下文取用）
    /// </summary>
    public const string CultureItemKey = "__XiHanCulture";

    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var options = optionsMonitor.CurrentValue;
        var culture = ResolveCulture(context, options);

        context.Items[CultureItemKey] = culture.Name;

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            await next(context);
        }
        finally
        {
            // 还原线程文化，避免线程被线程池复用时残留请求级文化
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static CultureInfo ResolveCulture(HttpContext context, XiHanLocalizationOptions options)
    {
        // 1. 自定义请求头（如 X-Language）
        var headerName = string.IsNullOrWhiteSpace(options.CultureHeaderName) ? "X-Language" : options.CultureHeaderName;
        var fromHeader = context.Request.Headers[headerName].FirstOrDefault();
        if (TryResolveSupported(fromHeader, options, out var headerCulture))
        {
            return headerCulture;
        }

        // 2. 标准 Accept-Language（按 q 权重顺序取首个受支持项）
        foreach (var candidate in EnumerateAcceptLanguages(context))
        {
            if (TryResolveSupported(candidate, options, out var acceptCulture))
            {
                return acceptCulture;
            }
        }

        // 3. 默认文化
        return CreateCultureOrInvariant(options.DefaultCulture);
    }

    private static IEnumerable<string> EnumerateAcceptLanguages(HttpContext context)
    {
        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            yield break;
        }

        var weighted = new List<(string Value, double Quality)>();
        foreach (var part in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var segments = part.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var value = segments[0];
            if (string.IsNullOrWhiteSpace(value) || value == "*")
            {
                continue;
            }

            var quality = 1.0d;
            for (var i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(segment[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    quality = parsed;
                }
            }

            weighted.Add((value, quality));
        }

        foreach (var item in weighted.OrderByDescending(x => x.Quality))
        {
            yield return item.Value;
        }
    }

    private static bool TryResolveSupported(string? cultureName, XiHanLocalizationOptions options, out CultureInfo culture)
    {
        culture = CultureInfo.InvariantCulture;
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return false;
        }

        cultureName = cultureName.Trim();

        // 空列表表示不限制，仅要求为合法文化
        if (options.SupportedCultures is not { Count: > 0 } supported)
        {
            if (!TryCreateCulture(cultureName, out culture))
            {
                return false;
            }

            return true;
        }

        var matched = supported.FirstOrDefault(x => string.Equals(x, cultureName, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
        {
            return false;
        }

        return TryCreateCulture(matched, out culture);
    }

    private static bool TryCreateCulture(string cultureName, out CultureInfo culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
            return false;
        }
    }

    private static CultureInfo CreateCultureOrInvariant(string cultureName)
    {
        return TryCreateCulture(cultureName, out var culture) ? culture : CultureInfo.InvariantCulture;
    }
}
