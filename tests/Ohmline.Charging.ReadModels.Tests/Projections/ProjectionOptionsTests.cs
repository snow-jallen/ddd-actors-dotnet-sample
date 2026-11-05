using Akka.Configuration;
using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

public sealed class ProjectionOptionsTests
{
    [Fact]
    public void Both_settings_reach_the_read_journal_together()
    {
        var options = new ProjectionOptions
        {
            RefreshInterval = TimeSpan.FromMilliseconds(250),
            QueryDelay = TimeSpan.FromMilliseconds(50),
        };

        var config = ConfigurationFactory.ParseString(options.ToHocon())
            .GetConfig("akka.persistence.query.journal.sql");

        config.GetTimeSpan("refresh-interval").ShouldBe(TimeSpan.FromMilliseconds(250));
        config.GetTimeSpan("journal-sequence-retrieval.query-delay")
            .ShouldBe(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Given_no_setting_then_the_read_journal_polls_every_100_ms()
    {
        var options = new ProjectionOptions();

        options.RefreshInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
        options.QueryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
    }
}
