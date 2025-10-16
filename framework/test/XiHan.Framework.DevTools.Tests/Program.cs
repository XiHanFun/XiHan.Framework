using XiHan.Framework.DevTools.Tests.CommandLine;

try
{
    await UsageExample.InteractiveModeExample();

    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"程序运行异常: {ex}");
}
