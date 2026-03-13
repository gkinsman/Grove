# GitHub Actions Release Workflow

## TL;DR
> **Summary**: Create a single GitHub Actions workflow that manually builds a self-contained win-x64 Grove release and publishes it as a GitHub Release with a zip artifact.
> **Estimated Effort**: Quick

## Context
### Original Request
Create a GitHub Actions workflow for building and releasing the Grove .NET 10 Avalonia desktop app. Manual trigger only (`workflow_dispatch`), accepts a version input, builds self-contained win-x64, creates a tagged GitHub Release with a zip artifact.

### Key Findings
- **SDK**: .NET 10.0.200 with `rollForward: latestFeature` — need `dotnet-version: '10.0.x'` in setup-dotnet
- **Project**: `src/Grove/Grove.csproj` is a `WinExe` targeting `net10.0`, references `Grove.Core`
- **Solution**: `Grove.slnx` (new XML-based solution format)
- **Version**: `<Version>0.1.0</Version>` is set in `Grove.csproj` — workflow will override via `/p:Version=`
- **Build props**: `TreatWarningsAsErrors` is enabled globally — build must be clean
- **Icon**: `src/Grove/Assets/grove-logo.ico` included via `<AvaloniaResource>`
- **No existing workflows** — `.github/workflows/` doesn't exist yet

## Objectives
### Core Objective
Ship a repeatable, one-click release process for Grove.

### Deliverables
- [ ] GitHub Actions workflow file at `.github/workflows/release.yml`

### Definition of Done
- [ ] Pushing the workflow to `main` and triggering "Run workflow" with a version like `0.1.0` produces a GitHub Release tagged `v0.1.0` with a `Grove-0.1.0-win-x64.zip` asset

### Guardrails (Must NOT)
- No automatic triggers (push/PR) — `workflow_dispatch` only
- No multi-platform builds — win-x64 only for now
- No NuGet publishing
- No Docker

## TODOs

- [ ] 1. Create `.github/workflows/release.yml`
  **What**: Create the workflow file with the full content below.
  **Files**: `.github/workflows/release.yml` (create)
  **Acceptance**: File exists, YAML is valid, workflow appears in GitHub Actions tab.

## Workflow File Content

Create `.github/workflows/release.yml` with exactly this content:

```yaml
name: Release

on:
  workflow_dispatch:
    inputs:
      version:
        description: 'Release version (e.g. 0.2.0)'
        required: true
        type: string

permissions:
  contents: write

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  release:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          dotnet-quality: 'ga'

      - name: Publish
        run: >
          dotnet publish src/Grove/Grove.csproj
          -c Release
          -r win-x64
          --self-contained
          -p:Version=${{ inputs.version }}
          -o publish

      - name: Zip artifact
        run: Compress-Archive -Path publish\* -DestinationPath Grove-${{ inputs.version }}-win-x64.zip

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          tag_name: v${{ inputs.version }}
          name: Grove v${{ inputs.version }}
          files: Grove-${{ inputs.version }}-win-x64.zip
          generate_release_notes: true
```

## Design Decisions

| Decision | Rationale |
|---|---|
| `dotnet publish` (not `dotnet build`) | Produces a ready-to-run output with all dependencies |
| `--self-contained` | User doesn't need .NET installed |
| `-r win-x64` | Single target per requirements |
| `-p:Version=` override | Stamps the input version into the assembly, overriding the csproj default |
| `softprops/action-gh-release@v2` | De facto standard for creating releases; handles tag creation + asset upload in one step |
| `generate_release_notes: true` | Auto-generates changelog from commits since last release |
| `windows-latest` runner | Avalonia WinExe — build on Windows to avoid cross-compile edge cases |
| `dotnet-quality: 'ga'` | Ensures we get a stable SDK, not a preview |
| `permissions: contents: write` | Required for creating tags and releases |

## Verification
- [ ] YAML passes lint (`actionlint` or GitHub's built-in validation on push)
- [ ] Manual trigger shows version input field in GitHub UI
- [ ] Build succeeds and zip contains `Grove.exe` + dependencies
- [ ] GitHub Release is created with correct tag, name, and zip asset
