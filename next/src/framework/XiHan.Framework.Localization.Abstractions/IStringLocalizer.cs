namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 表示字符串本地化器抽象。
/// </summary>
public interface IStringLocalizer
{
    /// <summary>
    /// 根据资源键获取本地化文本。
    /// </summary>
    /// <param name="name">资源键。</param>
    /// <returns>本地化结果，若不存在则返回原始键。</returns>
    string this[string name] { get; }
}
