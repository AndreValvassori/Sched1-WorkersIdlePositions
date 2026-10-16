# Automatic release version implementation plan

**Goal:** Increment the minor version on every release: 0.2.0 to 0.3.0 to 0.4.0.

**Architecture:** Keep the project version as the source of truth. Validate all three version declarations before updating them, restore source contents if the release fails, and package using the new version.

**Tech stack:** PowerShell, .NET, MelonLoader.

- [x] Update `release.ps1` to calculate major.(minor+1).0, synchronize `WorkersPlaces.csproj`, `Mod.cs`, and `README.txt`, and restore source contents on failure. Ordinary `build.ps1` must retain the current version.
- [x] Update the packaging instructions in `README.md` with the automatic increment and `-NoDeploy` behavior.
- [x] Exercise two successful releases, version mismatch rejection, and build failure rollback with isolated files and a fake dotnet command. Run the actual script with `-NoDeploy` once to produce 0.3.0; inspect ZIP contents, version metadata, and DLL hashes.
