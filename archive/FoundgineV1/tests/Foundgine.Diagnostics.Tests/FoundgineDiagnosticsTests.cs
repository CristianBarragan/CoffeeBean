using System.Diagnostics;
using System.Diagnostics.Metrics;
using Xunit;

namespace Foundgine.Diagnostics.Tests;

public class FoundgineDiagnosticsTests
{
    [Fact]
    public void ActivitySource_UsesTheSharedSourceName()
    {
        Assert.Equal("Foundgine", FoundgineDiagnostics.SourceName);
        Assert.Equal(FoundgineDiagnostics.SourceName, FoundgineDiagnostics.ActivitySource.Name);
    }

    [Fact]
    public void Meter_UsesTheSharedSourceName()
    {
        Assert.Equal(FoundgineDiagnostics.SourceName, FoundgineDiagnostics.Meter.Name);
    }

    [Fact]
    public void ActivitySource_ProducesActivities_WhenAListenerIsRegistered()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FoundgineDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = FoundgineDiagnostics.ActivitySource.StartActivity("Execute Query");

        Assert.NotNull(activity);
        Assert.Equal("Execute Query", activity!.OperationName);
    }

    [Fact]
    public void ActivitySource_ProducesNoActivity_WithoutAListener()
    {
        // Without a registered listener, StartActivity is a documented no-op --
        // this is the "no cost for a consumer who isn't listening" guarantee
        // called out in FoundgineDiagnostics' own doc comment.
        using var freshSource = new ActivitySource("Foundgine.Tests.Unlistened");

        using var activity = freshSource.StartActivity("whatever");

        Assert.Null(activity);
    }

    [Fact]
    public void Metrics_AreRegisteredUnderExpectedNames()
    {
        var recorded = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == FoundgineDiagnostics.SourceName)
            {
                recorded.Add(instrument.Name);
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();

        // Touching each instrument (even without recording) is enough to
        // trigger InstrumentPublished for a Meter created before the listener
        // started, since MeterListener.Start() replays already-created meters.
        FoundgineDiagnostics.QueriesExecuted.Add(0);
        FoundgineDiagnostics.MutationsExecuted.Add(0);
        FoundgineDiagnostics.PlansReused.Add(0);
        FoundgineDiagnostics.ExecutionDuration.Record(0);
        FoundgineDiagnostics.MaterializationDuration.Record(0);

        Assert.Contains("foundgine.queries.executed", recorded);
        Assert.Contains("foundgine.mutations.executed", recorded);
        Assert.Contains("foundgine.plans.reused", recorded);
        Assert.Contains("foundgine.execution.duration", recorded);
        Assert.Contains("foundgine.materialization.duration", recorded);
    }

    [Fact]
    public void QueriesExecutedCounter_RecordsMeasurements()
    {
        long total = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument == FoundgineDiagnostics.QueriesExecuted)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => total += measurement);
        listener.Start();

        FoundgineDiagnostics.QueriesExecuted.Add(1);
        FoundgineDiagnostics.QueriesExecuted.Add(2);
        listener.RecordObservableInstruments();

        Assert.Equal(3, total);
    }
}
