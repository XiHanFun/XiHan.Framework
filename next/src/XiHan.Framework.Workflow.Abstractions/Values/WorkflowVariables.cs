// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// 实例变量的视图。
/// </summary>
/// <remarks>
/// 按名读写 JSON 值并做类型化转换；写入的值深拷贝（JSON 节点只能有一个父）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowVariables
{
    /// <summary>
    /// 底层对象（实例的 <see cref="WorkflowInstance.Variables"/>）。
    /// </summary>
    private readonly JsonObject _values;

    /// <summary>
    /// 包装一个对象。
    /// </summary>
    /// <remarks>
    /// 修改直接作用于它。
    /// </remarks>
    public WorkflowVariables(JsonObject values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    /// <summary>
    /// 变量名。
    /// </summary>
    public IEnumerable<string> Names => _values.Select(static pair => pair.Key);

    /// <summary>
    /// 变量个数。
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    /// 按名取值。
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>。
    /// </remarks>
    public JsonNode? this[string name] => Get(name);

    /// <summary>
    /// 是否有该变量（值为 null 也算有）。
    /// </summary>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.ContainsKey(name);
    }

    /// <summary>
    /// 按名取值。
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>。
    /// </remarks>
    public JsonNode? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.TryGetPropertyValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// 按名取值并转成 <typeparamref name="T"/>。
    /// </summary>
    /// <remarks>
    /// 没有或转不成返回默认值。
    /// </remarks>
    public T? Get<T>(string name) => WorkflowValues.TryConvert<T>(Get(name), out var value) ? value : default;

    /// <summary>
    /// 按名取值并转成 <typeparamref name="T"/>。
    /// </summary>
    public bool TryGet<T>(string name, [MaybeNullWhen(false)] out T value)
    {
        if (WorkflowValues.TryConvert<T>(Get(name), out var converted) && converted is not null)
        {
            value = converted;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 设值（深拷贝）。
    /// </summary>
    public void Set(string name, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = value?.DeepClone();
    }

    /// <summary>
    /// 合并一组值（逐个深拷贝，同名覆盖）。
    /// </summary>
    public void Merge(JsonObject? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var (name, value) in values)
        {
            _values[name] = value?.DeepClone();
        }
    }

    /// <summary>
    /// 删除变量。
    /// </summary>
    public bool Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.Remove(name);
    }

    /// <summary>
    /// 深拷贝快照。
    /// </summary>
    public JsonObject Snapshot() => (JsonObject)_values.DeepClone();

    /// <summary>
    /// 底层对象。
    /// </summary>
    /// <remarks>
    /// 不拷贝；表达式求值用。
    /// </remarks>
    public JsonObject AsJsonObject() => _values;
}
