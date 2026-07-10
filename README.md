# ☕ GraphQL Coffee Beanery

> **Compile-time optimized GraphQL query execution for Hot Chocolate using source-generated mappings, Dapper, and Native AOT-friendly architecture.**

GraphQL Coffee Beanery is a high-performance execution engine that transforms GraphQL selection sets into optimized SQL queries using compile-time generated metadata instead of runtime reflection.

Unlike traditional GraphQL data access patterns that rely heavily on DataLoaders, manual projections, or runtime expression trees, Coffee Beanery generates strongly typed mapping metadata during compilation, allowing GraphQL requests to be translated into efficient SQL while remaining fully compatible with Native AOT.

Coffee Beanery is designed to work alongside **Hot Chocolate**, allowing developers to keep their existing GraphQL schema, mutations, and business logic while dramatically reducing boilerplate for complex read operations.

---

# Why Coffee Beanery?

Building GraphQL APIs over relational databases usually introduces several challenges:

- N+1 query problems
- Large numbers of DataLoaders
- Runtime reflection
- Complex projection logic
- Manual object mapping
- Difficult Native AOT compatibility
- Repetitive SQL generation

Coffee Beanery approaches these problems differently.

Instead of resolving relationships at runtime through multiple database calls, Coffee Beanery analyzes the GraphQL selection tree and compiles it into an optimized SQL query using metadata generated at build time.

The result is a predictable execution pipeline that minimizes allocations, reduces runtime work, and simplifies the implementation of large GraphQL APIs.

---

# Key Features

- 🚀 Optimized SQL generation from GraphQL selection sets
- ⚡ Compile-time source-generated mappings
- 🧠 Native AOT friendly
- 🔄 Hot Chocolate integration
- 🏗 Strongly typed object materialization
- 📦 Dapper support
- 🛠 EF Core model integration
- 🌳 Deep nested GraphQL selection support
- 🔍 Elimination of common N+1 scenarios
- 🧩 Centralized post-processing pipeline
- 🔒 Enterprise customization hooks
- 📈 PostgreSQL optimized
- 🌐 Apache AGE compatible
- ⚖️ Citus compatible
- 🧪 CQRS-friendly architecture

---

# Design Philosophy

Coffee Beanery focuses on one responsibility:

> **Efficiently executing GraphQL read operations.**

Instead of becoming an ORM or replacing your application's business layer, Coffee Beanery specializes in translating GraphQL selection trees into efficient database queries.

Business logic remains inside your application.

Validation remains inside your application.

Transactions remain inside your application.

Coffee Beanery simply provides an optimized execution engine for retrieving the requested GraphQL data.

This separation keeps the architecture clean, composable, and easy to reason about.

---

# Architecture Overview

Coffee Beanery is built around four independent execution pipelines.

```text
                GraphQL Request
                       │
                       ▼
             Hot Chocolate Execution
                       │
          ┌────────────┴────────────┐
          │                         │
          ▼                         ▼
    Query Pipeline           Mutation Pipeline
          │                         │
          ▼                         ▼
 Coffee Beanery             Business Logic
 SQL Compiler               EF Core / Dapper
          │                         │
          ▼                         ▼
 Optimized SQL             Database Transaction
          │                         │
          └────────────┬────────────┘
                       ▼
             GraphQL Selection Phase
                       ▼
             Coffee Beanery Compiler
                       ▼
               ProcessService
                       ▼
              GraphQL Response
```

Each pipeline has a dedicated responsibility.

| Pipeline | Responsibility |
|----------|----------------|
| Query Pipeline | Compiles GraphQL selections into optimized SQL |
| Mutation Pipeline | Executes writes, validation, transactions, and business rules |
| Materialization Pipeline | Hydrates strongly typed objects and applies custom processing |
| Build Pipeline | Generates compile-time mapping metadata for runtime execution |

This separation enables applications to evolve independently without coupling read optimization to business logic.

---

# How Coffee Beanery Works

Coffee Beanery executes GraphQL requests in four stages.

## 1. Source Generation

During compilation, source generators inspect your mapping configuration and produce strongly typed metadata describing:

- Entity relationships
- Property mappings
- Foreign keys
- Collection navigation
- SQL aliases
- Materialization rules

Because this work happens at build time, runtime reflection is avoided.

---

## 2. GraphQL Parsing

When a GraphQL request arrives, Coffee Beanery reads the selection tree produced by Hot Chocolate.

For example:

```graphql
query {

  products {

    id

    name

    supplier {

      name

      address {

        city

      }

    }

  }

}
```

Coffee Beanery converts this selection tree into an internal execution graph that describes exactly which entities and relationships are required.

No unnecessary fields are fetched.

No runtime expression trees are built.

---

## 3. SQL Compilation

Using the generated metadata, Coffee Beanery produces optimized SQL tailored to the requested selection.

Instead of multiple database round trips, related entities are retrieved together whenever possible.

This dramatically reduces unnecessary database access while preserving the GraphQL response shape.

---

## 4. Materialization

The resulting database rows are transformed into strongly typed object graphs.

This process is handled by **ProcessService**, which reconstructs nested relationships and prepares the objects returned to Hot Chocolate.

Unlike traditional object mappers, this pipeline also serves as the central extension point for enterprise customization.

# Query & Mutation Execution

One of the most important architectural concepts in Coffee Beanery is the separation between **writing data** and **reading data**.

GraphQL mutations naturally execute in two distinct phases:

1. **Mutation Phase** – Execute business logic and persist state changes.
2. **Selection Phase** – Resolve the fields requested in the mutation response.

Coffee Beanery intentionally focuses on the second phase.

This allows existing mutation handlers to remain unchanged while still benefiting from optimized query execution when returning complex GraphQL response shapes.

---

# The Two-Pipeline Execution Model

When Hot Chocolate executes a mutation, the request naturally flows through two independent pipelines.

```text
                 Incoming GraphQL Mutation
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                  Mutation Pipeline                           │
│                                                              │
│  • Validation                                                │
│  • Authorization                                             │
│  • Business Rules                                            │
│  • EF Core / Dapper                                          │
│  • Transactions                                              │
│  • Domain Events                                             │
│  • Save Changes                                              │
└──────────────────────────────────────────────────────────────┘
                            │
                            ▼
              Returns Root Object / Identifier
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                GraphQL Selection Pipeline                    │
│                                                              │
│  • Reads requested GraphQL fields                            │
│  • Builds execution graph                                    │
│  • Compiles optimized SQL                                    │
│  • Materializes nested object graph                          │
│  • Returns requested response shape                          │
└──────────────────────────────────────────────────────────────┘
                            │
                            ▼
                  GraphQL Response
```

Because these responsibilities are independent, Coffee Beanery can optimize every read operation without interfering with how writes are implemented.

---

# Why This Matters

Many GraphQL applications optimize queries but unintentionally fall back to inefficient execution when returning data from mutations.

Consider the following mutation:

```graphql
mutation {

  adjustStock(
    input: {
      productId: 42
      adjustment: -5
    }
  ) {

    product {

      id

      name

      stockLevel

      supplier {

        name

        contact {

          email

        }

      }

    }

  }

}
```

Although this is a mutation, the response body is simply another GraphQL selection tree.

After the write completes, Hot Chocolate begins resolving the requested fields.

Coffee Beanery treats this exactly like any other GraphQL query.

The requested object graph is analyzed, optimized, and materialized using the same execution engine used for standard queries.

This means deeply nested mutation responses can avoid common N+1 scenarios without requiring additional DataLoaders or handwritten projections.

---

# Example Mutation

Your mutation remains focused exclusively on business logic.

```csharp
public sealed class InventoryMutations
{
    public async Task<ProductPayload> AdjustStockAsync(
        AdjustStockInput input,
        [ScopedService] AppDbContext db)
    {
        var product = await db.Products.FindAsync(input.ProductId);

        product.StockLevel += input.Adjustment;

        await db.SaveChangesAsync();

        return new ProductPayload
        {
            ProductId = product.Id
        };
    }
}
```

Notice that the mutation only performs the write operation.

There is no manual loading of related entities.

There are no DataLoaders.

There is no SQL projection.

The mutation simply returns the information required for the GraphQL selection pipeline to continue execution.

---

# Automatic Response Optimization

Suppose the client requests additional information.

```graphql
mutation {

  adjustStock(input: {
      productId: 42
      adjustment: -5
  }) {

    product {

      id

      name

      stockLevel

      category {

          name

      }

      supplier {

          name

          contact {

              email

              phone

          }

      }

      warehouse {

          address {

              city

              country

          }

      }

    }

  }

}
```

After the mutation commits successfully:

1. Hot Chocolate begins resolving the selection tree.
2. Coffee Beanery analyzes every requested field.
3. Generated metadata identifies required relationships.
4. SQL is compiled for the requested graph.
5. ProcessService materializes the object graph.
6. The response is returned.

No manual projections are necessary.

No additional DataLoaders are required.

No custom response-building logic needs to be written.

---

# CQRS-Friendly by Design

Coffee Beanery naturally complements CQRS architectures.

The write side remains completely independent from the read side.

```text
                  GraphQL Request
                         │
        ┌────────────────┴────────────────┐
        │                                 │
        ▼                                 ▼
      Commands                         Queries
        │                                 │
        ▼                                 ▼
 Business Logic                 Coffee Beanery
 EF Core                        SQL Compiler
 Transactions                   Materialization
 Validation                     Response
        │                                 │
        └──────────────┬──────────────────┘
                       ▼
                GraphQL Response
```

Each side can evolve independently.

Command handlers remain focused on state changes.

Coffee Beanery remains focused on efficient data retrieval.

This separation simplifies testing, improves maintainability, and keeps responsibilities clearly defined.

---

# Benefits of the Two-Pipeline Model

Separating writes from reads provides several advantages.

## Transaction Safety

Mutations continue using your preferred transaction strategy.

Coffee Beanery does not interfere with transactional boundaries.

---

## Business Logic Isolation

Validation, authorization, domain rules, and messaging remain inside the mutation handler where they belong.

The query engine remains focused solely on retrieving data.

---

## Optimized Mutation Responses

Large response payloads are retrieved using the same optimized execution engine as regular GraphQL queries.

This allows clients to request rich, nested response shapes without introducing unnecessary database round trips.

---

## Reduced Boilerplate

Mutation handlers remain small and focused.

Developers do not need to manually populate response graphs or duplicate query logic inside every mutation.

---

## Consistent GraphQL Execution

Whether a client executes a query or requests nested data from a mutation, Coffee Beanery uses the same optimized execution pipeline.

The result is a consistent programming model across your entire GraphQL API.

# ProcessService: The Materialization & Extension Pipeline

After Coffee Beanery executes the generated SQL, the returned data still needs to be transformed into the object graph expected by your GraphQL schema.

This responsibility belongs to **ProcessService**.

Rather than acting as a simple object mapper, ProcessService is the central execution stage where raw database results become strongly typed domain models.

It also provides a single, well-defined extension point for implementing enterprise-specific behavior without modifying query generation or GraphQL resolvers.

```text
              Generated SQL
                    │
                    ▼
          Database Result Set
                    │
                    ▼
           Metadata Resolution
                    │
                    ▼
             ProcessService
                    │
      ┌─────────────┼─────────────┐
      │             │             │
      ▼             ▼             ▼
 Materialize   Transform      Validate
      │             │             │
      └─────────────┼─────────────┘
                    ▼
          GraphQL Response Model
```

---

# Responsibilities

ProcessService is responsible for:

- Materializing strongly typed objects
- Reconstructing nested relationships
- Populating collections
- Resolving parent/child graphs
- Applying generated mapping metadata
- Producing the final object graph returned to Hot Chocolate

Because it operates after SQL execution, it provides a natural place to introduce application-specific processing while preserving the efficiency of the generated query.

---

# Enterprise Extension Point

Many enterprise applications require additional processing that cannot—or should not—be expressed directly in SQL.

Rather than scattering this logic across GraphQL resolvers, ProcessService centralizes these concerns into a single pipeline.

Typical scenarios include:

- Computed fields
- Currency conversion
- Tax calculations
- Localization
- User-specific formatting
- Business rule evaluation
- Response enrichment
- Security trimming
- Data masking
- Payload caching
- Audit metadata
- Multi-tenant filtering

This keeps GraphQL resolvers simple while allowing applications to evolve without modifying generated SQL.

---

# Computed Business Logic

Not every value belongs in the database.

Some fields depend on runtime context or business rules that are only available within the application.

Examples include:

- Discounted prices
- Loyalty rewards
- Inventory availability
- Shipping estimates
- Tax calculations
- Feature flags
- Dynamic permissions

Instead of embedding these concerns into SQL, ProcessService can compute them after the object graph has been materialized.

```text
SQL Result
      │
      ▼
Product
      │
      ▼
Apply Business Rules
      │
      ▼
Calculated Fields
      │
      ▼
Return GraphQL Response
```

This keeps SQL focused on retrieving data while allowing application logic to remain inside .NET.

---

# Payload Caching

ProcessService is also an ideal integration point for response caching.

Instead of caching raw SQL rows, applications can cache the fully materialized object graph.

```text
Incoming GraphQL Request
            │
            ▼
     Cache Lookup
      │        │
 Cache Hit   Cache Miss
      │        │
      ▼        ▼
 Return     Execute SQL
 Object         │
                ▼
         Materialize Objects
                │
                ▼
           Store in Cache
                │
                ▼
          Return Response
```

Possible implementations include:

- IMemoryCache
- Redis
- Distributed Cache
- Hybrid cache strategies

Caching complete payloads avoids repeated materialization work and reduces database load for frequently requested queries.

---

# Dynamic Field Masking

Many applications must protect sensitive information based on the current user's identity.

Examples include:

- Email addresses
- Phone numbers
- Salary information
- Personal identifiers
- Financial records
- Healthcare information

Because ProcessService operates on strongly typed objects, fields can be modified before they are returned.

For example:

- Replace values with masked text
- Return null
- Apply role-based filtering
- Remove restricted collections
- Hide confidential properties

This approach keeps authorization concerns separate from SQL generation while supporting GDPR, HIPAA, and similar compliance requirements.

---

# Multi-Tenant Applications

Enterprise systems frequently require tenant-aware responses.

After objects have been materialized, ProcessService can:

- Filter collections
- Remove inaccessible entities
- Inject tenant metadata
- Enforce tenant boundaries
- Apply organization-specific transformations

This allows tenant rules to remain centralized rather than duplicated across individual GraphQL resolvers.

---

# Response Enrichment

Applications often need to enrich data using services outside the database.

Examples include:

- Exchange rate services
- Recommendation engines
- Inventory systems
- Distributed caches
- External APIs
- Machine learning predictions

Because ProcessService executes after database retrieval, these integrations can be introduced without affecting SQL compilation.

---

# Why This Preserves Native AOT

A common concern with extensibility is whether customization reintroduces reflection.

Coffee Beanery avoids this problem.

ProcessService operates on strongly typed models produced by the source generators.

No runtime type discovery is required.

No dynamic proxies are created.

No reflection-based object mapping occurs.

The execution flow remains compatible with Native AOT while allowing rich application-specific customization.

```text
Source Generator
        │
        ▼
Generated Metadata
        │
        ▼
Strongly Typed Models
        │
        ▼
ProcessService
        │
        ▼
GraphQL Response
```

---

# Keeping Responsibilities Separate

One of the design goals of Coffee Beanery is ensuring each stage has a single responsibility.

| Component | Responsibility |
|-----------|----------------|
| Source Generator | Generate mapping metadata |
| SQL Compiler | Build optimized SQL |
| Database | Execute relational queries |
| ProcessService | Materialize and transform objects |
| Hot Chocolate | Serialize the GraphQL response |

This separation keeps the architecture modular and makes it easier to customize behavior without modifying unrelated parts of the pipeline.

---

# Why ProcessService Matters

ProcessService transforms Coffee Beanery from a SQL generation library into a complete GraphQL execution pipeline.

It provides a single place to introduce business-specific behavior while preserving the performance benefits of compile-time generated mappings.

Instead of spreading business rules across GraphQL resolvers, DataLoaders, middleware, and object mappers, applications can centralize post-processing in one predictable location.

The result is a cleaner architecture that scales from simple CRUD applications to enterprise systems requiring caching, security, compliance, and complex business rules—all without sacrificing the performance characteristics of Native AOT or the efficiency of Coffee Beanery's query compilation pipeline.

---

# Source-Generated Mapping

Coffee Beanery is built around **compile-time generated mapping metadata**.

Rather than discovering entities, relationships, and navigation properties through runtime reflection, Coffee Beanery generates strongly typed metadata during compilation that describes how your GraphQL schema maps to your relational model.

This generated metadata drives every stage of execution:

- GraphQL selection analysis
- SQL generation
- Join resolution
- Object materialization
- Nested relationship reconstruction

Because the metadata is generated at build time, runtime execution remains lightweight, predictable, and compatible with Native AOT.

---

# Why Source Generators?

Traditional ORMs and GraphQL frameworks frequently depend on runtime reflection to inspect entity models.

Reflection offers flexibility but introduces several trade-offs:

- Additional startup work
- Higher memory usage
- Runtime metadata discovery
- Dynamic expression generation
- Limited Native AOT compatibility

Coffee Beanery takes a different approach.

Instead of discovering metadata during execution, it generates the required information once during compilation.

```text
Application Build
        │
        ▼
Source Generator
        │
        ▼
Generate Mapping Metadata
        │
        ▼
Compile Application
        │
────────────────────────────────────
Runtime
        │
        ▼
Read Generated Metadata
        │
        ▼
Compile SQL
        │
        ▼
Execute Query
```

The result is less runtime work and greater predictability.

---

# Generated Metadata

The generated metadata contains everything Coffee Beanery needs to execute GraphQL queries efficiently.

Typical information includes:

- Entity names
- Database tables
- Primary keys
- Foreign keys
- Navigation properties
- Collection relationships
- Column mappings
- SQL aliases
- Join definitions
- Materialization instructions

Because this information already exists before the application starts, there is no need to inspect types dynamically.

---

# Mapping Once, Using Everywhere

One of the core principles of Coffee Beanery is that mapping information should only need to be defined once.

After relationships are configured, the generated metadata is reused throughout the framework.

The same metadata powers:

- Query compilation
- SQL generation
- Relationship traversal
- Nested object reconstruction
- Materialization
- Mutation response optimization

This avoids duplicated configuration across multiple layers of the application.

---

# GraphQL Selection Analysis

Consider the following GraphQL query:

```graphql
query {

    customers {

        id

        name

        orders {

            orderDate

            total

            items {

                quantity

                product {

                    name

                    category {

                        name

                    }

                }

            }

        }

    }

}
```

Coffee Beanery walks the selection tree and identifies exactly which entities and relationships are required.

```text
Customer
    │
    ├──────────────► Orders
                         │
                         ▼
                      Items
                         │
                         ▼
                      Product
                         │
                         ▼
                     Category
```

Only the requested fields participate in query generation.

Unused properties are ignored.

---

# Relationship Resolution

The generated mapping metadata defines how entities relate to one another.

Coffee Beanery uses this information to construct joins automatically.

For example:

```text
Customer
      │
      ▼
Order
      │
      ▼
OrderItem
      │
      ▼
Product
      │
      ▼
Supplier
```

Because relationships are already known, SQL generation becomes deterministic.

There is no need to inspect CLR types or dynamically infer navigation paths.

---

# SQL Generation

After the execution graph has been built, Coffee Beanery compiles the required SQL.

Conceptually, the execution pipeline looks like this:

```text
GraphQL Query
        │
        ▼
Selection Tree
        │
        ▼
Execution Graph
        │
        ▼
Generated Mapping Metadata
        │
        ▼
SQL Compiler
        │
        ▼
Optimized SQL
```

The compiler only generates the joins, columns, and relationships required to satisfy the client's request.

---

# Materialization Metadata

Generating SQL is only half of the problem.

The returned rows must also be reconstructed into the nested object graph expected by GraphQL.

Coffee Beanery generates metadata describing how flat relational data maps back into strongly typed objects.

```text
Database Rows

Customer
Order
Item
Product

        │
        ▼

Generated Materialization Metadata

        │
        ▼

Customer
 ├── Orders
 │      ├── Items
 │      │      └── Product
 │      └── ...
 └── ...
```

This allows ProcessService to efficiently rebuild complex object graphs without reflection.

---

# Native AOT Benefits

Compile-time metadata generation is one of the key reasons Coffee Beanery works well with Native AOT.

Instead of relying on runtime type discovery, execution uses generated code that is already known at compile time.

Benefits include:

- Faster startup
- Reduced memory allocations
- Smaller deployment size
- Elimination of reflection
- Predictable execution
- Improved cold-start performance
- Better compatibility with containerized workloads

---

# Strongly Typed Execution

Every stage of Coffee Beanery is designed around strongly typed models.

```text
Source Generator
        │
        ▼
Generated C# Metadata
        │
        ▼
SQL Compiler
        │
        ▼
Database
        │
        ▼
ProcessService
        │
        ▼
Strongly Typed Domain Models
        │
        ▼
Hot Chocolate
```

This approach minimizes runtime surprises while improving maintainability and debugging.

---

# Design Goals

The mapping system was designed with several principles in mind.

## Single Source of Truth

Relationship definitions should exist in one place and be reused throughout the framework.

---

## Compile-Time Validation

Whenever possible, mapping problems should be detected during compilation rather than at runtime.

---

## Minimal Runtime Work

Runtime execution should focus on query compilation and data retrieval—not metadata discovery.

---

## Native AOT Compatibility

Avoid runtime reflection whenever possible.

---

## Extensibility

Generated metadata should provide a foundation that can be extended without requiring consumers to rewrite the query engine.

---

By combining source generators with strongly typed mapping metadata, Coffee Beanery shifts work from runtime to build time.

This approach reduces boilerplate, simplifies GraphQL execution, and provides the foundation for efficient SQL generation, optimized materialization, and enterprise customization through ProcessService.

---

# Performance Philosophy

Coffee Beanery was designed around a simple principle:

> **Move as much work as possible from runtime to compile time.**

Rather than relying on reflection, runtime metadata discovery, or manually maintained DataLoaders, Coffee Beanery generates the information required to execute GraphQL queries during the build process.

At runtime, the execution engine focuses on four responsibilities:

1. Analyze the GraphQL selection tree.
2. Compile an optimized SQL query.
3. Materialize the returned data.
4. Produce the requested GraphQL response.

Everything else is handled during compilation.

---

# Eliminating Runtime Complexity

Many GraphQL applications gradually accumulate runtime infrastructure as they grow.

A typical application may include:

- DataLoaders
- Custom projections
- Manual object mapping
- Reflection-based metadata discovery
- Expression tree generation
- Resolver-specific SQL

Each component solves an individual problem, but together they increase the complexity of the execution pipeline.

Coffee Beanery consolidates much of this work into a single, predictable pipeline driven by compile-time generated metadata.

```text
Traditional GraphQL

Resolver
    │
    ▼
Projection
    │
    ▼
DataLoader
    │
    ▼
Mapping
    │
    ▼
Database

────────────────────────────────────

Coffee Beanery

GraphQL Selection
        │
        ▼
Generated Metadata
        │
        ▼
SQL Compiler
        │
        ▼
Database
        │
        ▼
ProcessService
```

The goal is not to replace every GraphQL pattern, but to reduce the amount of infrastructure required for common read scenarios.

---

# Compile-Time vs Runtime

Coffee Beanery intentionally shifts responsibility toward compilation.

| Build Time | Runtime |
|------------|---------|
| Generate mapping metadata | Parse GraphQL selection |
| Validate relationships | Compile SQL |
| Generate materialization metadata | Execute query |
| Prepare execution model | Materialize objects |

This results in less runtime work and more predictable execution.

---

# Native AOT as a Design Goal

Native AOT is not an afterthought.

It influenced the design of the framework from the beginning.

Whenever possible, Coffee Beanery avoids features that typically make Native AOT more difficult, such as:

- Runtime reflection
- Dynamic proxy generation
- Runtime expression compilation
- Dynamic type discovery

Instead, generated code provides the metadata required during execution.

---

# Scaling with Schema Complexity

As GraphQL schemas grow, applications often introduce:

- Additional DataLoaders
- Resolver-specific optimization
- Duplicate mapping logic
- Specialized projection code

Coffee Beanery approaches scalability differently.

The same execution pipeline is used regardless of whether a query requests:

- One entity
- Multiple relationships
- Deeply nested collections
- Complex response graphs

Because execution is driven by the GraphQL selection tree and generated metadata, increasing schema size does not require fundamentally different infrastructure.

---

# Large GraphQL Responses

GraphQL allows clients to request rich object graphs.

For example:

```graphql
query {

    customers {

        orders {

            items {

                product {

                    supplier {

                        address {

                            country

                        }

                    }

                }

            }

        }

    }

}
```

Coffee Beanery is designed to analyze these selection trees and retrieve the requested data efficiently without requiring developers to manually assemble the response.

The client defines the shape.

Coffee Beanery determines how to retrieve it.

---

# Mutation Responses Follow the Same Pipeline

One important design decision is that mutation responses are treated exactly like query responses.

After the mutation completes successfully:

```text
Mutation
      │
      ▼
Business Logic
      │
      ▼
Database Commit
      │
      ▼
GraphQL Selection
      │
      ▼
Coffee Beanery
      │
      ▼
Optimized Response
```

Because GraphQL mutation payloads are simply selection trees, the same query compilation pipeline can be reused.

This keeps the programming model consistent across queries and mutations.

---

# Comparison with Traditional Approaches

Every GraphQL data access strategy has different trade-offs.

The table below summarizes common architectural characteristics.

| Capability | Coffee Beanery | Hot Chocolate + EF Core | Hot Chocolate + DataLoaders |
|------------|----------------|--------------------------|-----------------------------|
| Compile-time mapping | ✅ | ❌ | ❌ |
| Source generators | ✅ | ❌ | ❌ |
| Runtime reflection minimized | ✅ | Partial | Partial |
| Native AOT friendly | ✅ | Partial | Partial |
| Optimized SQL generation | ✅ | EF Core dependent | Resolver dependent |
| Deep graph support | ✅ | ✅ | ✅ |
| Mutation response optimization | ✅ | Manual | Manual |
| Centralized materialization pipeline | ✅ | ❌ | ❌ |
| Business extension pipeline | ✅ | Partial | Resolver-specific |
| DataLoader management | Minimal | Optional | Extensive |

Each approach has strengths.

Coffee Beanery is optimized for applications that want a compile-time driven execution model with minimal runtime infrastructure.

---

# Designed for Enterprise Applications

Coffee Beanery is intended to support applications with requirements such as:

- Large GraphQL schemas
- Multiple development teams
- Native AOT deployments
- High-throughput APIs
- Complex relational models
- Deep object graphs
- PostgreSQL
- Citus clusters
- Apache AGE
- CQRS architectures

Rather than introducing custom infrastructure for each use case, the framework provides a consistent execution pipeline that can be extended where necessary.

---

# A Foundation, Not a Framework Lock-In

Coffee Beanery is designed to complement existing .NET applications rather than replace them.

You remain free to choose:

- EF Core
- Dapper
- CQRS
- Repository patterns
- Domain-driven design
- Transaction strategies
- Authentication and authorization
- Dependency injection
- Caching technologies

Coffee Beanery focuses on one responsibility:

Efficiently executing GraphQL read operations.

Everything else remains under your application's control.

---

# Design Principles

The architecture of Coffee Beanery is guided by a few core principles.

### Build-Time First

Perform expensive work during compilation whenever possible.

---

### Separation of Responsibilities

Keep query execution, business logic, materialization, and schema execution independent.

---

### Strong Typing

Prefer generated, strongly typed metadata over runtime discovery.

---

### Extensibility

Provide well-defined customization points without requiring consumers to modify the query engine.

---

### Predictable Execution

The same GraphQL request should consistently follow the same execution pipeline.

---

These principles make Coffee Beanery suitable for applications ranging from small GraphQL services to large enterprise systems, while preserving a clean separation between application logic and query execution.

---

# Getting Started

Coffee Beanery is designed to integrate naturally with existing **Hot Chocolate** applications.

The framework focuses on optimizing GraphQL read execution while allowing you to continue using your preferred persistence strategy for writes.

A typical application consists of:

- Hot Chocolate
- Coffee Beanery
- Dapper (query execution)
- EF Core (optional model definitions and mutations)
- Source-generated mappings

```text
ASP.NET Core
      │
      ▼
Hot Chocolate
      │
      ▼
Coffee Beanery
      │
      ▼
Generated Mapping Metadata
      │
      ▼
SQL Compiler
      │
      ▼
Database
```

---

# Basic Execution Flow

Every GraphQL request follows the same high-level pipeline.

```text
GraphQL Request
        │
        ▼
Hot Chocolate
        │
        ▼
GraphQL Selection Tree
        │
        ▼
Coffee Beanery
        │
        ▼
Generated Mapping Metadata
        │
        ▼
SQL Compilation
        │
        ▼
Database
        │
        ▼
ProcessService
        │
        ▼
GraphQL Response
```

This execution model remains consistent regardless of query complexity.

---

# Defining Your Domain

Coffee Beanery works with strongly typed domain models.

A simplified example might look like:

```csharp
public class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Order> Orders { get; set; } = [];
}

public class Order
{
    public int Id { get; set; }

    public DateTime OrderDate { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }

    public Product Product { get; set; } = default!;
}

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
```

These models define the shape of your GraphQL response while the generated mapping metadata describes how they relate to the underlying database.

---

# Configuring Relationships

Coffee Beanery uses mapping configuration to understand how entities relate to one another.

Conceptually, relationships are defined once and reused throughout the execution pipeline.

```text
Customer
      │
      ▼
Order
      │
      ▼
OrderItem
      │
      ▼
Product
```

Once generated, this metadata is reused for:

- SQL generation
- Join resolution
- Relationship traversal
- Materialization
- Mutation response execution

No resolver-specific relationship configuration is required.

---

# Executing a GraphQL Query

Suppose a client executes:

```graphql
query {

    customers {

        id

        name

        orders {

            orderDate

            items {

                quantity

                product {

                    name

                }

            }

        }

    }

}
```

Coffee Beanery analyzes the selection tree and determines:

- Which entities are required
- Which joins are necessary
- Which columns should be selected
- How the response should be materialized

The generated SQL only includes the requested data.

---

# Deep Relationship Traversal

One of the strengths of Coffee Beanery is handling deeply nested object graphs.

For example:

```graphql
query {

    customers {

        orders {

            items {

                product {

                    supplier {

                        address {

                            country

                        }

                    }

                }

            }

        }

    }

}
```

Execution remains identical.

The only difference is the execution graph generated from the requested fields.

```text
Customer
      │
      ▼
Order
      │
      ▼
OrderItem
      │
      ▼
Product
      │
      ▼
Supplier
      │
      ▼
Address
```

Because the relationships are already known through generated metadata, Coffee Beanery can construct the required SQL without additional resolver logic.

---

# Materializing the Response

Once the database returns the requested rows, ProcessService reconstructs the GraphQL response.

```text
Flat Database Rows

Customer
Order
Item
Supplier

        │
        ▼

ProcessService

        │
        ▼

Customer
 └── Orders
      └── Items
            └── Product
                  └── Supplier
```

This reconstruction is driven entirely by generated metadata rather than reflection.

---

# Working with EF Core

Coffee Beanery does not replace EF Core.

Instead, both technologies complement one another.

A common architecture is:

| Responsibility | Technology |
|----------------|------------|
| Mutations | EF Core |
| Transactions | EF Core |
| Change Tracking | EF Core |
| Business Rules | Application Layer |
| Query Execution | Coffee Beanery |
| GraphQL Schema | Hot Chocolate |

This allows developers to continue benefiting from EF Core's rich write capabilities while using Coffee Beanery to optimize GraphQL reads.

---

# Working with Dapper

Coffee Beanery naturally integrates with Dapper for query execution.

The framework focuses on:

- Building optimized SQL
- Executing the generated query
- Materializing strongly typed objects

This combination provides a lightweight and predictable read pipeline while keeping full control over the generated SQL.

---

# CQRS Example

Coffee Beanery fits naturally into a CQRS architecture.

```text
GraphQL

        │

 ┌──────┴──────┐

 ▼             ▼

Commands     Queries

 ▼             ▼

EF Core   Coffee Beanery

 ▼             ▼

Database   Database
```

Commands remain responsible for changing state.

Coffee Beanery remains responsible for reading state.

The two pipelines share the same GraphQL schema while remaining architecturally independent.

---

# End-to-End Request Lifecycle

Putting everything together, a typical request follows this path.

```text
Client

 │

 ▼

GraphQL Request

 │

 ▼

Hot Chocolate

 │

 ▼

Selection Tree

 │

 ▼

Coffee Beanery

 │

 ▼

Generated Metadata

 │

 ▼

SQL Compiler

 │

 ▼

Database

 │

 ▼

ProcessService

 │

 ▼

Strongly Typed Objects

 │

 ▼

GraphQL Response
```

Every request follows the same predictable execution model regardless of schema size or query depth.

---

# Next Steps

After understanding the execution pipeline, the next areas to explore are:

- Source-generated mappings
- Custom ProcessService extensions
- Mutation response optimization
- Native AOT deployment
- Advanced PostgreSQL scenarios
- Apache AGE graph traversal
- Citus distributed execution

Each builds upon the same compile-time metadata and execution pipeline described throughout this guide.

---

# Coffee Beanery vs DataLoaders

DataLoaders are one of the most important tools in the GraphQL ecosystem.

Coffee Beanery does **not** replace DataLoaders.

Instead, it addresses a different layer of the GraphQL execution pipeline.

Understanding this distinction helps determine when each approach is most appropriate.

---

# What DataLoaders Solve

GraphQL executes fields independently.

Without batching, resolving nested relationships can produce the well-known **N+1 query problem**.

For example:

```graphql
query {

    customers {

        name

        orders {

            orderDate

        }

    }

}
```

A naïve resolver implementation might execute:

```text
SELECT * FROM Customers;

SELECT * FROM Orders WHERE CustomerId = 1;

SELECT * FROM Orders WHERE CustomerId = 2;

SELECT * FROM Orders WHERE CustomerId = 3;

...
```

DataLoaders batch these requests together.

```text
SELECT * FROM Customers;

SELECT *
FROM Orders
WHERE CustomerId IN (...)
```

This dramatically reduces unnecessary database round trips.

---

# What Coffee Beanery Solves

Coffee Beanery approaches the problem from an earlier stage.

Instead of optimizing resolver execution after GraphQL begins resolving fields, Coffee Beanery analyzes the entire GraphQL selection tree before execution.

```text
GraphQL Query

        │

        ▼

Selection Tree

        │

        ▼

Execution Graph

        │

        ▼

SQL Compilation

        │

        ▼

Database

        │

        ▼

Materialized Graph
```

Because the complete selection tree is already known, Coffee Beanery can generate SQL specifically for the requested response shape.

Rather than coordinating many independent resolvers, it plans the read operation as a whole.

---

# Different Responsibilities

Although both approaches improve GraphQL performance, they operate at different layers.

| Concern | DataLoader | Coffee Beanery |
|----------|------------|----------------|
| Resolver batching | ✅ | Not required |
| Prevent N+1 | ✅ | ✅ |
| SQL generation | ❌ | ✅ |
| Compile-time metadata | ❌ | ✅ |
| Source generators | ❌ | ✅ |
| Object materialization | Partial | ✅ |
| Native AOT friendly | Partial | ✅ |
| Mutation response optimization | Manual | ✅ |
| Centralized post-processing | ❌ | ✅ |

They are complementary technologies rather than competing ones.

---

# Resolver-Centric vs Query-Centric

Traditional GraphQL execution is typically resolver-centric.

```text
Resolver

    │

    ▼

DataLoader

    │

    ▼

Database

    │

    ▼

Resolver

    │

    ▼

DataLoader

    │

    ▼

Database
```

Each resolver is responsible for retrieving its own data.

Coffee Beanery is query-centric.

```text
Entire GraphQL Query

          │

          ▼

Analyze Selection Tree

          │

          ▼

Compile SQL

          │

          ▼

Database

          │

          ▼

Materialize Object Graph
```

Instead of coordinating many individual fetches, Coffee Beanery plans the complete read operation up front.

---

# A Simpler Mental Model

With Coffee Beanery, developers spend less time thinking about:

- Which DataLoader should be used?
- Which resolver owns this relationship?
- Where should batching occur?
- Which projection is responsible for this field?

Instead, the GraphQL selection itself becomes the execution plan.

The requested fields determine the generated SQL.

---

# Queries and Mutations Behave the Same

One of the advantages of this architecture is consistency.

Whether a client executes:

```graphql
query {

    customer(id: 1) {

        orders {

            items {

                product {

                    supplier {

                        name

                    }

                }

            }

        }

    }

}
```

or

```graphql
mutation {

    updateCustomer(...) {

        customer {

            orders {

                items {

                    product {

                        supplier {

                            name

                        }

                    }

                }

            }

        }

    }

}
```

the response selection is processed by the same query compilation pipeline.

No separate optimization strategy is required for mutation payloads.

---

# Can They Be Used Together?

Absolutely.

Coffee Beanery is designed to integrate with existing Hot Chocolate applications.

If your application already uses DataLoaders for scenarios outside Coffee Beanery's execution pipeline, they can continue to coexist.

For example, DataLoaders remain valuable when resolving:

- External REST APIs
- gRPC services
- Message queues
- Legacy systems
- Third-party integrations
- Distributed services

Coffee Beanery focuses specifically on optimizing relational GraphQL read execution.

---

# Choosing the Right Tool

Use Coffee Beanery when you want:

- Compile-time generated mapping metadata
- Optimized SQL generation
- Strongly typed materialization
- Native AOT compatibility
- Minimal GraphQL boilerplate
- Efficient nested relational queries
- Consistent query and mutation response execution

Use DataLoaders when your application needs to batch requests across multiple independent data sources that are not naturally expressed as a single relational query.

Many applications will benefit from both approaches.

Coffee Beanery is not intended to replace GraphQL best practices—it provides an additional execution strategy for applications that want to optimize relational query execution through compile-time metadata and SQL generation.

## Contributing

Contributions, feedback, and collaboration are welcome.

### Ways to Contribute

- Feature requests
- Bug reports
- Performance improvements
- Documentation enhancements
- Architecture proposals
- New mapping strategies
- Testing improvements

Whether you're improving documentation or proposing major architectural changes, every contribution helps improve the project.

---

## Support

If Coffee Beanery helps your team build faster and more scalable GraphQL APIs, consider supporting the project.

[Buy me a Coffee ☕] *I would love a 100% colombian coffee!*

[![Buy Me A Coffee](https://cdn.buymeacoffee.com/buttons/default-orange.png)](https://www.buymeacoffee.com/cristianbarragan)

---

## Keywords

GraphQL SQL Generator • GraphQL Query Planner • GraphQL Dapper • GraphQL PostgreSQL • GraphQL Database First • GraphQL Runtime SQL • GraphQL Query Optimization • GraphQL Performance • GraphQL N+1 Solution • GraphQL Execution Engine • GraphQL Relationship Mapping • GraphQL Join Generation • GraphQL AST Translation • High Performance GraphQL • Hot Chocolate Dapper • .NET GraphQL Framework • PostgreSQL GraphQL Framework

---

## AI Documentation

- [llms.txt](./llms.txt)
- [ai.seo.md](./ai.seo.md)