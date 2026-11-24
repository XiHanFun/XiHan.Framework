// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Configuration.Nacos.Tests;

/// <summary>
/// 回放 <c>remote-documents.json</c>：远程配置文档的展平与 Nacos 的内容版本
/// </summary>
/// <remarks>
/// <c>configuration/remote-documents.json</c>（RFC-0014「远程配置」与「Nacos Provider」的「文档格式」，ADR-0277、ADR-0286）。展平经内部的 <see cref="NacosDocumentParser"/> 回放；版本的算法在客户端里是私有的，经 <see cref="NacosConfigurationClient"/> 取一份不带 <c>Content-MD5</c> 头的响应走到它（HTTP 桩，不连真实的 Nacos），再拿这个版本去监听，桩按 Nacos 的约定逐字比较
/// </remarks>
public sealed class RemoteDocumentVectorTests
{
    /// <summary>
    /// 取文档用的命名空间（Nacos 的 <c>dataId</c>）
    /// </summary>
    private const string Namespace = "orders";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly DocumentVectors _vectors = SpecVectors.Read<DocumentVectors>("configuration", "remote-documents.json");

    [Fact]
    public void ErrorCode_IsTheOneInTheVectors()
        => Assert.Equal(ConfigurationErrors.DocumentInvalid, _vectors.Code);

    [Fact]
    public void Parser_FlattensEveryDocument()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var values = NacosDocumentParser.Parse(vector.Content, Enum.Parse<NacosConfigFormat>(vector.Format));
            var actual = Describe(values);
            var expected = Describe(vector.Values);
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: flattened to [{actual}], the vector has [{expected}]");
            }

            // 向量里的键照文档里写的大小写给出：上面按序数逐字比较过；这里再核对读出来的键不区分大小写
            if (vector.Values.Keys.Any(key => !values.ContainsKey(key.ToUpperInvariant()) || !values.ContainsKey(key.ToLowerInvariant())))
            {
                failures.Add($"{vector.Name}: the flattened keys are case-sensitive");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Client_FetchesEveryDocument_WithTheMd5OfItsContentAsTheVersion()
    {
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            // 响应不带 Content-MD5 头：版本由客户端按内容自己算。桩存的 MD5 取向量的版本，监听时逐字比较
            using var handler = new NacosStubHandler { Content = vector.Content, ContentMd5 = vector.Version, SendContentMd5Header = false };
            using var http = new HttpClient(handler, disposeHandler: false);
            using var client = new NacosConfigurationClient(Options(vector.Format), http, TimeProvider.System);

            var fetched = await client.FetchAsync(Namespace, null, CancellationToken.None);
            if (!fetched.TryGetValue(out var found) || !found.TryGetValue(out var document))
            {
                failures.Add($"{vector.Name}: the fetch returned '{fetched}' instead of a document");
                continue;
            }

            if (document.Version != vector.Version)
            {
                failures.Add($"{vector.Name}: the version is '{document.Version}', the vector has '{vector.Version}'");
            }

            var actual = Describe(document.Values);
            var expected = Describe(vector.Values);
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: the document has [{actual}], the vector has [{expected}]");
            }

            // 键不区分大小写
            if (vector.Values.Keys.Any(key => !document.Values.ContainsKey(key.ToUpperInvariant()) || !document.Values.ContainsKey(key.ToLowerInvariant())))
            {
                failures.Add($"{vector.Name}: the keys of the document are case-sensitive");
            }

            // 以这个版本监听：服务端认出没有变化，客户端不再取整份文档
            var unchanged = await client.FetchAsync(Namespace, document.Version, CancellationToken.None);
            if (!unchanged.TryGetValue(out var again) || again.HasValue || handler.Fetches != 1 || !handler.ListenedMd5.SequenceEqual([vector.Version]))
            {
                failures.Add($"{vector.Name}: listening with the version returned '{unchanged}' after {handler.Fetches} fetch(es), having reported [{string.Join(", ", handler.ListenedMd5)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task InvalidDocuments_AreRejected_WithTheCodeInTheVectors()
    {
        Assert.NotEmpty(_vectors.Invalid);
        var failures = new List<string>();
        foreach (var vector in _vectors.Invalid)
        {
            Assert.Throws<FormatException>(() => NacosDocumentParser.Parse(vector.Content, Enum.Parse<NacosConfigFormat>(vector.Format)));

            using var handler = new NacosStubHandler { Content = vector.Content };
            using var http = new HttpClient(handler, disposeHandler: false);
            using var client = new NacosConfigurationClient(Options(vector.Format), http, TimeProvider.System);
            var fetched = await client.FetchAsync(Namespace, null, CancellationToken.None);
            if (fetched.Error?.Code != _vectors.Code)
            {
                failures.Add($"{vector.Name}: the fetch returned '{fetched}', the vector has '{_vectors.Code}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverBothFormats_AndTheRulesOfEach()
    {
        // 防止向量换了写法后某条规则一条都没核对还照样通过：两种格式都要有
        var json = _vectors.Cases.Where(static vector => vector.Format == nameof(NacosConfigFormat.Json)).ToList();
        var properties = _vectors.Cases.Where(static vector => vector.Format == nameof(NacosConfigFormat.Properties)).ToList();
        Assert.NotEmpty(json);
        Assert.NotEmpty(properties);
        Assert.Equal(_vectors.Cases.Length, json.Count + properties.Count);

        // JSON：嵌套对象、数组下标、保留下来的空值、没有键的空对象；版本按 UTF-8 字节算，所以要有非 ASCII 的内容
        Assert.Contains(json, static vector => vector.Values.Keys.Any(static key => key.Split(':').Length > 2));
        Assert.Contains(json, static vector => vector.Values.Keys.Any(static key => key.Split(':').Any(static segment => segment.Length > 0 && segment.All(char.IsAsciiDigit))));
        Assert.Contains(json, static vector => vector.Values.Values.Any(static value => value is null));
        Assert.Contains(json, static vector => vector.Values.Count == 0);
        Assert.Contains(_vectors.Cases, static vector => !vector.Content.All(char.IsAscii));

        // Properties：键里的点换成冒号、注释行、值里的等号
        Assert.Contains(properties, static vector => vector.Values.Keys.Any(static key => key.Contains(':', StringComparison.Ordinal)));
        Assert.Contains(properties, static vector => vector.Content.Split('\n').Any(static line => line.StartsWith('#')) && vector.Content.Split('\n').Any(static line => line.StartsWith('!')));
        Assert.Contains(properties, static vector => vector.Values.Values.Any(static value => value is not null && value.Contains('=', StringComparison.Ordinal)));

        // JSON 的文本形式与宽松之处（ADR-0286）：布尔、带小数点或指数的数字、注释、尾逗号、正好到上限的嵌套
        Assert.Contains(json, static vector => vector.Values.ContainsValue("True") && vector.Values.ContainsValue("False"));
        Assert.Contains(json, static vector => vector.Values.Values.Any(static value => value is not null && value.Contains('.') && value.All(static c => char.IsAsciiDigit(c) || c is '.' or '-')) && vector.Values.Values.Any(static value => value is not null && value.Contains('e')));
        Assert.Contains(json, static vector => vector.Content.Contains("//", StringComparison.Ordinal) && vector.Content.Contains("/*", StringComparison.Ordinal));
        Assert.Contains(json, static vector => vector.Content.Contains(",]", StringComparison.Ordinal) && vector.Content.Contains(",}", StringComparison.Ordinal));
        Assert.Contains(json, static vector => vector.Values.Keys.Any(static key => key.Split(':').Length == MaxDepth));

        // Properties：冒号作分隔符、两侧的空白（含 ASCII 以外的）、没有分隔符的行
        Assert.Contains(properties, static vector => !vector.Content.Contains('=') && vector.Values.Count > 0 && vector.Values.Values.All(static value => value is { Length: > 0 }));
        Assert.Contains(properties, static vector => vector.Content.Contains('\u00a0') && vector.Values.All(static value => value.Key == value.Key.Trim() && value.Value == value.Value?.Trim()));
        Assert.Contains(properties, static vector => vector.Values.Count > 0 && vector.Values.Values.All(static value => value is { Length: 0 }) && !vector.Content.Contains('=') && !vector.Content.Contains(':'));

        // 不合法的：两种格式里重复的键（含只差大小写的）、顶层不是对象、嵌套只多一层
        Assert.Contains(_vectors.Invalid, static vector => vector.Format == nameof(NacosConfigFormat.Json) && vector.Content == """{"a":1,"a":2}""");
        Assert.Contains(_vectors.Invalid, static vector => vector.Format == nameof(NacosConfigFormat.Json) && vector.Content.Contains("\"Name\"", StringComparison.Ordinal) && vector.Content.Contains("\"name\"", StringComparison.Ordinal));
        Assert.Contains(_vectors.Invalid, static vector => vector.Format == nameof(NacosConfigFormat.Properties) && vector.Content.Split('\n').Where(static line => line.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count() < vector.Content.Split('\n').Count(static line => line.Length > 0) + 1 && vector.Content.Contains("A.B", StringComparison.Ordinal));
        Assert.Contains(_vectors.Invalid, static vector => vector.Format == nameof(NacosConfigFormat.Json) && vector.Content.StartsWith('['));
        Assert.Contains(_vectors.Invalid, static vector => vector.Format == nameof(NacosConfigFormat.Json) && vector.Content.Count(static c => c == '{') == MaxDepth + 1);
    }

    /// <summary>
    /// JSON 文档里对象与数组最多嵌套的层数
    /// </summary>
    private const int MaxDepth = 64;

    /// <summary>
    /// 指向 HTTP 桩、按给定格式解析的选项
    /// </summary>
    /// <param name="format">向量里的文档格式</param>
    private static NacosOptions Options(string format)
        => new() { ServerAddresses = ["http://nacos.test:8848"], Format = Enum.Parse<NacosConfigFormat>(format) };

    /// <summary>
    /// 展平后的键值的描述：按键排序，空值写成 null
    /// </summary>
    /// <param name="values">键到值</param>
    private static string Describe(IReadOnlyDictionary<string, string?> values)
        => string.Join(", ", values.OrderBy(static value => value.Key, StringComparer.Ordinal).Select(static value => $"{value.Key}={(value.Value is null ? "null" : $"'{value.Value}'")}"));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Code">文档不合声明格式时的错误码</param>
    /// <param name="Cases">必须读得出的文档</param>
    /// <param name="Invalid">不合声明格式的文档</param>
    private sealed record DocumentVectors(string Code, DocumentCase[] Cases, InvalidDocument[] Invalid);

    /// <summary>
    /// 一份必须读得出的文档
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Format">文档格式</param>
    /// <param name="Content">远端的文档原文</param>
    /// <param name="Version">这份文档的版本：内容按 UTF-8 编码的 MD5（小写十六进制）</param>
    /// <param name="Values">展平后的键到值；<see langword="null"/> 是保留下来的空值</param>
    private sealed record DocumentCase(string Name, string Format, string Content, string Version, Dictionary<string, string?> Values);

    /// <summary>
    /// 一份不合声明格式的文档
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Format">文档格式</param>
    /// <param name="Content">远端的文档原文</param>
    private sealed record InvalidDocument(string Name, string Format, string Content);
}
