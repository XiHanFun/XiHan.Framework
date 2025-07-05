using Serilog;
using XiHan.Framework.AspNetCore.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Web.Test;

try
{
    var builder = WebApplication.CreateBuilder(args);

    _ = await builder.Services.AddApplicationAsync<XiHanWebTestModule>();

    var app = builder.Build();

    await app.InitializeApplicationAsync();

    var totalSize = 100000000;
    var progress = new Progress<long>(downloaded =>
    {
        var percent = totalSize > 0 ? downloaded * 100 / totalSize : 0;
        Console.WriteLine($"下载进度：{percent}% ({downloaded}/{totalSize} bytes)");
    });

    var fileName = "normal2ssbump.exe";
    var savePath = Path.Combine("C:\\Users\\zhaifanhua\\Downloads", fileName);
    var url = "https://onmun.s3.ap-east-1.amazonaws.com/normal2ssbump.exe";
    var response = await url.DownloadAsync(savePath, progress);

    await app.RunAsync();

    Log.Information("应用启动");
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用关闭");
}
finally
{
    Log.CloseAndFlush();
}
