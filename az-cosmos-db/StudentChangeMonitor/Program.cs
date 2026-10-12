
using Microsoft.Azure.Cosmos;

string endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")
    ?? throw new Exception("Set COSMOS_ENDPOINT");

string key = Environment.GetEnvironmentVariable("COSMOS_KEY")
    ?? throw new Exception("Set COSMOS_KEY");

using CosmosClient client = new CosmosClient(endpoint, key);

Container monitoredContainer =
    client.GetContainer("LearningDB", "Students");

Container leaseContainer =
    client.GetContainer("LearningDB", "Leases");

// Handle a batch of changed student documents
static Task HandleChangesAsync(
    ChangeFeedProcessorContext context,
    IReadOnlyCollection<Student> changes,
    CancellationToken cancellationToken)
{
    Console.WriteLine(
        $"Received {changes.Count} change(s). " +
        $"Request charge: {context.Headers.RequestCharge} RU");

    foreach (Student student in changes)
    {
        Console.WriteLine(
            $"Student changed: {student.id}, " +
            $"{student.name}, {student.course}, score={student.score}");
    }

    return Task.CompletedTask;
}

// Build the processor
ChangeFeedProcessor processor = monitoredContainer
    .GetChangeFeedProcessorBuilder<Student>(
        processorName: "studentMonitor",
        onChangesDelegate: HandleChangesAsync)
    .WithInstanceName("studentMonitorConsole")
    .WithLeaseContainer(leaseContainer)
    .Build();

// Start listening
await processor.StartAsync();

Console.WriteLine("Listening for student changes...");
Console.WriteLine("Press ENTER to stop.");

Console.ReadLine();

await processor.StopAsync();

public record Student(
    string id,
    string course,
    string name,
    int score);
