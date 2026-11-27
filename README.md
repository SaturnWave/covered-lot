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
- `Assets/CoveredLot/UI/CoveredLot.uss` holds the whole look: every colour, font size, border
  and spacing. `Assets/CoveredLot/Scenes/Main.unity` is the only scene. For now it opens the
  Example, the rules board, on the play screen; the level list comes later.
- The engine DLLs are imported with Auto Reference off, so only an assembly whose asmdef
  names them compiles against them. Their two `.meta` files were set once by hand, and
  `tools/sync-engine.sh` never rewrites a `.meta` file. Core's and the Client's asmdefs name
  them, and the Editor's names no DLL at all; none of the three takes the package DLLs, such
  as NUnit, that Unity otherwise hands every assembly.

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
  the taps 0,3,1,8 fail with the bays full. After the win and after the fail, a tap is ignored
  as the level is over and leaves the state, the peak and the outcome as they were. On each of
  the 98 levels `presentation.json` lists, the engine's par search with the level's own bays
  and 200,000 nodes finds the `peakPar` the file gives, and the winning order it finds wins
  through a session with three stars. Three seeded runs of taps per level, on any of its
  vehicles, keep the peak equal to `LotSolver.PeakOf` over the exits played after every tap,
  give each bump the first blocker the engine's level names, ignore a tap only on a vehicle
  that has left the lot (`NotInLot`), and win no level below its par. The star rule
  (`Stars.For`) gives three stars when the peak is at most the par, two at one bay more, and
  one for any other win;
- the board and tag pictures (`BoardPicture`, `CluePicture`), what the board and the tags list
  show of a session. On the Example they give the known answers: each vehicle's box and label,
  from `0 (0,1,2x1) B^` to `8 (5,3,1x2) Y>`, and the pillar at (0,4); vehicles 3 and 7 shown
  covered with no colour and the others open in their own swatch; vehicle 3's badge 1 and its
  row of the tags list, `1: not R`; after the taps 0 and 3, a ghost for each, vehicle 3's `Bv`
  with badge 1 at (2,2,2x1), and the row still there; and a bump of vehicle 5 marks tile 4 as
  its blocker until the next tap. On a variant tagged `Not(3,R);Not(3,Y);Ahead(7,Same);Touch(7,Y,2)`,
  which the engine writes back as given and whose tags are all true of the board, the rows read
  `1: not R, not Y` and `2: same as ahead, touches 2 Y`, and the one link runs from cell 27 to
  cell 24 and stays as vehicles 6 and 7 leave; on another, an east-facing tagged vehicle's link
  starts at the highest cell of its footprint and its row reads `differs from ahead`. Along the
  winning order the par search finds for each shipped level, the Example included, the board
  has a tile for each vehicle in the lot and none for any other, a tile the session shows
  covered is covered with `?` for its letter, every other tile is open in its vehicle's swatch
  and letter, and a covered level shows a ghost for each vehicle that has left, a level without
  covers none; and each Ahead tag of the shipped levels links its vehicle's front cell to the
  first cell of the vehicle ahead on its path, as the engine's geometry gives them. The palette
  gives the colour ids 1 to 6 the six swatches with the letters R, B, Y, G, P and O, any other id
  throws, each swatch has a USS class of its own, and no two style names are the same;
  `CoveredLot.uss` has a rule for each of the 43 classes the style names give, and names no
  other `cl-` class;
- the HUD and the end card (`HudPicture`, `EndPicture`) and their copy. On the Example the HUD
  reads `4 bays free` and `Most bays used: 0 (par 3)` at the start, over four empty bays, with
  the ledger `R 1`, `B 1`, `Y 0` and the row `1: not R`; after the taps 0 and 3 the ledger
  reads `R 1`, `B 0`, `Y 0` and the status `Cover lifted: blue.`; after 0, 3 and 4, `Three blue
  cleared.`; after 0, 3 and 1 the bays hold B, B, R and an empty one, with `1 bay free`; and a
  bump reads `Something is in its way.`. Along the winning line the status names each lifted
  cover and cleared set, joined by a space when one tap does both, and on the Example with its
  blue vehicles made green, pink or orange it names those colours. Level 1, which has no
  covers, shows no ledger and no tags list. The winning line's card is the win card with three
  stars, `3 stars` and `Most bays used: 3 (par 3)`, and the taps 0,3,1,8 give the fail card's
  `All bays are full.`; a win one bay over its par reads `2 stars`, two over `1 star`, and a
  fail with no move left, which no loaded level reaches, `No move is possible.`. The buttons
  and the headings read as the design gives them, and each entry is titled `Example` or
  `Level n`. Along the winning order of every shipped level and in the seeded runs, every
  string of the board, the HUD and the end card, found by walking the pictures, is printable
  ASCII and holds none of the banned words (`TextRules`: jam, parking, bus, tray, board and
  block, in any letter case); in the seeded runs, after every tap, the HUD's bays also hold
  what the state's bays hold, its ledger counts the vehicles the engine's rules call covered,
  and each tile lies over exactly its vehicle's footprint;
- the screen flow, the board's geometry, the safe area and the development line
  (`Navigator`, `BoardGeometry`, `SafeInsets`, `DevFacts`). A catalogue that loads starts on
  the level list, and a broken one on the problem screen, which no action leaves; `Open` plays
  the entry at its index with a new session, and an index outside the list changes nothing;
  `Next` from the Example plays Level 1, and after the last entry it is None and changes
  nothing; `Again` plays the level once more with a fresh session, at peak 0 with its covers
  back; and `BackToList` shows the list again. For each of the 16 lot sizes, 5x5 to 8x8, in a
  phone's frame of 344x420 units, a short one of 344x300 and a wide one of 760x600, the cells
  are square, 2 units apart and the largest that fit, the board is centred in the frame, and
  each tile's rectangle spans exactly its vehicle's cells; in the phone frame the square lots
  get cells of 67, 55, 47 and 41 units. On a 1080x2400 screen a safe area 63 pixels up and
  2247 high pads the 360x800 panel 30 units at the top and 21 at the bottom, part of a unit
  takes the whole unit, and a safe area past the screen's edge, or a screen with no pixels,
  pads nothing. In Level 25 the development line reads, exactly, `slot 0024 | par 4 | tr-TR |
  1080x2400 | safe 0 63 1080 2247 | cell 47`, and the culture button cycles the device's
  culture, tr-TR and sv-SE;
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
`[HostileCulture]`, `TestCulture`, the culture guard that both test assemblies run and
`ShownElements`, which reads what a screen shows, then `EditMode` and `PlayMode`. Every fixture runs under `[HostileCulture]`: before each test's
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
- Core's level catalogue and play session inside Unity: fed the texts Resources holds by the
  Client's host, `ResourcesLevelTexts`, the catalogue loads the Example first with par 3, then the 98 levels
  `presentation.json` lists as Level 1 to 98, each with the slot and par that file gives; each
  level it loaded writes back to its asset text, and each listed level has its presentation
  fingerprint; a level text that Resources lacks gives its development line; and on the
  catalogue's Example, `GameSession` gives the known answers the .NET tests pin: a new session
  shows vehicles 3 and 7 covered with a peak of 0 and no tap yet, tapping vehicle 5 first bumps
  it into vehicle 4 and leaves the state, the covers shown, the peak and the outcome as they
  were, the taps 0,3,4 reach a peak of 3 and leave every bay empty, the taps 0,3,4,2,8,5,1,6,7
  win with a peak of 3 for three stars, and the taps 0,3,1,8 fill the bays and fail; after the
  win and after the fail, a tap is ignored as the level is over and leaves the state, the peak
  and the outcome as they were;
- the settings that Unity reads through its own APIs: portrait, the product name
  `Covered Lot`, which is also the title the game shows, the Android application id, IL2CPP,
  ARM64 only, API level 25 (Unity reads a lower saved level as 25, so the .NET checks pin the
  saved value), the Game category, .NET Standard 2.1, minimal stripping, editor 6000.3.29f1,
  and Main as the only scene in the build;
- that every fixture of the test assemblies carries `[HostileCulture]`;
- the play screen, clicked as a player clicks it, in a panel of the UI Test Framework that no
  window shows, 360 x 800 units with the game's own theme (Unity's default runtime theme, then
  `CoveredLot.uss`), over the catalogue `ResourcesLevelTexts` loads, with the Example open:
  clicking tile 5 outlines tile 4 alone and the status reads `Something is in its way.` until
  the next tap; the winning line shows the win card with `Cleared!`, `3 stars` and `Most bays
  used: 3 (par 3)`, and Play again, Next level and Levels; Next level then opens Level 1, a
  plain level of nine vehicles and 5 bays, where, once its vehicle 3 has left, 8 tiles show,
  the bays read Y and four empty ones with `4 bays free` and `Most bays used: 1 (par 3)`, and
  no ledger, tags list, badge, covered tile, ghost or card shows; the taps 0,3,1,8 show the
  fail card, `All bays are full.` with Retry and Levels, over the lot and filled with the page
  colour, so nothing shows through it and a click at the centre of each of the 5 tiles left
  reaches the card; where the fail left the bays B, B, R and Y with `0 bays free`, Retry then
  shows four empty bays with `4 bays free` and `Most bays used: 0 (par 3)`, nine tiles, tile 3
  covered as `?v`, the ledger `R 1`, `B 1`, `Y 0`, no blocker outline and no card; a bump
  followed by Restart leaves no blocker outline and an empty status line; after the taps 0 and
  3 the ghosts of 0 and 3 show, the ghost of 3 reading `Bv` with badge 1, and the tags list
  keeps its row `1`, `not R`; an entry made from the variant text tagged
  `Not(3,R);Not(3,Y);Ahead(7,Same);Touch(7,Y,2)` shows one link, from cell 27 to cell 24,
  and draws its arrow; in play and with each card, every button shown is at least 48 x 48 units and every label at least 14, a badge 12, with the
  counts of buttons and labels pinned; and the title, the status line, the HUD's lines, the
  headings, the pills and the tags show the ink of `CoveredLot.uss`, not a colour of the default
  theme;
- how the Client makes Unity's floats whole and non-negative (`Whole`): rounded down or up, and
  0 for a NaN, a negative value or zero.

The PlayMode tests run the culture guard, and load the Main scene by name. Its Game object's
UIDocument is then attached to a panel and shows the play screen of the Example alone, titled
`Example`. Clicked through the UI Test Framework on that document, the winning line shows the
win card, and Play again brings the covers of vehicles 3 and 7 back on all nine tiles. After
the taps 0 and 3, the document disabled and enabled again makes a new root element, and the
play screen comes back in it with the same session: the status `Cover lifted: blue.`, seven
tiles, and the ghosts of 0 and 3. The editor's batch-mode screen is 640 x 480 pixels, a panel
of 360 x 270 units with the game's panel settings, which are matched on the width, and a
runtime panel drops a click below its bottom edge; so these click tests give the document a
copy of its panel settings matched on the height, a panel 800 units high, and give it its own
back afterwards.

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
