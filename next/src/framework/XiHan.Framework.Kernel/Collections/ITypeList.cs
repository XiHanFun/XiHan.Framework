namespace XiHan.Framework.Kernel.Collections;

/// <summary>
/// 表示类型列表抽象。
/// </summary>
public interface ITypeList : ITypeList<object>;

/// <summary>
/// 表示带基础类型约束的类型列表抽象。
/// </summary>
/// <typeparam name="TBaseType">基础类型。</typeparam>
public interface ITypeList<in TBaseType> : IList<Type>
{
    /// <summary>
    /// 添加指定泛型类型。
    /// </summary>
    void Add<T>() where T : TBaseType;

    /// <summary>
    /// 尝试添加指定泛型类型。
    /// </summary>
    bool TryAdd<T>() where T : TBaseType;

    /// <summary>
    /// 判断是否包含指定泛型类型。
    /// </summary>
    bool Contains<T>() where T : TBaseType;

    /// <summary>
    /// 移除指定泛型类型。
    /// </summary>
    void Remove<T>() where T : TBaseType;
}
