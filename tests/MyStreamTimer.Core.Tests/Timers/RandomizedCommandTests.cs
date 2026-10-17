using System.Collections.Concurrent;
using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using Xunit.Abstractions;

namespace MyStreamTimer.Core.Tests.Timers;

public sealed class RandomizedCommandTests(ITestOutputHelper output)
{
    const int OperationsPerSeed = 25_000;
    static readonly double[] BoundaryValues =
    [
        double.NaN, double.NegativeInfinity, double.PositiveInfinity, double.MinValue, double.MaxValue,
        float.MinValue, float.MaxValue, -1, 0, double.Epsilon, 1.0 / 60, 0.5, 1, 60, 1000,
    ];
    static readonly string[] Queries =
    [
        "mins", "secs", "addmins", "addsecs", "subtractmins", "subtractsecs", "to",
    ];
    static readonly string[] Values =
    [
        "", "0", "-1", "1", "30", "0.1", "1e100", "-1e100", "NaN", "Infinity", "1,5",
        "23:59", "00:00", "%00", "%7B0%7D", new('9', 256),
    ];

    [Theory]
    [InlineData(26200)]
    [InlineData(30000)]
    [InlineData(31000)]
    [InlineData(63801)]
    public async Task Seeded_command_sequences_do_not_throw_or_fault(int seed)
    {
        var random = new Random(seed);
        var faults = new ConcurrentQueue<Exception>();
        var scenarios = TimerKindExtensions.All.Select(kind => new Scenario(kind, faults)).ToArray();
        var recent = new Queue<string>();
        var statuses = new int[3];
        var step = 0;
        try
        {
            for (; step < OperationsPerSeed; step++)
            {
                var scenario = scenarios[random.Next(scenarios.Length)];
                var engine = scenario.Engine;
                var value = random.Next(2) == 0
                    ? BoundaryValues[random.Next(BoundaryValues.Length)]
                    : random.Next(-1000, 1001) / 60.0;
                var action = random.Next(11);
                var description = $"step={step}; timer={engine.Kind}; operation={action}; value={value:R}";
                recent.Enqueue(description);
                if (recent.Count > 20)
                    recent.Dequeue();

                CommandResult? result = null;
                switch (action)
                {
                    case 0:
                    case 1:
                        var host = random.Next(8) == 0 ? "unknown" : engine.Kind.Id();
                        var query = random.Next(3) == 0
                            ? new[] { "pause", "resume", "reset", "stop", "topofhour", "start", "unknown" }[random.Next(7)]
                            : $"{Queries[random.Next(Queries.Length)]}={Values[random.Next(Values.Length)]}";
                        result = engine.Apply(UrlCommandParser.Parse($"mystreamtimer://{host}/?{query}", scenario.Clock.Now));
                        break;
                    case 2:
                        result = engine.Apply(new((CommandAction)random.Next(10), (float)value, engine.Kind.Id()));
                        break;
                    case 3:
                        result = engine.AddMinutes(value);
                        break;
                    case 4:
                        engine.StartStop();
                        break;
                    case 5:
                        engine.PauseResume();
                        break;
                    case 6:
                        engine.Reset();
                        break;
                    case 7:
                        engine.Stop();
                        break;
                    case 8:
                        result = engine.StartAtBoot((float)value);
                        break;
                    case 9:
                        if (engine.State == TimerState.Idle)
                        {
                            scenario.Settings.Minutes = random.Next(0, 1001);
                            scenario.Settings.Seconds = random.Next(60);
                            scenario.Settings.OutputStyle = random.Next(4);
                            scenario.Settings.UseMinutes = random.Next(2) == 0;
                            scenario.Settings.FinishAtTime = TimeSpan.FromMinutes(random.Next(1440));
                        }
                        break;
                    case 10:
                        var url = UrlCommandParser.Build(engine.Kind, (CommandAction)random.Next(8), value);
                        result = engine.Apply(UrlCommandParser.Parse(url, scenario.Clock.Now));
                        break;
                }
                if (result is { } outcome)
                {
                    Assert.True(Enum.IsDefined(outcome.Status), description);
                    if (!outcome.Applied)
                        Assert.False(string.IsNullOrWhiteSpace(outcome.Message), description);
                    statuses[(int)outcome.Status]++;
                }
                Assert.True(Enum.IsDefined(engine.State), description);
                if (step % 128 == 0)
                {
                    scenario.Clock.Advance(TimeSpan.FromSeconds(random.Next(1, 121)));
                    await Task.Yield();
                }
                Assert.True(faults.IsEmpty, $"Background timer failure at {description}: {string.Join("; ", faults)}");
            }

            foreach (var scenario in scenarios)
                scenario.Engine.Stop();
            await Task.Delay(50);
            foreach (var scenario in scenarios)
            {
                Assert.Equal(TimerState.Idle, scenario.Engine.State);
                Assert.False(scenario.Platform.HasRunningTimers);
            }
            Assert.Empty(faults);
            Assert.All(statuses, count => Assert.True(count > 0));
            output.WriteLine($"seed={seed}; operations={OperationsPerSeed}; applied={statuses[0]}; ignored={statuses[1]}; rejected={statuses[2]}; backgroundFaults={faults.Count}");
        }
        catch (Exception exception)
        {
            Assert.Fail($"seed={seed}; step={step}\nRecent operations:\n{string.Join("\n", recent)}\n{exception}");
        }
        finally
        {
            foreach (var scenario in scenarios)
                scenario.Engine.Dispose();
        }
    }

    sealed class Scenario
    {
        public FakeClock Clock { get; } = new();
        public TrackingPlatform Platform { get; } = new();
        public TimerSettings Settings { get; }
        public TimerEngine Engine { get; }

        public Scenario(TimerKind kind, ConcurrentQueue<Exception> faults)
        {
            var store = new InMemorySettingsStore();
            var global = new GlobalSettings(store, @"C:\randomized-test") { IsGold = true };
            Settings = new TimerSettings(store, kind);
            Engine = new(Settings, global, new ProEntitlement(global), new FakeFiles(), Platform, Clock);
            Engine.Faulted += (_, exception) => faults.Enqueue(exception);
        }
    }

    sealed class TrackingPlatform : ITimerPlatform
    {
        readonly ConcurrentDictionary<string, byte> active = new();
        public bool HasRunningTimers => !active.IsEmpty;
        public void StartActivity(string id) => active[id] = 0;
        public void StopActivity(string id) => active.TryRemove(id, out _);
        public Task BeepAsync(EndSoundSelection sound) => Task.CompletedTask;
    }
}
