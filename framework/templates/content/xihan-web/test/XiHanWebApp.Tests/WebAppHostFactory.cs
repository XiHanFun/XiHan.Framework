using Microsoft.AspNetCore.Mvc.Testing;
#if (Data)
using Microsoft.AspNetCore.Hosting;
#endif

namespace XiHanWebApp.Tests;

/// <summary>
/// 应用测试宿主
/// </summary>
public class WebAppHostFactory : WebApplicationFactory<Program>
{
#if (Data)
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"webapphost-db-test-{Guid.NewGuid():N}.db");

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 每个测试宿主使用独立的临时 SQLite 文件
        builder.UseSetting("XiHan:Data:SqlSugarCore:ConnectionConfigs:0:ConnectionString", $"Data Source={_databasePath};Pooling=False");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        GC.SuppressFinalize(this);

        // 删除临时数据库文件
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
        }
    }
#endif
}
