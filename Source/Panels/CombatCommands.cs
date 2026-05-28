using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PeskyGizmo
{
    /// <summary>
    /// Explicit combat commands per spec section 5.
    ///   A      → Move
    ///   LT+A   → Cover-to-cover move
    ///   X      → Attack
    ///   LT+X   → Toggle melee/ranged
    ///   RB     → Use current ability
    ///   LT+RB  → Cycle ability
    /// </summary>
    public static class CombatCommands
    {
        private static readonly HashSet<int> _holding = new HashSet<int>();
        public static bool IsHolding(Pawn p) => p != null && _holding.Contains(p.thingIDNumber);

        private static bool _forceMelee;
        public static bool ForceMelee => _forceMelee;
        public static void ToggleMelee() => _forceMelee = !_forceMelee;

        private static int _abilityIndex;

        public static List<Pawn> DraftedSelected()
        {
            var pawns = Find.Selector.SelectedObjectsListForReading
                .OfType<Pawn>()
                .Where(p => p.IsColonistPlayerControlled && p.drafter != null && !p.Dead && !p.Downed)
                .ToList();
            foreach (var p in pawns)
                if (!p.drafter.Drafted) p.drafter.Drafted = true;
            return pawns;
        }

        // ---- move ---------------------------------------------------------------
        public static void MoveTo(List<Pawn> pawns, IntVec3 cell)
        {
            foreach (var p in pawns)
            {
                if (!p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) continue;
                Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.locomotionUrgency = LocomotionUrgency.Sprint;
                job.playerForced = true;
                try { p.jobs.TryTakeOrderedJob(job, JobTag.Misc); } catch { /* Job assignment may fail on dead/despawned pawns */ }
                _holding.Remove(p.thingIDNumber);
            }
        }

        // ---- cover move ----------------------------------------------------------
        public static void CoverMoveTo(List<Pawn> pawns, IntVec3 dest)
        {
            Map? map = Find.CurrentMap;
            if (map == null) return;
            foreach (var p in pawns)
            {
                IntVec3 cover = FindBestCoverToward(p, dest, map);
                if (cover.IsValid)
                {
                    Job goCover = JobMaker.MakeJob(JobDefOf.Goto, cover);
                    goCover.locomotionUrgency = LocomotionUrgency.Sprint;
                    goCover.playerForced = true;
                    Job goDest = JobMaker.MakeJob(JobDefOf.Goto, dest);
                    goDest.locomotionUrgency = LocomotionUrgency.Sprint;
                    goDest.playerForced = true;
                    try
                    {
                        p.jobs.TryTakeOrderedJob(goCover, JobTag.Misc);
                        p.jobs.jobQueue.EnqueueLast(goDest, JobTag.Misc);
                        _holding.Add(p.thingIDNumber);
                    }
                    catch { /* Job assignment may fail on dead/despawned pawns */ }
                }
                else
                {
                    MoveTo(new List<Pawn> { p }, dest);
                }
            }
        }

        // ---- attack --------------------------------------------------------------
        public static void AttackTarget(List<Pawn> pawns, Thing target)
        {
            if (pawns == null || target == null) return;
            foreach (var p in pawns)
            {
                if (p?.jobs == null || p.Dead || p.Downed) continue;
                if (_forceMelee)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                    job.maxNumMeleeAttacks = int.MaxValue;
                    job.expiryInterval = 5000;
                    try { p.jobs.TryTakeOrderedJob(job, JobTag.Misc); } catch { /* Job assignment may fail on dead/despawned pawns */ }
                }
                else
                {
                    Verb? verb = p.TryGetAttackVerb(target, !p.IsColonist);
                    if (verb == null) continue;
                    JobDef def = verb.IsMeleeAttack ? JobDefOf.AttackMelee : JobDefOf.AttackStatic;
                    Job job = JobMaker.MakeJob(def, target);
                    job.maxNumMeleeAttacks = int.MaxValue;
                    job.maxNumStaticAttacks = int.MaxValue;
                    job.expiryInterval = 5000;
                    job.canUseRangedWeapon = !verb.IsMeleeAttack;
                    try { p.jobs.TryTakeOrderedJob(job, JobTag.Misc); } catch { /* Job assignment may fail on dead/despawned pawns */ }
                }
                _holding.Remove(p.thingIDNumber);
            }
        }

        // ---- abilities -----------------------------------------------------------
        public static List<Ability> GetAvailableAbilities(Pawn p)
        {
            if (p.abilities == null) return new List<Ability>();
            return p.abilities.AllAbilitiesForReading
                .Where(a => (bool)a.CanCast && !a.OnCooldown && a.GizmosVisible())
                .ToList();
        }

        public static string CurrentAbilityLabel(Pawn p)
        {
            var list = GetAvailableAbilities(p);
            if (list.Count == 0) return "No ability";
            _abilityIndex = Mathf.Clamp(_abilityIndex, 0, list.Count - 1);
            var a = list[_abilityIndex];
            string label = a.def.label.CapitalizeFirst();
            if (a.UsesCharges) label += $" ({a.RemainingCharges})";
            return label;
        }

        public static void CycleAbility(Pawn p)
        {
            var list = GetAvailableAbilities(p);
            if (list.Count == 0) return;
            _abilityIndex = (_abilityIndex + 1) % list.Count;
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        public static void UseCurrentAbility(Pawn p, LocalTargetInfo target)
        {
            var list = GetAvailableAbilities(p);
            if (list.Count == 0) return;
            _abilityIndex = Mathf.Clamp(_abilityIndex, 0, list.Count - 1);
            var a = list[_abilityIndex];
            if (!(bool)a.CanApplyOn(target)) return;
            try
            {
                a.Activate(target, LocalTargetInfo.Invalid);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            catch { /* Ability activation may fail on invalid target or cooldown state */ }
        }

        // ---- stop ----------------------------------------------------------------
        public static void StopAll(List<Pawn> pawns)
        {
            foreach (var p in pawns)
            {
                try { p.jobs.StopAll(); } catch { /* Job stop may fail on dead/despawned pawns */ }
                _holding.Remove(p.thingIDNumber);
            }
        }

        // ---- private helpers -----------------------------------------------------
        private static IntVec3 FindBestCoverToward(Pawn p, IntVec3 dest, Map map)
        {
            IntVec3 origin = p.Position;
            IntVec3 best = IntVec3.Invalid;
            float bestScore = 0f;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(origin, 8f, true))
            {
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (c == origin) continue;
                Thing? cov = c.GetCover(map);
                if (cov == null) continue;
                if (!p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
                float block = cov.def.BaseBlockChance();
                float distOrigin = (c - origin).LengthHorizontal;
                float distDest = (c - dest).LengthHorizontal;
                float score = block - distOrigin * 0.02f - distDest * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }
    }
}
