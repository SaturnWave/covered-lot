# Covered Lot

Covered Lot (working title) is a mobile puzzle game for Android and iOS.

This repository holds the game's Unity client. The rules engine it uses lives in
[SaturnWave/puzzle-engine](https://github.com/SaturnWave/puzzle-engine) and is copied
into the Unity project as DLLs.

Status: pre-production. A simple, playable Unity UI is being added on the `simple-ui`
branch: a level list, the board, the bays, the covers, the tags and the ledger, and a win
or fail card. It has no money, offers or meta, and its look is a placeholder until the
designs arrive.

## Checks

Run these from the repository root in Git Bash:

- `tools/check-repo.sh` checks the ignore and line-ending rules against a fixed table.
- `tools/scan-before-push.sh` runs before every push. The repository is public, so it
  refuses to pass any secret, personal path, user name or non-noreply email in the commits
  a push would publish. `tools/scan-self-test.sh` proves it catches each kind.
