// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tools;

/// <summary>
/// 工具目录的实现（单例）：构造时从登记表与显式登记建表，工具名不合法或重复以 <see cref="AIErrors.RequestInvalid"/> 失败。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AIToolCatalog : IAIToolCatalog
{
    /// <summary>
    /// 没有请求 Schema 时的输入 Schema。
    /// </summary>
    private const string DefaultInputSchema = """{"type":"object"}""";

    /// <summary>
    /// 名字 → 条目。
    /// </summary>
    private readonly SortedDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 调用器。
    /// </summary>
    private readonly IOperationInvoker _invoker;

    /// <summary>
    /// 作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// 本地化器（可无）。
    /// </summary>
    private readonly IMessageLocalizer? _localizer;

    /// <summary>
    /// 创建。
    /// </summary>
    public AIToolCatalog(AIOptions options, OperationRegistry registry, IOperationInvoker invoker, IServiceScopeFactory scopes, IEnumerable<AITool> registered, IMessageLocalizer? localizer = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(registered);
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _localizer = localizer;
        foreach (var tool in registered)
        {
            Add(tool.Name, new Entry(tool.Name, tool, null, default, null), tool.GetType().FullName ?? tool.Name);
        }

        foreach (var descriptor in registry.ExposedTo(Expose.Ai))
        {
            // 内部稳定性的操作只供进程内调用，不交给模型
            if (descriptor.Contract.Stability == Stability.Internal)
            {
                continue;
            }

            var name = options.Tools.TryGetValue(descriptor.Id, out var overridden) ? overridden : AIToolNameConvention.Derive(descriptor.Contract);
            if (!AIIdentifiers.IsValidToolName(name))
            {
                throw new FrameworkException(AIErrors.RequestInvalid, string.Format(CultureInfo.InvariantCulture, "Tool name '{0}' derived for operation '{1}' is not valid; override it with '{2}:Tools:{1}'.", name, descriptor.Id, AIOptions.ConfigurationSection));
            }

            var schema = ParseObjectSchema(descriptor.Schema?.Request) ?? ParseObjectSchema(DefaultInputSchema)!.Value;
            Add(name, new Entry(name, null, descriptor, schema, ParseObjectSchema(descriptor.Schema?.Response)), descriptor.Id);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AITool> All() => _entries.Values.Select(entry => Declare(entry, DefaultLocale())).ToList();

    /// <inheritdoc />
    public Maybe<AITool> Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.TryGetValue(name, out var entry) ? Maybe.Some(Declare(entry, DefaultLocale())) : Maybe.None<AITool>();
    }

    /// <inheritdoc />
    public IReadOnlyList<AITool> Bind(InvocationContext caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var locale = _localizer?.Options.Resolve(caller);
        return _entries.Values.Select(entry => entry.Tool ?? new OperationAIFunction(entry.Name, Describe(entry.Descriptor!, locale), entry.Schema, entry.ReturnSchema, entry.Descriptor!, _invoker, _scopes, _localizer, caller)).ToList();
    }

    /// <summary>
    /// 加一条，名字重复即失败。
    /// </summary>
    private void Add(string name, Entry entry, string owner)
    {
        if (_entries.TryGetValue(name, out var existing))
        {
            var first = existing.Descriptor?.Id ?? existing.Tool?.GetType().FullName ?? name;
            throw new FrameworkException(AIErrors.RequestInvalid, string.Format(CultureInfo.InvariantCulture, "AI tool name '{0}' is used by both '{1}' and '{2}'.", name, first, owner));
        }

        _entries.Add(name, entry);
    }

    /// <summary>
    /// 声明：显式工具原样，操作工具为不可调用的声明。
    /// </summary>
    private AITool Declare(Entry entry, string? locale)
        => entry.Tool ?? AIFunctionFactory.CreateDeclaration(entry.Name, Describe(entry.Descriptor!, locale), entry.Schema, entry.ReturnSchema);

    /// <summary>
    /// 操作描述：消息目录里 <c>&lt;操作 ID&gt;.description</c> 按区域翻译；没有为空。
    /// </summary>
    private string? Describe(OperationDescriptor descriptor, string? locale)
    {
        if (_localizer is null || locale is null)
        {
            return null;
        }

        var key = AIToolNameConvention.DescriptionKeyOf(descriptor.Id);
        return MessageTemplate.IsValidKey(key) && _localizer.Find(key, locale).TryGetValue(out var text) ? text : null;
    }

    /// <summary>
    /// 默认区域。
    /// </summary>
    private string? DefaultLocale() => _localizer?.Options.DefaultLocale;

    /// <summary>
    /// 只接受对象 Schema。
    /// </summary>
    private static JsonElement? ParseObjectSchema(string? schema)
    {
        if (schema is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(schema);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && string.Equals(type.GetString(), "object", StringComparison.Ordinal)
            ? root.Clone()
            : null;
    }

    /// <summary>
    /// 一条工具：显式工具或操作。
    /// </summary>
    private sealed record Entry(string Name, AITool? Tool, OperationDescriptor? Descriptor, JsonElement Schema, JsonElement? ReturnSchema);
}
