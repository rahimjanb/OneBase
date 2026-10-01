using OneBase.Infrastructure.Linko;

namespace OneBase.Sales.Tests;

public class SyncProgressTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static (LinkoSyncProgress Progress, Clock Clock) Started()
    {
        var clock = new Clock(T0);
        var progress = new LinkoSyncProgress(clock);
        progress.Start(LinkoSyncMode.Full);
        progress.Plan([new("users", "Справочники", 1), new("orders", "История", 2), new(null, "Пересчёт отчётов", 1)]);
        return (progress, clock);
    }

    [Fact]
    public void Percent_follows_step_weights_and_rows_inside_the_step()
    {
        var (progress, _) = Started();

        progress.Step("Справочники", "users");
        Assert.Equal((0, 0, 3), (progress.Current!.Percent, progress.Current.StepsDone, progress.Current.StepsTotal));

        progress.Step("История", "orders");
        Assert.Equal(25, progress.Current!.Percent); // users (вес 1 из 4) пройден

        progress.Expect(1000);
        progress.AddRows(500);
        Assert.Equal(50, progress.Current!.Percent); // (1 + 2 × 0,5) / 4
        Assert.Equal(1000, progress.Current.Expected);

        progress.Step("Пересчёт отчётов", null);
        Assert.Equal(75, progress.Current!.Percent);
    }

    [Fact]
    public void Remaining_time_is_estimated_from_elapsed_time_once_enough_is_done()
    {
        var (progress, clock) = Started();
        progress.Step("Справочники", "users");
        clock.Now = T0.AddSeconds(30);
        Assert.Null(progress.Current!.RemainingSeconds); // 0 % — оценивать рано

        progress.Step("История", "orders"); // 25 % за 30 с → осталось ~90 с
        Assert.Equal(90, progress.Current!.RemainingSeconds);
    }

    [Fact]
    public void Steps_outside_the_plan_do_not_move_percent_and_finish_clears()
    {
        var (progress, _) = Started();
        progress.Step("История", "orders");
        progress.Step("Отмена…", null);
        Assert.Equal(25, progress.Current!.Percent);

        progress.Finish();
        Assert.Null(progress.Current);
        Assert.False(progress.IsActive);
    }

    [Fact]
    public void Percent_never_shows_100_before_the_end()
    {
        var (progress, _) = Started();
        progress.Step("Пересчёт отчётов", null);
        progress.Expect(10);
        progress.AddRows(10);
        Assert.Equal(99, progress.Current!.Percent);
    }
}
