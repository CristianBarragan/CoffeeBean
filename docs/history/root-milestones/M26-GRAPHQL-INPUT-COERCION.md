# M26 — GraphQL Input Coercion and Defaults

M26 makes GraphQL variables behave like real GraphQL input values rather than merely passing through an arbitrary CLR object.

## Boundary

```text
GraphQL operation
       +
runtime variables
       ↓
HotChocolate adapter
       ↓
variable coercion
       ↓
MutationIntent / SemanticRequest
       ↓
Foundgine core
```

GraphQL variable syntax and declared variable types stop at the adapter boundary.

## Implemented

- Required/non-null variable validation.
- Nullable omitted variables resolve to `null`.
- Variable default values are used when no runtime value is supplied.
- Default values are coerced against the declared variable type.
- `Int`, `Float`, `String`, `Boolean`, and `ID` scalar coercion.
- List coercion, including GraphQL singleton-to-list input coercion.
- Null rejection for non-null variables.
- Rejection of runtime variables that are not declared by the operation.
- Normalization of JSON runtime values into ordinary CLR values.

## Deliberate scope

The adapter does not recreate Hot Chocolate's full schema type system. A named type such as `CustomerInput`, an enum, or a custom scalar requires schema metadata to validate every nested field or enum member. M26 therefore preserves normalized values for those named types rather than inventing a second schema system inside Foundgine.

## Example

```graphql
mutation CreateCustomer($input: CustomerInput!) {
  createCustomer(input: $input) {
    id
    name
  }
}
```

Runtime variables:

```json
{
  "input": {
    "name": "Ada"
  }
}
```

The mutation planner receives an ordinary input object. It does not receive `$input`, `VariableNode`, or a GraphQL type node.

## Why this matters

M22 introduced variables. M26 makes those variables semantically trustworthy before they cross into Foundgine. This prevents GraphQL transport concerns from becoming hidden assumptions in planning or SQL execution.


### M27 — GraphQL Error Semantics
Structured GraphQL-facing adapter errors via `TryAdapt(...)`, without leaking GraphQL error concepts into Foundgine core.
