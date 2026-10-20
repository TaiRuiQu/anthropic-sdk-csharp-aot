# Claude SDK for C# (Native AOT fork)

[![NuGet Version](https://img.shields.io/nuget/vpre/TairuiQu.Anthropic.Aot.svg)](https://www.nuget.org/packages/TairuiQu.Anthropic.Aot)

> [!IMPORTANT]
> This is an **unofficial fork** of the official [Anthropic C# SDK](https://github.com/anthropics/anthropic-sdk-csharp), with .NET 10 and Native AOT support added. It is not affiliated with or endorsed by Anthropic. The official package is [`Anthropic`](https://www.nuget.org/packages/Anthropic); use this fork only if you need its Native AOT support.

The Claude SDK for C# provides access to the [Claude API](https://docs.anthropic.com/en/api/) from C# applications.

## Documentation

Full documentation is available at **[platform.claude.com/docs/en/api/sdks/csharp](https://platform.claude.com/docs/en/api/sdks/csharp)**.

## Installation

```bash
dotnet add package TairuiQu.Anthropic.Aot --prerelease
```

The cloud platform clients are published under matching IDs, for example
`TairuiQu.Anthropic.Bedrock` and `TairuiQu.Anthropic.Vertex`.

## Getting started

```csharp
using System;
using Anthropic;
using Anthropic.Models.Messages;

AnthropicClient client = new();

MessageCreateParams parameters = new()
{
    MaxTokens = 1024,
    Messages =
    [
        new()
        {
            Role = Role.User,
            Content = "Hello, Claude",
        },
    ],
    Model = "claude-opus-5-5",
};

var message = await client.Messages.Create(parameters);

Console.WriteLine(message);
```

## Requirements

.NET Standard 2.0+

## Native AOT

The `net8.0`, `net9.0`, and `net10.0` builds are annotated with `IsAotCompatible` and use a
source-generated `System.Text.Json` context, so they can be published with Native AOT. The
`examples/AotExample` project is a runnable smoke test:

```bash
dotnet publish examples/AotExample/AotExample.csproj -c Release
```

Schema generation for caller-defined structured-output types
(`StructuredOutput.CreateJsonFormat<T>` and friends) still uses reflection, so it is not
AOT-safe unless those types are registered in a source-generated `JsonSerializerContext`.

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md).

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
