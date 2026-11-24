// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Kernel.Tests.Json;

/// <summary>
/// 回放 Result 与 Error 线上编码的一致性向量
/// </summary>
/// <remarks>
/// <c>primitives/wire-encoding.json</c>（RFC-0001「Result」，ADR-0244）：valid 读得出、再写出时按值模型等于 canonical（没有时等于原样），invalid 与 duplicates 被拒绝。
/// results 的 kind：<c>value</c> 读成值为 <see cref="JsonElement"/> 的结果，<c>unit</c> 读成 <see cref="Result"/>，<c>maybe</c> 读成值为 <see cref="Maybe{T}"/> 的结果
/// </remarks>
public sealed class WireEncodingVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly WireEncodingVectors _vectors = SpecVectors.Read<WireEncodingVectors>("primitives", "wire-encoding.json");

    [Fact]
    public void Results_FollowTheWireEncodingVectors()
    {
        Assert.NotEmpty(_vectors.Results.Valid);
        Assert.NotEmpty(_vectors.Results.Invalid);
        var failures = new List<string>();
        foreach (var vector in _vectors.Results.Valid)
        {
            var written = RoundTripResult(vector.Kind!, vector.Json.GetRawText());
            if (written.Error is { } error)
            {
                failures.Add($"{vector.Name}: must be read, got {error}");
            }
            else if (ValueModel(written.ValueOrThrow()) != ValueModel((vector.Canonical ?? vector.Json).GetRawText()))
            {
                failures.Add($"{vector.Name}: written {written.ValueOrThrow()}, expected {(vector.Canonical ?? vector.Json).GetRawText()}");
            }
        }

        foreach (var vector in _vectors.Results.Invalid)
        {
            if (RoundTripResult(vector.Kind!, vector.Json.GetRawText()) is { IsSuccess: true } written)
            {
                failures.Add($"{vector.Name}: must be rejected, read as {written.ValueOrThrow()}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Errors_FollowTheWireEncodingVectors()
    {
        Assert.NotEmpty(_vectors.Errors.Valid);
        Assert.NotEmpty(_vectors.Errors.Invalid);
        var failures = new List<string>();
        foreach (var vector in _vectors.Errors.Valid)
        {
            var read = ResultJson.ReadError(vector.Json.GetRawText());
            if (read.Error is { } error)
            {
                failures.Add($"{vector.Name}: must be read, got {error}");
                continue;
            }

            var written = ResultJson.WriteError(read.ValueOrThrow());
            if (ValueModel(written) != ValueModel((vector.Canonical ?? vector.Json).GetRawText()))
            {
                failures.Add($"{vector.Name}: written {written}, expected {(vector.Canonical ?? vector.Json).GetRawText()}");
            }
        }

        foreach (var vector in _vectors.Errors.Invalid)
        {
            if (ResultJson.ReadError(vector.Json.GetRawText()).IsSuccess)
            {
                failures.Add($"{vector.Name}: must be rejected");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Duplicates_FollowTheWireEncodingVectors()
    {
        Assert.NotEmpty(_vectors.Duplicates);
        var failures = new List<string>();
        foreach (var vector in _vectors.Duplicates)
        {
            var error = vector.Target switch
            {
                "result" => RoundTripResult(vector.Kind!, vector.Text).Error,
                "error" => ResultJson.ReadError(vector.Text).Error,
                _ => throw new InvalidOperationException($"Unknown target '{vector.Target}' in '{vector.Name}'."),
            };

            // 重复成员由共同的 JSON 读取检查报出，不论它在哪一层
            if (error?.MessageKey != KernelMessages.WireInvalidJson)
            {
                failures.Add($"{vector.Name}: must be rejected as a duplicate member, got {error?.ToString() ?? "success"}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 按样例的种类读结果再写出
    /// </summary>
    /// <returns>读不出时是读取的失败；否则是再写出的文本</returns>
    private static Result<string> RoundTripResult(string kind, string json)
    {
        switch (kind)
        {
            case "value":
                var value = ResultJson.Read(json, VectorJsonContext.Default.JsonElement);
                return value.Error is { } valueError ? Result.Failure<string>(valueError) : ResultJson.Write(value.ValueOrThrow(), VectorJsonContext.Default.JsonElement);
            case "unit":
                var unit = ResultJson.Read(json);
                return unit.Error is { } unitError ? Result.Failure<string>(unitError) : ResultJson.Write(unit.ValueOrThrow());
            case "maybe":
                var maybe = ResultJson.ReadMaybe(json, VectorJsonContext.Default.JsonElement);
                return maybe.Error is { } maybeError ? Result.Failure<string>(maybeError) : ResultJson.WriteMaybe(maybe.ValueOrThrow(), VectorJsonContext.Default.JsonElement);
            default:
                throw new InvalidOperationException($"Unknown result kind '{kind}'.");
        }
    }

    /// <summary>
    /// 按值模型写出 JSON 文本的规范形式，用于比较
    /// </summary>
    /// <remarks>
    /// 不比较成员顺序与空白；64 位范围内整数写法的数字是整数、要精确相等，其余是双精度浮点、按值相等，整数与浮点不相等（ADR-0236）
    /// </remarks>
    private static string ValueModel(string json)
    {
        using var document = JsonDocument.Parse(json);
        var text = new StringBuilder();
        Append(text, document.RootElement);
        return text.ToString();
    }

    /// <summary>
    /// 把一个值按值模型的规范形式写进文本
    /// </summary>
    private static void Append(StringBuilder text, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                text.Append('{');
                foreach (var member in element.EnumerateObject().OrderBy(static m => m.Name, StringComparer.Ordinal))
                {
                    text.Append('"').Append(JsonEncodedText.Encode(member.Name).Value).Append("\":");
                    Append(text, member.Value);
                    text.Append(',');
                }

                text.Append('}');
                break;
            case JsonValueKind.Array:
                text.Append('[');
                foreach (var item in element.EnumerateArray())
                {
                    Append(text, item);
                    text.Append(',');
                }

                text.Append(']');
                break;
            case JsonValueKind.Number when element.TryGetInt64(out var integer):
                text.Append("i:").Append(integer.ToString(CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.Number:
                // -0.0 与 0.0 按值相等
                var number = element.GetDouble();
                text.Append("f:").Append((number == 0 ? 0d : number).ToString("R", CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.String:
                text.Append('"').Append(JsonEncodedText.Encode(element.GetString()!).Value).Append('"');
                break;
            default:
                text.Append(element.GetRawText());
                break;
        }
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record WireEncodingVectors(WireCases Results, WireCases Errors, DuplicateCase[] Duplicates);

    /// <summary>
    /// 一组样例：必须读得出的与必须拒绝的
    /// </summary>
    private sealed record WireCases(WireCase[] Valid, WireCase[] Invalid);

    /// <summary>
    /// 一条样例：结果的种类（错误没有）、原文与再写出时的规范形式
    /// </summary>
    private sealed record WireCase(string Name, JsonElement Json, string? Kind = null, JsonElement? Canonical = null);

    /// <summary>
    /// 一条有重复成员的样例：目标、结果的种类与原文
    /// </summary>
    private sealed record DuplicateCase(string Name, string Target, string Text, string? Kind = null);
}
