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

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set OPENAI_API_KEY before running this sample.");
    return 2;
}

const int customerIdColumn = 11;
const int customerNameColumn = 12;
const int customerTenantColumn = 13;
var customer = new EntityId(1);
var id = new FieldId(1);
var name = new FieldId(2);
var tenantId = new FieldId(3);

var model = new SemanticModelBuilder()
    .Entity(customer, "Customer", e => e
        .Identity(id, "Id")
        .Field(name, "Name", typeof(string))
        .Field(tenantId, "TenantId", typeof(int)))
    .Build();

var metadata = new MetadataRegistry();
metadata.Register(new EntityMetadata(
    customer,
    "Customer",
    [
        new ColumnMetadata(new ColumnId(customerIdColumn), "Id"),
        new ColumnMetadata(new ColumnId(customerNameColumn), "Name"),
        new ColumnMetadata(new ColumnId(customerTenantColumn), "TenantId")
    ],
    Fields:
    [
        new FieldMetadata(id, "Id", typeof(int), new ColumnReference(customer, new ColumnId(customerIdColumn))),
        new FieldMetadata(name, "Name", typeof(string), new ColumnReference(customer, new ColumnId(customerNameColumn))),
        new FieldMetadata(tenantId, "TenantId", typeof(int), new ColumnReference(customer, new ColumnId(customerTenantColumn)))
    ],
    PrimaryKey: new ColumnReference(customer, new ColumnId(customerIdColumn))));

var data = new InMemoryDataSet()
    .Add(new InMemoryRow(customer, new Dictionary<FieldId, object?>
    {
        [id] = 1, [name] = "Alice", [tenantId] = 7
    }))
    .Add(new InMemoryRow(customer, new Dictionary<FieldId, object?>
    {
        [id] = 2, [name] = "Bob", [tenantId] = 7
    }))
    .Add(new InMemoryRow(customer, new Dictionary<FieldId, object?>
    {
        [id] = 3, [name] = "Carol", [tenantId] = 9
    }));

var services = new ServiceCollection();
services.AddSingleton<IProviderPlanCompiler>(_ => new InMemoryCompiler(metadata, data));
services.AddSingleton<IExecutionProvider>(_ => new InMemoryExecutionProvider(metadata, data));
services.AddFoundgine(model, new AllowAllSemanticAuthorizationPolicy());

await using var provider = services.BuildServiceProvider();
var foundgine = provider.GetRequiredService<IFoundgine>();

var chatClient = new OpenAIClient(apiKey)
    .GetChatClient(Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini")
    .AsIChatClient();

var toolset = new FoundgineAiToolset(
    foundgine,
    contextFactory: () => new ExecutionContext());

var agent = new FoundgineAiAgent(chatClient, toolset);
var prompt = args.Length == 0
    ? "List the customers and their names."
    : string.Join(' ', args);

var response = await agent.RunAsync(prompt);
Console.WriteLine(response.Text);
