namespace Jsonsui.Core.Tests.TestSupport;

internal static class TestDataPaths
{
    public static string TestDataDirectory => Path.Combine(AppContext.BaseDirectory, "TestData");

    public static string File(string fileName) => Path.Combine(TestDataDirectory, fileName);
}
