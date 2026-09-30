namespace XiHanWebApp.Data;

/// <summary>
/// 创建待办事项的输入
/// </summary>
public class CreateTodoItemInput
{
    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// 待办事项输出
/// </summary>
public class TodoItemDto
{
    /// <summary>
    /// 主键
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsDone { get; set; }
}
