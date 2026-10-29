# AOT review after upstream 13.3.0 merge

The upstream merge added model and collection shapes that were absent from the
fork's checked-in JSON source-generation context. With reflection disabled, those
paths cannot resolve serialization metadata.

The fix adds explicit registrations for the missing model, enum, ApiEnum and
collection shapes, including the mutable and read-only dictionary forms used by
FrozenDictionaryConverter. It also registers JsonElement[] used by message-thread
reply conversion. Existing closed FrozenDictionary converters for UserProfileCyberTier
and UserProfileEfsFeature were retained from the merge.

The default serializer now consults JsonSerializer.IsReflectionEnabledByDefault
before adding the reflection resolver. AOT applications use the generated context;
JIT applications retain the reflection fallback for caller-defined types.

The assembly-based generator also records the new dictionary and thread array
shapes. To supplement checked-in metadata without compiling or installing tools:

```powershell
python scripts/update-aot-json-context.py
python scripts/update-aot-json-context.py --check
```

This source scanner targets the generated C# model templates. It preserves existing
registrations, resolves namespaces and aliases, and adds closed collection shapes.
It fails on unresolved type names rather than silently omitting them. It is not a
C# compiler or a replacement for an AOT publish test.

Validation performed without installation or compilation:

- Source coverage check: 4,645 required model and collection shapes registered.
- All 4,138 existing registrations preserved; 1,386 registrations added.
- Explicit TypeInfoPropertyName values are unique.
- Git whitespace/error checks pass.

No build, dependency restore, source-generator execution, or Native AOT binary was
run locally. Existing reflection-based convenience APIs for caller-defined
structured-output models remain outside this upstream-delta review. Such models
still require an application-supplied serialization/schema strategy for AOT.
