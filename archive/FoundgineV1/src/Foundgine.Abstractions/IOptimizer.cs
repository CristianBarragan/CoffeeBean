namespace Foundgine.Abstractions;

/// <summary>Optimizes execution plans.</summary>
public interface IOptimizer<TPlan>
{
    TPlan Optimize(TPlan plan);
}
