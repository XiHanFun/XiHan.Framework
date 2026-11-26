// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 按请求的文本交回不同字符串的操作
/// </summary>
/// <remarks>
/// 请求的 JSON 里放不进孤立的代理项（读取一侧就拒绝），用例只能让处理器自己造出这样的响应。文本是形状的名字，见 <see cref="Render"/>
/// </remarks>
internal sealed class TextsOperation : IOperation<EchoRequest, TextsResponse>
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public const string Id = "texts.render";

    /// <summary>
    /// 孤立的高代理项
    /// </summary>
    public const char HighSurrogate = (char)0xD800;

    /// <summary>
    /// 孤立的低代理项
    /// </summary>
    public const char LowSurrogate = (char)0xDC00;

    /// <summary>
    /// 本来就是 U+FFFD 的字符
    /// </summary>
    public const char ReplacementCharacter = (char)0xFFFD;

    /// <inheritdoc />
    public ValueTask<Result<TextsResponse>> HandleAsync(EchoRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(Render(request.Text ?? string.Empty)));

    /// <summary>
    /// 按形状的名字造响应
    /// </summary>
    /// <remarks>
    /// <c>value-high</c>、<c>value-low</c>、<c>reversed</c> 把孤立的代理项放进值，<c>key</c> 放进字典的键，<c>item</c> 放进数组元素，<c>node-value</c> 与 <c>node-key</c> 放进 JSON 节点的值与成员名，<c>utf8</c> 以 UTF-8 字节写出编码成 UTF-8 的代理项；
    /// 其余是合法的文本：代理对、本来就是 U+FFFD 的字符（每一处都有）、HTML 敏感字符、引号、反斜杠与控制字符
    /// </remarks>
    /// <param name="shape">形状的名字</param>
    public static TextsResponse Render(string shape)
    {
        var valid = new TextsResponse(
            "订单 😀 " + ReplacementCharacter + " <a href='x'>&</a> \"q\" \\ " + (char)0x01 + (char)0x7F + (char)0x2028,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["😀"] = "v", ["<键>"] = "值", ["k" + ReplacementCharacter] = "v" },
            ["a", "b" + ReplacementCharacter],
            new JsonObject { ["😀"] = "v" + ReplacementCharacter, [ReplacementCharacter + "键"] = new JsonArray("值", "<&>") },
            new Utf8Text([.. "ok 😀 "u8, 0xEF, 0xBF, 0xBD]));
        return shape switch
        {
            "value-high" => valid with { Text = "a" + HighSurrogate },
            "value-low" => valid with { Text = LowSurrogate + "b" },
            "reversed" => valid with { Text = new string([LowSurrogate, HighSurrogate]) },
            "key" => valid with { Tags = new Dictionary<string, string>(StringComparer.Ordinal) { ["k" + HighSurrogate] = "v" } },
            "item" => valid with { Items = ["ok", HighSurrogate.ToString()] },
            "node-value" => valid with { Node = new JsonObject { ["k"] = new JsonArray("ok", "a" + HighSurrogate) } },
            "node-key" => valid with { Node = new JsonObject { ["k" + LowSurrogate] = 1 } },
            "utf8" => valid with { Bytes = new Utf8Text([0x61, 0xED, 0xA0, 0x80]) },
            _ => valid,
        };
    }
}
