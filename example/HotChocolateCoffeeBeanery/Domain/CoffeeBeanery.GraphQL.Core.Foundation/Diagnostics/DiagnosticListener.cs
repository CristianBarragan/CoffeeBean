namespace CoffeeBeanery.GraphQL.Core.Foundation.Diagnostics;
public sealed class DiagnosticListener { private readonly List<DiagnosticEvent> _events=new(); public IReadOnlyList<DiagnosticEvent> Events=>_events; public void Publish(DiagnosticEvent e)=>_events.Add(e); }
