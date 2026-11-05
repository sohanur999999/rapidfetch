# Contributing to RapidFetch

Thanks for helping improve RapidFetch.

## Before you start

- Search existing issues and pull requests.
- Keep changes focused. One feature or fix per pull request.
- Do not commit build output, downloaded tools, logs, personal download
  history, or browser profiles.
- Read `PRIVACY.md` before changing anything that touches page content,
  permissions, or the native bridge.

## Build

Requirements:

- Windows 10 or 11, 64-bit
- .NET SDK 10
- Node.js 22 (for extension syntax checks and the local harness)
- PowerShell 5.1 or newer

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The packaged build is written to `dist\RapidFetch`.

Run the engine suite:

```powershell
dotnet run --project tools\EngineSmoke\EngineSmoke.csproj -c Release
```

Check the extension:

```powershell
node --check extension\content.js
node --check extension\popup.js
node --check extension\background.js
```

## Extension changes

- Keep the extension Manifest V3 compatible.
- Do not add remote code, remote scripts, ad injection, or analytics without
  prior discussion.
- Do not widen permissions unless the feature requires it. Explain every new
  permission in the pull request.
- Test the popup, floating panel, options page, picker, and native bridge.
- Reload the unpacked extension before testing.

## Desktop changes

- Keep the app self-contained and per-user installable.
- Do not require administrator rights.
- Preserve the default-safe remove behavior: files are kept unless the user
  explicitly selects **Also delete files from storage**.
- Test light, dark, and system themes.
- Test at 100%, 125%, and 150% display scaling where possible.

## Commit messages

Use a short prefix:

```
feat: add scheduler presets
fix: keep partial file after pause
docs: update Chrome Web Store listing
chore: refresh build scripts
```

## Pull request checklist

- [ ] Build succeeds.
- [ ] `EngineSmoke` reports `ALL TESTS PASSED`.
- [ ] Extension scripts pass `node --check`.
- [ ] No secrets, tokens, personal data, or build artifacts are included.
- [ ] Documentation is updated when behavior changes.
- [ ] The change does not add ads, tracking, or remote code without discussion.

## License

By contributing, you agree that your contribution is licensed under the MIT
License in `LICENSE`.
