namespace XiHan.Framework.Kernel.DependencyInjection;

/// <summary>
/// 提供对象访问器默认实现。
/// </summary>
/// <typeparam name="T">对象类型。</typeparam>
public class ObjectAccessor<T> : IObjectAccessor<T>
{
    /// <summary>
    /// 初始化访问器实例。
    /// </summary>
    public ObjectAccessor()
    {
    }

    /// <summary>
    /// 使用指定对象初始化访问器实例。
    /// </summary>
    /// <param name="value">对象值。</param>
    public ObjectAccessor(T? value)
    {
        Value = value;
    }

    /// <inheritdoc />
    public T? Value { get; set; }
}
