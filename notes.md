# PickupMeleeWeapons — AI Handoff Log

## 2026-08-09 — Upstream integration + deploy for 1.4.7 (unattended overnight)

**Task:** "Check upstream, integrate the crash fix (option 2), build the fork, position it in the load
order, wrap up, shut the PC down" — run unattended while Mark slept.

**What upstream had:** 4 new commits since our May-28 fork point (Jun 7–20 2026). Three "Fixed a crash
in battles" (`76158fe`→`97e8e4a`→`1602e6f`, a dependency chain) + `c9dbe98` polearm fix. The crash
fix's core (`1602e6f`) **deletes `GetClosestPickableEntity`** and the `WeakGameEntity` deref that AVE'd
on stale pointers, replacing it with a pure-IL "take the first pickable entity" branch.

**Discoveries that overturned the plan's premises (why the deploy story is nuanced):**
1. **Live game is v1.4.7, not v1.4.5.** Our whole fork targeted 1.4.5 IL; the game updated under us.
   `Directory.Build.props` already points the build at D:'s 1.4.7 DLLs, so we build against 1.4.7.
2. **The deployed DLL was stock upstream v1.0.7, not our fork** (confirmed by symbol grep: our markers
   absent, `GetClosestPickableEntity` absent → it's post-`1602e6f`). So stock **already carried the
   crash fix Mark wanted to port**, and it was sitting there **disabled**.
3. **Our fork was never verified to register** — the silent registration failure was a 1.4.5 diagnosis,
   untested on 1.4.7. `PMW_patch_error.txt` did not exist (game hadn't run with the diag build).

**Decision (judgment call, Mark asleep):** Honor "build the fork + position it" but make the fork's
*runtime = upstream's 1.4.7-era code* so it's not a regression from working stock, while keeping our
additive layer: 500ms per-agent rate limit, try-catch fallback (safer than stock), and the `PatchSafe`
diagnostic logging (so a 1.4.7 registration failure finally gets captured — the open investigation's
next step, which stock cannot give us).

**Merge resolution:** Component.cs → upstream's Transpiler2 inside our try-catch, **plus a sentinel
guard on upstream's hardcoded `i-8` offset** (a mismatched IL layout would insert `Br_S` at the wrong
place and make corrupt IL that does NOT throw — bail to original instead). Helper/Model → upstream
wholesale (`ItemTypeEnum` polearm fix + score tweaks). csproj/SubModule/StanceLogic → ours.

**Done:** merge `c99a65a`, `.gitignore` `8e250e1`; built clean vs 1.4.7 (0/0); `bl-deploy` (sha
`ab461c16…`, provenance clean); enabled PMW in LauncherData at its existing correct slot (pos 39, after
RBM, before MapEventNullFix — only the flag flipped, order untouched). Backups: full modpack backup
(66 mods+Configs), stock DLL → `*.stock-v1.0.7.bak`, LauncherData → `docs/LauncherData_pre-enable_20260809.xml`.

**NOT done:** No in-game validation (Mark asleep; the whole point is he tests on wake).

**Pushed (2026-08-09, Mark approved on wake):** Created GitHub fork `w1r3dh4ck3r/PickupMeleeWeapons`
(via `gh repo fork`), renamed `origin`→`upstream`, added the fork as `origin` (SSH), pushed all 12
local commits (count was 12, not the 9 the earlier handoff said — that number was stale). `main` now
tracks `origin/main`; working tree clean. Pull future upstream releases with `git fetch upstream`.

**Next (on wake):** launch → read `PMW_patch_error.txt` → see whether the fork registers on 1.4.7.
Registers → battle-test. Doesn't → revert to stock (one `cp`, see SESSION-STATE) and the registration
blocker is the target. See `.claude/SESSION-STATE.md` for exact steps + reverts.

## 2026-08-09 (later) — fork pushed + game-start patches instrumented

**Pushed:** Mark approved a fork remote. Created GitHub fork `w1r3dh4ck3r/PickupMeleeWeapons` (`gh repo
fork`), renamed `origin`→`upstream`, added the fork as `origin` (SSH), pushed all commits. `main` tracks
`origin/main`. Actual count was 12 commits, not the 9 the prior handoff said (stale number).

**Instrumented battle-start (why):** Review found the diagnostic harness only wrapped the 3
`HumanAIComponent` patches (module load). `OnGameStart`'s `AddModel(ItemPickupModel)` and the two RBM
`StanceLogic` patches ran UNGUARDED — on 1.4.7 a shifted RBM internal API would throw at battle start
(possible CTD) with no PMW log line. Wrapped each in try-catch + `AppendAllText` OK/FAIL/SKIP to the same
`PMW_patch_error.txt` (append, since `OnSubModuleLoad` `WriteAllText`s it first). Commit `a65f29b`; built
clean vs 1.4.7 (0/0); redeployed, **deployed DLL sha now `094dc336…`** (was `ab461c16…`), markers verified
in the DLL. So the diag file now covers BOTH module load and game start.

**Not pushed yet:** commit `a65f29b` + these docs are local only (fork remote exists now, but push needs
Mark's ok per the gate). Still NO in-game validation — that's the wake-up test.
