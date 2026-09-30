using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;
using XiHan.Framework.Domain.Repositories;

namespace XiHanWebApp.Data;

/// <summary>
/// 待办事项服务（由动态 API 自动暴露为接口）
/// </summary>
[DynamicApi]
public class TodoItemAppService(IRepositoryBase<TodoItem, long> repository) : ApplicationServiceBase
{
    /// <summary>
    /// 创建待办事项
    /// </summary>
    /// <param name="input">输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建后的待办事项</returns>
    public async Task<TodoItemDto> CreateAsync(CreateTodoItemInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Title);

        var entity = await repository.AddAsync(new TodoItem { Title = input.Title.Trim() }, cancellationToken);
        return ToDto(entity);
    }

    /// <summary>
    /// 获取全部待办事项
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>待办事项列表</returns>
    public async Task<IReadOnlyList<TodoItemDto>> GetListAsync(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return [.. entities.Select(ToDto)];
    }

    private static TodoItemDto ToDto(TodoItem entity)
    {
        return new TodoItemDto
        {
            Id = entity.BasicId,
            Title = entity.Title,
            IsDone = entity.IsDone
        };
    }
}
