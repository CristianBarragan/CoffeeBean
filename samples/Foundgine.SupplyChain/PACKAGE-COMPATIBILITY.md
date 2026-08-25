# Foundgine 0.5.2 package compatibility

This sample intentionally consumes the released Foundgine **0.5.2 NuGet packages**.

The Model/ERP mapping attributes introduced in the unreleased development source
(`FoundgineModelEntityMapAttribute` and `FoundgineConnectionMapAttribute`) are not
part of Foundgine 0.5.2, so they are **not used by this sample**.

With 0.5.2, the AOT declarations that need to be combined by the generator live in
the same compilation. The sample therefore keeps the model declarations and ERP
entity declarations in the Domain project while keeping the two representations
as separate CLR types. The model does not inherit from or reuse the ERP type; the
only runtime relationship declaration is the released `FoundgineConnection(Type)`
API.

When the newer Model/ERP mapping API is published, this sample can be moved to a
separate Entity project without changing the public semantic model surface.
