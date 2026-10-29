// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 消息解析的环境作用域。
/// </summary>
/// <remarks>
/// 当前异步流里的解析器与区域。内核在每次调用的入口按 Baggage 里的区域安装一个作用域，本地化特性启动时用默认区域设置进程默认；于是 <c>key.L()</c> 在调用链任何位置都得到调用方语言，<see cref="Error.Message"/> 得到默认语言。没有任何作用域时文案退回 <see cref="MessageTemplate.Describe"/>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessageScope : IDisposable
{
    /// <summary>
    /// 当前异步流的作用域。
    /// </summary>
    /// <remarks>
    /// 随 <see cref="AsyncLocal{T}"/> 传播到子任务。
    /// </remarks>
    private static readonly AsyncLocal<MessageScope?> _current = new();

    /// <summary>
    /// 进程默认作用域。
    /// </summary>
    /// <remarks>
    /// 由本地化特性在启动时设置。
    /// </remarks>
    private static volatile MessageScope? _default;

    /// <summary>
    /// 安装本作用域前的作用域，释放时恢复。
    /// </summary>
    private readonly MessageScope? _previous;

    /// <summary>
    /// 是否已释放。
    /// </summary>
    /// <remarks>
    /// 重复释放幂等。
    /// </remarks>
    private bool _disposed;

    /// <summary>
    /// 创建作用域。
    /// </summary>
    private MessageScope(IMessageResolver? resolver, string? locale, MessageScope? previous)
    {
        Resolver = resolver;
        Locale = locale;
        _previous = previous;
    }

    /// <summary>
    /// 当前异步流的作用域。
    /// </summary>
    /// <remarks>
    /// 没有安装过时为进程默认，都没有时为 <see langword="null"/>。
    /// </remarks>
    public static MessageScope? Current => _current.Value ?? _default;

    /// <summary>
    /// 进程默认作用域（默认区域）。
    /// </summary>
    /// <remarks>
    /// <see cref="Error.Message"/> 用它渲染，不受调用方区域影响。
    /// </remarks>
    public static MessageScope? Default => _default;

    /// <summary>
    /// 解析器。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时本作用域只改区域、不解析。
    /// </remarks>
    public IMessageResolver? Resolver { get; }

    /// <summary>
    /// 区域。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时由解析器用默认区域。
    /// </remarks>
    public string? Locale { get; }

    /// <summary>
    /// 设置进程默认作用域。
    /// </summary>
    /// <remarks>
    /// 传 <see langword="null"/> 清除。
    /// </remarks>
    public static void SetDefault(IMessageResolver? resolver, string? locale)
        => _default = resolver is null && locale is null ? null : new MessageScope(resolver, locale, null);

    /// <summary>
    /// 在当前异步流安装作用域。
    /// </summary>
    /// <remarks>
    /// <paramref name="resolver"/> 为 <see langword="null"/> 时沿用当前作用域的解析器，<paramref name="locale"/> 为 <see langword="null"/> 时沿用当前区域。释放时恢复先前作用域；嵌套安装按栈顺序释放。
    /// </remarks>
    public static MessageScope Use(IMessageResolver? resolver, string? locale)
    {
        var current = Current;
        var scope = new MessageScope(resolver ?? current?.Resolver, locale ?? current?.Locale, _current.Value);
        _current.Value = scope;
        return scope;
    }

    /// <summary>
    /// 用本作用域解析并格式化。
    /// </summary>
    /// <remarks>
    /// 没有解析器、找不到键或占位符缺值时退回键与参数的描述。
    /// </remarks>
    public string Resolve(string key, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Resolver is not null && Resolver.TryResolve(key, Locale, arguments, out var text)
            ? text
            : MessageTemplate.Describe(key, arguments);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // 只在自己仍是栈顶时恢复；恢复目标跳过已被乱序释放的作用域，释放顺序不影响最终状态
        if (ReferenceEquals(_current.Value, this))
        {
            var target = _previous;
            while (target is { _disposed: true })
            {
                target = target._previous;
            }

            _current.Value = target;
        }
    }
}
