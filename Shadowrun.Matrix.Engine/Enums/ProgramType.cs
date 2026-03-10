namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Defines the size and price tier of a program.
/// Medium programs are 2× the size/cost of Small at the same level.
/// Large programs are 3× the size/cost of Small at the same level.
/// Special programs (Degrade, Rebound) use hardcoded sizes and prices.
/// </summary>
public enum ProgramType
{
    Small   = 1,
    Medium  = 2,
    Large   = 3,
    Special = 0
}
