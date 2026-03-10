using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// Stateless handler that resolves the game-world effect of each program.
///
/// Calling convention:
/// <code>
/// ProgramEffect effect = ProgramEffectHandler.Handle(program, targetIce, persona, system);
/// </code>
///
/// The handler does NOT mutate any state directly — it returns a
/// <see cref="ProgramEffect"/> describing what should happen. The caller
/// (<see cref="MatrixSession"/>) applies the mutations after inspecting
/// the result, so the handler remains purely functional and testable.
///
/// Handler selection is driven by <see cref="ProgramName"/>. Programs that
/// have no ICE target (Medic, Analyze, Shield, Smoke, Mirrors) pass
/// <c>null</c> for <paramref name="targetIce"/>.
/// </summary>
public static class ProgramEffectHandler
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Slow effect speed multiplier applied to target ICE.</summary>
    public const float SlowSpeedMultiplier = 0.35f;

    /// <summary>Duration in seconds of Slow and Mirrors effects.</summary>
    public const float StatusEffectDuration = 4.0f;

    /// <summary>Maximum Rebound deflections before the program breaks.</summary>
    public const int ReboundMaxDeflections = 3;

    // ── Main dispatch ─────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the full effect of <paramref name="program"/> given the current
    /// combat context.
    /// </summary>
    /// <param name="program">The program being run.</param>
    /// <param name="targetIce">
    /// The ICE being targeted. Null for non-combat programs.
    /// </param>
    /// <param name="persona">The acting Persona.</param>
    /// <param name="system">The active Matrix system.</param>
    /// <param name="succeeded">
    /// Whether the underlying success roll passed. Computed by
    /// <see cref="Persona.ComputeSuccessChance"/> before calling here.
    /// </param>
    public static ProgramEffect Handle(
        Program      program,
        Ice?         targetIce,
        Persona      persona,
        MatrixSystem system,
        bool         succeeded,
        Random?      rng = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(system);

        rng ??= Random.Shared;

        return program.Spec.Name switch
        {
            ProgramName.Attack    => HandleAttack(program,    targetIce, persona, succeeded, rng),
            ProgramName.Slow      => HandleSlow(program,      targetIce, succeeded),
            ProgramName.Degrade   => HandleDegrade(program,   system,    succeeded),
            ProgramName.Rebound   => HandleRebound(program,   targetIce, succeeded),
            ProgramName.Medic     => HandleMedic(program,     persona,   succeeded, rng),
            ProgramName.Shield    => HandleShield(program,               succeeded),
            ProgramName.Smoke     => HandleSmoke(program,                succeeded),
            ProgramName.Mirrors   => HandleMirrors(program,              succeeded),
            ProgramName.Sleaze    => HandleSleaze(program,    targetIce, succeeded),
            ProgramName.Deception => HandleDeception(program, targetIce, persona, succeeded),
            ProgramName.Relocate  => HandleRelocate(program,  targetIce, succeeded),
            ProgramName.Analyze   => HandleAnalyze(program,   persona,   succeeded),
            _ => throw new InvalidOperationException(
                     $"No effect handler registered for program '{program.Spec.Name}'.")
        };
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deals damage to ICE proportional to program level × combat skill vs
    /// ice effective rating. Hits cancel active probes on Access/Barrier ICE.
    /// </summary>
    private static ProgramEffect HandleAttack(
        Program program, Ice? targetIce, Persona persona, bool succeeded, Random rng)
    {
        if (targetIce is null)
            return ProgramEffect.Failure(ProgramName.Attack, "No target ICE to attack.");

        if (!succeeded)
        {
            // Distinguish a genuine miss (program capable but roll failed) from a
            // program failure (program too outmatched to execute — raw chance at floor).
            float rawChance = program.Spec.Level * Math.Max(1, persona.DeckerRef.ComputerSkill)
                              / ((float)targetIce.EffectiveRating * Persona.SuccessDifficultyConstant);
            bool isMiss = rawChance > Persona.SuccessFloor;
            return ProgramEffect.Failure(ProgramName.Attack, isMiss
                ? $"Attack missed {targetIce.Spec.Type} (rating {targetIce.EffectiveRating})."
                : $"Attack L{program.Spec.Level} failed — program overloaded by {targetIce.Spec.Type} (rating {targetIce.EffectiveRating}).");
        }

        float damage = ComputeAttackDamage(program, targetIce, persona.DeckerRef.CombatSkill, rng);

        // Light attack (low-level program) cancels probes fastest
        bool cancelsProbe = targetIce.Spec.Type is IceType.Access or IceType.Barrier;

        string narrative = cancelsProbe
            ? $"Attack hit {targetIce.Spec.Type} for {damage:F1} damage — probe cancelled."
            : $"Attack hit {targetIce.Spec.Type} for {damage:F1} damage.";

        return ProgramEffect.Success(
            ProgramName.Attack,
            narrative,
            damageToIce: damage);
    }

    // ── Slow ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduces ICE action speed for <see cref="StatusEffectDuration"/> seconds.
    /// No effect on Trace ICE. Repeated Slow at minimum speed eventually
    /// destroys the ICE (though this is impractical — use Attack instead).
    /// </summary>
    private static ProgramEffect HandleSlow(
        Program program, Ice? targetIce, bool succeeded)
    {
        if (targetIce is null)
            return ProgramEffect.Failure(ProgramName.Slow, "No target ICE to slow.");

        if (targetIce.Spec.IsTraceType)
            return ProgramEffect.Failure(ProgramName.Slow,
                "Slow has no effect on Trace ICE.");

        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Slow,
                $"Slow failed against {targetIce.Spec.Type}.");

        // If already at minimum speed, deal a small amount of destruction damage
        bool alreadyAtMinSpeed = targetIce.SpeedMultiplier <= SlowSpeedMultiplier + 0.05f;
        float bonusDamage = alreadyAtMinSpeed ? targetIce.EffectiveRating * 2f : 0f;

        string narrative = alreadyAtMinSpeed
            ? $"Slow pushed {targetIce.Spec.Type} past its limit — {bonusDamage:F1} damage dealt."
            : $"{targetIce.Spec.Type} slowed to {SlowSpeedMultiplier:P0} speed for {StatusEffectDuration}s.";

        return ProgramEffect.Success(
            ProgramName.Slow,
            narrative,
            statusEffectDuration: StatusEffectDuration,
            slowSpeedMultiplier:  SlowSpeedMultiplier,
            damageToIce:          bonusDamage);
    }

    // ── Degrade ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lowers the current node's security rating by 1, weakening all ICE on it.
    /// Only available at program levels 3 and 6.
    /// </summary>
    private static ProgramEffect HandleDegrade(
        Program program, MatrixSystem system, bool succeeded)
    {
        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Degrade,
                "Degrade failed — node security unchanged.");

        return ProgramEffect.Success(
            ProgramName.Degrade,
            "Node security rating reduced by 1. ICE is now weaker.",
            securityRatingDelta: -1);
    }

    // ── Rebound ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates a deflection shield that bounces incoming ICE attacks back at
    /// the attacker. Breaks after <see cref="ReboundMaxDeflections"/> deflections.
    /// Ineffective against BlackIce.
    /// </summary>
    private static ProgramEffect HandleRebound(
        Program program, Ice? targetIce, bool succeeded)
    {
        if (targetIce?.Spec.Type == IceType.BlackIce)
            return ProgramEffect.Failure(ProgramName.Rebound,
                "Rebound is ineffective against BlackIce.");

        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Rebound,
                "Rebound failed to initialise.");

        return ProgramEffect.Success(
            ProgramName.Rebound,
            $"Rebound shield active — up to {ReboundMaxDeflections} deflections.");
    }

    // ── Medic ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restores Persona energy. Heals less efficiently when the Persona is
    /// seriously damaged (below 25% energy).
    /// Must reload after each use.
    /// </summary>
    private static ProgramEffect HandleMedic(
        Program program, Persona persona, bool succeeded, Random rng)
    {
        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Medic,
                "Medic program failed to initialise.");

        // Base heal = programLevel × 8 × 2d6 variance; halved below 25% energy
        float baseHeal         = program.Spec.Level * 8f * Roll2d6Scale(rng);
        bool  seriouslyDamaged = persona.Energy < persona.EnergyMax * 0.25f;
        float actualHeal       = seriouslyDamaged ? baseHeal * 0.5f : baseHeal;

        string narrative = seriouslyDamaged
            ? $"Medic healed {actualHeal:F1} energy (reduced — Persona critically damaged)."
            : $"Medic restored {actualHeal:F1} Persona energy.";

        return ProgramEffect.Success(
            ProgramName.Medic,
            narrative,
            energyHealed: actualHeal);
    }

    /// <summary>
    /// Rolls 2d6 normalised to mean 1.0 (range ≈ 0.286–1.714).
    /// Injects realistic dice-roll variance into damage and healing.
    /// </summary>
    private static float Roll2d6Scale(Random rng) =>
        (rng.Next(1, 7) + rng.Next(1, 7)) / 7f;

    // ── Shield ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduces incoming damage per hit. Degrades with each hit.
    /// Has no effect against BlackIce (which routes damage to physical health).
    /// </summary>
    private static ProgramEffect HandleShield(Program program, bool succeeded)
    {
        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Shield,
                "Shield failed to initialise.");

        // Shield strength scales with program level — tracked by session
        float shieldStrength = program.Spec.Level * 3f;

        return ProgramEffect.Success(
            ProgramName.Shield,
            $"Shield active — absorbs up to {shieldStrength:F0} damage per hit. " +
            $"Note: no effect against BlackIce.",
            shieldStrength: shieldStrength);
    }

    // ── Smoke ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates electronic chaos, applying a difficulty penalty to ALL actions
    /// (both Persona and ICE) for <see cref="StatusEffectDuration"/> seconds.
    /// Primary use: deliberately cause a program failure to bait Tar Pit.
    /// Must reload after each use.
    /// </summary>
    private static ProgramEffect HandleSmoke(Program program, bool succeeded)
    {
        // Smoke can fail itself (which is sometimes the point — see Tar Pit bait)
        string narrative = succeeded
            ? $"Smoke deployed — all actions penalised for {StatusEffectDuration}s."
            : "Smoke failed to deploy.";

        return succeeded
            ? ProgramEffect.Success(ProgramName.Smoke, narrative,
                statusEffectDuration: StatusEffectDuration)
            : ProgramEffect.Failure(ProgramName.Smoke, narrative);
    }

    // ── Mirrors ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduces ICE attack accuracy for <see cref="StatusEffectDuration"/> seconds.
    /// Unlike Smoke, only affects ICE — Persona actions are unpenalised.
    /// Must reload after each use.
    /// </summary>
    private static ProgramEffect HandleMirrors(Program program, bool succeeded)
    {
        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Mirrors,
                "Mirrors failed to initialise.");

        return ProgramEffect.Success(
            ProgramName.Mirrors,
            $"Mirrors active — ICE attack accuracy reduced for {StatusEffectDuration}s.",
            statusEffectDuration: StatusEffectDuration);
    }

    // ── Sleaze ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bypasses the current node without defeating its ICE. Works against all
    /// ICE types including Barrier and BlackIce. The ICE remains and must be
    /// dealt with again on any revisit. Cannot be used to perform in-node actions.
    /// </summary>
    private static ProgramEffect HandleSleaze(
        Program program, Ice? targetIce, bool succeeded)
    {
        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Sleaze,
                targetIce is not null
                    ? $"Sleaze failed to bypass {targetIce.Spec.Type}."
                    : "Sleaze failed — node not bypassed.");

        string iceNote = targetIce is not null
            ? $" ICE ({targetIce.Spec.Type}) remains active — will need re-bypassing."
            : string.Empty;

        return ProgramEffect.Success(
            ProgramName.Sleaze,
            $"Node bypassed successfully.{iceNote}",
            nodeBypassed: true);
    }

    // ── Deception ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates false passcodes to instantly defeat Access, Blaster, Killer,
    /// Trace &amp; Burn, and Trace &amp; Dump ICE.
    /// No effect on Barrier or BlackIce.
    /// Only usable before combat starts (<see cref="CombatState.None"/>).
    /// The most important program in the game.
    /// </summary>
    private static ProgramEffect HandleDeception(
        Program program, Ice? targetIce, Persona persona, bool succeeded)
    {
        if (targetIce is null)
            return ProgramEffect.Failure(ProgramName.Deception, "No target ICE.");

        // Deception cannot be run in active combat — enforced by Persona.RunProgram,
        // but we double-check here for safety
        if (persona.CombatState == CombatState.Active)
            return ProgramEffect.Failure(ProgramName.Deception,
                "Deception cannot be used once combat has started.");

        if (targetIce.Spec.Type is IceType.Barrier or IceType.BlackIce)
            return ProgramEffect.Failure(ProgramName.Deception,
                $"Deception has no effect on {targetIce.Spec.Type}. Use Attack instead.");

        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Deception,
                $"Deception failed against {targetIce.Spec.Type} " +
                $"(rating {targetIce.EffectiveRating}). " +
                $"Consider upgrading Masking or a higher-level Deception.");

        return ProgramEffect.Success(
            ProgramName.Deception,
            $"Deception fooled {targetIce.Spec.Type} — ICE instantly defeated.",
            // Damage value large enough to guarantee a kill; Ice.TakeDamage clamps to 0
            damageToIce: float.MaxValue);
    }

    // ── Relocate ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Leads any Trace ICE on a wild goose chase, instantly defeating it.
    /// Only effective against Trace &amp; Burn and Trace &amp; Dump.
    /// Redundant with Deception before combat; uniquely useful once the
    /// probe is already moving (Deception is blocked in combat).
    /// </summary>
    private static ProgramEffect HandleRelocate(
        Program program, Ice? targetIce, bool succeeded)
    {
        if (targetIce is null)
            return ProgramEffect.Failure(ProgramName.Relocate, "No target ICE.");

        if (!targetIce.Spec.IsTraceType)
            return ProgramEffect.Failure(ProgramName.Relocate,
                $"Relocate only works against Trace ICE, not {targetIce.Spec.Type}.");

        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Relocate,
                $"Relocate failed — Trace probe still advancing.");

        return ProgramEffect.Success(
            ProgramName.Relocate,
            $"Trace probe successfully relocated — {targetIce.Spec.Type} defeated.",
            damageToIce: float.MaxValue);
    }

    // ── Analyze ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans the current node and its ICE for information.
    /// Multiple runs may be required for a complete scan.
    /// Once fully scanned, enables the success-bar display for loaded programs.
    /// Uses the deck's Sensor attribute as a multiplier.
    /// </summary>
    private static ProgramEffect HandleAnalyze(
        Program program, Persona persona, bool succeeded)
    {
        if (!succeeded)
            return ProgramEffect.Failure(ProgramName.Analyze,
                "Analyze scan incomplete — run again for more data.");

        // Full scan probability scales with (programLevel × sensor) / 10
        float scanChance   = (program.Spec.Level * Math.Max(1, persona.Deck.Stats.Sensor)) / 10f;
        bool  fullyScanned = scanChance >= 1.0f ||
                             Random.Shared.NextDouble() <= Math.Clamp(scanChance, 0.1f, 1.0f);

        string narrative = fullyScanned
            ? "Node fully scanned — ICE stats known and success bars active."
            : "Partial scan complete — run Analyze again for full data.";

        return ProgramEffect.Success(
            ProgramName.Analyze,
            narrative,
            nodeFullyScanned: fullyScanned);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static float ComputeAttackDamage(Program program, Ice ice, int combatSkill, Random rng)
    {
        // Damage = (programLevel × combatSkill) / iceRating × scaling × 2d6 variance.
        // Floor applied to raw BEFORE scaling, not to the final result.
        // Applying Math.Max(1f, raw * scale) instead would make the floor dominate
        // whenever raw < ~0.58 — all dice results clamp to the same 1.0 and the
        // roll appears fixed. Flooring raw first ensures 2d6 variance is always
        // visible regardless of program level vs ICE rating.
        float raw        = (program.Spec.Level * Math.Max(1, combatSkill))
                           / (float)ice.EffectiveRating
                           * 8f;
        float clampedRaw = Math.Max(1f, raw);
        return clampedRaw * Roll2d6Scale(rng);
    }
}
