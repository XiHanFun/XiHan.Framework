namespace XiHan.Framework.Kernel.DependencyInjection;

/// <summary>
/// 表示对象访问器抽象。
/// </summary>
/// <typeparam name="T">对象类型。</typeparam>
public interface IObjectAccessor<out T>
{
    /// <summary>
    /// 获取当前对象值。
    /// </summary>
    T? Value { get; }
}
