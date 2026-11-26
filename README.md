# Covered Lot

Covered Lot (working title) is a mobile puzzle game for Android and iOS.

This repository holds the game's Unity client. The rules engine it uses lives in
[SaturnWave/puzzle-engine](https://github.com/SaturnWave/puzzle-engine) and is copied
into the Unity project as DLLs.

Status: pre-production. A simple, playable Unity UI is being added on the `simple-ui`
branch: a level list, the board, the bays, the covers, the tags and the ledger, and a win
or fail card. It has no money, offers or meta, and its look is a placeholder until the
designs arrive.

## The engine and the levels

`Assets/Generated/` is written only by `tools/sync-engine.sh`; never edit it by hand. It
holds, from one engine commit:

- `PuzzleCore/PuzzleCore.dll` and `PuzzleCore/PuzzleCore.Jam.dll`, the engine's Release
  builds;
- `Resources/Levels/`: one canonical level text per campaign level, as the engine's `gen`
  writes it, the pack's `presentation.json`, and `six-by-six.txt`, the rules Example;
- `engine-sync.txt`, which records the engine commit, the SDK and the gen command, and
  `engine-sync.sha256`, the checksums of every file above.

To take a newer engine commit, close the Unity editor and run, with your own engine
checkout in place of `<engine-dir>`:

    tools/sync-engine.sh <engine-dir> <commit>

The commit must already be on the engine's `origin/master`. To prove that the committed
files are exactly what that commit builds, run `tools/sync-engine.sh <engine-dir> --check`.

## Checks

Run these from the repository root in Git Bash:

- `tools/check-repo.sh` checks the ignore and line-ending rules against a fixed table.
- `tools/scan-before-push.sh` runs before every push. The repository is public, so it
  refuses to pass any secret, personal path, user name or non-noreply email in the commits
  a push would publish. `tools/scan-self-test.sh` proves it catches each kind.
