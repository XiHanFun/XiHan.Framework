// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Web.Api.DynamicApi.Controllers;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Options;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 动态 API 文件上传的端到端测试
/// </summary>
/// <remarks>
/// 在回环地址上拉起只含一个上传示例服务的 MVC 宿主，以真实 multipart 请求验证生成的动作方法能绑定到文件与随附字段。
/// 本类内的用例共享 <see cref="DynamicApiControllerFactory"/> 的静态缓存，故不并行执行。
/// </remarks>
[Collection("DynamicApiFactory")]
public class DynamicApiFormFileUploadTests : IAsyncLifetime
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private WebApplication? _app;
    private HttpClient? _client;
    private Dictionary<string, string> _routes = [];

    /// <summary>
    /// 启动测试宿主
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        DynamicApiControllerFactory.ClearCache();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var options = new DynamicApiOptions();
        var convention = new DefaultDynamicApiConvention(options);

        builder.Services.AddTransient<UploadSampleAppService>();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                // 只暴露上传示例服务，避免扫到测试程序集里故意制造冲突的其他应用服务
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new SingleTypePart(typeof(UploadSampleAppService)));
                manager.FeatureProviders.Add(new DynamicApiControllerFeatureProvider(convention, options));
            });

        _app = builder.Build();
        _app.MapControllers();
        await _app.StartAsync();

        _routes = _app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Endpoint: endpoint, Action: endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(item => item.Action is not null)
            .ToDictionary(item => item.Action!.ActionName, item => "/" + item.Endpoint.RoutePattern.RawText!.TrimStart('/'), StringComparer.Ordinal);

        _client = new HttpClient
        {
            BaseAddress = new Uri(_app.Urls.First()),
            Timeout = RequestTimeout
        };
    }

    /// <summary>
    /// 停止测试宿主
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        DynamicApiControllerFactory.ClearCache();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 单个文件参数
    /// </summary>
    [Fact]
    public async Task SingleFile_IsBound()
    {
        var body = await PostAsync(nameof(UploadSampleAppService.UploadFileAsync), content =>
        {
            AddFile(content, "file", "a.txt", "aaa");
        });

        Assert.Equal("a.txt:3", body);
    }

    /// <summary>
    /// 多文件集合参数在一次请求内全部绑定
    /// </summary>
    /// <param name="methodName">方法名</param>
    [Theory]
    [InlineData(nameof(UploadSampleAppService.UploadFilesAsync))]
    [InlineData(nameof(UploadSampleAppService.UploadFileArrayAsync))]
    [InlineData(nameof(UploadSampleAppService.UploadFileCollectionAsync))]
    public async Task MultipleFiles_InOneRequest_AreAllBound(string methodName)
    {
        var body = await PostAsync(methodName, content =>
        {
            AddFile(content, "files", "a.txt", "aaa");
            AddFile(content, "files", "b.txt", "bb");
        });

        Assert.Equal("a.txt:3,b.txt:2", body);
    }

    /// <summary>
    /// DTO 内的文件列表与普通字段一起绑定
    /// </summary>
    [Fact]
    public async Task DtoWithFileList_IsBound()
    {
        var body = await PostAsync(nameof(UploadSampleAppService.UploadBatchAsync), content =>
        {
            AddFile(content, "Files", "a.txt", "aaa");
            AddFile(content, "Files", "b.txt", "bb");
            content.Add(new StringContent("note"), "Remark");
        });

        Assert.Equal("note|a.txt:3,b.txt:2", body);
    }

    /// <summary>
    /// 文件参数与复杂类型参数并存时，复杂类型从同一个表单绑定，与参数先后无关
    /// </summary>
    /// <param name="methodName">方法名</param>
    [Theory]
    [InlineData(nameof(UploadSampleAppService.UploadFilesWithOptionsAsync))]
    [InlineData(nameof(UploadSampleAppService.UploadOptionsWithFilesAsync))]
    public async Task FilesWithComplexParameter_BindsComplexFromForm(string methodName)
    {
        var body = await PostAsync(methodName, content =>
        {
            AddFile(content, "files", "a.txt", "aaa");
            content.Add(new StringContent("avatar"), "Category");
        });

        Assert.Equal("avatar|a.txt:3", body);
    }

    /// <summary>
    /// 文件参数与简单元素集合并存时，集合从同一个表单绑定
    /// </summary>
    [Fact]
    public async Task FilesWithSimpleCollection_BindsCollectionFromForm()
    {
        var body = await PostAsync(nameof(UploadSampleAppService.UploadFilesWithTagsAsync), content =>
        {
            AddFile(content, "files", "a.txt", "aaa");
            content.Add(new StringContent("7"), "tagIds");
            content.Add(new StringContent("9"), "tagIds");
        });

        Assert.Equal("7,9|a.txt:3", body);
    }

    /// <summary>
    /// 文件参数与未标注的简单参数并存时，简单参数仍走查询串
    /// </summary>
    [Fact]
    public async Task FilesWithSimpleParameter_BindsSimpleFromQuery()
    {
        var body = await PostAsync(nameof(UploadSampleAppService.UploadFilesWithRemarkAsync), content =>
        {
            AddFile(content, "files", "a.txt", "aaa");
        }, "?remark=note");

        Assert.Equal("note|a.txt:3", body);
    }

    /// <summary>
    /// 文件参数与显式 [FromForm] 的简单参数并存时，简单参数从表单绑定
    /// </summary>
    [Fact]
    public async Task FilesWithExplicitFormSimpleParameter_BindsSimpleFromForm()
    {
        var body = await PostAsync(nameof(UploadSampleAppService.UploadFilesWithFormRemarkAsync), content =>
        {
            AddFile(content, "files", "a.txt", "aaa");
            content.Add(new StringContent("note"), "remark");
        });

        Assert.Equal("note|a.txt:3", body);
    }

    /// <summary>
    /// 元素内含文件的列表按索引绑定
    /// </summary>
    [Fact]
    public async Task ListOfItemsWithFile_IsBound()
    {
        var body = await PostAsync(nameof(UploadSampleAppService.UploadItemsAsync), content =>
        {
            content.Add(new StringContent("first"), "items[0].Name");
            AddFile(content, "items[0].File", "a.txt", "aaa");
            content.Add(new StringContent("second"), "items[1].Name");
            AddFile(content, "items[1].File", "b.txt", "bb");
        });

        Assert.Equal("first=a.txt:3,second=b.txt:2", body);
    }

    /// <summary>
    /// 以 multipart 请求调用指定方法，断言成功并返回响应体
    /// </summary>
    /// <param name="methodName">服务方法名</param>
    /// <param name="fill">填充表单内容</param>
    /// <param name="queryString">附加的查询串，含前导问号</param>
    /// <returns>响应体</returns>
    private async Task<string> PostAsync(string methodName, Action<MultipartFormDataContent> fill, string queryString = "")
    {
        var actionName = methodName.EndsWith("Async", StringComparison.Ordinal) ? methodName[..^5] : methodName;
        Assert.True(_routes.TryGetValue(actionName, out var route), $"未找到动作 '{actionName}'，现有动作：{string.Join(", ", _routes.Keys)}");

        using var content = new MultipartFormDataContent();
        fill(content);

        using var response = await _client!.PostAsync(route + queryString, content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route} 返回 {(int)response.StatusCode}：{body}");
        return body;
    }

    /// <summary>
    /// 向表单追加一个文件
    /// </summary>
    /// <param name="content">表单</param>
    /// <param name="name">字段名</param>
    /// <param name="fileName">文件名</param>
    /// <param name="text">文件内容</param>
    private static void AddFile(MultipartFormDataContent content, string name, string fileName, string text)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, name, fileName);
    }

    /// <summary>
    /// 只提供指定类型的应用部件
    /// </summary>
    /// <param name="type">类型</param>
    private sealed class SingleTypePart(Type type) : ApplicationPart, IApplicationPartTypeProvider
    {
        /// <inheritdoc />
        public override string Name => nameof(DynamicApiFormFileUploadTests);

        /// <inheritdoc />
        public IEnumerable<TypeInfo> Types { get; } = [type.GetTypeInfo()];
    }
}

/// <summary>
/// 文件上传测试用的应用服务
/// </summary>
public class UploadSampleAppService : IApplicationService
{
    /// <summary>
    /// 上传单个文件
    /// </summary>
    /// <param name="file">文件</param>
    /// <returns></returns>
    public Task<string> UploadFileAsync(IFormFile file) => Task.FromResult(Describe([file]));

    /// <summary>
    /// 一次上传多个文件，列表形式
    /// </summary>
    /// <param name="files">文件列表</param>
    /// <returns></returns>
    public Task<string> UploadFilesAsync(List<IFormFile> files) => Task.FromResult(Describe(files));

    /// <summary>
    /// 一次上传多个文件，数组形式
    /// </summary>
    /// <param name="files">文件数组</param>
    /// <returns></returns>
    public Task<string> UploadFileArrayAsync(IFormFile[] files) => Task.FromResult(Describe(files));

    /// <summary>
    /// 一次上传多个文件，表单文件集合形式
    /// </summary>
    /// <param name="files">文件集合</param>
    /// <returns></returns>
    public Task<string> UploadFileCollectionAsync(IFormFileCollection files) => Task.FromResult(Describe(files));

    /// <summary>
    /// 一次上传多个文件，文件列表包在 DTO 内
    /// </summary>
    /// <param name="input">上传输入</param>
    /// <returns></returns>
    public Task<string> UploadBatchAsync(BatchUploadInput input) => Task.FromResult($"{input.Remark}|{Describe(input.Files)}");

    /// <summary>
    /// 一次上传多个文件，另带一个复杂类型参数
    /// </summary>
    /// <param name="files">文件列表</param>
    /// <param name="options">上传选项</param>
    /// <returns></returns>
    public Task<string> UploadFilesWithOptionsAsync(List<IFormFile> files, UploadOptionsInput options) => Task.FromResult($"{options.Category}|{Describe(files)}");

    /// <summary>
    /// 一次上传多个文件，复杂类型参数排在文件之前
    /// </summary>
    /// <param name="options">上传选项</param>
    /// <param name="files">文件列表</param>
    /// <returns></returns>
    public Task<string> UploadOptionsWithFilesAsync(UploadOptionsInput options, List<IFormFile> files) => Task.FromResult($"{options.Category}|{Describe(files)}");

    /// <summary>
    /// 一次上传多个文件，另带一个简单元素列表
    /// </summary>
    /// <param name="files">文件列表</param>
    /// <param name="tagIds">标签主键列表</param>
    /// <returns></returns>
    public Task<string> UploadFilesWithTagsAsync(List<IFormFile> files, List<long> tagIds) => Task.FromResult($"{string.Join(",", tagIds)}|{Describe(files)}");

    /// <summary>
    /// 一次上传多个文件，另带一个未标注的简单参数
    /// </summary>
    /// <param name="files">文件列表</param>
    /// <param name="remark">备注</param>
    /// <returns></returns>
    public Task<string> UploadFilesWithRemarkAsync(List<IFormFile> files, string remark) => Task.FromResult($"{remark}|{Describe(files)}");

    /// <summary>
    /// 一次上传多个文件，另带一个显式标注 [FromForm] 的简单参数
    /// </summary>
    /// <param name="files">文件列表</param>
    /// <param name="remark">备注</param>
    /// <returns></returns>
    public Task<string> UploadFilesWithFormRemarkAsync(List<IFormFile> files, [FromForm] string remark) => Task.FromResult($"{remark}|{Describe(files)}");

    /// <summary>
    /// 一次上传多个条目，每个条目带一个文件
    /// </summary>
    /// <param name="items">条目列表</param>
    /// <returns></returns>
    public Task<string> UploadItemsAsync(List<UploadItemInput> items) => Task.FromResult(string.Join(",", items.Select(item => $"{item.Name}={Describe([item.File])}")));

    private static string Describe(IEnumerable<IFormFile> files) => string.Join(",", files.Select(file => $"{file.FileName}:{file.Length}"));
}

/// <summary>
/// 批量上传输入
/// </summary>
public class BatchUploadInput
{
    /// <summary>
    /// 文件列表
    /// </summary>
    public List<IFormFile> Files { get; set; } = [];

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 上传选项输入
/// </summary>
public class UploadOptionsInput
{
    /// <summary>
    /// 分类
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// 上传条目输入
/// </summary>
public class UploadItemInput
{
    /// <summary>
    /// 名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 文件
    /// </summary>
    public IFormFile File { get; set; } = null!;
}
