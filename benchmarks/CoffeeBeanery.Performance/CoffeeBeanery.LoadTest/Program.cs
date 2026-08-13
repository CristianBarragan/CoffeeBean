using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string query = """
query Top50Customers {
  customer(first: 50) {
    id
    customerKey
    firstName
    lastName
    fullName
    customerBankingRelationship {
      id
      customerBankingRelationshipKey
      contract {
        id
        contractKey
        amount
        transaction {
          id
          transactionKey
          amount
          balance
        }
      }
    }
  }
}
""";

const string mutation = """
mutation CreateCustomer($input: CreateCustomerInput!) {
  createCustomer(input: $input) {
    id
    customerKey
    firstName
    lastName
    fullName
    customerBankingRelationship {
      id
      customerBankingRelationshipKey
      contract {
        id
        contractKey
        amount
        transaction {
          id
          transactionKey
          amount
          balance
        }
      }
    }
  }
}
""";

const string upsertSelectMutation = """
mutation UpsertCustomer($input: CreateCustomerInput!, $conflict: [String!]) {
  upsertCustomer(input: $input, onConflict: $conflict) {
    id
    customerKey
    firstName
    lastName
    fullName
  }
}
""";

var duration = GetInt("BENCHMARK_DURATION_SECONDS", 10);
var warmup = GetInt("BENCHMARK_WARMUP_SECONDS", 3);
var requestTimeoutSeconds = GetInt("BENCHMARK_REQUEST_TIMEOUT_SECONDS", 5);
var readinessTimeoutSeconds = GetInt("BENCHMARK_READINESS_TIMEOUT_SECONDS", 180);
var drainTimeoutSeconds = GetInt("BENCHMARK_RESET_TIMEOUT_SECONDS", 180);
var concurrency = (Environment.GetEnvironmentVariable("BENCHMARK_CONCURRENCY") ?? "1,8,32")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(int.Parse)
    .Where(x => x > 0)
    .Distinct()
    .ToArray();

if (concurrency.Length == 0)
    throw new InvalidOperationException("BENCHMARK_CONCURRENCY must contain at least one positive integer.");

var batchSizes = (Environment.GetEnvironmentVariable("BENCHMARK_BATCH_SIZES") ?? "1,10,50")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(int.Parse)
    .Where(x => x > 0)
    .Distinct()
    .ToArray();

if (batchSizes.Length == 0)
    throw new InvalidOperationException("BENCHMARK_BATCH_SIZES must contain at least one positive integer.");

var dockerContainer = Environment.GetEnvironmentVariable("BENCHMARK_DOCKER_CONTAINER");

var reportDirectory =
    Environment.GetEnvironmentVariable("BENCHMARK_REPORT_DIRECTORY") ?? "/reports";

Directory.CreateDirectory(reportDirectory);

var targetName = GetRequiredEnvironmentVariable("BENCHMARK_TARGET_NAME");
var targetUrl = GetRequiredEnvironmentVariable("BENCHMARK_TARGET_URL");

var targets = new[]
{
    new Target(targetName, targetUrl)
};

using var client = new HttpClient
{
    // Request cancellation is controlled explicitly below.
    Timeout = Timeout.InfiniteTimeSpan
};

client.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Foundgine-CoffeeBeanery-Benchmark/3.0");

Console.WriteLine("==============================================");
Console.WriteLine(" CoffeeBeanery / Single API Benchmark");
Console.WriteLine("==============================================");
Console.WriteLine($"Warm-up:              {warmup}s");
Console.WriteLine($"Measurement:          {duration}s");
Console.WriteLine($"Request timeout:      {requestTimeoutSeconds}s");
Console.WriteLine($"Readiness timeout:    {readinessTimeoutSeconds}s");
Console.WriteLine($"Drain timeout:        {drainTimeoutSeconds}s");
Console.WriteLine($"Concurrency:          {string.Join(", ", concurrency)}");
Console.WriteLine($"Batch sizes:           {string.Join(", ", batchSizes)}");
if (!string.IsNullOrWhiteSpace(dockerContainer))
    Console.WriteLine($"Docker metrics:        {dockerContainer}");
Console.WriteLine();

await WaitForTargetsAsync(targets, client, readinessTimeoutSeconds);

var results = new List<BenchmarkResult>();

foreach (var operation in new[]
         {
             BenchmarkOperation.QueryTop50,
             BenchmarkOperation.MutationWholeGraph,
             BenchmarkOperation.UpsertSelect
         })
{
    var operationBatchSizes = operation switch
    {
        BenchmarkOperation.MutationWholeGraph => batchSizes,
        BenchmarkOperation.UpsertSelect => batchSizes,
        _ => [1]
    };

    Console.WriteLine($"== {BenchmarkOperationExtensions.DisplayName(operation)} ==");

    foreach (var batchSize in operationBatchSizes)
    {
        if (operation is BenchmarkOperation.MutationWholeGraph or BenchmarkOperation.UpsertSelect)
            Console.WriteLine($"  Batch size={batchSize}");

        foreach (var target in targets)
        {
            foreach (var c in concurrency)
            {
                Console.WriteLine(
                    $"  {target.Name} C={c} batch={batchSize}: warm-up...");

                var warmupResult =
                    await Run(
                        target,
                        operation,
                        c,
                        batchSize,
                        warmup,
                        requestTimeoutSeconds,
                        drainTimeoutSeconds,
                        client,
                        metricsEnabled: false);

                Console.WriteLine(
                    $"    warm-up completed={warmupResult.Requests}, " +
                    $"errors={warmupResult.Errors}, " +
                    $"timeouts={warmupResult.Timeouts}");

                if (warmupResult.Errors != 0 ||
                    warmupResult.Timeouts != 0)
                {
                    Console.WriteLine(
                        $"    PREFLIGHT WARNING: {DescribeFirstError(warmupResult)}");
                }

                if (warmupResult.Requests == 0)
                {
                    Console.WriteLine("    PREFLIGHT FAILED: zero completed requests.");

                    results.Add(
                        BenchmarkResult.Failed(
                            operation,
                            target,
                            c,
                            batchSize,
                            1));

                    continue;
                }

                Console.WriteLine(
                    $"  {target.Name} C={c} batch={batchSize}: measuring...");

                var result =
                    await Run(
                        target,
                        operation,
                        c,
                        batchSize,
                        duration,
                        requestTimeoutSeconds,
                        drainTimeoutSeconds,
                        client,
                        metricsEnabled: !string.IsNullOrWhiteSpace(dockerContainer));

                var benchmark = new BenchmarkResult(
                    operation,
                    target.Name,
                    c,
                    batchSize,
                    result.RequestsPerSecond,
                    result.RequestsPerSecond * batchSize,
                    Percentile(result.Latencies, .50),
                    Percentile(result.Latencies, .95),
                    Percentile(result.Latencies, .99),
                    result.Errors + result.Timeouts,
                    result.Metrics?.AverageCpuPercent ?? 0,
                    result.Metrics?.MaxCpuPercent ?? 0,
                    result.Metrics?.AverageMemoryMb ?? 0,
                    result.Metrics?.MaxMemoryMb ?? 0,
                    result.Metrics?.EndMemoryMb ?? 0,
                    result.Requests > 0 && result.Errors == 0 && result.Timeouts == 0);

                results.Add(benchmark);

                Console.WriteLine(
                    $"    RPS={benchmark.RequestsPerSecond:F2} " +
                    $"logical/s={benchmark.LogicalOperationsPerSecond:F2} " +
                    $"p50={benchmark.P50Ms:F1}ms " +
                    $"p95={benchmark.P95Ms:F1}ms " +
                    $"p99={benchmark.P99Ms:F1}ms " +
                    $"CPU avg/max={benchmark.AverageCpuPercent:F1}%/{benchmark.MaxCpuPercent:F1}% " +
                    $"MEM avg/max/end={benchmark.AverageMemoryMb:F1}/{benchmark.MaxMemoryMb:F1}/{benchmark.EndMemoryMb:F1}MB " +
                    $"completed={result.Requests} " +
                    $"errors={result.Errors} " +
                    $"timeouts={result.Timeouts}");

                if (result.Errors != 0 || result.Timeouts != 0)
                    Console.WriteLine($"    First error: {DescribeFirstError(result)}");
            }

            Console.WriteLine();
        }
    }
}

var timestamp =
    DateTimeOffset.UtcNow.ToString(
        "yyyyMMdd-HHmmss",
        CultureInfo.InvariantCulture);

var report = new BenchmarkReport(
    DateTimeOffset.UtcNow,
    warmup,
    duration,
    requestTimeoutSeconds,
    readinessTimeoutSeconds,
    drainTimeoutSeconds,
    concurrency,
    results);

var safeTargetName = string.Concat(
    targetName.Select(c => char.IsLetterOrDigit(c) ? c : '-'));

var jsonPath =
    Path.Combine(
        reportDirectory,
        $"benchmark-{safeTargetName}-{timestamp}.json");

var csvPath =
    Path.Combine(
        reportDirectory,
        $"benchmark-{safeTargetName}-{timestamp}.csv");

var mdPath =
    Path.Combine(
        reportDirectory,
        $"benchmark-{safeTargetName}-{timestamp}.md");

await File.WriteAllTextAsync(
    jsonPath,
    JsonSerializer.Serialize(
        report,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }));

var csv = new StringBuilder();

csv.AppendLine(
    "operation,target,concurrency,batch_size,requests_per_second,logical_operations_per_second," +
    "p50_ms,p95_ms,p99_ms,errors,avg_cpu_percent,max_cpu_percent," +
    "avg_memory_mb,max_memory_mb,end_memory_mb,successful");

foreach (var r in results)
{
    csv.AppendLine(
        string.Join(',',
            Csv(BenchmarkOperationExtensions.DisplayName(r.Operation)),
            Csv(r.Target),
            r.Concurrency,
            r.BatchSize,
            Number(r.RequestsPerSecond),
            Number(r.LogicalOperationsPerSecond),
            Number(r.P50Ms),
            Number(r.P95Ms),
            Number(r.P99Ms),
            r.Errors,
            Number(r.AverageCpuPercent),
            Number(r.MaxCpuPercent),
            Number(r.AverageMemoryMb),
            Number(r.MaxMemoryMb),
            Number(r.EndMemoryMb),
            r.Successful));
}

await File.WriteAllTextAsync(csvPath, csv.ToString());
await File.WriteAllTextAsync(mdPath, BuildMarkdownReport(report));

Console.WriteLine("==============================================");
Console.WriteLine(" Benchmark completed");
Console.WriteLine("==============================================");
Console.WriteLine($"JSON report: {jsonPath}");
Console.WriteLine($"CSV report:  {csvPath}");
Console.WriteLine($"Markdown:    {mdPath}");

static async Task WaitForTargetsAsync(
    IReadOnlyList<Target> targets,
    HttpClient client,
    int readinessTimeoutSeconds)
{
    foreach (var target in targets)
    {
        var health = ToHealthUrl(target.Url);
        var deadline = Stopwatch.StartNew();
        var ready = false;

        while (deadline.Elapsed < TimeSpan.FromSeconds(readinessTimeoutSeconds))
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var response = await client.GetAsync(health, timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ready: {target.Name}");
                    ready = true;
                    break;
                }
            }
            catch
            {
                // Target is still starting.
            }

            await Task.Delay(1000);
        }

        if (!ready)
        {
            throw new InvalidOperationException(
                $"Target did not become ready within {readinessTimeoutSeconds}s: " +
                $"{target.Name} ({health})");
        }
    }

    Console.WriteLine();
}

static async Task<RunResult> Run(
    Target target,
    BenchmarkOperation operation,
    int concurrency,
    int batchSize,
    int seconds,
    int requestTimeoutSeconds,
    int drainTimeoutSeconds,
    HttpClient client,
    bool metricsEnabled)
{
    var latencies = new ConcurrentBag<double>();
    var errors = 0;
    var timeouts = 0;
    var requests = 0L;
    string? firstError = null;
    var metricsSampler = metricsEnabled
        ? new DockerMetricsSampler(
            Environment.GetEnvironmentVariable("BENCHMARK_DOCKER_CONTAINER")!)
        : null;

    // IMPORTANT:
    // Do not cancel worker tasks at the phase boundary.
    // The old implementation used a CancellationToken to stop the phase,
    // which created a race where workers could observe cancellation before
    // starting their first request. That produced:
    //
    //   completed=0, errors=0, timeouts=0, cancelled=N
    //
    // The benchmark phase is now controlled only by a deadline. Workers stop
    // STARTING new requests after the deadline, while any request already in
    // flight is allowed to finish (or hit its request timeout).
    var deadline = Stopwatch.StartNew();

    if (metricsSampler is not null)
        metricsSampler.Start();

    var workers = Enumerable.Range(0, concurrency)
        .Select(_ => WorkerAsync())
        .ToArray();

    // Wait until every worker has stopped naturally.
    // There is deliberately no phase cancellation here.
    var allWorkers = Task.WhenAll(workers);

    var completed = await Task.WhenAny(
        allWorkers,
        Task.Delay(TimeSpan.FromSeconds(
            Math.Max(
                seconds + requestTimeoutSeconds + 5,
                drainTimeoutSeconds))));

    if (completed != allWorkers)
    {
        // This should only happen if the entire worker group has exceeded the
        // configured safety drain period. Do not cancel HTTP requests here:
        // cancellation would corrupt the benchmark accounting.
        Console.WriteLine(
            $"    WARNING: {target.Name} C={concurrency} " +
            $"did not drain within {drainTimeoutSeconds}s.");
    }

    try
    {
        await allWorkers;
    }
    catch
    {
        // WorkerAsync captures request-level failures.
    }

    DockerMetrics? metrics = null;
    if (metricsSampler is not null)
        metrics = await metricsSampler.StopAsync();

    return new RunResult(
        requests,
        errors,
        timeouts,
        0,
        latencies.ToArray(),
        seconds,
        firstError,
        0,
        metrics);

    async Task WorkerAsync()
    {
        var batchKeys = operation == BenchmarkOperation.UpsertSelect
            ? Enumerable.Range(0, batchSize)
                .Select(_ => Guid.NewGuid())
                .ToArray()
            : [];

        while (deadline.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            for (var batchIndex = 0;
                 batchIndex < (operation is BenchmarkOperation.MutationWholeGraph or BenchmarkOperation.UpsertSelect ? batchSize : 1);
                 batchIndex++)
            {
                if (deadline.Elapsed >= TimeSpan.FromSeconds(seconds))
                    break;

                var body = operation switch
                {
                    BenchmarkOperation.QueryTop50 =>
                        JsonSerializer.Serialize(new { query }),

                    BenchmarkOperation.MutationWholeGraph =>
                        JsonSerializer.Serialize(new
                        {
                            query = mutation,
                            operationName = "CreateCustomer",
                            variables = CreateMutationVariables()
                        }),

                    BenchmarkOperation.UpsertSelect =>
                        JsonSerializer.Serialize(new
                        {
                            query = upsertSelectMutation,
                            operationName = "UpsertCustomer",
                            variables = new
                            {
                                input = new
                                {
                                    customerKey = batchKeys[batchIndex],
                                    firstName = "Benchmark",
                                    lastName = "Upsert",
                                    fullName = "Benchmark Upsert"
                                },
                                conflict = new[] { "customerKey" }
                            }
                        }),

                    _ => throw new ArgumentOutOfRangeException()
                };

                using var content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json");

            using var requestCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(requestTimeoutSeconds));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var response = await client.PostAsync(
                    target.Url,
                    content,
                    requestCts.Token);

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                stopwatch.Stop();

                Interlocked.Increment(ref requests);
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);

                if (!response.IsSuccessStatusCode ||
                    responseBody.Contains(
                        "\"errors\"",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref errors);

                    Interlocked.CompareExchange(
                        ref firstError,
                        responseBody,
                        null);
                }
            }
            catch (OperationCanceledException)
                when (requestCts.IsCancellationRequested)
            {
                stopwatch.Stop();

                Interlocked.Increment(ref timeouts);

                Interlocked.CompareExchange(
                    ref firstError,
                    $"request timeout after {requestTimeoutSeconds}s",
                    null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                Interlocked.Increment(ref errors);

                Interlocked.CompareExchange(
                    ref firstError,
                    ex.ToString(),
                    null);
            }

                // The deadline is checked again at the top of the loop.
                // Therefore an in-flight request that finishes after the phase
                // boundary is counted, but no new request is started.
            }
        }
    }
}

static string ToHealthUrl(string url)
{
    if (url.EndsWith("/graphql/cold", StringComparison.OrdinalIgnoreCase) ||
        url.EndsWith("/graphql/warm", StringComparison.OrdinalIgnoreCase))
    {
        return url[..url.LastIndexOf("/graphql", StringComparison.OrdinalIgnoreCase)] + "/health";
    }

    if (url.EndsWith("/graphql", StringComparison.OrdinalIgnoreCase))
        return url[..^"/graphql".Length] + "/health";

    return url.TrimEnd('/') + "/health";
}

static string GetRequiredEnvironmentVariable(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"Required environment variable '{name}' is not set.");

static object CreateMutationVariables()
{
    var customerKey = Guid.NewGuid();
    var relationshipKey = Guid.NewGuid();
    var contractKey = Guid.NewGuid();

    return new
    {
        input = new
        {
            customerKey,
            firstName = "Benchmark",
            lastName = "Customer",
            fullName = "Benchmark Customer",
            customerBankingRelationship = new[]
            {
                new
                {
                    customerBankingRelationshipKey = relationshipKey,
                    contract = new[]
                    {
                        new
                        {
                            contractKey,
                            amount = 1000.50m,
                            transaction = new[]
                            {
                                new
                                {
                                    transactionKey = Guid.NewGuid(),
                                    amount = 100m,
                                    balance = 1200m
                                },
                                new
                                {
                                    transactionKey = Guid.NewGuid(),
                                    amount = 125m,
                                    balance = 1075m
                                }
                            }
                        }
                    }
                }
            }
        }
    };
}

static double Percentile(
    double[] values,
    double percentile)
{
    if (values.Length == 0)
        return 0;

    Array.Sort(values);

    var index =
        (int)Math.Ceiling(
            percentile * values.Length) - 1;

    return values[
        Math.Clamp(
            index,
            0,
            values.Length - 1)];
}

static int GetInt(
    string name,
    int fallback) =>
    int.TryParse(
        Environment.GetEnvironmentVariable(name),
        out var value) &&
    value > 0
        ? value
        : fallback;

static string Csv(string value) =>
    $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

static string Number(double value) =>
    double.IsNaN(value) ||
    double.IsInfinity(value)
        ? ""
        : value.ToString(
            "F2",
            CultureInfo.InvariantCulture);

static string DescribeFirstError(RunResult result) =>
    result.FirstError ??
    (result.Timeouts > 0
        ? "request timeout"
        : result.Errors > 0
            ? "request returned an error"
            : "no error");

static string BuildMarkdownReport(
    BenchmarkReport report)
{
    var sb = new StringBuilder();

    sb.AppendLine("# CoffeeBeanery / Three API Performance Benchmark");
    sb.AppendLine();
    sb.AppendLine($"- Generated: `{report.GeneratedAt:O}`");
    sb.AppendLine($"- Warm-up: `{report.WarmupSeconds}s`");
    sb.AppendLine($"- Measurement: `{report.DurationSeconds}s`");
    sb.AppendLine($"- Request timeout: `{report.RequestTimeoutSeconds}s`");
    sb.AppendLine($"- Readiness timeout: `{report.ReadinessTimeoutSeconds}s`");
    sb.AppendLine($"- Drain timeout: `{report.DrainTimeoutSeconds}s`");
    sb.AppendLine($"- Concurrency: `{string.Join(", ", report.Concurrency)}`");
    sb.AppendLine();
    sb.AppendLine("## Results");
    sb.AppendLine();
    sb.AppendLine("| Operation | Target | Concurrency | Batch | RPS | logical/s | p50 | p95 | p99 | CPU avg/max | MEM avg/max/end MB | Errors |");
    sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

    foreach (var r in report.Results)
    {
        sb.AppendLine(
            $"| {BenchmarkOperationExtensions.DisplayName(r.Operation)} | " +
            $"{r.Target} | " +
            $"{r.Concurrency} | " +
            $"{r.BatchSize} | " +
            $"{Number(r.RequestsPerSecond)} | " +
            $"{Number(r.LogicalOperationsPerSecond)} | " +
            $"{Number(r.P50Ms)} ms | " +
            $"{Number(r.P95Ms)} ms | " +
            $"{Number(r.P99Ms)} ms | " +
            $"{Number(r.AverageCpuPercent)}% / {Number(r.MaxCpuPercent)}% | " +
            $"{Number(r.AverageMemoryMb)} / {Number(r.MaxMemoryMb)} / {Number(r.EndMemoryMb)} | " +
            $"{r.Errors} |");
    }

    sb.AppendLine();
    sb.AppendLine("## Workloads");
    sb.AppendLine();
    sb.AppendLine(
        "**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, " +
        "one Contract and two Transactions in one GraphQL mutation. " +
        "The child foreign keys are resolved from the parent operation results by Foundgine. " +
        "For batch sizes 1, 10 and 50, each worker executes that many independent whole-graph " +
        "GraphQL mutations per benchmark batch. `logical/s` is therefore `RPS * batch size`; " +
        "this is client-side batching, not an HTTP GraphQL batch protocol.");
    sb.AppendLine();
    sb.AppendLine(
        "**QueryTop50** selects the first 50 customers and traverses " +
        "Customer -> CustomerBankingRelationship -> Contract -> Transaction.");
    sb.AppendLine();
    sb.AppendLine(
        "**GraphQLUpsertSelect** performs a normal GraphQL `upsertCustomer` operation " +
        $"with `onConflict: [\"customerKey\"] and returns the selected customer fields. " +
        "For batch sizes 1, 10 and 50, each worker executes that many logical GraphQL " +
        "operations per benchmark batch. `logical/s` is therefore `RPS * batch size`; " +
        "this is client-side batching, not an HTTP GraphQL batch protocol.");
    sb.AppendLine();
    sb.AppendLine(
        "Docker CPU and memory are sampled from the target container during each " +
        "measurement phase. CPU is Docker's container CPU percentage; memory is the " +
        "container working-set reported by `docker stats`.");
    sb.AppendLine();
    sb.AppendLine(
        "The Foundgine warm cache applies to the provider execution plan for the " +
        "read/query workload. Mutation plans are compiled per request because their " +
        "parameter values are intentionally dynamic.");
    sb.AppendLine();
    sb.AppendLine(
        "Each warm-up and measurement phase has a hard wall-clock boundary. " +
        "At phase expiry, new requests stop immediately and in-flight HTTP requests " +
        "are cancelled. Cancelled requests at the phase boundary are not counted as " +
        "errors or measured samples.");
    sb.AppendLine();
    sb.AppendLine(
        "Requests that exceed the explicit request timeout are counted as timeouts. " +
        "The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.");

    return sb.ToString();
}


sealed class DockerMetricsSampler
{
    private readonly string _container;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentBag<DockerMetricSample> _samples = new();
    private Task? _task;

    public DockerMetricsSampler(string container) =>
        _container = container ?? throw new ArgumentNullException(nameof(container));

    public void Start() => _task = Task.Run(SampleLoopAsync);

    public async Task<DockerMetrics?> StopAsync()
    {
        _cts.Cancel();

        if (_task is not null)
        {
            try { await _task; }
            catch (OperationCanceledException) { }
        }

        var samples = _samples.ToArray();
        if (samples.Length == 0)
            return null;

        DockerMetricSample? finalSample = null;
        try
        {
            finalSample = ReadSample();
        }
        catch
        {
        }

        return new DockerMetrics(
            samples.Average(x => x.CpuPercent),
            samples.Max(x => x.CpuPercent),
            samples.Average(x => x.MemoryMb),
            samples.Max(x => x.MemoryMb),
            finalSample?.MemoryMb ?? samples[^1].MemoryMb);
    }

    private async Task SampleLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var sample = ReadSample();
                if (sample is not null)
                    _samples.Add(sample);
            }
            catch
            {
                // Docker metrics are diagnostic; they must never fail a benchmark.
            }

            try
            {
                await Task.Delay(500, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        try
        {
            var final = ReadSample();
            if (final is not null)
                _samples.Add(final);
        }
        catch
        {
        }
    }

    private DockerMetricSample? ReadSample()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"stats --no-stream --format \"{{{{.CPUPerc}}}}|{{{{.MemUsage}}}}|{{{{.MemPerc}}}}\" {_container}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
            return null;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(3000);

        if (process.ExitCode != 0)
            return null;

        var line = output
            .Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (line is null)
            return null;

        var parts = line.Split('|', 3);
        if (parts.Length < 2)
            return null;

        var cpu = ParsePercent(parts[0]);
        var memory = ParseMemoryMb(parts[1]);

        return new DockerMetricSample(cpu, memory);
    }

    private static double ParsePercent(string value) =>
        double.TryParse(
            value.Trim().TrimEnd('%'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : 0;

    private static double ParseMemoryMb(string value)
    {
        var left = value.Split('/', 2)[0].Trim();
        var match = Regex.Match(
            left,
            @"^(?<value>[0-9]+(?:\.[0-9]+)?)\s*(?<unit>KiB|MiB|GiB|KB|MB|GB|B)$",
            RegexOptions.IgnoreCase);

        if (!match.Success ||
            !double.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number))
            return 0;

        return match.Groups["unit"].Value.ToUpperInvariant() switch
        {
            "B" => number / 1024d / 1024d,
            "KB" => number / 1000d,
            "KIB" => number / 1024d,
            "MB" => number,
            "MIB" => number,
            "GB" => number * 1000d,
            "GIB" => number * 1024d,
            _ => 0
        };
    }
}

record DockerMetricSample(double CpuPercent, double MemoryMb);

record DockerMetrics(
    double AverageCpuPercent,
    double MaxCpuPercent,
    double AverageMemoryMb,
    double MaxMemoryMb,
    double EndMemoryMb);

record Target(
    string Name,
    string Url);

enum BenchmarkOperation
{
    QueryTop50,
    MutationWholeGraph,
    UpsertSelect
}

static class BenchmarkOperationExtensions
{
    public static string DisplayName(
        this BenchmarkOperation operation) =>
        operation switch
        {
            BenchmarkOperation.QueryTop50 =>
                "Query top 50 graph",

            BenchmarkOperation.MutationWholeGraph =>
                "Mutation whole graph",

            BenchmarkOperation.UpsertSelect =>
                "GraphQL upsert + select",

            _ => operation.ToString()
        };
}

record RunResult(
    long Requests,
    int Errors,
    int Timeouts,
    int Cancelled,
    double[] Latencies,
    int DurationSeconds,
    string? FirstError,
    int Drained,
    DockerMetrics? Metrics)
{
    public double RequestsPerSecond =>
        Requests /
        (double)Math.Max(1, DurationSeconds);
}

record BenchmarkResult(
    BenchmarkOperation Operation,
    string Target,
    int Concurrency,
    int BatchSize,
    double RequestsPerSecond,
    double LogicalOperationsPerSecond,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    int Errors,
    double AverageCpuPercent,
    double MaxCpuPercent,
    double AverageMemoryMb,
    double MaxMemoryMb,
    double EndMemoryMb,
    bool Successful)
{
    public static BenchmarkResult Failed(
        BenchmarkOperation operation,
        Target target,
        int concurrency,
        int batchSize,
        int errors) =>
        new(
            operation,
            target.Name,
            concurrency,
            batchSize,
            0,
            0,
            0,
            0,
            0,
            errors,
            0,
            0,
            0,
            0,
            0,
            false);
}

record BenchmarkReport(
    DateTimeOffset GeneratedAt,
    int WarmupSeconds,
    int DurationSeconds,
    int RequestTimeoutSeconds,
    int ReadinessTimeoutSeconds,
    int DrainTimeoutSeconds,
    int[] Concurrency,
    IReadOnlyCollection<BenchmarkResult> Results);
