namespace Foundgine.Foundation;
public static class ThrowHelper { public static Exception Invalid(string message)=>new InvalidOperationException(message); }
