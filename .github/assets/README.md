# BenchmarkGate logo assets

Follows the Bijecta product-family system: parent square+circle+line construction, Amber accent (#B7791F). Circle carries a small needle (it measures); square carries a small checkmark (it gates); a bar crosses the line at the square's exit, marking the threshold.

## Files
- `benchmarkgate-icon.svg` — transparent background, Ink stroke. Use on light UI, docs.
- `benchmarkgate-icon-light-bg.svg` / `benchmarkgate-icon-dark-bg.svg` — icon on a white / Ink square. Use for GitHub org/repo avatar, NuGet package icon, app icons — anywhere a solid-background square is required.
- `benchmarkgate-avatar-512.png` — 512×512, Ink background. **Use for GitHub avatar.**
- `benchmarkgate-nuget-128.png` — 128×128, Ink background. **Use as the NuGet package icon** (`<PackageIcon>` in the `.nuspec`/`.csproj`).
- `benchmarkgate-icon-64.png`, `-favicon-32.png`, `-favicon-16.png` — transparent, for docs/UI and browser favicon.

## README embed
```md
<img src="./.github/assets/benchmarkgate-icon.svg" width="40" height="40" alt="BenchmarkGate" />

# BenchmarkGate
Performance regression gates for BenchmarkDotNet and CI/CD.
```

## NuGet (.csproj)
```xml
<PropertyGroup>
  <PackageIcon>benchmarkgate-nuget-128.png</PackageIcon>
</PropertyGroup>

<ItemGroup>
  <None Include="..\..\.github\assets\benchmarkgate-nuget-128.png" Pack="true" PackagePath="" />
</ItemGroup>
```
