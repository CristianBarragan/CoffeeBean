# GraphQL Coffee Beanery

<p align="center">
  <img src="docs/images/coffee-beanery-logo.png" alt="GraphQL Coffee Beanery" width="300" />
</p>

<p align="center">
  <strong>Compile-time optimized GraphQL execution engine for .NET</strong>
</p>

<p align="center">
  Transform GraphQL AST selections into optimized SQL operations using source-generated metadata, supporting queries, mutations, upserts, and strongly typed materialization.
</p>

---

# Overview

GraphQL Coffee Beanery is a high-performance GraphQL execution engine designed for the .NET ecosystem.

Built around **Hot Chocolate**, **C# Source Generators**, and an AST-driven execution model, Coffee Beanery analyzes GraphQL operations and translates them into optimized database operations while minimizing runtime overhead.

Unlike traditional GraphQL implementations that rely heavily on resolver orchestration, runtime reflection, and manually maintained DataLoaders, Coffee Beanery moves intelligence into compile time.

The result is a predictable execution pipeline capable of handling:

- Complex GraphQL queries
- Deep relationship graphs
- Mutation execution
- Insert operations
- Update operations
- Upsert operations
- Optimized mutation responses
- Native AOT deployments
- Enterprise-scale workloads

---

# Core Philosophy

Coffee Beanery is built around one principle:

> **The GraphQL AST is the execution plan.**

A GraphQL operation already contains the information required to understand:

- Requested fields
- Relationships
- Data dependencies
- Mutation inputs
- Response shape

Coffee Beanery uses this information to generate the most efficient execution path possible.

Instead of:

```text
GraphQL Request

        │

        ▼

Resolver Chain

        │

        ▼

DataLoaders

        │

        ▼

Multiple Database Queries

        │

        ▼

Manual Mapping

---

# Architecture Deep Dive

Coffee Beanery is built around a unified AST-driven execution architecture.

The same GraphQL operation model powers:

- Query generation
- Mutation generation
- Upsert execution
- Response projection
- Object materialization

The framework does not treat queries and mutations as unrelated systems.

Instead, they are different execution paths originating from the same GraphQL AST.

---

# GraphQL AST as the Execution Source

Every GraphQL operation contains two important pieces of information:

## Input Shape

For mutations:

```graphql
mutation {

  upsertProduct(
    input: {
      id: 100
      name: "Ethiopian Blend"
    }
  )

}

---

# Mutation Response Optimization

A GraphQL mutation is not only a write operation.

The mutation response is another GraphQL selection tree.

This distinction is important because the operation that changes state and the operation that returns data can be optimized independently.

Example:

```graphql
mutation {

    upsertOrder(
        input: {
            customerId: 42
            items: [
                {
                    productId: 10
                    quantity: 2
                }
            ]
        }
    ) {

        order {

            id

            status

            total

            customer {

                name

                address {

                    city

                }

            }

            items {

                quantity

                product {

                    name

                    supplier {

                        name

                    }

                }

            }

        }

    }

}

---

# Source Generators & Compile-Time Metadata

A core design decision behind Coffee Beanery is moving execution knowledge from runtime into compile time.

Instead of discovering relationships, mappings, and execution rules dynamically, Coffee Beanery uses C# Source Generators to produce strongly typed metadata during the build process.

This approach provides:

- Faster startup
- Reduced runtime overhead
- Better Native AOT compatibility
- Predictable execution
- Strong typing
- Fewer runtime surprises

---

# Traditional Runtime Discovery

Many data access systems rely on runtime inspection.

A simplified execution model:

```text
GraphQL Request

        │

        ▼

Runtime Reflection

        │

        ▼

Discover Types

        │

        ▼

Discover Relationships

        │

        ▼

Build Query

        │

        ▼

Execute

---

# Database Architecture

Coffee Beanery is designed to work with modern .NET persistence architectures.

The framework does not require applications to adopt a single database access pattern.

Instead, it provides an execution layer that can integrate with:

- Dapper
- EF Core
- PostgreSQL
- Distributed PostgreSQL
- Graph database extensions
- Custom SQL strategies

The goal is to provide optimized GraphQL execution while allowing applications to choose the right persistence approach for each workload.

---

# Dapper Integration

Coffee Beanery naturally complements Dapper-based architectures.

Dapper provides:

- Lightweight database access
- High-performance execution
- Direct SQL control
- Minimal abstraction overhead

Coffee Beanery adds:

- GraphQL AST processing
- SQL generation
- Relationship traversal
- Generated mappings
- Object materialization

The execution model becomes:

```text
GraphQL Request

        │

        ▼

Coffee Beanery AST Processing

        │

        ▼

Generated SQL

        │

        ▼

Dapper Execution

        │

        ▼

Generated Object Graph

        │

        ▼

GraphQL Response

---

# CQRS Architecture

Coffee Beanery naturally fits into Command Query Responsibility Segregation (CQRS) architectures.

CQRS separates application responsibilities:

- Commands modify state.
- Queries retrieve state.

Coffee Beanery extends this model by providing an optimized GraphQL execution layer for both sides when desired.

---

# Traditional CQRS Model

A common CQRS architecture looks like:

```text
                    GraphQL API

                         │

             ┌───────────┴───────────┐

             ▼                       ▼

        Commands                  Queries

             │                       │

             ▼                       ▼

     Domain Application        Read Application

             │                       │

             ▼                       ▼

        Write Database          Read Database
		
---

# Coffee Beanery vs DataLoaders

DataLoaders are an important part of the GraphQL ecosystem.

Coffee Beanery does not replace DataLoaders.

Instead, Coffee Beanery and DataLoaders solve different problems at different layers of GraphQL execution.

Understanding this distinction helps determine when each approach is appropriate.

---

# What DataLoaders Solve

GraphQL fields are resolved independently.

Without batching, nested relationships can create the classic N+1 query problem.

Example:

```graphql
query {

    customers {

        name

        orders {

            id

        }

    }

}

---

# Performance Philosophy

Coffee Beanery is designed around a simple performance principle:

> Move expensive decisions to compile time, and keep runtime execution focused on performing the operation.

GraphQL is powerful because clients can request exactly the data they need.

However, flexibility can introduce execution challenges:

- Dynamic field selection
- Deep relationship graphs
- Runtime mapping
- Excessive resolver execution
- N+1 query patterns
- Repeated metadata discovery

Coffee Beanery addresses these challenges by combining:

- GraphQL AST analysis
- Source-generated metadata
- Optimized SQL generation
- Strongly typed materialization
- Unified query and mutation execution

---

# Traditional GraphQL Runtime Model

A traditional resolver-based architecture often looks like:

```text id="b1m4ka"
GraphQL Request

        │

        ▼

Resolver Execution

        │

        ▼

Runtime Mapping

        │

        ▼

Database Calls

        │

        ▼

Object Construction

        │

        ▼

Response

---

# Architecture Comparison

GraphQL applications can be implemented using many different execution strategies.

Coffee Beanery focuses on solving a specific challenge:

> How do we provide GraphQL flexibility while maintaining database-level performance and enterprise extensibility?

The following comparison highlights architectural differences.

---

# Execution Strategy Comparison

| Capability | Coffee Beanery | Traditional Resolver GraphQL | DataLoaders | Pure ORM Projection |
|---|---|---|---|---|
| GraphQL AST execution | ✅ | Partial | ❌ | Partial |
| Generated SQL | ✅ | ❌ | ❌ | Partial |
| Generated mutations | ✅ | ❌ | ❌ | ❌ |
| Generated upserts | ✅ | ❌ | ❌ | ❌ |
| Deep relationship handling | ✅ | Partial | Partial | Partial |
| N+1 prevention | ✅ | Manual | ✅ | Partial |
| Source-generated metadata | ✅ | ❌ | ❌ | ❌ |
| Native AOT focus | ✅ | Partial | Partial | Partial |
| Custom business logic | ✅ | ✅ | ✅ | ✅ |
| External APIs | Partial | ✅ | ✅ | Partial |
| Runtime flexibility | High | High | High | High |
| Compile-time optimization | High | Low | Low | Medium |

---

# Coffee Beanery vs EF Core

Coffee Beanery and EF Core solve different problems.

EF Core is a powerful object-relational mapper.

Coffee Beanery is a GraphQL execution engine.

They can work together.

---

## EF Core Strengths

EF Core provides:

- Change tracking
- Entity lifecycle management
- Migrations
- Transactions
- Domain modeling
- Rich ORM capabilities

Example:

```text id="8j3y6f"
Domain Entity

       │

       ▼

EF Core Change Tracking

       │

       ▼

Database Update

---

# Getting Started

This section walks through setting up Coffee Beanery in a .NET GraphQL application.

Coffee Beanery is designed to integrate naturally with:

- ASP.NET Core
- Hot Chocolate GraphQL
- PostgreSQL
- Dapper
- EF Core
- Existing domain architectures

---

# Installation

Install the required NuGet packages:

```bash
dotnet add package GraphQL.Coffee.Beanery

---

# Advanced Configuration

Coffee Beanery is designed for simple adoption but supports advanced enterprise customization.

The execution pipeline exposes extension points for:

- Business logic
- Caching
- Security
- Data transformation
- Multi-tenancy
- Auditing
- Custom execution workflows

The main extension point is:

```text
ProcessService

---

# Advanced Configuration

Coffee Beanery is designed for simple adoption but supports advanced enterprise customization.

The execution pipeline exposes extension points for:

- Business logic
- Caching
- Security
- Data transformation
- Multi-tenancy
- Auditing
- Custom execution workflows

The main extension point is:

```text
ProcessService

---

# Roadmap

Coffee Beanery is evolving toward a complete AST-driven GraphQL execution platform.

The roadmap focuses on improving:

- Performance
- Database capabilities
- Developer experience
- Enterprise integrations
- Source generation capabilities

---

# Current Capabilities

Implemented capabilities include:

## GraphQL Execution

✅ GraphQL AST analysis  
✅ Selection-driven execution  
✅ Deep relationship traversal  
✅ Optimized response projection  

---

## Query Generation

✅ Generated SQL execution  
✅ Relationship mapping  
✅ Strongly typed materialization  
✅ N+1 prevention  

---

## Mutation Generation

✅ Mutation AST processing  
✅ Insert generation  
✅ Update generation  
✅ Upsert generation  
✅ Mutation response optimization  

---

## Source Generation

✅ Compile-time metadata generation  
✅ Strongly typed mappings  
✅ Reduced runtime discovery  
✅ Native AOT-friendly architecture  

---

## Enterprise Extensions

✅ ProcessService pipeline  
✅ Response transformation  
✅ Business calculations  
✅ Security filtering  
✅ Caching integration  

---

# Future Directions

Potential future improvements include:

## More Database Providers

Expanding support for additional relational databases.

---

## Advanced Query Optimization

Possible enhancements:

- Cost-based execution strategies
- Query plan optimization
- Automatic index recommendations
- Advanced batching strategies

---

## Distributed Execution

Future scenarios:

- Distributed GraphQL execution
- Federated database execution
- Multi-region architectures

---

## Enhanced Mutation Workflows

Future mutation capabilities may include:

- More complex transactional workflows
- Advanced validation pipelines
- Event-driven mutation patterns
- Generated domain commands

---

# Contributing

Contributions are welcome.

Coffee Beanery benefits from contributions in:

- Performance improvements
- Database providers
- Source generator enhancements
- Documentation
- Testing
- Example applications

---

# Development Setup

Clone the repository:

```bash
git clone https://github.com/CristianBarragan/GraphQL-Coffee-Beanery.git
```

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