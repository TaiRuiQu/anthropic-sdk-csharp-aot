# Publishing to NuGet

This fork publishes under the `TairuiQu.Anthropic.*` package IDs. It does not publish to the
official `Anthropic` IDs, which are owned by Anthropic.

## One-time setup

### 1. nuget.org trusted publishing policy

Sign in to nuget.org, open **Trusted Publishing**, and add a policy:

| Field           | Value                          |
| --------------- | ------------------------------ |
| Repository Owner | `TairuiQu`                    |
| Repository       | `anthropic-sdk-csharp-aot`    |
| Workflow File    | `publish-nuget.yml`           |
| Environment      | leave empty                   |

Set the policy scope to push new packages and package versions for the `TairuiQu.Anthropic.*`
glob. The workflow file name is the name under `.github/workflows/`, not the full path.

### 2. How the workflow authenticates

The workflow requests `id-token: write`, exchanges the GitHub OIDC token for a short-lived
nuget.org API key with `NuGet/login`, and pushes the packages. No long-lived NuGet API key is
stored in the repository. It does not use a GitHub environment, so the policy's Environment
field must stay empty.

### 3. nuget.org user name

`NuGet/login` takes the nuget.org profile name, not the GitHub login or the account email. The
workflow uses the repository variable `NUGET_USER` when it is set and falls back to `TairuiQu`.
Set the variable under **Settings -> Secrets and variables -> Actions -> Variables** if the
nuget.org profile name differs. The account must already exist on nuget.org: sign up at
<https://www.nuget.org/users/account/LogOn> before creating the trusted publishing policy.

## Releasing a version

1. Bump `VersionPrefix` in the package projects:
   - `src/Anthropic/Anthropic.csproj`
   - `src/Anthropic.Bedrock/Anthropic.Bedrock.csproj`
   - `src/Anthropic.Vertex/Anthropic.Vertex.csproj`
   - `src/Anthropic.Aws/Anthropic.Aws.csproj`
   - `src/Anthropic.Foundry/Anthropic.Foundry.csproj`
   - `src/Anthropic.GoogleCloud/Anthropic.GoogleCloud.csproj`
   - `src/Anthropic.Mcp/Anthropic.Mcp.csproj`
2. Commit and push the version bump to `main`.
3. Run **Actions -> Publish to NuGet -> Run workflow** on `main`.

A NuGet package version is immutable. Once a version is pushed it cannot be replaced, so use a
new version for every attempt (for example `12.58.0-aot.2`).

## Building packages locally

```sh
dotnet pack Anthropic.sln -c Release -o artifacts/packages
```

The output contains `.nupkg` files and matching `.snupkg` symbol packages. To test the package
before publishing:

```sh
dotnet add package TairuiQu.Anthropic.Aot --version <version> --source artifacts/packages
```

## Notes

- The `README.md` bundled in the package is the repository README. Keep the unofficial-fork
  notice at the top so the package page does not imply an official Anthropic release.
- `PackageProjectUrl`, `RepositoryUrl`, `Authors`, and `Copyright` are set in
  `src/Directory.Build.props`.
- The `.release-please-manifest.json` file still contains the upstream versions. It is not used by
  the manual publish workflow; bump versions in the project files when releasing.
