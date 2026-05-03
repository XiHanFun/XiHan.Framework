namespace XiHan.Framework.Kernel.Exceptions;

/// <summary>
/// 表示框架级基础异常。
/// </summary>
public class XiHanException : Exception
{
    /// <summary>
    /// 初始化异常实例。
    /// </summary>
    public XiHanException()
    {
    }

    /// <summary>
    /// 使用指定异常消息初始化异常实例。
    /// </summary>
    /// <param name="message">异常消息。</param>
    public XiHanException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用指定异常消息和内部异常初始化异常实例。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <param name="innerException">内部异常。</param>
    public XiHanException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
