# CLAUDE.md — PSIV Remake (Unity)

## Project overview
A dungeon-crawling RPG in Unity inspired by Phantasy Star IV. Original project, not a direct remake. Currently focused on battle and character systems.

## Architecture

### Core layers
- **Data Layer**: ScriptableObjects (CharacterDefinition, BattleActorVisualCatalog, AttackData)
- **World/Mode Layer**: GameModeManager, BattleGateway, EncounterTrigger, ExplorerHealth, PlayerController
- **Runtime Layer**: PartyManager, BattleManager, BattleFactory, StatCalculator, GrowthSystem (future)
- **Presentation Layer**: BattleUI, PartyStripController, PartyStripEntry

### Battle system script map
- `BattleManager.cs` — battle flow, turn/phases, damage resolution, applying HP; waits for full popup lifetime after each action
- `BattleUI.cs` — menus, input, visual sequencing, party strip + enemy name bar + damage popup integration
- `PartyStripController.cs` — 5-tile party strip, global status icons, CastAnchor per tile, portrait hit flash
- `PartyStripEntry.cs` — per-tile rendering (name, HP/TP, status icon, class badge, portrait)
- `EnemyNameBar.cs` — manages the row of enemy name boxes at top of battle screen; rebuilds from living enemies each command phase, hides on action phase
- `EnemyNameBox.cs` — single enemy name label with pop-in/pop-out animation; `RequireComponent(UiPopAnimation)`
- `UiPopAnimation.cs` — scale pop animation (0→1 show, 1→0 hide) on a RectTransform; pivot forced to (0.5,0.5); `animateBothAxes` flag for XY pop vs X-only wipe; exposes `ShowDuration`/`HideDuration` for lifetime calculations; `Show()` activates + plays show anim; `Hide()` plays hide anim then deactivates — use these instead of `SetActive` on any animated UI element
- `DamagePopup.cs` — single-use floating number; sets text+color, plays show anim → hold → hide anim → destroys self; `RequireComponent(UiPopAnimation)`
- `DamagePopupManager.cs` — spawns damage/heal popups for party and enemies; party popup uses CastAnchor screen position; enemy popup converts VfxAnchor world X via `Camera.WorldToScreenPoint` to canvas X with fixed Y band; exposes `TotalLifetimeSeconds` for BattleManager timing
- `BattleActorVisualCatalog.cs` — ScriptableObject mapping characterId + actionType + weaponStyle → VFX entries
- `BattleVfxSprite.cs` — sprite-animated VFX with caller-controlled lifetime
- `BattleUiVfx.cs` — tiny MonoBehaviour helper; stores `durationSeconds` for UI VFX cleanup in BattleUI
- `HitReactionGroup.cs` — shader-based enemy hit flash (_FlashAmount) on all renderers in group
- `BaseEnemyAI.cs` — enemy prefab controller, owns VfxAnchor, animation events; handles both Exploration and Battle modes
- `BattleTypes.cs` — BattleCombatant, BattleResult, BattleRewards, BattleOutcome, WeaponStyle, WeaponStyleUtil, DamageFactor, DamageFactorInfo, HandItemType
- `BattleFactory.cs` — static helper; converts `List<BaseEnemyAI>` → `List<BattleCombatant>` for BattleManager
- `PartyManager.cs` — singleton, owns activeParty (CharacterData), builds BattleCombatant list
- `StatCalculator.cs` — centralized stat formulas (ATK/DEF/MAG/MRES/HIT/EVD/CRIT)

### Battle shader assets
- `Resources/Battle/SG_SpriteHitFlash_preserve-lines-white-out.shadergraph` — ShaderGraph flash shader for enemy SpriteRenderers; uses `_FlashAmount` (0→1 blends to white)
- `Resources/Battle/UIFlash.shader` — Built-in Pipeline UI-compatible flash shader with the same `_FlashAmount` logic; required for portrait Image components (ShaderGraph Canvas target is URP-only)

### Exploration/world layer script map
- `GameModeManager.cs` — singleton enum (`Exploration`/`Battle`), fires `OnModeChanged` event; DontDestroyOnLoad
- `BattleGateway.cs` — singleton bridge between exploration and battle: disables exploration controls, swaps UI, calls `BattleFactory` + `BattleManager.BeginBattle()`, handles `OnBattleFinished` (victory/flee/defeat), syncs HP back via `PartyManager.SyncHPBackFromBattle()`
- `EncounterTrigger.cs` — `RequireComponent(BaseEnemyAI)`; proximity check → `BattleGateway.StartBattle()`
- `ExplorerHealth.cs` — HP for the exploration avatar; syncs bidirectionally with `PartyManager` leader; forwards damage and heals into `leader.currentHP`
- `PlayerController.cs` — first-person movement + camera bobbing; class is named `NewBehaviourScript` (rename candidate); freezes on `GameModeManager.IsBattle`
- `PlayerCombat.cs` — exploration-phase melee attack using Physics.OverlapSphere → `BaseEnemyAI.TakeDamage`; may be superseded by EncounterTrigger flow
- `Projectile.cs` — homing exploration projectile; hits `ExplorerHealth.TakeDamage` on collision

### Data layer script map
- `CharacterDefinitions.cs` — `CharacterDefinition` ScriptableObject (note: filename has an 's', class does not); holds identity, stats, leveling growth per character
- `AttackData.cs` — ScriptableObject with `attackName`, `damage`, `range`, `attackRate`, `projectilePrefab`, `DamageFactor`

### Legacy / dead code
- `EnemyHealth.cs` — old standalone HP component (`TakeDamage` → `Destroy`), predates `BaseEnemyAI`. Nothing in the current system calls it. **Candidate for deletion.**
- `Old Scripts/OLDEnemyAi.cs`, `OLD2EnemyAi.cs`, `OLD3EnemyAi.cs`, `OLDBaseEnemyAI.cs` — archived iterations, not active

### Portrait visibility rules
- **Battle start** (before first command): all portraits visible
- **Command selection**: only the hero currently choosing a command
- **Action phase**: no portraits — `RefreshStatus` is called immediately after `HideMenusForActionPhase()` to establish this consistently regardless of initiative order
- **Enemy targeting a party member**: that member's portrait is shown via `BattleUI.ShowTargetedPartyPortrait()` before the attack animation plays, so the player knows who is being attacked
- **Hit on a party member**: `PartyStripEntry.PlayPortraitHitFlash()` forces the portrait visible and flashes it; this runs independently of strip state and must not be removed
- Portrait visibility in `PartyStripController.RefreshStrip` is driven by `activeHeroIndex`: `-2` = none, `-1` = all, `>= 0` = only that index

### Damage popup rules
- **Player attacks enemy**: popup spawns on enemy after `PlayActionSequence_PlayerVsEnemy` completes and damage is applied; BattleManager waits `TotalLifetimeSeconds` before next action
- **Enemy attacks party**: popup spawns inside `PlayEnemyHitSequenceOnParty` after the portrait flash completes; portrait stays visible until popup finishes, then `RefreshStatus` hides it; BattleManager additionally waits `TotalLifetimeSeconds` after the flash wait
- Canvas is Screen Space Overlay — no camera needed for party popups (UI transform.position is already screen space); enemy popups use a serialized `battleCamera` reference (never `Camera.main`)
- Enemy popup Y is a fixed inspector-tunable value (`enemyPopupY`); only the world X of VfxAnchor is converted to canvas space
- `DamagePopupManager.battleCamera` must be assigned in the inspector — the system logs a warning and skips the popup if it's null
- Do NOT call `ShowCharacterDamagePopup` from both `OnEnemyHit` and `PlayEnemyHitSequenceOnParty` — they are the same code path; the call lives only in `PlayEnemyHitSequenceOnParty`
- String interpolation (`$"..."`) in BattleUI has caused smart-quote encoding corruption via the Edit tool; use concatenation (`+`) for any new `ShowMessage` calls in that file

### Menu animation rules
- `battleMenuPopup` and `comdMenuPopup` are controlled exclusively via `ShowMenu(_mainMenuAnim)` / `HideMenu(_comdMenuAnim)` helpers in BattleUI — never raw `SetActive`
- `ShowMenu` calls `UiPopAnimation.Show()` (activate + animate in); `HideMenu` calls `UiPopAnimation.Hide()` (animate out then deactivate)
- Both popups start deactivated in `OnEnable` — `PlayBattleIntroSequence` is the sole entry point that shows the main menu
- `BeginHeroTurn` skips `ShowMenu(_mainMenuAnim)` if the popup is already active (avoids restarting the animation when the intro already showed it)

### Battle intro / round-start sequence
- `BattleUI.PlayBattleIntroSequence(players, enemies)` is the single transition point used at both battle start and the top of every new round
- Flow: `RefreshStatus` (fires `ShowFromEnemies`) → `yield null` (ContentSizeFitter layout frame) → `WaitForSeconds(enemyNameBar.BoxShowDuration)` → `ShowMenu(_mainMenuAnim)`
- `EnemyNameBar.BoxShowDuration` reads `ShowDuration` from the box prefab's `UiPopAnimation` component
- `BattleManager.BattleLoop` yields on the intro at battle start; `ActionPhase` yields on it at the end of each surviving round instead of calling `RefreshStatus` directly
- **Known bug (#27)**: names and command menu are still animating simultaneously in some round transitions — not yet diagnosed

### Enemy name bar rules
- **Battle start**: names animate in via `PlayBattleIntroSequence` → `RefreshStatus` → `EnemyNameBar.ShowFromEnemies`
- **Command selection**: no change — `ShowFromEnemies` is a no-op if the living-enemy name list hasn't changed (cache check)
- **Action phase**: names animate out — `HideMenusForActionPhase` calls `EnemyNameBar.HideAll`
- **Next command phase**: names rebuild from surviving enemies — `ShowFromEnemies` detects cache mismatch and re-instantiates boxes; main menu appears after names fully open
- Layout: N=1 → left, N=2 → left+right, N=3 → left+centre+right, N>3 → evenly distributed
- Each box is content-sized (ContentSizeFitter + HorizontalLayoutGroup on the prefab); positioned after a one-frame coroutine yield so ContentSizeFitter has time to calculate widths
- Padding from screen edges is controlled by the Container RectTransform's Left/Right offsets in the inspector — not in code
- All boxes animate from centre (UiPopAnimation pivot is always (0.5,0.5)); do not set pivot in layout code

### Key data flow
1. `EncounterTrigger` detects player proximity → `BattleGateway.StartBattle(enemies)`
2. `BattleGateway` sets `GameModeManager` to Battle, disables exploration scripts, swaps UI
3. `BattleFactory.CreateEnemiesFromBaseAI()` + `PartyManager.BuildBattleParty()` build combatant lists
4. `BattleManager.BeginBattle()` runs PlayerPhase (command input) → ActionPhase (resolve in initiative order)
5. ActionPhase: `HideMenusForActionPhase()` + immediate `RefreshStatus` hides all portraits; then per action:
   - **Player attack**: `PlayActionSequence_PlayerVsEnemy()` plays visuals BEFORE damage text (PSIV feel)
   - **Enemy attack**: `ShowTargetedPartyPortrait()` reveals target → `yield return StartCoroutine(PlayBattleAttack())` awaits full animation → hit frame fires `OnEnemyHit` → `PlayEnemyHitSequenceOnParty()` flashes portrait → `RefreshStatus` hides it
6. Visual lookup: `BattleActorVisualCatalog.TryGet(characterId, actionType, weaponStyle, abilityId)`
7. Enemy flash: `HitReactionGroup.PlayHitFlashRoutine()` using shader `_FlashAmount` property
8. On battle end: `BattleGateway.OnBattleFinished()` handles victory/flee/defeat, calls `PartyManager.SyncHPBackFromBattle()`, restores exploration state

## Critical rules

### NEVER stub out or empty these methods during refactors:
- `BattleUI.PlayActionSequence_PlayerVsEnemy` — this IS the entire visual pipeline (caster pose, VFX, enemy blink). Removing its body silently kills all battle visuals with no compiler error.
- `HitReactionGroup.PlayHitFlashRoutine` — enemy hit reaction

### Ownership boundaries (do not cross)
- BattleManager decides outcomes (hit/miss/crit/damage) and applies HP changes
- BattleUI plays visuals only — does NOT compute damage or apply HP
- Screen flash (PlayDamageFlash) is crit-only; BattleManager is the only caller
- HitReactionGroup owns enemy blink — BattleUI calls it, never searches for renderers itself
- BattleGateway is the only entry/exit point for battle — do not call BattleManager.BeginBattle directly
- ExplorerHealth syncs HP to PartyManager leader; BattleGateway syncs HP back after battle via SyncHPBackFromBattle

### Asset type assumptions
- Command icons are Image components in the UI hierarchy, not bare Sprites
- VFX prefabs require BattleVfxSprite + SpriteRenderer + Animator
- Enemy prefabs MUST have a VfxAnchor child transform (logs error and returns null if missing, no hard crash)
- Enemy prefabs MUST have HitReactionGroup for shader-based flash

### When making changes
- Prefer full-file delivery over piecemeal snippets
- Scan for duplicate method declarations before saving
- Always verify field types match (Sprite vs Image vs GameObject)
- Do not rebuild the UI hierarchy unless explicitly agreed upon
- Keep the game runnable after every change — no half-migrated systems

## Current state
- Battle visuals pipeline is working: catalog lookup → caster prefab → per-hit VFX → shader blink → damage text
- Portrait visibility: all at battle start → active hero during command selection → none during action phase → target revealed before enemy attack → flash on hit
- Portrait hit flash works for enemy → party impacts; uses `UIFlash.shader` (Built-in UI-compatible, `_FlashAmount`)
- Enemy name bar implemented: names pop in at battle start, hide on action phase, rebuild from survivors each round
- Damage popups implemented: white numbers on enemies after player attacks, white numbers on party portraits after enemy attacks; BattleManager waits for full popup lifetime before proceeding
- Menu animations: `battleMenuPopup` and `comdMenuPopup` animate in/out via `UiPopAnimation.Show()`/`Hide()`; no raw `SetActive` calls on these objects
- Battle intro sequence: enemy names animate in first, then main menu pops in — same flow used at battle start and every round transition (`PlayBattleIntroSequence`)
- **Known bug (#27)**: names and command menu still animate simultaneously in some round transitions
- Attack and Defend commands functional; Tech/Skill/Item are stubs
- BattleGateway wired: exploration → battle → exploration round-trip works
- Secondary objectives system designed but not yet implemented (design doc exists, needs data layer decision)

## Build phases (from architecture doc)
- Phase 0: Done (working attack/flee prototype)
- Phase 1: In progress (CharacterDefinition wiring for Chaz/Alys/Hahn)
- Phase 2: In progress (StatCalculator exists; derived stats integration may be incomplete)
- Phase 3: Future (GrowthSystem + BehaviorTracker)
- Phase 4: Future (first technique in battle — Chaz Res)

## Style preferences
- Challenge ideas when you have doubts — don't accept framings uncritically
- Ask clarifying questions before writing code
- Diagnose from actual source files, not from prior AI output or assumptions
