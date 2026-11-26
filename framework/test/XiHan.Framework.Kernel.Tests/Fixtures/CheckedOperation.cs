// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 按请求的文本交回不同形状的 <see cref="CheckedResponse"/> 的操作
/// </summary>
/// <remarks>
/// 文本是形状的名字，见 <see cref="Render"/>
/// </remarks>
internal sealed class CheckedOperation : IOperation<EchoRequest, CheckedResponse>
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public const string Id = "texts.check";

    /// <inheritdoc />
    public ValueTask<Result<CheckedResponse>> HandleAsync(EchoRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(Render(request.Text ?? string.Empty)));

    /// <summary>
    /// 按形状的名字造响应
    /// </summary>
    /// <remarks>
    /// 响应本身是第 1 层。<c>depth-64</c> 嵌到第 64 层（最里层有值），<c>depth-65</c> 多一层；<c>raw-depth</c> 原样嵌进去的 64 层数组在第 2 层起算，共 65 层；
    /// <c>extension</c>、<c>dictionary-policy</c>、<c>element</c>、<c>node</c>、<c>converter</c>、<c>raw-duplicate</c> 各写出一个重复成员；<c>raw-escape</c> 原样嵌进孤立代理项的转义；其余是合法的响应
    /// </remarks>
    /// <param name="shape">形状的名字</param>
    public static CheckedResponse Render(string shape) => shape switch
    {
        "depth-64" => new CheckedResponse { Nest = DepthNode.Make(63) },
        "depth-65" => new CheckedResponse { Nest = DepthNode.Make(64) },
        "raw-depth" => new CheckedResponse { Raw = new RawJson(new string('[', 64) + "0" + new string(']', 64)) },
        "extension" => new CheckedResponse { Name = "x", Extra = new(StringComparer.Ordinal) { ["name"] = Parse("1") } },
        "dictionary-policy" => new CheckedResponse { Counts = new Dictionary<string, int>(StringComparer.Ordinal) { ["A"] = 1, ["a"] = 2 } },
        "element" => new CheckedResponse { Element = Parse("""{"a":1,"a":2}""") },
        "node" => new CheckedResponse { Node = JsonNode.Parse("""{"a":1,"a":2}""") },
        "converter" => new CheckedResponse { Repeated = new RepeatedMember("a") },
        "raw-duplicate" => new CheckedResponse { Raw = new RawJson("""{"a":1,"a":2}""") },
        "raw-escape" => new CheckedResponse { Raw = new RawJson("\"" + (char)92 + "ud800\"") },
        _ => new CheckedResponse
        {
            Name = "x",
            Nest = DepthNode.Make(3),
            Element = Parse("""{"a":[1,2]}"""),
            Node = new JsonObject { ["a"] = 1, ["b"] = new JsonArray(1, 2) },
            Counts = new Dictionary<string, int>(StringComparer.Ordinal) { ["A"] = 1, ["b"] = 2 },
            Raw = new RawJson("""{"ok":[1]}"""),
            Extra = new(StringComparer.Ordinal) { ["other"] = Parse("1") },
        },
    };

    /// <summary>
    /// 把文本读成脱离文档的 JSON 元素
    /// </summary>
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
