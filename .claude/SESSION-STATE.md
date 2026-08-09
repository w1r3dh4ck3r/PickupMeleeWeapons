# PickupMeleeWeapons — Session State

## Key facts (read these first)
- **Live game is v1.4.7, NOT v1.4.5.** The old `docs/v1.4.5-port-handoff.md` is obsolete — the game
  updated under us since the fork's May 2026 work. That doc's diagnostic hypotheses are still useful;
  its version/status is not.
- **Stock upstream v1.0.7 already carried the crash fix** Mark asked to port (`1602e6f` deleted the
  `GetClosestPickableEntity`/`WeakGameEntity` deref that AVE'd). It was deployed but **disabled**.
- **The fork was never verified to even register.** Its silent patch-registration failure was diagnosed
  on 1.4.5 and is **UNTESTED on 1.4.7**. So the fork is still *expected to possibly do nothing* until
  the wake-up launch proves otherwise.
- Origin remote is OrderWOPower's repo (upstream), NOT a fork remote. **Do not push** — needs Mark's
  decision on where our 9 local commits should live.

## Current Task
Integrate upstream v1.0.7 crash fix into the fork, build against 1.4.7, deploy + enable for Mark to
test on wake. DONE (build-verified; NOT in-game validated — Mark asleep, waived to wake).

## Last Action
Merged upstream (`c99a65a`), added `.gitignore` (`8e250e1`), built clean against 1.4.7 (0 err/0 warn),
deployed fork DLL via `bl-deploy` (sha `ab461c16…`, provenance `main@8e250e1`), and **enabled PMW in
LauncherData** (position 39 — already correctly slotted after RBM, before MapEventNullFix; only the
IsSelected flag flipped, load order untouched).

## Next Step (on wake — this is the open investigation's real next step)
1. Launch to main menu with PMW enabled, then **Read `C:\Users\w1r3d\AppData\Local\Temp\PMW_patch_error.txt`**.
   The fork's `PatchSafe` harness writes one line per patch: `[PMW] OK:` or `[PMW] FAIL: <exception>`.
   This finally reveals whether the fork registers on 1.4.7 and, if not, why.
2. If it registers → test in a battle (troops pick up dropped melee weapons; watch for crashes).
3. If it FAILs or does nothing → the registration blocker is the target; the stock v1.0.7 that already
   works is the fallback (revert below).

## Revert (one line each)
- **DLL → stock v1.0.7:** `cp ".../Modules/PickupMeleeWeapons/bin/Win64_Shipping_Client/PickupMeleeWeapons.dll.stock-v1.0.7.bak" ".../PickupMeleeWeapons.dll"`
- **Disable again:** flip `<IsSelected>true</IsSelected>` → `false` in PMW's block (line ~160) of
  LauncherData.xml. Pre-enable backup: `docs/LauncherData_pre-enable_20260809.xml`.

## Files to touch next
- `C:/Users/w1r3d/AppData/Local/Temp/PMW_patch_error.txt` (READ on wake — the diagnostic output)
- `PickupMeleeWeaponsSubModule.cs` (the PatchSafe registration harness — if a FAIL is logged, fix here)
- `PickupMeleeWeaponsComponent.cs` (Transpiler2 IL if the failure is transpiler-side on 1.4.7 layout)
