#if !NET
#pragma warning disable IDE0130 // Namespace does not match folder structure
#pragma warning disable CS9113 // Unused parameters

namespace System.Runtime.CompilerServices
{
    // Allow `required` to compile when targeting .NET Standard 2.0.
    [AttributeUsage(
        AttributeTargets.Class
            | AttributeTargets.Struct
            | AttributeTargets.Property
            | AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = false
    )]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string feature) { }
    }

    // Allow `init` to compile when targeting .NET Standard 2.0.
    internal static class IsExternalInit { }
}

namespace System.Diagnostics.CodeAnalysis
{
    // Allow `[SetsRequiredMembers]` to compile when targeting .NET Standard 2.0.
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }

    // Allow `[MaybeNullWhen(...)]` to compile when targeting .NET Standard 2.0.
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue) { }
    }

    // Allow `[NotNullWhen(...)]` to compile when targeting .NET Standard 2.0.
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) { }
    }

    // Allow the trimming/AOT suppressions to compile when targeting .NET Standard 2.0. The
    // analyzers that read it only run on the .NET targets, so it is inert here.
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        public string Category { get; }

        public string CheckId { get; }

        public string? Justification { get; set; }

        public string? Scope { get; set; }

        public string? Target { get; set; }

        public string? MessageId { get; set; }
    }
}
#endif
