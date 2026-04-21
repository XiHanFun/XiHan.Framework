namespace XiHan.Framework.Bootstrap.Abstractions.Reflections;

/// <summary>
/// 表示类型查找器抽象。
/// </summary>
public interface ITypeFinder
{
    /// <summary>
    /// 获取当前可发现的类型集合。
    /// </summary>
    IReadOnlyList<Type> Types { get; }
}
