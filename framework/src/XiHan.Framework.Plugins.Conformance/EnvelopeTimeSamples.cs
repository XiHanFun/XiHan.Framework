// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Json;

namespace XiHan.Framework.Plugins.Conformance;

/// <summary>
/// 一致性用例投递的信封 <c>time</c> 文本
/// </summary>
/// <remarks>
/// 取自随包内嵌的规范向量 <c>plugin/envelope-times.json</c> 与 <c>plugin/sdk-envelope-times.json</c>（<c>invocation.md</c> §7，ADR-0327），不在这里另抄一份：
/// 插件必须读不出的是前者的 <c>invalid</c> 与后者 <c>unreadable</c> 的 <c>text</c>，宿主的写法是前者 <c>written</c> 里第一个有时刻的 <c>text</c>
/// </remarks>
internal static class EnvelopeTimeSamples
{
    /// <summary>
    /// 内嵌向量的逻辑名前缀
    /// </summary>
    private const string ResourcePrefix = "Vectors/plugin/";

    /// <summary>
    /// 插件必须读不出、以 gRPC <c>InvalidArgument</c> 拒绝那次投递的文本
    /// </summary>
    private static readonly Lazy<IReadOnlyList<string>> _unreadable = new(LoadUnreadable);

    /// <summary>
    /// 宿主写给插件的一个文本
    /// </summary>
    private static readonly Lazy<string> _hostWritten = new(LoadHostWritten);

    /// <summary>
    /// 插件必须读不出的文本
    /// </summary>
    /// <remarks>
    /// 按向量里的先后：先 <c>envelope-times.json</c> 的 <c>invalid</c>，再 <c>sdk-envelope-times.json</c> 的 <c>unreadable</c>
    /// </remarks>
    public static IReadOnlyList<string> Unreadable => _unreadable.Value;

    /// <summary>
    /// 宿主的写法
    /// </summary>
    /// <remarks>
    /// 换算成 UTC、七位小数、<c>+00:00</c>（<c>invocation.md</c> §4「宿主写给插件的」）
    /// </remarks>
    public static string HostWritten => _hostWritten.Value;

    /// <summary>
    /// 读两份向量里必须读不出的文本
    /// </summary>
    private static IReadOnlyList<string> LoadUnreadable()
    {
        using var host = Load("envelope-times.json");
        using var sdk = Load("sdk-envelope-times.json");
        return
        [
            .. host.RootElement.GetProperty("invalid").EnumerateArray().Select(static text => text.GetString() ?? throw Malformed("envelope-times.json")),
            .. sdk.RootElement.GetProperty("unreadable").EnumerateArray().Select(static item => item.GetProperty("text").GetString() ?? throw Malformed("sdk-envelope-times.json")),
        ];
    }

    /// <summary>
    /// 读 <c>written</c> 里第一个有时刻的文本
    /// </summary>
    private static string LoadHostWritten()
    {
        using var host = Load("envelope-times.json");
        foreach (var written in host.RootElement.GetProperty("written").EnumerateArray())
        {
            if (written.GetProperty("instant").ValueKind != JsonValueKind.Null)
            {
                return written.GetProperty("text").GetString() ?? throw Malformed("envelope-times.json");
            }
        }

        throw Malformed("envelope-times.json");
    }

    /// <summary>
    /// 读一份内嵌的向量
    /// </summary>
    /// <remarks>
    /// 先过 JSON 文本的共同检查（重复成员、嵌套层数），与测试里严格读取向量的口径相同
    /// </remarks>
    private static JsonDocument Load(string name)
    {
        using var stream = typeof(EnvelopeTimeSamples).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"The spec vector 'plugin/{name}' is not embedded in the conformance suite.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var text = buffer.ToArray();
        return JsonText.FindProblem(text).HasValue
            ? throw Malformed(name)
            : JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = JsonText.MaxDepth, AllowDuplicateProperties = false });
    }

    /// <summary>
    /// 内嵌的向量不是预期形态时的异常
    /// </summary>
    private static InvalidOperationException Malformed(string name) => new($"The embedded spec vector 'plugin/{name}' does not have the expected shape.");
}
