// // Polyfills required because this project targets netstandard2.0 (so it loads as an
// // analyzer in any consumer's compiler, regardless of the consumer's own TFM) but uses
// // C# 11's `required` member feature, whose runtime attributes don't exist below net7.0.
// // This is the standard workaround documented by the Roslyn team for source generator projects.
//
// #if !NET7_0_OR_GREATER
// namespace System.Runtime.CompilerServices
// {
//     internal static class IsExternalInit { }
//
//     [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
//     internal sealed class RequiredMemberAttribute : Attribute { }
//
//     [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor, AllowMultiple = true, Inherited = false)]
//     internal sealed class CompilerFeatureRequiredAttribute : Attribute
//     {
//         public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;
//         public string FeatureName { get; }
//         public bool IsOptional { get; set; }
//         public const string RefStructs = nameof(RefStructs);
//         public const string RequiredMembers = nameof(RequiredMembers);
//     }
// }
//
// namespace System.Diagnostics.CodeAnalysis
// {
//     [AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
//     internal sealed class SetsRequiredMembersAttribute : Attribute { }
// }
// #endif
