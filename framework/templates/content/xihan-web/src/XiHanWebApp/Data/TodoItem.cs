using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHanWebApp.Data;

/// <summary>
/// 待办事项实体
/// </summary>
[SugarTable("Demo_TodoItem", "待办事项")]
#if (MultiTenancy)
public class TodoItem : SugarMultiTenantEntity<long>
#else
public class TodoItem : SugarEntity<long>
#endif
{
    /// <summary>
    /// 标题
    /// </summary>
    [SugarColumn(ColumnName = "Title", Length = 200, ColumnDescription = "标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 是否已完成
    /// </summary>
    [SugarColumn(ColumnName = "Is_Done", ColumnDescription = "是否已完成")]
    public bool IsDone { get; set; }
}
