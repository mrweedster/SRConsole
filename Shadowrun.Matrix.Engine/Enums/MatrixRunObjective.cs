namespace Shadowrun.Matrix.Enums;

/// <summary>
/// The specific task a Decker must complete inside a Matrix system
/// to fulfil a contracted shadowrun.
/// </summary>
public enum MatrixRunObjective
{
    /// <summary>Retrieve a specific file from a target DS Node.</summary>
    DownloadData,

    /// <summary>Send a file to a specific DS Node.</summary>
    UploadData,

    /// <summary>Erase a specific file from a target DS Node.</summary>
    DeleteData,

    /// <summary>Execute the Crash System command at the system CPU.</summary>
    CrashCpu
}
