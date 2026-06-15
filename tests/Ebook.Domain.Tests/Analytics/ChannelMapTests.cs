using Ebook.Domain.Analytics;

namespace Ebook.Domain.Tests.Analytics;

public class ChannelMapTests
{
    [Theory]
    [InlineData("instagram", AnalyticsChannel.Instagram)]
    [InlineData("Instagram", AnalyticsChannel.Instagram)]
    [InlineData("ig", AnalyticsChannel.Instagram)]
    [InlineData("facebook", AnalyticsChannel.Facebook)]
    [InlineData("fb", AnalyticsChannel.Facebook)]
    [InlineData("x", AnalyticsChannel.X)]
    [InlineData("twitter", AnalyticsChannel.X)]
    [InlineData("newsletter", AnalyticsChannel.Direct)]
    [InlineData("", AnalyticsChannel.Direct)]
    [InlineData(null, AnalyticsChannel.Direct)]
    public void From_mapeia_utm_source_para_canal(string? utm, AnalyticsChannel expected)
    {
        Assert.Equal(expected, ChannelMap.From(utm));
    }
}
