using Foundgine;
using Foundgine.AI;
using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.InMemory;
using Foundgine.Metadata;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set OPENAI_API_KEY before running this sample.");
    return 2;
}

var customer = new EntityId(1);
var id = new FieldId(1);
var name = new FieldId(2);

var model = new SemanticModelBuilder()
    .Entity(customer, "Customer", e => e
        .Identity(id, "Id")
        .Field(name, "Name", typeof(string)))
    .Build();

var metadata = new MetadataRegistry();
metadata.Register(new EntityMetadata(
    customer,
    "Customer",
    [
        new ColumnMetadata(new ColumnId(1), "Id"),
        new ColumnMetadata(new ColumnId(2), "Name")
    ],
    Fields:
    [
        new FieldMetadata(id, "Id", typeof(int), new ColumnReference(customer, new ColumnId(1))),
        new FieldMetadata(name, "Name", typeof(string), new ColumnReference(customer, new ColumnId(2)))
    ],
    PrimaryKey: new ColumnReference(customer, new ColumnId(1))));

var data = new InMemoryDataSet()
    .Add(new InMemoryRow(customer, new Dictionary<FieldId, object?> { [id] = 1, [name] = "Alice" }))
    .Add(new InMemoryRow(customer, new Dictionary<FieldId, object?> { [id] = 2, [name] = "Bob" }));

var services = new ServiceCollection();
services.AddSingleton<IProviderPlanCompiler>(_ => new InMemoryCompiler(metadata, data));
services.AddSingleton<IExecutionProvider>(_ => new InMemoryExecutionProvider(metadata, data));
services.AddFoundgine(model, new AllowAllSemanticAuthorizationPolicy());

await using var provider = services.BuildServiceProvider();
var foundgine = provider.GetRequiredService<IFoundgine>();

var chatClient = new OpenAIClient(apiKey)
    .GetChatClient(Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini")
    .AsIChatClient();

var agent = new FoundgineAiAgent(
    chatClient,
    new FoundgineAiToolset(foundgine, () => new ExecutionContext()));

var prompt = args.Length == 0 ? "List the customers and their names." : string.Join(' ', args);
var response = await agent.RunAsync(prompt);

Console.WriteLine(response.Text);
return 0;
