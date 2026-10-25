// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Proto;

/// <summary>
/// 一个 protobuf 消息的描述：名字与按字段号排列的字段。由 <see cref="ProtoSchema.FromJsonSchema"/> 从 JSON Schema 建出；编解码与 <c>.proto</c> 生成都只看它。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ProtoMessage
{
    /// <summary>
    /// 字段号 → 字段。
    /// </summary>
    private readonly Dictionary<int, ProtoField> _byNumber = [];

    /// <summary>
    /// JSON 名 → 字段。
    /// </summary>
    private readonly Dictionary<string, ProtoField> _byJsonName = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建空消息；字段由 <see cref="Complete"/> 填入（递归引用需要先有消息再填字段）。
    /// </summary>
    public ProtoMessage(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// 消息名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 字段，按字段号升序。
    /// </summary>
    public IReadOnlyList<ProtoField> Fields { get; private set; } = [];

    /// <summary>
    /// 按字段号找字段。
    /// </summary>
    public bool TryGetField(int number, [MaybeNullWhen(false)] out ProtoField field) => _byNumber.TryGetValue(number, out field);

    /// <summary>
    /// 按 JSON 名找字段。
    /// </summary>
    public bool TryGetField(string jsonName, [MaybeNullWhen(false)] out ProtoField field)
    {
        ArgumentNullException.ThrowIfNull(jsonName);
        return _byJsonName.TryGetValue(jsonName, out field);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Fields.Count} fields)";

    /// <summary>
    /// 填入字段；字段号与 JSON 名都必须唯一，只能填一次。
    /// </summary>
    internal void Complete(IReadOnlyList<ProtoField> fields)
    {
        if (Fields.Count > 0 || _byNumber.Count > 0)
        {
            throw new InvalidOperationException($"Message '{Name}' already has fields.");
        }

        foreach (var field in fields)
        {
            if (field.Number <= 0 || !_byNumber.TryAdd(field.Number, field))
            {
                throw new ProtoException($"Message '{Name}' has an invalid or duplicate field number {field.Number}.");
            }

            if (!_byJsonName.TryAdd(field.JsonName, field))
            {
                throw new ProtoException($"Message '{Name}' has a duplicate field '{field.JsonName}'.");
            }
        }

        Fields = fields.OrderBy(static f => f.Number).ToList();
    }
}
