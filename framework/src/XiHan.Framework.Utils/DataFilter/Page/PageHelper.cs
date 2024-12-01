namespace XiHan.Framework.Utils.DataFilter.Page;

//public static class PageHelper
//{
//    /// <summary>
//    /// 对 IQueryable 进行分页、排序和过滤
//    /// </summary>
//    public static PageResponseDto<T> ApplyPaging<T>(IQueryable<T> source, PageQueryDto queryDto)
//    {
//        // 处理查询所有数据的情况
//        if (queryDto.IsQueryAll == true)
//        {
//            var allData = source.ToList();
//            return new PageResponseDto<T>(new PageInfoDto(1, allData.Count), allData);
//        }

//        // 处理选择条件
//        if (queryDto.SelectConditions != null)
//        {
//            foreach (var condition in queryDto.SelectConditions)
//            {
//                source = ApplySelectCondition(source, condition);
//            }
//        }

//        // 处理排序条件
//        if (queryDto.SortConditions != null)
//        {
//            source = ApplySortConditions(source, queryDto.SortConditions);
//        }

//        // 获取分页信息
//        var pageInfo = queryDto.PageInfo ?? new PageInfoDto();
//        _ = source.Count();
//        var pagedData = source
//            .Skip((pageInfo.CurrentIndex - 1) * pageInfo.PageSize)
//            .Take(pageInfo.PageSize)
//            .ToList();

//        return new PageResponseDto<T>(pageInfo, pagedData);
//    }

//    /// <summary>
//    /// 对 IEnumerable 进行分页、排序和过滤
//    /// </summary>
//    public static PageResponseDto<T> ApplyPaging<T>(IEnumerable<T> source, PageQueryDto queryDto)
//    {
//        return ApplyPaging(source.AsQueryable(), queryDto);
//    }

//    private static IQueryable<T> ApplySelectCondition<T>(
//        IQueryable<T> source,
//        SelectConditionDto condition)
//    {
//        // 这里需要实现根据条件动态生成表达式树进行过滤
//        // 由于篇幅限制，具体实现略
//        return source;
//    }

//    private static IQueryable<T> ApplySortConditions<T>(
//        IQueryable<T> source,
//        List<SortConditionDto> sortConditions)
//    {
//        IOrderedQueryable<T>? orderedQuery = null;
//        foreach (var condition in sortConditions)
//        {
//            orderedQuery = orderedQuery == null
//                ? condition.SortDirection == SortDirectionEnum.Asc
//                    ? source.OrderBy(x => EF.Property<object>(x, condition.SortField))
//                    : source.OrderByDescending(x => EF.Property<object>(x, condition.SortField))
//                : condition.SortDirection == SortDirectionEnum.Asc
//                    ? orderedQuery.ThenBy(x => EF.Property<object>(x, condition.SortField))
//                    : orderedQuery.ThenByDescending(x => EF.Property<object>(x, condition.SortField));
//        }
//        return orderedQuery ?? source;
//    }
//}
