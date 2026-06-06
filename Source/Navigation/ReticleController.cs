using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PeskyGizmo
{
    public static class ReticleController
    {
        public static IntVec3 CurrentCell { get; private set; }
        public static Thing? HoveredThing { get; private set; }
        public static Pawn? HoveredPawn { get; private set; }
        public static bool HasTarget => HoveredThing != null;

        private static float _lastSelectTime;
        private static Thing? _lastSelectedThing;

        private static bool _prevAButton;
        private static bool _prevBButton;
        private static bool _prevXButton;
        private static bool _prevYButton;
        private static bool _prevRBButton;
        private static bool _prevLBButton;

        private static bool _simulatingLeft;
        private static bool _simulatingRight;

        public static void Update(bool xConsumed = false)
        {
            bool mapSafe = ControllerDetector.IsMapSafeForInput();

            if (mapSafe && Find.CurrentMap != null)
            {
                UpdateWorldPosition();
                UpdateHover();
            }
            else
            {
                // Don't let the reticle pretend to hover world objects through a menu / dialog.
                HoveredThing = null;
                HoveredPawn = null;
            }

            // HandleButtons must still run when the map isn't safe (main menu, dialogs)
            // so A/B/etc. still drive UI click simulation.
            HandleButtons(xConsumed);
        }

        private static void UpdateWorldPosition()
        {
            CurrentCell = Verse.UI.MouseCell();
        }

        private static void UpdateHover()
        {
            HoveredThing = null;
            HoveredPawn = null;

            var map = Find.CurrentMap;
            if (map == null || map.thingGrid == null) return;
            // World view (and any time the virtual cell drifts off-map) must not query
            // the thing grid — ThingsListAt throws "out of bounds" otherwise.
            if (RimWorld.Planet.WorldRendererUtility.WorldSelected) return;
            if (!CurrentCell.InBounds(map)) return;
            List<Thing> things = map.thingGrid.ThingsListAt(CurrentCell);
            if (things == null || things.Count == 0) return;

            Pawn? pawn = things.OfType<Pawn>().FirstOrDefault(p => p.RaceProps.Humanlike);
            if (pawn == null) pawn = things.OfType<Pawn>().FirstOrDefault();

            if (pawn != null)
            {
                HoveredThing = pawn;
                HoveredPawn = pawn;
            }
            else
            {
                HoveredThing = things.FirstOrDefault(t => t.def.selectable);
            }
        }

        private static void HandleButtons(bool xConsumed)
        {
            var s = GizmoMod.Settings;
            bool overUI = IsCursorOverAnyConsoleUI();
            bool inspectContent = InspectPanel.InContentMode;
            bool mapSafe = ControllerDetector.IsMapSafeForInput();

            bool aDown = ControllerState.IsButtonDown(s.btnA);
            bool bDown = ControllerState.IsButtonDown(s.btnB);
            bool xDown = ControllerState.IsButtonDown(s.btnX);
            bool yDown = ControllerState.IsButtonDown(s.btnY);
            bool rbDown = ControllerState.IsButtonDown(s.btnRB);
            bool lbDown = ControllerState.IsButtonDown(s.btnLB);
            bool ltHeld = ControllerState.IsButtonDown(s.btnLT);

            // Universal mode: contextual smart-X + vanilla click simulation
            if (xDown && !_prevXButton && TrySmartCombatAction())
            {
                _prevXButton = xDown;
                _prevAButton = aDown;
                _prevBButton = bDown;
                _prevYButton = yDown;
                _prevRBButton = rbDown;
                _prevLBButton = lbDown;
                return;
            }

            if (lbDown && !_prevLBButton) CycleColonist(false);
            if (rbDown && !_prevRBButton) CycleColonist(true);

            // A Button: Always Select (Left-Click)
            if (aDown)
            {
                if (NavHelper.IsActive && NavHelper.HasSelection)
                {
                    // Handled by NavHelper A-press click simulation inside Widgets patches
                }
                else if (inspectContent && InspectPanel.CursorInsidePanel())
                {
                    if (!_prevAButton)
                    {
                        _simulatingLeft = true;
                        ControllerState.IsSimulatingClick = true;
                        ControllerState.SimulatedClickCooldown = Time.unscaledTime + 0.08f;
                        MouseSimulation.SendLeftDown();
                    }
                }
                else if (xConsumed || overUI)
                {
                    // Consumed by UI
                }
                else if (!_prevAButton && Time.unscaledTime - _lastSelectTime < 0.4f && HoveredThing != null && HoveredThing == _lastSelectedThing)
                {
                    DoSelectSimilar(HoveredThing);
                }
                else
                {
                    if (!_prevAButton)
                    {
                        _simulatingLeft = true;
                        ControllerState.IsSimulatingClick = true;
                        ControllerState.SimulatedClickCooldown = Time.unscaledTime + 0.08f;
                        MouseSimulation.SendLeftDown();
                        
                        _lastSelectTime = Time.unscaledTime;
                        _lastSelectedThing = HoveredThing;
                    }
                }
            }
            else
            {
                if (_simulatingLeft)
                {
                    _simulatingLeft = false;
                    ControllerState.IsSimulatingClick = _simulatingRight;
                    if (_simulatingRight) ControllerState.SimulatedClickCooldown = Time.unscaledTime + 0.08f;
                    MouseSimulation.SendLeftUp();
                }
            }

            // X Button: Always Interact/Move (Right-Click)
            if (xDown)
            {
                if (NavHelper.IsActive && NavHelper.HasSelection)
                {
                    // Bypassed: NavHelper owns navigation/input
                }
                else if (!xConsumed && !overUI && !_prevXButton)
                {
                    _simulatingRight = true;
                    ControllerState.IsSimulatingClick = true;
                    ControllerState.SimulatedClickCooldown = Time.unscaledTime + 0.08f;
                    MouseSimulation.SendRightDown();
                }
            }
            else
            {
                if (_prevXButton)
                {
                    _simulatingRight = false;
                    ControllerState.IsSimulatingClick = _simulatingLeft;
                    if (_simulatingLeft) ControllerState.SimulatedClickCooldown = Time.unscaledTime + 0.08f;
                    MouseSimulation.SendRightUp();
                }
            }

            // Removed Y button draft logic (moved to R3 globally)

            _prevAButton = aDown;
            _prevBButton = bDown;
            _prevXButton = xDown;
            _prevYButton = yDown;
            _prevRBButton = rbDown;
            _prevLBButton = lbDown;
        }

        // ---- smart X: context-sensitive combat (console reticle style) ---------

        private static bool TrySmartCombatAction()
        {
            if (Find.CurrentMap == null) return false;
            var sel = Find.Selector;
            if (sel == null || sel.NumSelected == 0) return false;

            var selectedPawns = sel.SelectedObjectsListForReading
                .OfType<Pawn>()
                .Where(p => p.IsColonistPlayerControlled && p.drafter != null)
                .ToList();
            if (selectedPawns.Count == 0) return false;
            Pawn selectedPawn = selectedPawns[0];

            Thing? attackTarget = HoveredPawn ?? HoveredThing;
            bool hoveringEnemy = attackTarget != null && IsAttackable(attackTarget, selectedPawn);
            bool hoveringGround = HoveredThing == null && HoveredPawn == null;

            if (hoveringEnemy)
            {
                bool isHostile = attackTarget!.HostileTo(selectedPawn);

                // Friendly / neutral target: let vanilla right-click simulate
                // so the player gets the "Melee attack alpaca" / "Fire at..." float menu.
                if (!isHostile)
                {
                    return false;
                }

                // Fire — auto-draft and fan attack jobs to every selected pawn.
                foreach (var p in selectedPawns)
                    if (!p.drafter.Drafted) p.drafter.Drafted = true;

                IssueMassAttack(selectedPawns, attackTarget!);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                return true;
            }

            // Hovering empty ground with drafted pawn: right-click to move
            if (hoveringGround && selectedPawn.drafter != null && selectedPawn.drafter.Drafted)
            {
                ControllerState.IsSimulatingClick = true;
                ControllerState.SimulatedClickCooldown = Time.unscaledTime + 0.08f;
                MouseSimulation.SendRightDown();
                _simulatingRight = true;
                return true;
            }

            return false;
        }

        /// <summary>True if the selected pawn can target the given thing via smart-X.
        /// Allows hostile, neutral, and faction-less attackable things. Excludes the
        /// attacker itself and anything sharing the attacker's faction (own pawns /
        /// own buildings). Friendly / neutral targets still go through the arm-then-
        /// confirm flow in <see cref="TrySmartCombatAction"/>.</summary>
        public static bool IsAttackable(Thing t, Pawn attacker)
        {
            if (t == null || t == attacker) return false;
            if (t.Faction != null && attacker.Faction != null && t.Faction == attacker.Faction) return false;
            if (t is Pawn) return true;
            if (t.def?.category == ThingCategory.Plant) return false;
            if (t.def?.useHitPoints != true) return false;
            return true;
        }

        /// <summary>Issue an ordered attack job to every pawn in the list, picking
        /// AttackMelee or AttackStatic based on each pawn's best available verb.
        /// Used for multi-selection mass attack via the Targeter callback.</summary>
        private static void IssueMassAttack(List<Pawn> pawns, Thing target)
        {
            if (pawns == null || target == null) return;
            foreach (var p in pawns)
            {
                if (p?.jobs == null || p.Dead || p.Downed) continue;
                Verb verb = p.TryGetAttackVerb(target, !p.IsColonist);
                if (verb == null) continue;
                JobDef def = verb.IsMeleeAttack ? JobDefOf.AttackMelee : JobDefOf.AttackStatic;
                Job job = JobMaker.MakeJob(def, target);
                job.maxNumMeleeAttacks = int.MaxValue;
                job.maxNumStaticAttacks = int.MaxValue;
                job.expiryInterval = 5000;
                job.canUseRangedWeapon = !verb.IsMeleeAttack;
                try { p.jobs.TryTakeOrderedJob(job, JobTag.Misc); } catch { }
            }
        }

        /// <summary>Find the best available ranged or melee verb on a pawn that can hit a target.</summary>
        private static Verb? FindBestAttackVerb(Pawn attacker, Thing target)
        {
            if (attacker.verbTracker == null) return null;
            bool adjacent = attacker.Position.AdjacentTo8WayOrInside(target.Position);

            // Prefer ranged if not adjacent
            if (!adjacent)
            {
                foreach (var verb in attacker.verbTracker.AllVerbs)
                {
                    if (!verb.IsMeleeAttack && verb.Available() && verb.CanHitTarget(target))
                        return verb;
                }
            }

            // Melee if adjacent, or fallback
            foreach (var verb in attacker.verbTracker.AllVerbs)
            {
                if (verb.IsMeleeAttack && verb.Available() && verb.CanHitTarget(target))
                    return verb;
            }

            // Any available attack verb as last resort
            foreach (var verb in attacker.verbTracker.AllVerbs)
            {
                if (verb.Available() && verb.CanHitTarget(target))
                    return verb;
            }

            return null;
        }

        // Cached reflection handle for vanilla's private "select all similar" method.
        private static MethodInfo? _miSelectAllMatching;

        private static void DoSelectSimilar(Thing target)
        {
            if (target == null || Find.CurrentMap == null) return;
            var sel = Find.Selector;
            if (sel == null) return;

            if (_miSelectAllMatching == null)
            {
                _miSelectAllMatching = AccessTools.Method(typeof(Selector), "SelectAllMatchingObjectUnderMouseOnScreen");
            }
            if (_miSelectAllMatching == null)
            {
                Log.ErrorOnce("[Gizmo] Could not resolve Selector.SelectAllMatchingObjectUnderMouseOnScreen", 562001);
                return;
            }

            try
            {
                _miSelectAllMatching.Invoke(sel, null);
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[Gizmo] DoSelectSimilar failed: {ex}", 562002);
            }
        }

        public static void DoDraft()
        {
            // Mass draft/undraft all selected colonists
            try
            {
                var sel = Find.Selector;
                if (sel != null && sel.NumSelected > 0)
                {
                    var colonists = sel.SelectedObjectsListForReading
                        .OfType<Pawn>()
                        .Where(p => p.IsColonistPlayerControlled && p.drafter != null)
                        .ToList();

                    if (colonists.Count > 0)
                    {
                        // If any are undrafted, draft all; if all drafted, undraft all
                        bool anyUndrafted = colonists.Any(p => !p.drafter.Drafted);
                        bool target = anyUndrafted;
                        foreach (var p in colonists)
                            p.drafter.Drafted = target;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        return;
                    }
                }
            }
            catch { }

            // Fallback: draft/undraft the hovered pawn
            if (Find.CurrentMap != null && HoveredPawn != null && HoveredPawn.IsColonistPlayerControlled)
            {
                HoveredPawn.drafter.Drafted = !HoveredPawn.drafter.Drafted;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        private static void CycleColonist(bool forward)
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            if (colonists.Count == 0) return;

            Pawn? current = Find.Selector.SingleSelectedThing as Pawn;
            if (current == null && Find.Selector.SelectedObjects.Count > 0)
            {
                current = Find.Selector.SelectedObjects.OfType<Pawn>().FirstOrDefault();
            }
            if (current == null) current = colonists[0];

            int idx = colonists.IndexOf(current);
            if (idx == -1) idx = 0;

            if (forward) idx = (idx + 1) % colonists.Count;
            else idx = (idx - 1 + colonists.Count) % colonists.Count;

            CameraJumper.TryJumpAndSelect(colonists[idx]);
        }

        /// <summary>
        /// Returns true when the virtual cursor is positioned over an active console UI panel,
        /// so world clicks should be suppressed.
        /// </summary>
        private static bool IsCursorOverAnyConsoleUI()
        {
            Vector2 guiPos = new Vector2(
                ControllerState.CursorScreenPos.x / Prefs.UIScale,
                Verse.UI.screenHeight - (ControllerState.CursorScreenPos.y / Prefs.UIScale));
            return InspectPanel.IsOverAmbientPanel(guiPos);
        }
    }
}
