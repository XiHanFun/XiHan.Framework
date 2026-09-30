using System.Net;
using System.Net.Http.Json;
using XiHanWebApp.Data;

namespace XiHanWebApp.Tests;

/// <summary>
/// 待办事项接口测试（SQLite）
/// </summary>
public class TodoItemApiTests(WebAppHostFactory factory) : IClassFixture<WebAppHostFactory>
{
    [Fact]
    public async Task Create_ThenList_ReturnsCreatedItem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var title = $"待办-{Guid.NewGuid():N}";

        using var createResponse = await client.PostAsJsonAsync("/api/TodoItem/Create", new CreateTodoItemInput { Title = title }, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.ReadApiResultAsync<TodoItemDto>(cancellationToken);
        Assert.NotNull(created.Data);
        Assert.NotEqual(0, created.Data.Id);
        Assert.Equal(title, created.Data.Title);

        using var listResponse = await client.GetAsync("/api/TodoItem/List", cancellationToken);
        var list = await listResponse.ReadApiResultAsync<List<TodoItemDto>>(cancellationToken);
        Assert.Contains(list.Data ?? [], item => item.Id == created.Data.Id && item.Title == title);
    }

    [Fact]
    public async Task Create_WithBlankTitle_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/TodoItem/Create", new CreateTodoItemInput { Title = "  " }, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
#if (MultiTenancy)

    [Fact]
    public async Task TenantItem_IsInvisibleToPlatform()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var tenantClient = factory.CreateClient();
        tenantClient.DefaultRequestHeaders.Add("X-Tenant-Id", "1");
        using var platformClient = factory.CreateClient();
        var title = $"租户待办-{Guid.NewGuid():N}";

        using var createResponse = await tenantClient.PostAsJsonAsync("/api/TodoItem/Create", new CreateTodoItemInput { Title = title }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        using var tenantList = await tenantClient.GetAsync("/api/TodoItem/List", cancellationToken);
        var tenantItems = await tenantList.ReadApiResultAsync<List<TodoItemDto>>(cancellationToken);
        Assert.Contains(tenantItems.Data ?? [], item => item.Title == title);

        using var platformList = await platformClient.GetAsync("/api/TodoItem/List", cancellationToken);
        var platformItems = await platformList.ReadApiResultAsync<List<TodoItemDto>>(cancellationToken);
        Assert.DoesNotContain(platformItems.Data ?? [], item => item.Title == title);
    }
#endif
}
