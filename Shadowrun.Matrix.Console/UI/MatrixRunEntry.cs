using Shadowrun.Matrix.Models;

/// <summary>
/// Pairs a <see cref="MatrixRun"/> with the human-readable name of its target
/// Matrix system. The engine model only stores a system ID (GUID), so the
/// display name is captured at catalog-build time for use in the UI.
/// </summary>
public sealed record MatrixRunEntry(MatrixRun Run, string SystemName);
