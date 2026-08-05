# Runtime

## Request Flow

GraphQL Request
→ Context Resolver
→ CQRS Dispatcher
→ UnitOfWork
→ Metadata
→ Expression Builder
→ Database
→ Result Mapping

## Core Components

- CacheHelper
- ContextResolverHelper
- UnitOfWork
- UnitOfWorkContext
- QueryDispatcher
- CommandDispatcher
