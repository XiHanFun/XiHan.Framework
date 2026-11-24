// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.InMemory.Tests;

/// <summary>
/// 回放 <c>catalog-resolution.json</c>：消息目录的合并、冲突与文案的查找
/// </summary>
/// <remarks>
/// <c>localization/catalog-resolution.json</c>（RFC-0010「语言文件」「解析」，ADR-0277、ADR-0286）。目录按用例的次序经 <c>AddMessageCatalog</c> 注册：冲突的用例注册时就以向量给的错误码失败；其余的从容器取登记表，用 <see cref="InMemoryMessageLocalizer"/> 按用例的默认区域与内置的启用列表逐条查找
/// </remarks>
public sealed class CatalogResolutionVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly CatalogVectors _vectors = SpecVectors.Read<CatalogVectors>("localization", "catalog-resolution.json");

    [Fact]
    public void ErrorCode_IsTheOneInTheVectors()
        => Assert.Equal(LocalizationErrors.DuplicateCatalog, _vectors.Code);

    [Fact]
    public void Registration_MergesTheCatalogsOfTheSameName_AndFailsOnAConflict()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var services = new ServiceCollection();
            var conflict = Register(services, vector);
            if (vector.Conflict)
            {
                if (conflict?.Code != _vectors.Code)
                {
                    failures.Add($"{vector.Name}: the registration {(conflict is null ? "succeeded" : $"failed with '{conflict.Code}'")}, the vector has '{_vectors.Code}'");
                }

                continue;
            }

            if (conflict is not null)
            {
                failures.Add($"{vector.Name}: the registration failed with '{conflict.Code}', the vector has no conflict");
                continue;
            }

            // 同名的目录合并成一份，不同名的各留一份，按先注册的次序
            using var provider = services.BuildServiceProvider();
            var registered = provider.GetRequiredService<MessageCatalogRegistry>().Catalogs.Select(static catalog => catalog.Name);
            var expected = vector.Catalogs.Select(static catalog => catalog.Name).Distinct(StringComparer.Ordinal);
            if (!registered.SequenceEqual(expected, StringComparer.Ordinal))
            {
                failures.Add($"{vector.Name}: the registered catalogs are [{string.Join(", ", registered)}], the vector has [{string.Join(", ", expected)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Resolve_FindsTheTextOfEveryRequest()
    {
        var failures = new List<string>();
        var replayed = 0;
        foreach (var vector in _vectors.Cases.Where(static vector => !vector.Conflict))
        {
            var services = new ServiceCollection();
            Assert.Null(Register(services, vector));
            using var provider = services.BuildServiceProvider();
            var localizer = new InMemoryMessageLocalizer(provider.GetRequiredService<MessageCatalogRegistry>(), new LocalizationOptions { DefaultLocale = vector.Default ?? SupportedLocales.Default });

            foreach (var request in vector.Requests)
            {
                replayed++;
                var resolved = localizer.Resolve(request.Key, request.Locale, null);
                var actual = resolved.TryGetValue(out var text) ? $"'{text.Text}' from '{text.Locale}'" : "nothing";
                var expected = request.Expected is { } hit ? $"'{hit.Text}' from '{hit.Locale}'" : "nothing";
                if (actual != expected)
                {
                    failures.Add($"{vector.Name}: '{request.Key}' in '{request.Locale}' resolved to {actual}, the vector has {expected}");
                }

                // 模板都不带占位符：取模板与解析出的文案是同一段文本
                var template = localizer.Find(request.Key, request.Locale);
                if (template.HasValue != (request.Expected is not null) || (request.Expected is { } found && template.ValueOr(string.Empty) != found.Text))
                {
                    failures.Add($"{vector.Name}: the template of '{request.Key}' in '{request.Locale}' is {template}, the vector has {expected}");
                }
            }
        }

        Assert.True(replayed > 0, "The vectors have no request to replay.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverMergingConflictPrecedenceAndEveryLevelOfTheFallback()
    {
        // 防止向量换了写法后某条规则一条都没核对还照样通过：同名目录合并而不冲突、同名目录冲突、不同名的目录有同一键同一区域
        Assert.Contains(_vectors.Cases, static vector => !vector.Conflict && vector.Catalogs.Length > vector.Catalogs.Select(static catalog => catalog.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(_vectors.Cases, static vector => vector.Conflict);
        Assert.Contains(_vectors.Cases, static vector => !vector.Conflict && vector.Catalogs.Select(static catalog => catalog.Name).Distinct(StringComparer.Ordinal).Count() > 1);

        // 查找：命中请求区域、命中回退链上别的区域、哪一级都没有；默认区域不是 en-US 的用例也要有
        var requests = _vectors.Cases.SelectMany(static vector => vector.Requests).ToList();
        Assert.Contains(requests, static request => request.Expected is { } hit && hit.Locale == request.Locale);
        Assert.Contains(requests, static request => request.Expected is { } hit && hit.Locale != request.Locale);
        Assert.Contains(requests, static request => request.Expected is null);
        Assert.Contains(_vectors.Cases, static vector => vector.Default is { } defaultLocale && defaultLocale != SupportedLocales.Default);

        // 区域优先于目录的次序（ADR-0286）：命中的模板在后注册的目录里，而先注册的目录在别的区域有同一个键
        Assert.Contains(_vectors.Cases, static vector => !vector.Conflict && vector.Requests.Any(request => request.Expected is { } hit
            && vector.Catalogs[0].Messages.Values.Any(templates => templates.ContainsKey(request.Key))
            && !(vector.Catalogs[0].Messages.TryGetValue(hit.Locale, out var first) && first.ContainsKey(request.Key))
            && vector.Catalogs.Skip(1).Any(catalog => catalog.Messages.TryGetValue(hit.Locale, out var later) && later.GetValueOrDefault(request.Key) == hit.Text)));
    }

    /// <summary>
    /// 按用例的次序注册目录
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="vector">用例</param>
    /// <returns>注册抛出的框架异常；都注册上了为 <see langword="null"/></returns>
    private static FrameworkException? Register(ServiceCollection services, CatalogCase vector)
    {
        try
        {
            foreach (var catalog in vector.Catalogs)
            {
                services.AddMessageCatalog(new MessageCatalog(catalog.Name, catalog.Messages.ToDictionary(static locale => locale.Key, static locale => (IReadOnlyDictionary<string, string>)locale.Value, StringComparer.Ordinal)));
            }

            return null;
        }
        catch (FrameworkException exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Code">目录冲突的错误码</param>
    /// <param name="Cases">用例</param>
    private sealed record CatalogVectors(string Code, CatalogCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Catalogs">按次序注册的目录</param>
    /// <param name="Conflict">注册是否必须以冲突失败</param>
    /// <param name="Requests">合并之后的查找</param>
    /// <param name="Default">默认区域；不写即 <c>en-US</c></param>
    private sealed record CatalogCase(string Name, CatalogEntry[] Catalogs, bool Conflict, CatalogRequest[] Requests, string? Default = null);

    /// <summary>
    /// 一份注册的目录
    /// </summary>
    /// <param name="Name">目录名</param>
    /// <param name="Messages">区域到「键 → 模板」</param>
    private sealed record CatalogEntry(string Name, Dictionary<string, Dictionary<string, string>> Messages);

    /// <summary>
    /// 一次查找
    /// </summary>
    /// <param name="Key">消息键</param>
    /// <param name="Locale">请求的区域</param>
    /// <param name="Expected">期望的文案与命中的区域；<see langword="null"/> 表示哪一级都没有这个键</param>
    private sealed record CatalogRequest(string Key, string Locale, CatalogHit? Expected);

    /// <summary>
    /// 一次查找的结果
    /// </summary>
    /// <param name="Text">文案</param>
    /// <param name="Locale">实际命中的区域</param>
    private sealed record CatalogHit(string Text, string Locale);
}
