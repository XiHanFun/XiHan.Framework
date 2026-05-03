namespace XiHan.Framework.Shared.Models;

/// <summary>
/// 表示名称和值的字符串键值对。
/// </summary>
public class NameValue : NameValue<string>
{
    /// <summary>
    /// 初始化实例。
    /// </summary>
    public NameValue()
    {
    }

    /// <summary>
    /// 使用名称和值初始化实例。
    /// </summary>
    /// <param name="name">名称。</param>
    /// <param name="value">值。</param>
    public NameValue(string name, string value)
        : base(name, value)
    {
    }
}

/// <summary>
/// 表示名称和值的键值对。
/// </summary>
/// <typeparam name="T">值类型。</typeparam>
public class NameValue<T>
{
    /// <summary>
    /// 初始化实例。
    /// </summary>
    public NameValue()
    {
    }

    /// <summary>
    /// 使用名称和值初始化实例。
    /// </summary>
    /// <param name="name">名称。</param>
    /// <param name="value">值。</param>
    public NameValue(string name, T value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 获取或设置值。
    /// </summary>
    public T Value { get; set; } = default!;
}
