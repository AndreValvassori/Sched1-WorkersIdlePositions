# WorkersPlaces

A Schedule I MelonLoader mod for the IL2CPP game build. It lets the host choose an optional idle destination for each worker through the management clipboard. Unconfigured workers use the game's default destination.

## Environment

This is a C# class library, not a Unity Editor project. No Unity Editor installation is needed for this starter.

The installed game was inspected during setup: its MelonLoader log identifies IL2CPP, x64, MelonLoader 0.7.3 Open-Beta, and the net6 runtime. This project targets that installation. Steam's default branch uses IL2CPP; the alternate branch uses Mono and requires a different build setup.

Requirements:

- .NET SDK with .NET 6 targeting support (the local .NET 6 and 8 SDKs are available).
- Schedule I with MelonLoader installed and launched at least once to generate `MelonLoader/Il2CppAssemblies`.
- Optionally, Visual Studio, Rider, or VS Code with C# support.

## Local configuration

`GamePath.local.props` is already configured on this machine for `E:\SteamLibrary\steamapps\common\Schedule I`. It is excluded from Git.

On another machine, copy `GamePath.example.props` to `GamePath.local.props` and edit `GamePath`. References are read directly from the installed loader and generated game assemblies. Do not redistribute those DLLs or copy them into the mod output.

## Build

Run from the repository folder:

```powershell
dotnet build WorkersPlaces.csproj -c Release
```

Output: `bin/Release/net6.0/WorkersPlaces.dll`.

## Package a release

Close Schedule I, then run the release script from this folder:

```powershell
pwsh -NoProfile -File .\release.ps1
```

Each run increments the minor version and resets the patch to zero: `0.2.0` becomes `0.3.0`, then `0.4.0`. It updates the project, MelonInfo attribute, and bundled README together, builds Release, deploys `WorkersPlaces.dll` to the local game's `Mods` folder, and creates `release/WorkersPlaces-<version>.zip`. The ZIP contains `Mods/WorkersPlaces.dll` and a plain-text `README.txt`, so recipients can extract it into their Schedule I folder. The same DLL and README are also available separately under `release/Mods/WorkersPlaces.dll` and `release/README.txt`.

Use `pwsh -NoProfile -File .\release.ps1 -NoDeploy` to increment the version, build, and package without changing the installed game. The script checks that the project, mod attribute, and README versions match before updating them, and restores their previous contents if the release fails. A deployment or packaging failure may still leave build artifacts or an installed DLL from the attempted version. Ordinary `build.ps1` builds and deploys the current version without incrementing it. Edit the source `README.txt` for changes to the bundled instructions.

## Install and verify

Close the game, then build and copy only the mod DLL into the game's `Mods` directory:

```powershell
dotnet build WorkersPlaces.csproj -c Release -p:DeployMod=true
```

A normal build does not change the game folder. Deployment replaces an existing `WorkersPlaces.dll` there.

For a shortcut that always builds Release and deploys, run this from Command Prompt in the repository folder:

```cmd
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

From another folder, pass the full path to `build.ps1`. The script locates the project relative to itself and returns a nonzero exit code if building or deployment fails. The execution-policy override applies only to that PowerShell process.

Launch Schedule I through Steam. Check `MelonLoader/Latest.log` in the game folder for:

```text
WorkersPlaces initialized. Open a worker's management clipboard to set an idle position.
```

A successful build alone does not prove that the mod loads in the game. Restart the game after each deployment; DLL changes are not hot-reloaded.

## Using the mod

Open the management clipboard with exactly one botanist, chemist, cleaner, or packager selected. A small **Idle position** panel beside the clipboard shows **Default** or **Custom**. Click **Set** or **Change**; a top-center prompt appears while you walk to the spot where the worker should wait. Face the direction the worker should face and press **Space** to save the idle spot, teleport the worker there immediately, and turn them to face the same direction. Press Escape or right-click to cancel without changing the saved position or moving the worker. Use **Clear** to restore the game's normal waiting destination.

Placement also works while the worker is doing a job. Teleporting does not cancel that job, so the worker may walk away to continue working. **Clear** removes the saved idle destination but does not move the worker back.

Only the host can edit idle positions and teleport workers during placement. Other workers remain unaffected. The game still controls jobs, sleep, and other non-idle behavior. A custom position outside the worker's property is allowed. **Set** and **Change** record the player's position directly; the mod does not verify that the worker can reach it. Teleporting a worker into a place such as the RV does not add a navigation path, change their property assignment, or guarantee they can work or stay there. If the worker cannot navigate from the spot, use **Change** to save and teleport them to a reachable spot, or use **Clear**.

Settings store position and facing direction per game save under `UserData/WorkersPlaces` in the game folder and are keyed by the worker's persistent GUID. Positions saved by earlier builds have no facing direction; use **Change** and press **Space** again to capture it. Clearing a worker's position removes that override; firing a worker removes its saved entry. The mod never changes the game's own save files. If a settings file is malformed, the mod preserves it and logs a warning instead of overwriting it.

The idle controls follow the game's worker configuration panel but sit beside it on a paper-and-wood card, using the clipboard's font. Because the card sits outside the game's normal clipboard hit area, the mod checks clicks against its button rectangles only while that clipboard is open. If the panel cannot be found, on-screen controls remain available as a fallback. Runtime teleporting, UI alignment, job transitions, save reload, and co-op behavior still require in-game verification on the installed game version.

While the clipboard is closed, the mod does not scan all workers or calculate navigation paths on a timer. Placement does not run navigation checks.

To uninstall, close the game and remove only `Mods/WorkersPlaces.dll`.

## Development

- `Mod.cs`: mod identity, clipboard lifecycle, placement, and idle override.
- `ClipboardIdleControls.cs`: companion clipboard controls and paper-and-wood styling.
- `PlacementPrompt.cs`: top-center placement prompt styled like the game's HUD.
- `WorkerPositions.cs`: per-save position storage.
- `WorkersPlaces.csproj`: IL2CPP references, configuration validation, and optional deployment.
- Keep the version in `MelonInfo` and the project file synchronized when releasing.
- After a game or loader update, launch the game to refresh interop assemblies, then rebuild against that installation.
- The Harmony patch applies the saved destination as an employee enters normal idle. Recheck this behavior after game updates, especially if employee scheduling changes.

Documentation: [MelonLoader](https://github.com/LavaGang/MelonLoader), [mod quick start](https://github.com/LavaGang/MelonWiki/blob/master/docs/modders/quickstart.md).
