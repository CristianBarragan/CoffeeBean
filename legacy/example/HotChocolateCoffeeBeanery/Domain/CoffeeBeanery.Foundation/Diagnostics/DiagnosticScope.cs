namespace CoffeeBeanery.GraphQL.Core.Foundation.Diagnostics;
public sealed class DiagnosticScope : IDisposable { private readonly Action _dispose; public DiagnosticScope(Action dispose)=>_dispose=dispose; public void Dispose()=>_dispose(); }
