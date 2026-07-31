# Atlas.Demo

`Atlas.Demo` is a portable Blazor WebAssembly consumer sample. It uses the public
`Atlas.Core` and `Atlas.Blazor` NuGet packages and is intentionally excluded from `Atlas.sln`.

## Build from the repository

Ensure the `atlas-local` source in `NuGet.Config` contains Release packages for
`Atlas.Core` and `Atlas.Blazor`. Then restore and verify the independent sample:

```powershell
cd samples/Atlas.Demo
dotnet restore Atlas.Demo.sln --configfile NuGet.Config
dotnet build Atlas.Demo.sln --no-restore
dotnet test Atlas.Demo.sln --no-build --no-restore
dotnet run --project Atlas.Demo.csproj --no-restore
```

Open the URL printed by `dotnet run`. The sample references the RCL stylesheet through
`_content/Atlas.Blazor/atlas-v2/atlas.css`; it does not copy Atlas JavaScript or CSS sources.

## Move outside the repository

Copy this entire directory. Update the `atlas-local` source in `NuGet.Config` to point at a
feed containing `Atlas.Core` and `Atlas.Blazor`, and update their versions in
`Directory.Packages.props` when needed. No layout source changes are required.

The sample intentionally contains no ProjectReference to Atlas source projects, linked Atlas
source files, internal APIs, or test helpers.
