# RabbitMQ Integration Guide

> ⚠️ **STATUS: NOT IMPLEMENTED**
>
> This document is a **reference guide for a future integration** — RabbitMQ is
> **not wired into the Monburan codebase** as of 2026-10-16. There is no
> `Monburan.Core.Messaging` namespace, no `IRabbitMQBus` implementation, no
> `RabbitMQ` section in `appsettings.json`, and no `rabbitmq` service in the
> Docker Compose files. A local `RoggoR.RabbitMq` package exists under
> `nuget-packages/`, but it is not referenced by the application.
>
> Keep this guide as the blueprint for when messaging is added (see backlog
> issues #6/#9/#10 in `docs/scrum.md` for related planned work).

## Overview

The Monburan application now includes RabbitMQ integration for messaging. This guide explains how to use the RabbitMQ service in the application.

## Configuration

### Local Development

For local development, RabbitMQ is configured in `appsettings.json`:

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "RequestedHeartbeat": 30,
    "AutomaticRecoveryEnabled": true,
    "NetworkRecoveryInterval": 5000
  }
}
```

### Docker

For Docker deployment, RabbitMQ is configured via environment variables:

```bash
# .env file
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
```

## Usage Examples

### Publishing Messages

```csharp
using Monburan.Core.Messaging;

public class JobCreatedPublisher
{
    private readonly IRabbitMQBus _rabbitMQBus;

    public JobCreatedPublisher(IRabbitMQBus rabbitMQBus)
    {
        _rabbitMQBus = rabbitMQBus;
    }

    public async Task PublishJobCreatedAsync(Guid jobId, string title, CancellationToken cancellationToken = default)
    {
        var message = new JobCreatedMessage
        {
            JobId = jobId,
            Title = title,
            CreatedAt = DateTime.UtcNow
        };

        await _rabbitMQBus.PublishAsync("job.created", message, cancellationToken);
    }
}

public record JobCreatedMessage
{
    public Guid JobId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
```

### Subscribing to Messages

```csharp
using Monburan.Core.Messaging;

public class JobCreatedConsumer
{
    private readonly IRabbitMQBus _rabbitMQBus;
    private readonly ILogger<JobCreatedConsumer> _logger;

    public JobCreatedConsumer(IRabbitMQBus rabbitMQBus, ILogger<JobCreatedConsumer> logger)
    {
        _rabbitMQBus = rabbitMQBus;
        _logger = logger;
    }

    public async Task SubscribeAsync(CancellationToken cancellationToken = default)
    {
        await _rabbitMQBus.SubscribeAsync<JobCreatedMessage>(
            "job.created",
            async message =>
            {
                _logger.LogInformation("Job created: {JobId} - {Title}", message.JobId, message.Title);
                
                // Process the message
                await ProcessJobCreatedAsync(message, cancellationToken);
            },
            cancellationToken);
    }

    private async Task ProcessJobCreatedAsync(JobCreatedMessage message, CancellationToken cancellationToken)
    {
        // Implement your business logic here
        // For example: send notifications, update analytics, etc.
        await Task.CompletedTask;
    }
}
```

### Registering Consumers in Program.cs

```csharp
// Register consumer as singleton or hosted service
builder.Services.AddSingleton<JobCreatedConsumer>();

// Or register as background service
builder.Services.AddHostedService<RabbitMQBackgroundService>();
```

### Background Service for Message Consumption

```csharp
public class RabbitMQBackgroundService : BackgroundService
{
    private readonly IRabbitMQBus _rabbitMQBus;
    private readonly JobCreatedConsumer _jobCreatedConsumer;

    public RabbitMQBackgroundService(
        IRabbitMQBus rabbitMQBus,
        JobCreatedConsumer jobCreatedConsumer)
    {
        _rabbitMQBus = rabbitMQBus;
        _jobCreatedConsumer = jobCreatedConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _jobCreatedConsumer.SubscribeAsync(stoppingToken);
        
        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

## Docker Deployment

### Start RabbitMQ with Docker Compose

```bash
docker-compose up -d
```

### Access RabbitMQ Management UI

- URL: http://localhost:15672
- Username: guest
- Password: guest

### Verify RabbitMQ is Running

```bash
docker-compose ps rabbitmq
docker-compose logs rabbitmq
```

## Common Use Cases

### 1. Job Notifications

Publish events when jobs are created, updated, or expired:

```csharp
// In CreateJobUseCase
await _rabbitMQBus.PublishAsync("job.created", new JobCreatedMessage { ... });

// In UpdateJobUseCase
await _rabbitMQBus.PublishAsync("job.updated", new JobUpdatedMessage { ... });

// In JobExpirationBackgroundService
await _rabbitMQBus.PublishAsync("job.expired", new JobExpiredMessage { ... });
```

### 2. Application Status Updates

Notify when application status changes:

```csharp
// In UpdateApplicationStatusUseCase
await _rabbitMQBus.PublishAsync("application.status_changed", new ApplicationStatusChangedMessage { ... });
```

### 3. Email Notifications

Queue email notifications for asynchronous processing:

```csharp
// In notification use cases
await _rabbitMQBus.PublishAsync("email.send", new EmailMessage { ... });
```

## Best Practices

1. **Use separate queues for different message types** to avoid message mixing
2. **Implement proper error handling** in message handlers
3. **Use durable queues** to prevent message loss during restarts
4. **Monitor RabbitMQ metrics** using the management UI
5. **Set appropriate prefetch counts** for consumers to manage load
6. **Use dead-letter queues** for failed message processing
7. **Implement message idempotency** to handle duplicate messages

## Troubleshooting

### Connection Issues

If you encounter connection issues:

1. Verify RabbitMQ is running: `docker-compose ps rabbitmq`
2. Check connection settings in `appsettings.json`
3. Verify network connectivity between containers
4. Check RabbitMQ logs: `docker-compose logs rabbitmq`

### Message Not Being Consumed

If messages are not being consumed:

1. Verify the consumer is registered and running
2. Check the queue name matches between publisher and subscriber
3. Verify the message type is the same
4. Check RabbitMQ management UI for queue status

### Performance Issues

For performance optimization:

1. Use message batching for high-volume scenarios
2. Implement proper connection pooling
3. Monitor queue depth and consumer lag
4. Consider using multiple consumers for high-throughput queues

## Security Notes

For production deployments:

1. Change default RabbitMQ credentials
2. Use SSL/TLS for encrypted connections
3. Implement proper authentication and authorization
4. Restrict network access to RabbitMQ
5. Use environment variables for sensitive configuration
