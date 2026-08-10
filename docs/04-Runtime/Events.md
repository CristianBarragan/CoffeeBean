# Events

There is no active event-driven execution framework in the current core.

If events are added later, they should be treated as an execution integration rather than a reason for the planner to depend on a message broker.

Potential future boundary:

```text
Execution
   ↓
Domain/event outcome
   ↓
Application event infrastructure
```

Kafka, Azure Service Bus, RabbitMQ or other brokers remain external concerns.
