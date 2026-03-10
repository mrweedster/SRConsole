using Shadowrun.Matrix.Data;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;

/// <summary>
/// Builds the complete list of available Matrix run contracts and returns them
/// as <see cref="MatrixRunEntry"/> records (run + human-readable system name).
///
/// Externalised from <c>Program.cs</c> to keep the entry point minimal and to
/// give each entry access to the resolved system name for UI display.
/// </summary>
public static class MatrixRunCatalog
{
    public static IReadOnlyList<MatrixRunEntry> Build()
    {
        // ── Resolve every referenced system up-front ──────────────────────────
        //   BuildSystem is deterministic: same index → same layout every run.

        var sys0  = SystemCatalog.BuildSystem(0);   // (simple)
        var sys1  = SystemCatalog.BuildSystem(1);   // (simple)
        var sys2  = SystemCatalog.BuildSystem(2);   // (simple)
        var sys3  = SystemCatalog.BuildSystem(3);   // (simple)
        var sys9  = SystemCatalog.BuildSystem(9);   // (simple)

        var sys4  = SystemCatalog.BuildSystem(4);   // (moderate)
        var sys5  = SystemCatalog.BuildSystem(5);   // (moderate)
        var sys6  = SystemCatalog.BuildSystem(6);   // (moderate)
        var sys8  = SystemCatalog.BuildSystem(8);   // (moderate)
        var sys10 = SystemCatalog.BuildSystem(10);  // (moderate)
        var sys11 = SystemCatalog.BuildSystem(11);  // (moderate)
        var sys12 = SystemCatalog.BuildSystem(12);  // Club Penumbra       (moderate)
        var sys13 = SystemCatalog.BuildSystem(13);  // Seattle General     (moderate)

        var sys7  = SystemCatalog.BuildSystem(7);   // Aztechnology        (expert)
        var sys14 = SystemCatalog.BuildSystem(14);  // City Hall           (expert)
        var sys16 = SystemCatalog.BuildSystem(16);  // UCAS Fed. Gov.      (expert)
        var sys18 = SystemCatalog.BuildSystem(18);  // Ito's System        (expert)
        var sys19 = SystemCatalog.BuildSystem(19);  // Hollywood Corr.     (expert)
        var sys20 = SystemCatalog.BuildSystem(20);  // Mitsuhama           (expert)
        var sys22 = SystemCatalog.BuildSystem(22);  // Fuchi               (expert)

        return new List<MatrixRunEntry>
        {
            // ── SIMPLE  (Mortimer Reed, ~475¥, +2 karma) ─────────────────────

            E(sys0.Name,  MatrixRun.CreateSimple(
                johnsonName:     "Mortimer Reed",
                objective:       MatrixRunObjective.CrashCpu,
                targetSystemId:  sys0.Id,
                targetNodeId:    $"{sys0.Id}-4",
                targetNodeTitle: "Unlisted CPU")),

            E(sys1.Name,  MatrixRun.CreateSimple(
                johnsonName:        "Mortimer Reed",
                objective:          MatrixRunObjective.DownloadData,
                targetSystemId:     sys1.Id,
                targetNodeId:       $"{sys1.Id}-2",
                targetNodeTitle:    "Project Files",
                contractedFilename: "proj_alpha.dat")),

            E(sys2.Name,  MatrixRun.CreateSimple(
                johnsonName:        "Mortimer Reed",
                objective:          MatrixRunObjective.DeleteData,
                targetSystemId:     sys2.Id,
                targetNodeId:       $"{sys2.Id}-7",
                targetNodeTitle:    "Security Files",
                contractedFilename: "sec_log_7b.dat")),

            E(sys3.Name,  MatrixRun.CreateSimple(
                johnsonName:        "Mortimer Reed",
                objective:          MatrixRunObjective.UploadData,
                targetSystemId:     sys3.Id,
                targetNodeId:       $"{sys3.Id}-8",
                targetNodeTitle:    "System Files",
                contractedFilename: "patch_v2.dat")),

            E(sys9.Name,  MatrixRun.CreateSimple(
                johnsonName:        "Mortimer Reed",
                objective:          MatrixRunObjective.DownloadData,
                targetSystemId:     sys9.Id,
                targetNodeId:       $"{sys9.Id}-2",
                targetNodeTitle:    "Mngmnt Files",
                contractedFilename: "mgmt_roster.dat")),

            // ── MODERATE  (Julius Strouther, ~2750¥, +3 karma) ───────────────

            E(sys4.Name,  MatrixRun.CreateModerate(
                johnsonName:        "Julius Strouther",
                objective:          MatrixRunObjective.DownloadData,
                targetSystemId:     sys4.Id,
                targetNodeId:       $"{sys4.Id}-1",
                targetNodeTitle:    "Financial Data",
                contractedFilename: "q3_ledger.dat")),

            E(sys5.Name,  MatrixRun.CreateModerate(
                johnsonName:     "Julius Strouther",
                objective:       MatrixRunObjective.CrashCpu,
                targetSystemId:  sys5.Id,
                targetNodeId:    $"{sys5.Id}-A",
                targetNodeTitle: "Unlisted CPU")),

            E(sys6.Name,  MatrixRun.CreateModerate(
                johnsonName:        "Julius Strouther",
                objective:          MatrixRunObjective.DeleteData,
                targetSystemId:     sys6.Id,
                targetNodeId:       $"{sys6.Id}-6",
                targetNodeTitle:    "Financial Data",
                contractedFilename: "audit_trail.dat")),

            E(sys8.Name,  MatrixRun.CreateModerate(
                johnsonName:        "Julius Strouther",
                objective:          MatrixRunObjective.DownloadData,
                targetSystemId:     sys8.Id,
                targetNodeId:       $"{sys8.Id}-2",
                targetNodeTitle:    "Legal Files",
                contractedFilename: "case_7731.dat")),

            E(sys10.Name, MatrixRun.CreateModerate(
                johnsonName:     "Julius Strouther",
                objective:       MatrixRunObjective.CrashCpu,
                targetSystemId:  sys10.Id,
                targetNodeId:    $"{sys10.Id}-7",
                targetNodeTitle: "Unlisted CPU")),

            E(sys11.Name, MatrixRun.CreateModerate(
                johnsonName:        "Julius Strouther",
                objective:          MatrixRunObjective.UploadData,
                targetSystemId:     sys11.Id,
                targetNodeId:       $"{sys11.Id}-1",
                targetNodeTitle:    "Security Files",
                contractedFilename: "clearance_fake.dat")),

            E(sys12.Name, MatrixRun.CreateModerate(
                johnsonName:        "Julius Strouther",
                objective:          MatrixRunObjective.DeleteData,
                targetSystemId:     sys12.Id,
                targetNodeId:       $"{sys12.Id}-B",
                targetNodeTitle:    "Financial Data",
                contractedFilename: "ledger_penumbra.dat")),

            E(sys13.Name, MatrixRun.CreateModerate(
                johnsonName:        "Julius Strouther",
                objective:          MatrixRunObjective.DownloadData,
                targetSystemId:     sys13.Id,
                targetNodeId:       $"{sys13.Id}-7",
                targetNodeTitle:    "Mngmnt Files",
                contractedFilename: "patient_list.dat")),

            // ── EXPERT  (Caleb Brightmore, ~6100¥, +5 karma) ─────────────────

            E(sys7.Name,  MatrixRun.CreateExpert(
                johnsonName:     "Caleb Brightmore",
                objective:       MatrixRunObjective.CrashCpu,
                targetSystemId:  sys7.Id,
                targetNodeId:    $"{sys7.Id}-2",
                targetNodeTitle: "Aztechnology CPU")),

            E(sys14.Name, MatrixRun.CreateExpert(
                johnsonName:        "Caleb Brightmore",
                objective:          MatrixRunObjective.DownloadData,
                targetSystemId:     sys14.Id,
                targetNodeId:       $"{sys14.Id}-F",
                targetNodeTitle:    "Financial Data",
                contractedFilename: "city_funds.dat")),

            E(sys16.Name, MatrixRun.CreateExpert(
                johnsonName:        "Caleb Brightmore",
                objective:          MatrixRunObjective.DeleteData,
                targetSystemId:     sys16.Id,
                targetNodeId:       $"{sys16.Id}-D",
                targetNodeTitle:    "Prisoner Files",
                contractedFilename: "inmate_7734.dat")),

            E(sys18.Name, MatrixRun.CreateExpert(
                johnsonName:     "Caleb Brightmore",
                objective:       MatrixRunObjective.CrashCpu,
                targetSystemId:  sys18.Id,
                targetNodeId:    $"{sys18.Id}-5",
                targetNodeTitle: "Ito's System CPU")),

            E(sys19.Name, MatrixRun.CreateExpert(
                johnsonName:        "Caleb Brightmore",
                objective:          MatrixRunObjective.DownloadData,
                targetSystemId:     sys19.Id,
                targetNodeId:       $"{sys19.Id}-9",
                targetNodeTitle:    "Security Files",
                contractedFilename: "guard_roster.dat")),

            E(sys20.Name, MatrixRun.CreateExpert(
                johnsonName:        "Caleb Brightmore",
                objective:          MatrixRunObjective.UploadData,
                targetSystemId:     sys20.Id,
                targetNodeId:       $"{sys20.Id}-P",
                targetNodeTitle:    "System Files",
                contractedFilename: "backdoor_mk2.dat")),

            E(sys22.Name, MatrixRun.CreateExpert(
                johnsonName:        "Caleb Brightmore",
                objective:          MatrixRunObjective.DeleteData,
                targetSystemId:     sys22.Id,
                targetNodeId:       $"{sys22.Id}-5",
                targetNodeTitle:    "Security Files",
                contractedFilename: "blacklist_r9.dat")),
        }.AsReadOnly();
    }

    // ── Shorthand ─────────────────────────────────────────────────────────────

    private static MatrixRunEntry E(string systemName, MatrixRun run) =>
        new(run, systemName);
}
