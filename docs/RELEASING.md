# Releasing FAA Tools

There is no auto-update. A release is an `.msi` file you hand to end users (email, network
share, etc.) — they double-click it and it cleanly replaces whatever version they had before.

## Steps

1. **Bump the version.** Edit `version.props` at the repo root:
   ```xml
   <FaaToolsVersion>0.1.0</FaaToolsVersion>
   ```
   This is the single source of truth — it drives the assembly version of both `FaaTools.Core`
   and `FaaTools.RevitAddin`, and the MSI's `ProductVersion`.

   Stay under `1.0.0` while this is a partial migration (only Synagogue Excel + Synagogue Space
   are native so far); bump to `1.0.0` once the native add-in is considered a full pyRevit
   replacement.

2. **Rebuild in Release:**
   ```
   dotnet build FaaTools.RevitAddin.slnx -c Release
   ```
   This produces `FaaTools.Core.dll` / `FaaTools.RevitAddin.dll` for both `net8.0-windows`
   (Revit 2025/2026) and `net10.0-windows` (Revit 2027) under each project's
   `bin\Release\<tfm>\`.

3. **Rebuild the installer:**
   ```
   dotnet build installer\FaaTools.Installer\FaaTools.Installer.wixproj -c Release
   ```
   Produces `installer\FaaTools.Installer\bin\Release\FaaTools.Installer.msi`. The installer
   project reads the Release build output directly (see `Net8OutDir`/`Net10OutDir` in
   `FaaTools.Installer.wixproj`) and embeds the three `.addin` manifests from
   `src\FaaTools.RevitAddin\manifests\`.

4. **Hand out the new `.msi`.** Running it on a machine that already has an older version
   installed triggers a clean major-upgrade (old files removed, new files installed) — there is
   no need to uninstall first, and no risk of ending up with two registered add-ins.

## Hard rules

- **`UpgradeCode` in `installer\FaaTools.Installer\Product.wxs` must never change.** It is the
  identity Windows Installer uses to recognize "this is a newer version of the same product" and
  trigger the clean replace. Changing it would make a new install sit alongside the old one
  instead of replacing it.
- **`FaaToolsVersion` must always increase** between releases (MSI `ProductVersion` only honors
  the first three numeric fields for upgrade comparison — `0.1.0` is fine, don't add a 4th field
  here).
- Per-component `AddInId` GUIDs in the three `.addin` manifests must stay identical to each other
  (same logical add-in across Revit versions) — this is intentional, not a bug.

## Adding a Revit version later

When a new Revit version ships:
1. Confirm its hosted .NET version by checking `RevitAPI.runtimeconfig.json` in its install
   directory (`"tfm": "net8.0"` or `"net10.0"`, etc.) — don't assume.
2. If it matches an existing `<TargetFrameworks>` entry in `FaaTools.RevitAddin.csproj`, just add
   a new `.addin` manifest + a new `ComponentGroup`/`Property` registry-detection block in
   `Product.wxs` pointing at the existing build output.
3. If it needs a new .NET target, add it to `<TargetFrameworks>` in both `.csproj` files, add the
   matching `Revit<Year>Path` property to `Directory.Build.props`, and follow the existing
   per-TFM `<ItemGroup Condition="...">` pattern for the `RevitAPI`/`RevitAPIUI` references.
