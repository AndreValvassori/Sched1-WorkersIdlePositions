# WorkersPlaces — Custom Worker Idle Positions

## Description

Give each worker a place to wait between jobs. Instead of walking to their usual idle spot, a worker with a custom position returns to the spot you choose, facing the direction you set.

Place workers near their machines to reduce the walk back when work begins. Setting a position is **optional and individual**: workers you do not configure keep their normal behavior.

## Installation instructions

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) for the default **IL2CPP** version of Schedule I. Launch the game once after installing it, then close the game.
2. Extract the mod archive into your Schedule I game folder. Confirm the file is at `Schedule I/Mods/WorkersPlaces.dll`.
3. Start the game. The mod's settings are stored separately for each game save.

To uninstall, close the game and remove `Mods/WorkersPlaces.dll`. Your saved idle positions can be removed separately from `UserData/WorkersPlaces` if you no longer want them.

## Main features

- **Per-worker idle spots:** Configure each worker independently, or leave them on the game's default behavior.
- **Simple placement and teleport:** Select one worker on the management clipboard, then use the **Idle position** panel beside it. Choose **Set** or **Change**, walk to the desired spot, face the direction you want the worker to face, and press **Space** to save the spot, teleport the worker there, and turn them to face the same direction. This also works during a job, but the worker may leave to continue that job.
- **Easy cancellation and reset:** Press **Escape** or right-click to cancel placement. Choose **Clear** to restore that worker's default idle position.
- **Persistent settings:** The mod saves each worker's position and facing direction per game save, without modifying the game's save files.
- **Less unnecessary walking:** A well-placed idle spot can keep workers closer to their next task.

**Placement tip:** Choose a spot the worker can actually reach. The mod does not verify a path before saving or teleporting. Teleporting a worker inside the RV does not create an NPC route into it or change their property assignment, so it may not fix work or navigation there. If a worker cannot use a chosen spot, use **Change** to save and teleport them to a reachable spot, or use **Clear**.

## Requirements

- Schedule I on the default **IL2CPP** branch. The Mono branch is not supported.
- MelonLoader; this release was tested with version **0.7.3**.
- Only the host can edit worker idle positions. Multiplayer behavior has not been fully verified.

No S1API installation is required. Manual installation is the supported method for this release.

## Shout outs

Thanks to the MelonLoader team and the Schedule I modding community for making mods like this possible, and to the players whose feedback helped refine the clipboard UI and placement flow.

Source code and updates: [GitHub](https://github.com/AndreValvassori/Sched1-WorkersIdlePositions).
