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
- `BattleManager.cs` — battle flow, turn/phases, damage resolution, applying HP
- `BattleUI.cs` — menus, input, visual sequencing, party strip integration
- `PartyStripController.cs` — 5-tile party strip, global status icons, CastAnchor per tile, portrait hit flash
- `PartyStripEntry.cs` — per-tile rendering (name, HP/TP, status icon, class badge, portrait)
- `BattleActorVisualCatalog.cs` — ScriptableObject mapping characterId + actionType + weaponStyle → VFX entries
- `BattleVfxSprite.cs` — sprite-animated VFX with caller-controlled lifetime
- `BattleUiVfx.cs` — tiny MonoBehaviour helper; stores `durationSeconds` for UI VFX cleanup in BattleUI
- `HitReactionGroup.cs` — shader-based enemy hit flash (_FlashAmount) on all renderers in group
- `BaseEnemyAI.cs` — enemy prefab controller, owns VfxAnchor, animation events; handles both Exploration and Battle modes
- `BattleTypes.cs` — BattleCombatant, BattleResult, BattleRewards, BattleOutcome, WeaponStyle, WeaponStyleUtil, DamageFactor, DamageFactorInfo, HandItemType
- `BattleFactory.cs` — static helper; converts `List<BaseEnemyAI>` → `List<BattleCombatant>` for BattleManager
- `PartyManager.cs` — singleton, owns activeParty (CharacterData), builds BattleCombatant list
- `StatCalculator.cs` — centralized stat formulas (ATK/DEF/MAG/MRES/HIT/EVD/CRIT)

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

### Key data flow
1. `EncounterTrigger` detects player proximity → `BattleGateway.StartBattle(enemies)`
2. `BattleGateway` sets `GameModeManager` to Battle, disables exploration scripts, swaps UI
3. `BattleFactory.CreateEnemiesFromBaseAI()` + `PartyManager.BuildBattleParty()` build combatant lists
4. `BattleManager.BeginBattle()` runs PlayerPhase (command input) → ActionPhase (resolve in initiative order)
5. `BattleUI.PlayActionSequence_PlayerVsEnemy()` plays visuals BEFORE damage text (PSIV feel)
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
- Party strip shows portraits (all during action phase, active-only during command selection)
- Portrait hit flash works for enemy → party impacts
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
