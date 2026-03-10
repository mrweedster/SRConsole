using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// Represents a contracted Matrix shadowrun job issued by a Mr. Johnson.
///
/// Three difficulty tiers exist, each with typical pay and karma ranges:
/// <list type="bullet">
///   <item><b>Simple</b>  — ~425–500¥, +1–2 Karma. Low-security unnamed systems.</item>
///   <item><b>Moderate</b> — ~2,550–3,000¥, +2–3 Karma. Small named or unnamed systems.</item>
///   <item><b>Expert</b>  — ~6,000–6,350¥, +4–5 Karma. Large named corporate systems.</item>
/// </list>
///
/// A run is complete when <see cref="CheckObjectiveMet"/> returns true and
/// <see cref="MarkComplete"/> is called by the session layer.
/// </summary>
public class MatrixRun
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string Id           { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Name of the Johnson or fixer who issued this contract.</summary>
    public string JohnsonName  { get; }

    // ── Objective ─────────────────────────────────────────────────────────────

    public MatrixRunObjective Objective      { get; }

    /// <summary>ID of the Matrix system that must be penetrated.</summary>
    public string             TargetSystemId { get; }

    /// <summary>ID of the specific Node the Decker must interact with.</summary>
    public string             TargetNodeId   { get; }

    /// <summary>Human-readable label of the target node for the CyberInfo screen.</summary>
    public string             TargetNodeTitle { get; }

    /// <summary>
    /// Filename involved in data operations (download, upload, erase).
    /// Null when <see cref="Objective"/> is <see cref="MatrixRunObjective.CrashCpu"/>.
    /// </summary>
    public string?            ContractedFilename { get; }

    // ── Difficulty ────────────────────────────────────────────────────────────

    /// <summary>One of "simple", "moderate", or "expert".</summary>
    public string Difficulty { get; }

    // ── Reward ────────────────────────────────────────────────────────────────

    /// <summary>Base nuyen payment at negotiation 0–2.</summary>
    public int BasePayNuyen  { get; }

    public int KarmaReward   { get; }

    // ── State ─────────────────────────────────────────────────────────────────

    public bool IsComplete        { get; private set; }
    public bool ObjectiveAchieved { get; private set; }

    /// <summary>True once the nuyen/karma reward has been paid out to the Decker.</summary>
    public bool RewardClaimed     { get; private set; }

    /// <summary>Marks the reward as collected — prevents double-payment.</summary>
    public void MarkRewardClaimed() => RewardClaimed = true;

    // ── Construction ─────────────────────────────────────────────────────────

    public MatrixRun(
        string             johnsonName,
        MatrixRunObjective objective,
        string             targetSystemId,
        string             targetNodeId,
        string             targetNodeTitle,
        string             difficulty,
        int                basePayNuyen,
        int                karmaReward,
        string?            contractedFilename = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(johnsonName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSystemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeTitle);

        if (!ValidDifficulties.Contains(difficulty))
            throw new ArgumentException(
                $"Difficulty must be one of: {string.Join(", ", ValidDifficulties)}.",
                nameof(difficulty));

        if (basePayNuyen < 0)
            throw new ArgumentException("Base pay cannot be negative.", nameof(basePayNuyen));

        if (karmaReward < 0)
            throw new ArgumentException("Karma reward cannot be negative.", nameof(karmaReward));

        if (objective != MatrixRunObjective.CrashCpu && contractedFilename is null)
            throw new ArgumentException(
                "Data-transfer objectives require a ContractedFilename.", nameof(contractedFilename));

        JohnsonName        = johnsonName;
        Objective          = objective;
        TargetSystemId     = targetSystemId;
        TargetNodeId       = targetNodeId;
        TargetNodeTitle    = targetNodeTitle;
        Difficulty         = difficulty;
        BasePayNuyen       = basePayNuyen;
        KarmaReward        = karmaReward;
        ContractedFilename = contractedFilename;
    }

    // ── CyberInfo display ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the lightweight summary shown on the CyberInfo → Run Info screen.
    /// Only meaningful when the Decker is jacked into the target system.
    /// </summary>
    public RunInfo GetRunInfo() =>
        new(TargetNodeId, TargetNodeTitle, Objective, ContractedFilename);

    // ── Objective checking ────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the Decker has completed the required action
    /// at the correct node within the target system.
    /// </summary>
    /// <param name="completedAction">The action that was just executed.</param>
    /// <param name="atNodeId">The node at which the action was executed.</param>
    /// <param name="inSystemId">The system the Decker is currently running.</param>
    /// <param name="transferredFilename">
    /// For data objectives: the filename that was transferred.
    /// Ignored for <see cref="MatrixRunObjective.CrashCpu"/>.
    /// </param>
    public bool CheckObjectiveMet(
        NodeAction completedAction,
        string     atNodeId,
        string     inSystemId,
        string?    transferredFilename = null)
    {
        if (inSystemId != TargetSystemId) return false;
        if (atNodeId   != TargetNodeId)   return false;

        bool actionMatches = Objective switch
        {
            MatrixRunObjective.DownloadData =>
                completedAction == NodeAction.TransferData &&
                transferredFilename == ContractedFilename,

            MatrixRunObjective.UploadData =>
                completedAction == NodeAction.TransferData &&
                transferredFilename == ContractedFilename,

            MatrixRunObjective.DeleteData =>
                completedAction == NodeAction.Erase &&
                transferredFilename == ContractedFilename,

            MatrixRunObjective.CrashCpu =>
                completedAction == NodeAction.CrashSystem,

            _ => throw new InvalidOperationException($"Unhandled objective: {Objective}")
        };

        return actionMatches;
    }

    // ── Completion ────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks the run as complete.
    /// Throws if already completed (programming error — do not call twice).
    /// </summary>
    public void MarkComplete(bool objectiveAchieved = true)
    {
        if (IsComplete)
            throw new InvalidOperationException($"Run '{Id}' is already marked complete.");

        IsComplete        = true;
        ObjectiveAchieved = objectiveAchieved;
    }

    // ── Pay calculation ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the nuyen payment after applying the Decker's negotiation bonus.
    /// Each negotiation level multiplies the base pay by 1.05 (compound 5 % steps).
    /// Negotiation 0 → base pay; Negotiation 3 → base × 1.05³ ≈ +15.8 %.
    /// </summary>
    public int ComputePay(int negotiationRating)
    {
        if (negotiationRating is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(negotiationRating),
                "Negotiation rating must be 0–12.");

        return (int)Math.Round(BasePayNuyen * Math.Pow(1.05, negotiationRating));
    }

    // ── Fresh copy ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a brand-new <see cref="MatrixRun"/> with the same contract
    /// parameters but fully reset state (IsComplete / ObjectiveAchieved /
    /// RewardClaimed all false).
    ///
    /// Always call this when the player re-accepts a contract whose previous
    /// attempt left the catalog entry in a completed (failed) state.
    /// </summary>
    public MatrixRun CreateFresh() =>
        new(JohnsonName, Objective, TargetSystemId, TargetNodeId, TargetNodeTitle,
            Difficulty, BasePayNuyen, KarmaReward, ContractedFilename);

    // ── Static factories ──────────────────────────────────────────────────────

    /// <summary>Creates a simple-tier Matrix run (e.g. from Mortimer Reed).</summary>
    public static MatrixRun CreateSimple(
        string             johnsonName,
        MatrixRunObjective objective,
        string             targetSystemId,
        string             targetNodeId,
        string             targetNodeTitle,
        string?            contractedFilename = null) =>
        new(johnsonName, objective, targetSystemId, targetNodeId, targetNodeTitle,
            "simple", basePayNuyen: 475, karmaReward: 2, contractedFilename);

    /// <summary>Creates a moderate-tier Matrix run (e.g. from Julius Strouther).</summary>
    public static MatrixRun CreateModerate(
        string             johnsonName,
        MatrixRunObjective objective,
        string             targetSystemId,
        string             targetNodeId,
        string             targetNodeTitle,
        string?            contractedFilename = null) =>
        new(johnsonName, objective, targetSystemId, targetNodeId, targetNodeTitle,
            "moderate", basePayNuyen: 2_750, karmaReward: 3, contractedFilename);

    /// <summary>Creates an expert-tier Matrix run (e.g. from Caleb Brightmore).</summary>
    public static MatrixRun CreateExpert(
        string             johnsonName,
        MatrixRunObjective objective,
        string             targetSystemId,
        string             targetNodeId,
        string             targetNodeTitle,
        string?            contractedFilename = null) =>
        new(johnsonName, objective, targetSystemId, targetNodeId, targetNodeTitle,
            "expert", basePayNuyen: 6_100, karmaReward: 5, contractedFilename);

    // ── Constants ─────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> ValidDifficulties =
        ["simple", "moderate", "expert"];

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string status = IsComplete
            ? ObjectiveAchieved ? " [COMPLETE ✓]" : " [FAILED ✗]"
            : " [ACTIVE]";

        return $"[MatrixRun] {Difficulty.ToUpper()} — {Objective} " +
               $"@ {TargetNodeTitle} | {BasePayNuyen}¥ +{KarmaReward}K " +
               $"| Johnson: {JohnsonName}{status}";
    }
}
