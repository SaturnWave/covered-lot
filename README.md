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

`Assets/Generated/` is written only by `tools/sync-engine.sh`, apart from the `.meta` files
Unity adds (see the Unity project section); never edit its other files by hand. It holds,
from one engine commit:

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

## Unity project

The Unity project sits at the repository root, on Unity 6000.3.29f1, the version
`ProjectSettings/ProjectVersion.txt` names. It was made from the editor's bundled Universal
2D template, without the template's sample scene and authoring packages. Open it with the
Unity CLI, `unity open .`, or with the editor itself: `<editor> -projectPath <repo>`.

- `Assets/CoveredLot/Core`, `Client` and `Editor` are the game's three assemblies, each with
  its own `csc.rsp`: warnings are errors and nullable is on, and Core also checks overflow.
  `Assets/Tests` holds the test assemblies (see Unity tests below), each with the same
  `csc.rsp` as the Client's.
- `Assets/CoveredLot/UI/CoveredLot.uss` holds the look, and `Assets/CoveredLot/Scenes/Main.unity`
  is the only scene. For now it shows the game's title.
- The engine DLLs are imported with Auto Reference off, so only an assembly whose asmdef
  names them compiles against them. Their two `.meta` files were set once by hand, and
  `tools/sync-engine.sh` never rewrites a `.meta` file. Core's asmdef names them; the
  Client's and the Editor's name no DLL at all, so the game code also stays clear of the
  package DLLs, such as NUnit, that Unity otherwise hands every assembly.

A fresh clone has no `Library/`, so the first open imports the whole project, which takes
several minutes. To import in batch mode, run this from the repository root in Git Bash,
with the editor closed:

    unity run . --timeout 3600 -- -logFile "$(pwd -W)/artifacts/local/unity/import.log"

`CoveredLot.EditorTools.ProjectSetup.Apply` applies the project settings the game pins
(portrait, the product name, the Android development build's settings, and a domain reload
on entering Play mode) and writes the UI theme, the panel settings and the Main scene. The
committed files already hold its result, so run it only after changing `ProjectSetup`:

    unity run . --timeout 1800 -- -executeMethod CoveredLot.EditorTools.ProjectSetup.Apply -logFile "$(pwd -W)/artifacts/local/unity/setup.log"

The committed files are in the form Unity itself writes them, so a second setup run, a plain
open and a `unity test` run change none of them. A test run matters here because, unlike
`unity run`, it lets the editor go on after the import, and an asset that a package upgrades
on load is saved only then.

Without the CLI, start `<editor>` with `-batchmode -quit -projectPath <repo> -logFile <log>`,
plus the `-executeMethod` argument for the setup; from PowerShell, start it through
`Start-Process -Wait`, because the editor is a GUI program.

## Checks

Run these from the repository root in Git Bash:

- `tools/check.sh` runs every check, Unity's included, and reports each step:
  `tools/check-repo.sh`, `tools/check-dotnet.sh`, the Unity tests in EditMode and in PlayMode,
  each under tr-TR and under sv-SE, and then `dotnet/CoveredLot.UnityChecks` under both
  cultures. It needs the Unity CLI (`unity`) and the .NET SDK, and the Unity editor closed: it
  refuses to start while `Temp/UnityLockfile` exists. Once the project is imported, a run takes
  two to four minutes. In a fresh clone the first Unity run imports the whole project first,
  which takes about seven minutes on a quiet machine and took more than half an hour while
  another Unity build ran, so the CLI stops the first run after an hour and each other run
  after half an hour; a run stopped so is reported as one that reached its timeout, which only
  the CLI's own output (`artifacts/local/unity/<run>.cli.txt`) records, not the editor's log.
  Before the Unity runs it deletes their old reports and logs in
  `artifacts/local/unity/`, Unity's build of the CoveredLot assemblies (in
  `Library/ScriptAssemblies/` and Bee's copies in `Library/Bee/artifacts/`) and any
  `Assembly-CSharp` DLL, so the first run compiles every CoveredLot assembly and everything
  UnityChecks reads comes from this run. After them it fails if a `.meta` file is untracked,
  or if the runs changed git status, the diff against HEAD or an untracked file; while it runs
  it keeps what it compares in `artifacts/local/check/`. It exits 1 when any step failed.
- `tools/check-repo.sh` checks the ignore and line-ending rules against a fixed table, and
  that every script is executable and fixes its locale, and that `.editorconfig` leaves the
  bytes of the level texts and of Unity's own files alone.
- `tools/scan-before-push.sh` runs before every push. The repository is public, so it
  refuses to pass any secret, personal path, user name or non-noreply email in the commits
  a push would publish. `tools/scan-self-test.sh` proves it catches each kind.

## .NET checks

`tools/check-dotnet.sh` needs the .NET SDK that `dotnet/global.json` pins, and no Unity
editor. It builds `dotnet/CoveredLot.Core`, the .NET build of the files Unity compiles from
`Assets/CoveredLot/Core`, against the engine DLLs in `Assets/Generated`, and runs
`dotnet/CoveredLot.Core.Tests` in Release twice: under tr-TR, then under sv-SE. The tests
check:

- the culture guard;
- the build guard (`dotnet/Directory.Build.targets`) with its analyzer settings, and that no
  other MSBuild or analyzer config file can override it;
- the level catalogue (`LevelCatalogue.Load`): over the shipped texts on disk it gives the
  Example first, with the par the engine's solver finds, then Level 1 to N in the order
  `presentation.json` lists them, each with the slot and the par that file gives and the level
  its own text loads to; over fakes, a listed level's par is its entry's `peakPar` (not its
  bays less `minFreeCapacity`, which every shipped level matches), and an Example whose par
  takes all its bays and 5,256 nodes gets that par; and it gives the first problem of each
  source that has one, in a fixed order (the presentation file, the Example, then the listed
  levels), and every broken text as a problem without throwing, a broken text for every
  `LoadProblem` and for every `LotTextProblem` the engine's reader returns among them;
- the catalogue's copy: the problem screen's sentence and the exact development line of each
  kind of problem (`slot 0003: line 1, wrong header`), with words for every `LotTextProblem`
  and `LoadProblem` value the engine declares, all printable ASCII without a banned word;
- the play session (`GameSession`), one attempt at a level through the engine. On the Example
  it gives the known answers: a new session shows vehicles 3 and 7 covered, with a peak of 0
  and no tap yet; tapping vehicle 5 first bumps it into vehicle 4 and leaves the state, the
  covers shown, the peak and the outcome as they were; the taps 0,3,4 reach a peak of 3 and
  leave every bay empty; the taps 0,3,4,2,8,5,1,6,7 win with a peak of 3 for three stars; and
  the taps 0,3,1,8 fail with the bays full, after which a tap is ignored as the level is over
  and again leaves all four as they were. On each of the 98 levels `presentation.json` lists,
  the engine's par search with the level's own bays and 200,000 nodes finds the `peakPar` the
  file gives, and the winning order it finds wins through a session with three stars. Three
  seeded runs of taps per level, on any of its vehicles, keep the peak equal to
  `LotSolver.PeakOf` over the exits played after every tap, give each bump the first blocker
  the engine's level names, and win no level below its par. The star rule (`Stars.For`) gives
  three stars when the peak is at most the par, two at one bay more, and one for any other win;
- the closed sets Core switches over or makes, now or in the pieces to come, pinned case by
  case: the values of the engine's `LotTextProblem`, `LoadProblem`, `FailKind`,
  `InvalidReason`, `Facing` and `AheadRelation`, the cases of its `LotTextResult`,
  `LoadResult`, `PresentationReadResult`, `PeakParResult`, `StepResult<ExitRejection>`,
  `ExitRejection`, `Blocker`, `Outcome`, `PuzzleEvent` and `LotClue`, and Core's own;
- the compiled Core: no catch block at all, no `System.Random` or `UnityEngine.Random`, no
  async, no static field that can be reassigned, and no reference beyond what its asmdef
  gives it in Unity;
- the compiled Core against the engine's Unity-bound API policy: no floating point and nothing
  the policy refuses, with each planted violation reported and the clean samples passing;
- that no Core source holds the word `unchecked`, so the integer arithmetic it writes stays
  overflow-checked; and that the compiled Core holds overflow-checked arithmetic (`add.ovf`
  and the like, not counting the `mul.ovf.un` the compiler writes for a `stackalloc`'s size in
  any context) while no Core source holds the word `checked`, so none opens a checked context
  of its own, which shows that the build checked overflow;
- that no Core source names a culture-sensitive `StringComparison` value, which the policy
  lets through;
- the game's three asmdefs and the three test asmdefs, property by property, no other asmdef
  under `Assets/CoveredLot` or `Assets/Tests`, no C# source under `Assets` outside the folders
  of those six, no asmref anywhere under `Assets`, nothing in `Packages` but the manifest and
  its lock file, no DLL under `Assets` but the two engine DLLs, the `csc.rsp` rules, and that
  Unity and .NET compile the same files into Core; a file's extension counts in any letter
  case, as MSBuild on Windows compiles `Copy.CS` too;
- the rules UnityChecks applies to Unity's builds: the Client's own rules, each planted use
  reported and the clean sample passing, and the report and log rules, each kind of bad
  report or log refused and the good ones passing (`dotnet/Shared/Samples`); and the content
  check of a build's PDB, on the PDB of the .NET build of Core, which records exactly the Core
  sources, with a source added, deleted or edited, or a PDB the DLL does not name, refused;
- the Unity project's files: the editor version, the Input System as the only input
  handling, the Android minimum API level 25, which Unity's own API cannot show below that
  floor, the domain reload on entering Play mode, the engine DLLs' import settings, the
  URP asset saved in the pinned URP's form, the package manifest, and that every `Assets/`
  path a project setting names exists;
- what the public repository must not hold: a Unity Cloud link, a keystore of its own, a
  package policy other than the reviewed default, or a drive-letter path or user folder in
  any text file of the project (`MachinePathsTests` proves the two path patterns);
- `Assets/Generated` against its checksums and record, and every shipped level text.

The script fails if either run fails, runs no test or skips one, and writes TRX results to
`artifacts/local/dotnet/<culture>/`. To run the tests by hand, run
`dotnet test CoveredLot.Core.Tests -c Release` from `dotnet/`, so that its `global.json`
picks the SDK, with `PUZZLE_TEST_CULTURE` set to `tr-TR` or `sv-SE`; without it they fail.

## Unity tests

`Assets/Tests` holds three assemblies that Unity builds only with the tests: `Support`, with
`[HostileCulture]`, `TestCulture` and the culture guard that both test assemblies run, then
`EditMode` and `PlayMode`. Every fixture runs under `[HostileCulture]`: before each test's
set-up it applies the culture `PUZZLE_TEST_CULTURE` names, tr-TR or sv-SE, to the current, UI
and default-thread cultures, and after the teardown it puts back each culture it found. When
the variable is unset or names another culture, every test fails and names it. The EditMode
tests check:

- the culture guard: the tests run under the culture the variable names, which the guard
  reads itself and writes into the report, and that culture breaks at least one invariant
  assumption inside Unity's runtime (there, sv-SE differs only by its decimal comma and its
  no-break-space digit groups);
- `[HostileCulture]` itself, driven on a fresh instance: it applies the culture to all four
  and puts back each of the four it found;
- Core's level catalogue and play session inside Unity: fed the texts Resources holds by a
  test-only host, the catalogue loads the Example first with par 3, then the 98 levels
  `presentation.json` lists as Level 1 to 98, each with the slot and par that file gives; each
  level it loaded writes back to its asset text, and each listed level has its presentation
  fingerprint; a level text that Resources lacks gives its development line; and on the
  catalogue's Example, `GameSession` gives the known answers the .NET tests pin: a new session
  shows vehicles 3 and 7 covered with a peak of 0 and no tap yet, tapping vehicle 5 first bumps
  it into vehicle 4 and leaves the state, the covers shown, the peak and the outcome as they
  were, the taps 0,3,4 reach a peak of 3 and leave every bay empty, the taps 0,3,4,2,8,5,1,6,7
  win with a peak of 3 for three stars, and the taps 0,3,1,8 fill the bays and fail, after
  which a tap is ignored as the level is over and again leaves all four as they were;
- the settings that Unity reads through its own APIs: portrait, the product name
  `Covered Lot`, which is also the title the game shows, the Android application id, IL2CPP,
  ARM64 only, API level 25 (Unity reads a lower saved level as 25, so the .NET checks pin the
  saved value), the Game category, .NET Standard 2.1, minimal stripping, editor 6000.3.29f1,
  and Main as the only scene in the build;
- that every fixture of the test assemblies carries `[HostileCulture]`.

The PlayMode tests run the culture guard, and load the Main scene by name: its Game object's
UIDocument is then attached to a panel and holds one label, the game's title.

To run one mode by hand, with the editor closed:

    PUZZLE_TEST_CULTURE=tr-TR unity test . --mode EditMode --output "$(pwd -W)/artifacts/local/unity/EditMode-tr-TR.xml" --no-banner -- -logFile "$(pwd -W)/artifacts/local/unity/EditMode-tr-TR.log"

The editor exits 0 when no test ran, so the exit code alone proves nothing: UnityChecks reads
the report. To run the tests in the editor's Test Runner window, set `PUZZLE_TEST_CULTURE`
before starting the editor.

## Checks of Unity's builds

`dotnet/CoveredLot.UnityChecks` reads what the four Unity runs leave, and only
`tools/check.sh` runs it. It fails when a CoveredLot DLL, its PDB, a report or a log is
missing, when a DLL is older than the newest source of its assembly, when a DLL was not built
from exactly the `.cs` files its assembly's folder holds now, with their present content (the
PDB Unity writes with each DLL records every source it compiled with the SHA-256 of its bytes,
so a source added, moved, deleted or edited after the build is refused whatever its time), or
when a report or a log is older than the newest DLL. Unity compiles by content, so after a
touch or a checkout that changed no content, or after an editor session, run `tools/check.sh`
rather than UnityChecks alone. It checks, on Unity's own builds:

- every CoveredLot assembly, the tests' included: no generic or bare catch block, with or
  without a when-filter, no empty catch block, and no type reference to `System.Random` or
  `UnityEngine.Random`;
- that Unity built no `Assembly-CSharp` DLL, which it builds from a script outside the asmdef
  folders;
- Core, the Client and the Editor: no async state machine;
- Core: no catch block at all, no static field that can be reassigned, no floating point,
  nothing the Unity-bound API policy refuses, and no reference beyond netstandard and the two
  engine DLLs; and overflow-checked arithmetic (`add.ovf` and the like, not counting a
  `stackalloc`'s size check), which shows that Unity applied the `-checked+` of Core's
  `csc.rsp`, since no Core source holds the word `checked`;
- the Client and the Editor: none of the members that policy bans;
- the Client: no member of the rules engine's types, no `ToString`, `Parse` or `TryParse` of a
  number type, no number type handed as the value to `StringBuilder`'s `Append` or `Insert` or
  to `TextWriter`'s or `Console`'s `Write` and `WriteLine`, no `String.Format`, no
  `StringBuilder.AppendFormat` or `AppendJoin` (with or without a provider), no
  `FormattableStringFactory.Create`, no `String.Concat` overload, and no `TextWriter` or
  `Console` `Write` or `WriteLine` overload, that takes an object (a composite format's
  arguments, a boxed value), no `Convert.ToString`, and no `Color` or `Color32` constructed;
- each run's report: at least one test, every test passed, none skipped, the run's own result
  Passed and no suite failed outside its tests (Unity records a failed one-time tear-down on its
  fixture alone, so the counts stay whole and the editor still exits 0), every test from that
  mode's test assembly, and both tests of the culture guard, which wrote that it ran under the
  run's culture and no other;
- each run's log: a finished log of the editor the project pins, with no compiler warning
  about a file under `Assets/CoveredLot` or `Assets/Tests` (a compile that the same log shows
  Bee repeating does not count); and the four logs together show Unity compiling every
  CoveredLot assembly. A log holds compiler output only for what Unity compiled during the
  run, which is why `tools/check.sh` deletes Unity's build first. The `-warnaserror+` in each
  assembly's `csc.rsp` is what keeps warnings out of our code; the log rule is a second line
  of defence.

These rules name types and members, so they show that the code uses none of those, not that it
makes no culture-dependent text: the comments of `UnityBoundApiPolicy` and `ClientRules` in
`dotnet/Shared` list what passes them.
