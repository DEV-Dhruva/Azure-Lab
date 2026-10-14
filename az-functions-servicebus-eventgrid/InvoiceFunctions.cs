
using System.Text.Json;
using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace az_functions_servicebus_eventgrid;

public record InvoiceMessage(
    string InvoiceId,
    string FileName,
    decimal Amount);

public class InvoiceFunctions
{
    private readonly ILogger<InvoiceFunctions> _logger;

    public InvoiceFunctions(ILogger<InvoiceFunctions> logger)
    {
        _logger = logger;
    }

    // 1. Service Bus receives an invoice and publishes an event.
    [Function("ProcessInvoice")]
    public async Task ProcessInvoice(
        [ServiceBusTrigger(
            "invoice-requests",
            Connection = "ServiceBusConnection")] string message)
    {
        var invoice = JsonSerializer.Deserialize<InvoiceMessage>(message)
            ?? throw new InvalidOperationException("Invalid invoice message.");

        _logger.LogInformation(
            "Processing invoice {InvoiceId}", invoice.InvoiceId);

        var endpoint = Environment.GetEnvironmentVariable("EventGridEndpoint")!;
        var key = Environment.GetEnvironmentVariable("EventGridKey")!;

        var publisher = new EventGridPublisherClient(
            new Uri(endpoint), new AzureKeyCredential(key));

        var eventData = new EventGridEvent(
            subject: $"invoices/{invoice.InvoiceId}",
            eventType: "Invoice.Processed",
            dataVersion: "1.0",
            data: invoice);

        await publisher.SendEventAsync(eventData);

        _logger.LogInformation("Invoice processed event published.");
    }

    // 2. Event Grid receives the event and starts a Durable workflow.
    [Function("InvoiceEventHandler")]
    public async Task InvoiceEventHandler(
        [EventGridTrigger] EventGridEvent eventGridEvent,
        [DurableClient] DurableTaskClient durableClient)
    {
        var invoice = eventGridEvent.Data.ToObjectFromJson<InvoiceMessage>()
            ?? throw new InvalidOperationException("Invalid event data.");

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(InvoiceWorkflow), invoice);
    }

    // 3. Durable Function coordinates the workflow.
    [Function(nameof(InvoiceWorkflow))]
    public async Task<string> InvoiceWorkflow(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var invoice = context.GetInput<InvoiceMessage>()!;

        var validation = await context.CallActivityAsync<string>(
            nameof(ValidateInvoice), invoice);

        var approval = await context.CallActivityAsync<string>(
            nameof(ApproveInvoice), invoice);

        return $"{validation}; {approval}";
    }

    // 4. First Durable activity.
    [Function(nameof(ValidateInvoice))]
    public string ValidateInvoice(
        [ActivityTrigger] InvoiceMessage invoice)
    {
        return $"Invoice {invoice.InvoiceId} validated";
    }

    // 5. Second Durable activity.
    [Function(nameof(ApproveInvoice))]
    public string ApproveInvoice(
        [ActivityTrigger] InvoiceMessage invoice)
    {
        return $"Invoice {invoice.InvoiceId} approved (simulated)";
    }
}
