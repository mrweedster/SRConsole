using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.Data;

/// <summary>
/// Catalog of all 25 hard-coded Matrix systems from Shadowrun Genesis.
/// </summary>
public static class SystemCatalog
{
    /// <summary>All 25 systems indexed by their Pick System number (0–24).</summary>
    public static IReadOnlyDictionary<int, SystemDefinition> All { get; } =
        BuildAll().ToDictionary(s => s.SystemNumber).AsReadOnly();

    public static MatrixSystem BuildSystem(int systemNumber, Random? rng = null)
    {
        if (!All.TryGetValue(systemNumber, out var def))
            throw new ArgumentOutOfRangeException(nameof(systemNumber),
                $"No system defined for number {systemNumber}.");
        return SystemFactory.Build(def, rng);
    }

    // ── Catalog lookup for display ────────────────────────────────────────────

    /// <summary>Returns the NodeDefinition for a given system/key pair, for TODO-note display.</summary>
    public static NodeDefinition? FindNodeDef(int systemNumber, string nodeKey)
    {
        if (!All.TryGetValue(systemNumber, out var def)) return null;
        return def.Nodes.FirstOrDefault(n => n.Key == nodeKey);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static SmModuleType InferSmType(string title)
    {
        if (title.Contains("Maglock", StringComparison.OrdinalIgnoreCase))
            return SmModuleType.Maglocks;
        if (title.Contains("Camera", StringComparison.OrdinalIgnoreCase))
            return SmModuleType.Cameras;
        if (title.Equals("Alert Control", StringComparison.OrdinalIgnoreCase))
            return SmModuleType.AlertControl;
        return SmModuleType.Generic;
    }

    private static NodeDefinition Nd(
        string key, NodeType type, NodeColor color, int sr, string title,
        IceType? primaryIce = null, int primaryLv = 0,
        IceType? secondaryIce = null, int secondaryLv = 0,
        IceType? todoIce = null, int todoLv = 0)
    {
        bool secIsTar = secondaryIce is IceType.TarPaper or IceType.TarPit;

        return new NodeDefinition
        {
            Key            = key,
            Type           = type,
            Color          = color,
            SecurityRating = sr,
            Title          = title,
            SmModuleType   = type == NodeType.SM ? InferSmType(title) : null,
            PrimaryIce     = primaryIce is null ? null
                             : new IceDefinition { Type = primaryIce.Value, BaseRating = primaryLv },
            SecondaryIce   = (secondaryIce is not null && secIsTar)
                             ? new IceDefinition { Type = secondaryIce.Value, BaseRating = secondaryLv }
                             : null,
            // Non-Tar secondary stored as TODO
            TodoSecondaryIce = (secondaryIce is not null && !secIsTar)
                             ? new IceDefinition { Type = secondaryIce.Value, BaseRating = secondaryLv }
                             : todoIce is not null
                             ? new IceDefinition { Type = todoIce.Value, BaseRating = todoLv }
                             : null,
        };
    }

    private static IEnumerable<SystemDefinition> BuildAll()
    {
        yield return System0();
        yield return System1();
        yield return System2();
        yield return System3();
        yield return System4();
        yield return System5();
        yield return System6();
        yield return System7();
        yield return System8();
        yield return System9();
        yield return System10();
        yield return System11();
        yield return System12();
        yield return System13();
        yield return System14();
        yield return System15();
        yield return System16();
        yield return System17();
        yield return System18();
        yield return System19();
        yield return System20();
        yield return System21();
        yield return System22();
        yield return System23();
        yield return System24();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // System 0 — Unlisted (simple)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System0() => new()
    {
        SystemNumber = 0, Name = "Unlisted", Difficulty = "simple",
        Nodes =
        [
            Nd("1", NodeType.SM,  NodeColor.Green,  4, "Maglocks",      IceType.Killer,     1),
            Nd("2", NodeType.DS,  NodeColor.Orange, 4, "Financial Data", IceType.Killer,     2),
            Nd("3", NodeType.DS,  NodeColor.Green,  3, "Mngmnt Files",   IceType.TraceAndDump,2),
            Nd("4", NodeType.CPU, NodeColor.Orange, 4, "Unlisted",       IceType.Barrier,    2, IceType.TarPit,  1),
            Nd("5", NodeType.DS,  NodeColor.Green,  2, "Outdated Files", IceType.Barrier,    2),
            Nd("6", NodeType.IOP, NodeColor.Blue,   3, "Terminal"),
            Nd("7", NodeType.SAN, NodeColor.Blue,   4, "Unlisted"),
        ],
        Edges = [("1","2"),("1","4"),("2","4"),("3","4"),("4","5"),("4","6"),("4","7"),("6","7")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 1 — Unlisted (simple)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System1() => new()
    {
        SystemNumber = 1, Name = "Unlisted", Difficulty = "simple",
        Nodes =
        [
            // Node 1: Access+Killer (two non-Tar) → TODO secondary
            Nd("1", NodeType.SAN, NodeColor.Green,  4, "Unlisted",       IceType.Access,  2, todoIce: IceType.Killer,  todoLv: 1),
            // Node 2: Barrier+Blaster (two non-Tar) → TODO secondary
            Nd("2", NodeType.DS,  NodeColor.Orange, 4, "Project Files",  IceType.Barrier, 3, todoIce: IceType.Blaster, todoLv: 4),
            Nd("3", NodeType.IOP, NodeColor.Green,  3, "Terminal",       IceType.Access,  2),
            Nd("4", NodeType.CPU, NodeColor.Orange, 4, "Unlisted",       IceType.Barrier, 3, IceType.TarPaper, 3),
            // Node 5: Access+Killer (two non-Tar) → TODO secondary
            Nd("5", NodeType.SM,  NodeColor.Orange, 4, "Cameras",        IceType.Access,  3, todoIce: IceType.Killer,  todoLv: 2),
            Nd("6", NodeType.DS,  NodeColor.Orange, 3, "Mngmnt Files",   IceType.Killer,  4, IceType.TarPaper, 4),
        ],
        Edges = [("1","3"),("2","4"),("2","6"),("3","4"),("3","5"),("4","5"),("4","6")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 2 — Unlisted (simple)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System2() => new()
    {
        SystemNumber = 2, Name = "Unlisted", Difficulty = "simple",
        Nodes =
        [
            Nd("1", NodeType.DS,  NodeColor.Green,  3, "Dead Projects",   IceType.Blaster,     2, IceType.TarPaper, 2),
            Nd("2", NodeType.SPU, NodeColor.Green,  3, "Data Routing",    IceType.TraceAndDump, 3),
            Nd("3", NodeType.SAN, NodeColor.Orange, 3, "Unlisted",        IceType.Barrier,     2),
            Nd("4", NodeType.IOP, NodeColor.Green,  2, "Terminal",        IceType.Access,      2),
            // Node 5: Barrier+Killer (two non-Tar) → TODO
            Nd("5", NodeType.SPU, NodeColor.Orange, 4, "Research",        IceType.Barrier,     3, todoIce: IceType.Killer, todoLv: 4),
            Nd("6", NodeType.CPU, NodeColor.Orange, 5, "Unlisted",        IceType.Blaster,     3),
            Nd("7", NodeType.DS,  NodeColor.Orange, 3, "Security Files",  IceType.Blaster,     3, IceType.TarPaper, 3),
            Nd("8", NodeType.SM,  NodeColor.Orange, 4, "Security Cntrl",  IceType.Access,      3, IceType.TarPit,   3),
            Nd("9", NodeType.DS,  NodeColor.Orange, 3, "Project Files",   IceType.Blaster,     3),
        ],
        Edges = [("1","2"),("2","3"),("2","4"),("2","5"),("4","5"),("5","6"),("5","8"),("5","9"),("6","7")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 3 — Unlisted (simple)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System3() => new()
    {
        SystemNumber = 3, Name = "Unlisted", Difficulty = "simple",
        Nodes =
        [
            Nd("1", NodeType.IOP, NodeColor.Green,  4, "Terminal",      IceType.Blaster,      1),
            Nd("2", NodeType.IOP, NodeColor.Blue,   4, "Terminal"),
            Nd("3", NodeType.CPU, NodeColor.Green,  5, "Unlisted",      IceType.TraceAndBurn, 2),
            Nd("4", NodeType.SPU, NodeColor.Green,  3, "Data Routing",  IceType.Killer,       2),
            Nd("5", NodeType.SAN, NodeColor.Blue,   4, "Unlisted"),
            Nd("6", NodeType.SM,  NodeColor.Blue,   2, "HVAC Systems"),
            // Node 7: Barrier+Blaster (two non-Tar) → TODO
            Nd("7", NodeType.DS,  NodeColor.Green,  4, "Mngmnt Files",  IceType.Barrier,      2, todoIce: IceType.Blaster, todoLv: 2),
            Nd("8", NodeType.DS,  NodeColor.Green,  3, "System Files",  IceType.Access,       1),
        ],
        Edges = [("1","3"),("2","4"),("3","4"),("3","7"),("4","5"),("4","6"),("7","8")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 4 — Unlisted (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System4() => new()
    {
        SystemNumber = 4, Name = "Unlisted", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.DS,  NodeColor.Green,  3, "Financial Data", IceType.Barrier,     2),
            // Node 2: Access+Killer → TODO
            Nd("2", NodeType.SPU, NodeColor.Orange, 3, "Retail Control", IceType.Access,      2, todoIce: IceType.Killer,  todoLv: 2),
            Nd("3", NodeType.CPU, NodeColor.Orange, 4, "Unlisted",       IceType.Barrier,     3, IceType.TarPit,   3),
            Nd("4", NodeType.SAN, NodeColor.Blue,   4, "Unlisted"),
            // Node 5: Barrier+Blaster → TODO
            Nd("5", NodeType.DS,  NodeColor.Orange, 4, "Mngmnt Files",   IceType.Barrier,     2, todoIce: IceType.Blaster, todoLv: 2),
            Nd("6", NodeType.SM,  NodeColor.Orange, 4, "Maglocks",       IceType.TraceAndBurn, 2),
            Nd("7", NodeType.IOP, NodeColor.Blue,   3, "Terminal"),
        ],
        Edges = [("1","2"),("2","3"),("2","4"),("2","6"),("2","7"),("3","5"),("3","6")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 5 — Unlisted (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System5() => new()
    {
        SystemNumber = 5, Name = "Unlisted", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Green,  4, "Unlisted",       IceType.Access,   2),
            Nd("2", NodeType.IOP, NodeColor.Green,  4, "Terminal",       IceType.Blaster,  2),
            // Node 3: Barrier+Blaster → TODO
            Nd("3", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",   IceType.Barrier,  3, todoIce: IceType.Blaster, todoLv: 2),
            Nd("4", NodeType.SPU, NodeColor.Orange, 4, "Security",       IceType.Blaster,  3, IceType.TarPaper, 3),
            Nd("5", NodeType.DS,  NodeColor.Orange, 4, "Security Files", IceType.Blaster,  3, IceType.TarPit,   3),
            // Node 6: Barrier+Killer → TODO
            Nd("6", NodeType.IOP, NodeColor.Green,  5, "Terminal",       IceType.Barrier,  2, todoIce: IceType.Killer,  todoLv: 1),
            Nd("7", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",    IceType.Barrier,  2, IceType.TarPaper, 2),
            // Node 8: Access+Killer → TODO
            Nd("8", NodeType.SPU, NodeColor.Orange, 3, "Office Mangmnt", IceType.Access,   3, todoIce: IceType.Killer,  todoLv: 3),
            Nd("9", NodeType.SM,  NodeColor.Red,    3, "Alert Control",  IceType.BlackIce, 3),
            Nd("A", NodeType.CPU, NodeColor.Red,    4, "Unlisted",       IceType.BlackIce, 3),
            Nd("B", NodeType.DS,  NodeColor.Orange, 3, "Dead Projects",  IceType.Access,   2, IceType.TraceAndBurn, 3),
            Nd("C", NodeType.DS,  NodeColor.Green,  4, "System Files",   IceType.Blaster,  2),
            Nd("D", NodeType.DS,  NodeColor.Red,    3, "Project Files",  IceType.Killer,   3, IceType.TarPit,   3),
        ],
        Edges = [("1","3"),("2","3"),("3","4"),("3","6"),("4","5"),("4","7"),("4","8"),
                 ("5","8"),("8","A"),("8","C"),("9","A"),("A","D"),("B","C")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 6 — Unlisted (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System6() => new()
    {
        SystemNumber = 6, Name = "Unlisted", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.DS,  NodeColor.Green,  2, "Outdated Files",  IceType.Blaster,      3),
            Nd("2", NodeType.DS,  NodeColor.Orange, 4, "Mngmnt Files",    IceType.Access,       3, IceType.TarPit,   3),
            Nd("3", NodeType.DS,  NodeColor.Orange, 4, "Project Files",   IceType.Barrier,      3, IceType.TraceAndBurn, 2),
            Nd("4", NodeType.SPU, NodeColor.Orange, 3, "Shipping",        IceType.Killer,       2, IceType.TarPaper, 2),
            Nd("5", NodeType.CPU, NodeColor.Orange, 5, "Unlisted",        IceType.Barrier,      4, IceType.TarPit,   4),
            Nd("6", NodeType.DS,  NodeColor.Orange, 5, "Financial Data",  IceType.Barrier,      4, IceType.TraceAndDump, 3),
            Nd("7", NodeType.IOP, NodeColor.Green,  4, "Terminal",        IceType.Access,       3),
            Nd("8", NodeType.SM,  NodeColor.Green,  3, "Automtd. Equip",  IceType.TraceAndDump, 3),
            // Node 9: Access+TraceAndDump → TODO (two non-Tar primaries)
            Nd("9", NodeType.SPU, NodeColor.Orange, 4, "Office Mangmnt",  IceType.Access,       3, todoIce: IceType.TraceAndDump, todoLv: 4),
            Nd("A", NodeType.SAN, NodeColor.Green,  3, "Unlisted",        IceType.Blaster,      2),
        ],
        Edges = [("1","3"),("1","4"),("2","5"),("3","4"),("4","9"),("5","6"),("5","8"),("5","9"),("7","9"),("8","9"),("9","A")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 7 — Aztechnology (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System7() => new()
    {
        SystemNumber = 7, Name = "Aztechnology", Difficulty = "expert", CorporationName = "Aztechnology",
        Nodes =
        [
            Nd("1", NodeType.SM,  NodeColor.Red,    4, "Alert Control",   IceType.BlackIce,    4),
            // Node 2: Barrier+BlackIce → TODO (two non-Tar, BlackIce is not Tar)
            Nd("2", NodeType.CPU, NodeColor.Red,    5, "Aztechnology",    IceType.Barrier,     6, todoIce: IceType.BlackIce, todoLv: 4),
            Nd("3", NodeType.DS,  NodeColor.Red,    4, "Confidntl Data",  IceType.Barrier,     5, IceType.TarPit,   5),
            Nd("4", NodeType.SM,  NodeColor.Green,  4, "Elevator Cntrl",  IceType.TraceAndDump, 4),
            Nd("5", NodeType.DS,  NodeColor.Orange, 5, "Project Files",   IceType.Killer,      5, IceType.TarPaper, 5),
            // Node 6: Barrier+Blaster → TODO
            Nd("6", NodeType.SPU, NodeColor.Green,  4, "Building Maint",  IceType.Barrier,     5, todoIce: IceType.Blaster, todoLv: 5),
            Nd("7", NodeType.SM,  NodeColor.Orange, 5, "Automtd. Equip",  IceType.TraceAndBurn, 4),
            // Node 8: Access+BlackIce → TODO
            Nd("8", NodeType.IOP, NodeColor.Red,    4, "Matrix Jack",     IceType.Access,      6, todoIce: IceType.BlackIce, todoLv: 4),
            Nd("9", NodeType.DS,  NodeColor.Blue,   5, "Outdated Files"),
            Nd("A", NodeType.DS,  NodeColor.Red,    5, "Project Files",   IceType.BlackIce,    5),
            // Node B: Barrier+BlackIce → TODO
            Nd("B", NodeType.SPU, NodeColor.Orange, 4, "Marketing",       IceType.Barrier,     4, todoIce: IceType.BlackIce, todoLv: 3),
            // Node C: Access+TraceAndDump → TODO
            Nd("C", NodeType.SPU, NodeColor.Orange, 5, "Executive Area",  IceType.Access,      4, todoIce: IceType.TraceAndDump, todoLv: 4),
            Nd("D", NodeType.SPU, NodeColor.Green,  4, "Data Routing",    IceType.TraceAndDump, 4),
            Nd("E", NodeType.SPU, NodeColor.Red,    5, "Research",        IceType.Blaster,     5, IceType.TarPit,   5),
            Nd("F", NodeType.DS,  NodeColor.Green,  4, "Competition",     IceType.Access,      4, IceType.TarPit,   4),
            Nd("G", NodeType.SPU, NodeColor.Green,  4, "Data Routing",    IceType.TraceAndBurn, 4),
            // Node H: Access+BlackIce → TODO
            Nd("H", NodeType.DS,  NodeColor.Orange, 5, "Marketing Data",  IceType.Access,      5, todoIce: IceType.BlackIce, todoLv: 3),
            Nd("I", NodeType.DS,  NodeColor.Orange, 4, "System Files",    IceType.Killer,      3),
            Nd("J", NodeType.DS,  NodeColor.Red,    4, "Security Files",  IceType.TraceAndDump, 4),
            // Node K: Barrier+TarPit is valid Tar secondary
            Nd("K", NodeType.SPU, NodeColor.Orange, 4, "Office Mangmnt",  IceType.Barrier,     5, IceType.TarPit,   5),
            Nd("L", NodeType.SPU, NodeColor.Red,    5, "Security",        IceType.BlackIce,    5, IceType.TarPaper, 5),
            // Node M: Access+TarPaper is valid
            Nd("M", NodeType.SM,  NodeColor.Green,  4, "Automtd. Equip",  IceType.Access,      4, IceType.TarPaper, 4),
            // Node N: Access+TraceAndBurn → TODO (two non-Tar)
            Nd("N", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Access,      4, todoIce: IceType.TraceAndBurn, todoLv: 3),
            // Node O: Barrier+Blaster → TODO
            Nd("O", NodeType.DS,  NodeColor.Green,  4, "Project Files",   IceType.Barrier,     4, todoIce: IceType.Blaster,     todoLv: 3),
            Nd("P", NodeType.IOP, NodeColor.Green,  4, "Terminal",        IceType.TraceAndDump, 3),
            // Node Q: Access+TraceAndBurn → TODO
            Nd("Q", NodeType.DS,  NodeColor.Orange, 4, "System Files",    IceType.Access,      4, todoIce: IceType.TraceAndBurn, todoLv: 3),
            Nd("R", NodeType.SM,  NodeColor.Red,    4, "Cameras",         IceType.Barrier,     4, IceType.TarPit,   4),
            Nd("S", NodeType.SAN, NodeColor.Orange, 4, "Aztechnology",    IceType.Blaster,     4),
            // Node T: Access+BlackIce → TODO
            Nd("T", NodeType.SM,  NodeColor.Red,    4, "Alert Control",   IceType.Access,      6, todoIce: IceType.BlackIce,    todoLv: 4),
        ],
        Edges =
        [
            ("1","2"),("2","3"),("2","5"),("2","C"),("3","5"),("4","6"),("6","8"),("6","C"),
            ("6","D"),("6","G"),("7","E"),("9","D"),("A","E"),("B","C"),("B","F"),("B","H"),
            ("C","D"),("C","G"),("C","K"),("D","E"),("D","G"),("D","J"),("D","L"),("F","H"),
            ("G","I"),("G","K"),("G","L"),("G","N"),("J","L"),("K","L"),("K","M"),("K","N"),
            ("K","O"),("L","N"),("L","R"),("L","T"),("N","P"),("N","Q"),("N","S")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 8 — Unlisted (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System8() => new()
    {
        SystemNumber = 8, Name = "Unlisted", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.DS,  NodeColor.Blue,   4, "Outdated Files"),
            Nd("2", NodeType.DS,  NodeColor.Green,  3, "Legal Files",     IceType.Killer,  3),
            Nd("3", NodeType.SM,  NodeColor.Blue,   2, "HVAC Systems"),
            Nd("4", NodeType.SPU, NodeColor.Green,  5, "Office Mangmnt",  IceType.Blaster, 3),
            // Node 5: Access+Blaster → TODO
            Nd("5", NodeType.IOP, NodeColor.Green,  3, "Terminal",        IceType.Access,  2, todoIce: IceType.Blaster, todoLv: 2),
            // Node 6: Access+Killer → TODO
            Nd("6", NodeType.SPU, NodeColor.Green,  4, "Data Routing",    IceType.Access,  4, todoIce: IceType.Killer,  todoLv: 2),
            // Node 7: Barrier+Killer → TODO
            Nd("7", NodeType.SPU, NodeColor.Orange, 4, "Executive Area",  IceType.Barrier, 4, todoIce: IceType.Killer,  todoLv: 3),
            Nd("8", NodeType.DS,  NodeColor.Orange, 4, "Case Files",      IceType.Barrier, 5, IceType.TarPit,   5),
            // Node 9: Barrier+BlackIce → TODO
            Nd("9", NodeType.CPU, NodeColor.Orange, 4, "Unlisted",        IceType.Barrier, 5, todoIce: IceType.BlackIce, todoLv: 3),
            Nd("A", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.Blaster, 4),
            // Node B: Barrier+BlackIce → TODO
            Nd("B", NodeType.DS,  NodeColor.Orange, 4, "Project Files",   IceType.Barrier, 5, todoIce: IceType.BlackIce, todoLv: 4),
            Nd("C", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",     IceType.Killer,  3),
            Nd("D", NodeType.SAN, NodeColor.Blue,   5, "Unlisted"),
        ],
        Edges = [("1","4"),("2","4"),("3","4"),("4","6"),("4","7"),("4","D"),
                 ("5","6"),("6","7"),("6","9"),("6","D"),("7","A"),("7","B"),("7","D"),("8","9"),("9","C")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 9 — Unlisted (simple)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System9() => new()
    {
        SystemNumber = 9, Name = "Unlisted", Difficulty = "simple",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Green,  3, "Unlisted",       IceType.Access,      2),
            Nd("2", NodeType.DS,  NodeColor.Green,  4, "Mngmnt Files",   IceType.TraceAndDump, 3),
            Nd("3", NodeType.IOP, NodeColor.Blue,   3, "Terminal"),
            Nd("4", NodeType.CPU, NodeColor.Green,  5, "Unlisted",       IceType.TraceAndDump, 3),
            Nd("5", NodeType.DS,  NodeColor.Green,  3, "System Files",   IceType.Killer,      2),
            Nd("6", NodeType.SM,  NodeColor.Blue,   3, "Automtd. Equip"),
        ],
        Edges = [("1","4"),("2","5"),("3","4"),("4","5"),("4","6")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 10 — Unlisted (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System10() => new()
    {
        SystemNumber = 10, Name = "Unlisted", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Green,  4, "Unlisted",         IceType.Access,      4),
            Nd("2", NodeType.SM,  NodeColor.Green,  3, "Automtd. Equip",   IceType.Blaster,     3),
            Nd("3", NodeType.SPU, NodeColor.Orange, 3, "Office Mangmnt",   IceType.TraceAndBurn, 3),
            Nd("4", NodeType.IOP, NodeColor.Orange, 3, "Terminal",         IceType.Access,      3),
            // Node 5: Access+Blaster → TODO
            Nd("5", NodeType.SM,  NodeColor.Orange, 3, "Simsense Rec.",    IceType.Access,      3, todoIce: IceType.Blaster, todoLv: 2),
            // Node 6: Barrier+Blaster → TODO
            Nd("6", NodeType.SPU, NodeColor.Orange, 4, "Research",         IceType.Barrier,     5, todoIce: IceType.Blaster, todoLv: 4),
            Nd("7", NodeType.CPU, NodeColor.Orange, 5, "Unlisted",         IceType.Barrier,     4, IceType.TarPit,   4),
            Nd("8", NodeType.DS,  NodeColor.Orange, 4, "Marketing Data",   IceType.Access,      3, IceType.TarPaper, 3),
            Nd("9", NodeType.DS,  NodeColor.Green,  3, "System Files",     IceType.Access,      3),
            Nd("A", NodeType.DS,  NodeColor.Orange, 4, "Security Files",   IceType.Killer,      4, IceType.TarPaper, 4),
            Nd("B", NodeType.DS,  NodeColor.Orange, 4, "Simsense Files",   IceType.TraceAndBurn, 6),
        ],
        Edges = [("1","3"),("2","3"),("3","4"),("3","5"),("3","6"),("3","7"),
                 ("5","6"),("5","7"),("6","7"),("6","8"),("7","9"),("7","A"),("8","B"),("9","A")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 11 — Unlisted (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System11() => new()
    {
        SystemNumber = 11, Name = "Unlisted", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.DS,  NodeColor.Green,  4, "Security Files", IceType.Barrier,     3),
            Nd("2", NodeType.SAN, NodeColor.Green,  3, "Unlisted",       IceType.Access,      1),
            Nd("3", NodeType.IOP, NodeColor.Orange, 4, "Terminal",       IceType.Killer,      2),
            Nd("4", NodeType.SM,  NodeColor.Green,  4, "Security Cntrl", IceType.Barrier,     3),
            Nd("5", NodeType.SPU, NodeColor.Green,  4, "Data Routing",   IceType.TraceAndBurn, 4),
            // Node 6: Access+Killer → TODO
            Nd("6", NodeType.CPU, NodeColor.Orange, 4, "Unlisted",       IceType.Access,      4, todoIce: IceType.Killer, todoLv: 3),
            Nd("7", NodeType.DS,  NodeColor.Orange, 3, "System Files",   IceType.Blaster,     4),
            Nd("8", NodeType.IOP, NodeColor.Orange, 3, "Terminal",       IceType.Blaster,     3),
        ],
        Edges = [("1","5"),("2","5"),("3","5"),("3","6"),("4","6"),("5","6"),("5","7"),("6","8")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 12 — Club Penumbra (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System12() => new()
    {
        SystemNumber = 12, Name = "Club Penumbra", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Green,  4, "Club Penumbra",   IceType.Access,   4),
            Nd("2", NodeType.IOP, NodeColor.Green,  3, "Terminal",        IceType.Access,   4),
            Nd("3", NodeType.SM,  NodeColor.Orange, 4, "Automtd. Equip",  IceType.Barrier,  4),
            Nd("4", NodeType.SPU, NodeColor.Orange, 4, "Building Maint",  IceType.Blaster,  4),
            // Node 5: Access+Blaster → TODO
            Nd("5", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",     IceType.Access,   3, todoIce: IceType.Blaster, todoLv: 2),
            Nd("6", NodeType.SPU, NodeColor.Orange, 5, "Retail Control",  IceType.Blaster,  3),
            // Node 7: Access+Blaster → TODO
            Nd("7", NodeType.DS,  NodeColor.Green,  4, "System Files",    IceType.Access,   3, todoIce: IceType.Blaster, todoLv: 1),
            Nd("8", NodeType.SPU, NodeColor.Orange, 5, "Security",        IceType.Barrier,  4, IceType.TarPit,   4),
            Nd("9", NodeType.SM,  NodeColor.Orange, 4, "Cameras",         IceType.Killer,   4),
            Nd("A", NodeType.DS,  NodeColor.Green,  4, "System Files",    IceType.Killer,   3),
            // Node B: Barrier+BlackIce → TODO
            Nd("B", NodeType.DS,  NodeColor.Orange, 4, "Financial Data",  IceType.Barrier,  4, todoIce: IceType.BlackIce, todoLv: 2),
            // Node C: Barrier+BlackIce → TODO
            Nd("C", NodeType.CPU, NodeColor.Orange, 6, "Unlisted",        IceType.Barrier,  5, todoIce: IceType.BlackIce, todoLv: 3),
            Nd("D", NodeType.DS,  NodeColor.Green,  3, "Outdated Files",  IceType.Access,   3),
            Nd("E", NodeType.DS,  NodeColor.Orange, 5, "Security Files",  IceType.BlackIce, 3),
        ],
        Edges = [("1","4"),("1","6"),("2","6"),("3","4"),("4","6"),("4","7"),("5","6"),("5","8"),
                 ("6","8"),("6","A"),("6","C"),("8","9"),("8","E"),("A","C"),("B","C"),("C","D")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 13 — Seattle General Hospital (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System13() => new()
    {
        SystemNumber = 13, Name = "Seattle General Hospital", Difficulty = "moderate",
        Nodes =
        [
            // Node 1: Access+Killer → TODO
            Nd("1", NodeType.SAN, NodeColor.Green,  5, "Seattle Gneral",  IceType.Access,      4, todoIce: IceType.Killer, todoLv: 3),
            Nd("2", NodeType.DS,  NodeColor.Green,  4, "Outdated Files",  IceType.Blaster,     4),
            Nd("3", NodeType.SPU, NodeColor.Orange, 4, "Office Mangmnt",  IceType.Barrier,     3),
            Nd("4", NodeType.DS,  NodeColor.Green,  3, "System Files",    IceType.Access,      4),
            Nd("5", NodeType.IOP, NodeColor.Blue,   5, "Terminal"),
            Nd("6", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Barrier,     5, IceType.TraceAndBurn, 5),
            Nd("7", NodeType.DS,  NodeColor.Orange, 5, "Mngmnt Files",    IceType.Barrier,     5, IceType.TraceAndBurn, 4),
            // Node 8: Access+Blaster → TODO
            Nd("8", NodeType.IOP, NodeColor.Green,  3, "Terminal",        IceType.Access,      4, todoIce: IceType.Blaster, todoLv: 3),
            Nd("9", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",     IceType.Blaster,     6),
            // Node A: Barrier+Blaster → TODO
            Nd("A", NodeType.CPU, NodeColor.Orange, 5, "Unlisted",        IceType.Barrier,     6, todoIce: IceType.Blaster, todoLv: 5),
            Nd("B", NodeType.SM,  NodeColor.Orange, 4, "Automtd. Equip",  IceType.Blaster,     5),
        ],
        Edges = [("1","3"),("2","3"),("2","4"),("2","6"),("3","4"),("3","5"),("3","6"),
                 ("3","7"),("4","6"),("6","8"),("6","9"),("6","A"),("A","B")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 14 — City Hall (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System14() => new()
    {
        SystemNumber = 14, Name = "City Hall", Difficulty = "expert", CorporationName = "City Hall",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Green,  4, "City Hall",       IceType.Access,      4),
            // Node 2: Access+Blaster → TODO
            Nd("2", NodeType.DS,  NodeColor.Orange, 4, "Mngmnt Files",    IceType.Access,      4, todoIce: IceType.Blaster,     todoLv: 4),
            Nd("3", NodeType.DS,  NodeColor.Green,  4, "Outdated Files",  IceType.Killer,      4),
            Nd("4", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Access,      5),
            Nd("5", NodeType.IOP, NodeColor.Green,  3, "Terminal",        IceType.TraceAndDump, 3),
            Nd("6", NodeType.SM,  NodeColor.Green,  4, "HVAC Systems"),
            Nd("7", NodeType.SPU, NodeColor.Orange, 4, "Office Mangmnt",  IceType.Barrier,     4, IceType.TarPaper, 4),
            Nd("8", NodeType.SM,  NodeColor.Green,  3, "Automtd. Equip",  IceType.Access,      3),
            Nd("9", NodeType.SM,  NodeColor.Green,  5, "Satellite Feed",  IceType.Blaster,     5),
            Nd("A", NodeType.DS,  NodeColor.Orange, 5, "Project Files",   IceType.Access,      4, IceType.TarPit,   4),
            Nd("B", NodeType.IOP, NodeColor.Orange, 6, "Matrix Jack",     IceType.TraceAndDump, 5),
            Nd("C", NodeType.SM,  NodeColor.Orange, 4, "Cameras",         IceType.Blaster,     5),
            // Node D: Access+TraceAndBurn → TODO
            Nd("D", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.Access,      4, todoIce: IceType.TraceAndBurn, todoLv: 4),
            // Node E: Barrier+BlackIce → TODO
            Nd("E", NodeType.SPU, NodeColor.Orange, 7, "Executive Area",  IceType.Barrier,     5, todoIce: IceType.BlackIce,    todoLv: 4),
            Nd("F", NodeType.DS,  NodeColor.Red,    5, "Financial Data",  IceType.BlackIce,    5),
            Nd("G", NodeType.SM,  NodeColor.Orange, 5, "Maglocks",        IceType.Barrier,     5, IceType.TarPit,   5),
            Nd("H", NodeType.SPU, NodeColor.Red,    5, "Security",        IceType.BlackIce,    5, IceType.TarPaper, 5),
            Nd("I", NodeType.DS,  NodeColor.Red,    5, "Security Files",  IceType.Blaster,     6, IceType.TarPit,   6),
            // Node J: Barrier+Killer → TODO
            Nd("J", NodeType.SM,  NodeColor.Red,    5, "Alert Control",   IceType.Barrier,     7, todoIce: IceType.Killer,      todoLv: 7),
            Nd("K", NodeType.CPU, NodeColor.Red,    7, "City Hall",       IceType.BlackIce,    6, IceType.TarPit,   6),
            Nd("L", NodeType.SM,  NodeColor.Red,    4, "Cameras",         IceType.Barrier,     6, IceType.TarPit,   6),
            Nd("M", NodeType.DS,  NodeColor.Red,    5, "Confidntl Data",  IceType.BlackIce,    5, IceType.TarPaper, 5),
        ],
        Edges =
        [
            ("1","4"),("2","3"),("2","4"),("4","5"),("4","D"),("5","7"),("6","7"),
            ("7","8"),("7","B"),("9","E"),("A","F"),("B","H"),("C","H"),("C","K"),
            ("D","E"),("E","F"),("E","G"),("E","H"),("G","H"),("H","J"),("H","K"),
            ("I","K"),("I","M"),("K","L"),("K","M")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 15 — Unlisted (moderate)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System15() => new()
    {
        SystemNumber = 15, Name = "Unlisted", Difficulty = "moderate",
        Nodes =
        [
            Nd("1", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",     IceType.TraceAndBurn, 5),
            Nd("2", NodeType.SM,  NodeColor.Green,  5, "Automtd. Equip",  IceType.Killer,      3),
            // Node 3: Access+TraceAndDump → TODO
            Nd("3", NodeType.DS,  NodeColor.Orange, 5, "Simsense Files",  IceType.Access,      5, todoIce: IceType.TraceAndDump, todoLv: 3),
            // Node 4: Access+Killer → TODO
            Nd("4", NodeType.SPU, NodeColor.Orange, 6, "Simsense Rec.",   IceType.Access,      6, todoIce: IceType.Killer,      todoLv: 5),
            // Node 5: Barrier+Blaster → TODO
            Nd("5", NodeType.DS,  NodeColor.Orange, 6, "Mngmnt Files",    IceType.Barrier,     6, todoIce: IceType.Blaster,     todoLv: 5),
            Nd("6", NodeType.CPU, NodeColor.Orange, 7, "Unlisted",        IceType.BlackIce,    6),
            // Node 7: Access+Killer → TODO
            Nd("7", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.Access,      4, todoIce: IceType.Killer,      todoLv: 4),
            Nd("8", NodeType.SPU, NodeColor.Orange, 5, "Office Mangmnt",  IceType.Access,      5, IceType.TarPaper, 5),
            Nd("9", NodeType.SAN, NodeColor.Green,  4, "Unlisted"),
            Nd("A", NodeType.DS,  NodeColor.Orange, 5, "Marketing Data",  IceType.Blaster,     4),
            Nd("B", NodeType.SM,  NodeColor.Orange, 6, "Automtd. Equip",  IceType.Barrier,     5, IceType.TarPaper, 5),
            // Node C: Barrier+BlackIce → TODO
            Nd("C", NodeType.SM,  NodeColor.Orange, 7, "Maglocks",        IceType.Barrier,     5, todoIce: IceType.BlackIce,    todoLv: 3),
            Nd("D", NodeType.SPU, NodeColor.Orange, 7, "Security",        IceType.BlackIce,    6),
            Nd("E", NodeType.SPU, NodeColor.Green,  5, "Shipping",        IceType.Blaster,     4),
            // Node F: Barrier+Killer → TODO
            Nd("F", NodeType.DS,  NodeColor.Orange, 5, "Project Files",   IceType.Barrier,     5, todoIce: IceType.Killer,      todoLv: 4),
            // Node G: Barrier+Blaster → TODO
            Nd("G", NodeType.DS,  NodeColor.Orange, 7, "Security Files",  IceType.Barrier,     5, todoIce: IceType.Blaster,     todoLv: 6),
            Nd("H", NodeType.SM,  NodeColor.Green,  4, "Autmtd. Equip",   IceType.Barrier,     3),
        ],
        Edges =
        [
            ("1","6"),("2","4"),("3","4"),("4","7"),("4","8"),("4","9"),("4","E"),
            ("5","6"),("6","8"),("6","A"),("6","D"),("8","9"),("8","A"),("8","B"),
            ("8","D"),("8","E"),("9","E"),("A","D"),("C","D"),("D","G"),("E","F"),("E","H")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 16 — UCAS Federal Government (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System16() => new()
    {
        SystemNumber = 16, Name = "UCAS Federal Government", Difficulty = "expert", CorporationName = "UCAS Federal Government",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Orange, 4, "UCAS Fed. Gov.",  IceType.Blaster,     4),
            Nd("2", NodeType.SM,  NodeColor.Orange, 4, "Maglocks",        IceType.TraceAndDump, 5),
            // Node 3: Access+Killer → TODO
            Nd("3", NodeType.IOP, NodeColor.Green,  4, "Terminal",        IceType.Access,      4, todoIce: IceType.Killer,     todoLv: 3),
            // Node 4: Access+Killer → TODO
            Nd("4", NodeType.SPU, NodeColor.Green,  5, "Data Routing",    IceType.Access,      4, todoIce: IceType.Killer,     todoLv: 4),
            Nd("5", NodeType.IOP, NodeColor.Red,    4, "Matrix Jack",     IceType.Access,      5, IceType.TarPit,  5),
            // Node 6: Access+Blaster → TODO
            Nd("6", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",    IceType.Access,      5, todoIce: IceType.Blaster,    todoLv: 4),
            Nd("7", NodeType.SM,  NodeColor.Orange, 4, "Alert Control",   IceType.Killer,      5, IceType.TarPaper, 5),
            Nd("8", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.TraceAndDump, 5),
            // Node 9: Barrier+BlackIce → TODO
            Nd("9", NodeType.IOP, NodeColor.Red,    5, "Matrix Jack",     IceType.Barrier,     6, todoIce: IceType.BlackIce,   todoLv: 5),
            Nd("A", NodeType.DS,  NodeColor.Green,  4, "System Files",    IceType.TraceAndBurn, 5),
            Nd("B", NodeType.DS,  NodeColor.Green,  4, "Outdated Files",  IceType.Access,      4),
            // Node C: Barrier+BlackIce → TODO
            Nd("C", NodeType.SPU, NodeColor.Red,    6, "Security",        IceType.Barrier,     6, todoIce: IceType.BlackIce,   todoLv: 5),
            // Node D: Access+TraceAndDump → TODO
            Nd("D", NodeType.DS,  NodeColor.Red,    4, "Prisoner Files",  IceType.Access,      6, todoIce: IceType.TraceAndDump, todoLv: 7),
            // Node E: Access+TraceAndBurn → TODO
            Nd("E", NodeType.SPU, NodeColor.Orange, 4, "Building Maint",  IceType.Access,      5, todoIce: IceType.TraceAndBurn, todoLv: 4),
            Nd("F", NodeType.SM,  NodeColor.Orange, 5, "Satellite Feed",  IceType.Access,      6, IceType.TarPit,  6),
            Nd("G", NodeType.SM,  NodeColor.Red,    6, "Maglocks",        IceType.BlackIce,    6),
            Nd("H", NodeType.DS,  NodeColor.Green,  4, "Project Files",   IceType.TraceAndBurn, 5),
            // Node I: Access+TraceAndBurn → TODO
            Nd("I", NodeType.SPU, NodeColor.Orange, 4, "Research",        IceType.Access,      5, todoIce: IceType.TraceAndBurn, todoLv: 5),
            // Node J: Access+BlackIce → TODO
            Nd("J", NodeType.SM,  NodeColor.Red,    7, "Cameras",         IceType.Access,      7, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("K", NodeType.SPU, NodeColor.Red,    5, "Executive Area",  IceType.Access,      6, IceType.TarPit,  6),
            // Node L: Barrier+Killer → TODO
            Nd("L", NodeType.DS,  NodeColor.Red,    5, "Security Files",  IceType.Barrier,     6, todoIce: IceType.Killer,      todoLv: 6),
            // Node M: Access+TraceAndBurn → TODO
            Nd("M", NodeType.IOP, NodeColor.Orange, 5, "Terminal",        IceType.Access,      5, todoIce: IceType.TraceAndBurn, todoLv: 4),
            // Node N: Access+BlackIce → TODO
            Nd("N", NodeType.SPU, NodeColor.Red,    6, "Data Routing",    IceType.Access,      7, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("O", NodeType.SM,  NodeColor.Green,  5, "HVAC Systems",    IceType.TraceAndDump, 4),
            Nd("P", NodeType.SPU, NodeColor.Red,    5, "Office Mangmnt",  IceType.Barrier,     6, IceType.TarPit,  6),
            Nd("Q", NodeType.IOP, NodeColor.Red,    4, "Terminal",        IceType.Access,      6, IceType.BlackIce, 4), // BlackIce IS valid as secondary? No — need TODO check
            Nd("R", NodeType.DS,  NodeColor.Red,    6, "Financial Data",  IceType.Killer,      6, IceType.TarPit,  6),
            Nd("S", NodeType.CPU, NodeColor.Red,    7, "UCAS Fed. Gov.",  IceType.BlackIce,    5, IceType.TarPit,  5),
            Nd("T", NodeType.SM,  NodeColor.Red,    4, "Alert Control",   IceType.Barrier,     5, IceType.TarPaper, 5),
            Nd("U", NodeType.DS,  NodeColor.Red,    4, "Mngmnt Files",    IceType.Blaster,     5, IceType.TarPit,  5),
        ],
        Edges =
        [
            ("1","4"),("2","6"),("3","4"),("4","5"),("4","F"),("5","8"),("6","7"),
            ("6","A"),("7","E"),("8","B"),("8","C"),("9","C"),("A","D"),("A","K"),
            ("B","C"),("C","J"),("D","K"),("E","F"),("E","H"),("E","N"),("F","I"),
            ("G","K"),("H","N"),("I","L"),("I","O"),("I","P"),("I","S"),("I","T"),
            ("K","M"),("K","N"),("K","R"),("L","U"),("N","R"),("N","S"),("O","S"),
            ("P","Q"),("P","T"),("P","U")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 17 — Gates Undersound (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System17() => new()
    {
        SystemNumber = 17, Name = "Gates Undersound", Difficulty = "expert", CorporationName = "Gates Undersound",
        Nodes =
        [
            // Node 1: Barrier+BlackIce → TODO
            Nd("1", NodeType.SM,  NodeColor.Orange, 5, "Undrwatr Equip",  IceType.Barrier,     5, todoIce: IceType.BlackIce,    todoLv: 3),
            // Node 2: Access+BlackIce → TODO
            Nd("2", NodeType.DS,  NodeColor.Orange, 4, "Mngmnt Files",    IceType.Access,      5, todoIce: IceType.BlackIce,    todoLv: 2),
            Nd("3", NodeType.DS,  NodeColor.Orange, 5, "Financial Data",  IceType.Barrier,     6, IceType.TarPit,  6),
            Nd("4", NodeType.IOP, NodeColor.Green,  4, "Terminal",        IceType.TraceAndDump, 5),
            Nd("5", NodeType.CPU, NodeColor.Orange, 5, "Gates Undrsnd.",  IceType.BlackIce,    5),
            Nd("6", NodeType.SPU, NodeColor.Orange, 5, "Building Maint",  IceType.TraceAndBurn, 4),
            Nd("7", NodeType.SM,  NodeColor.Green,  4, "Automtd. Equip",  IceType.Access,      4),
            // Node 8: Access+TraceAndBurn → TODO
            Nd("8", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.Access,      6, todoIce: IceType.TraceAndBurn, todoLv: 4),
            // Node 9: Access+Killer → TODO
            Nd("9", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",    IceType.Access,      5, todoIce: IceType.Killer,      todoLv: 4),
            Nd("A", NodeType.SM,  NodeColor.Orange, 5, "Cameras",         IceType.Barrier,     5, IceType.TraceAndBurn, 6),
            // Node B: Access+Blaster → TODO
            Nd("B", NodeType.SPU, NodeColor.Green,  5, "Data Routing",    IceType.Access,      5, todoIce: IceType.Blaster,     todoLv: 4),
            // Node C: Access+Blaster → TODO
            Nd("C", NodeType.DS,  NodeColor.Green,  6, "System Files",    IceType.Access,      6, todoIce: IceType.Blaster,     todoLv: 4),
            Nd("D", NodeType.SAN, NodeColor.Green,  4, "Gates Undrsnd."),
            Nd("E", NodeType.DS,  NodeColor.Orange, 6, "Security Files",  IceType.Barrier,     6, IceType.TarPaper, 6),
            Nd("F", NodeType.SPU, NodeColor.Orange, 6, "Security",        IceType.Barrier,     7, IceType.TraceAndBurn, 7),
            Nd("G", NodeType.IOP, NodeColor.Orange, 6, "Matrix Jack",     IceType.Access,      5, IceType.TarPit,  5),
            Nd("H", NodeType.SM,  NodeColor.Green,  5, "Elevator Cntrl",  IceType.Blaster,     6),
            Nd("I", NodeType.SM,  NodeColor.Orange, 5, "Maglocks",        IceType.Killer,      6, IceType.TarPaper, 6),
            Nd("J", NodeType.DS,  NodeColor.Green,  4, "Outdated Files",  IceType.Killer,      4),
        ],
        Edges =
        [
            ("1","5"),("1","A"),("2","3"),("2","5"),("3","5"),("4","5"),("4","6"),("4","B"),
            ("5","6"),("5","A"),("5","B"),("6","7"),("6","B"),("6","C"),("7","9"),("8","9"),
            ("9","D"),("9","H"),("B","G"),("B","J"),("E","F"),("F","G"),("F","I")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 18 — Ito's System (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System18() => new()
    {
        SystemNumber = 18, Name = "Ito's System", Difficulty = "expert", CorporationName = "Ito's System",
        Nodes =
        [
            Nd("1", NodeType.DS,  NodeColor.Orange, 4, "System Files",   IceType.Blaster,  6),
            Nd("2", NodeType.DS,  NodeColor.Red,    6, "Project Files",  IceType.BlackIce, 5, IceType.TarPaper, 5),
            Nd("3", NodeType.SAN, NodeColor.Green,  4, "Ito's System"),
            Nd("4", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",   IceType.BlackIce, 6, IceType.TarPit,   6),
            Nd("5", NodeType.CPU, NodeColor.Red,    7, "Ito's System",   IceType.BlackIce, 5, IceType.TarPit,   5),
            // Node 6: Barrier+BlackIce → TODO
            Nd("6", NodeType.DS,  NodeColor.Red,    6, "Confidntl Data", IceType.Barrier,  6, todoIce: IceType.BlackIce, todoLv: 4),
            Nd("7", NodeType.IOP, NodeColor.Red,    6, "Matrix Jack",    IceType.Access,   7),
            // Node 8: Access+BlackIce → TODO
            Nd("8", NodeType.DS,  NodeColor.Red,    5, "Mngmnt Files",   IceType.Access,   7, todoIce: IceType.BlackIce, todoLv: 5),
            Nd("9", NodeType.DS,  NodeColor.Red,    6, "Financial Data", IceType.BlackIce, 4, IceType.TarPit,   4),
        ],
        Edges = [("1","4"),("2","5"),("3","4"),("4","5"),("4","7"),("4","8"),("5","6"),("5","8"),("5","9"),("8","9")]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 19 — Hollywood Correctional (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System19() => new()
    {
        SystemNumber = 19, Name = "Hollywood Correctional", Difficulty = "expert", CorporationName = "Hollywood Correctional",
        Nodes =
        [
            // Node 1: Access+TraceAndBurn → TODO
            Nd("1", NodeType.SM,  NodeColor.Orange, 5, "HVAC Systems",   IceType.Access,   4, todoIce: IceType.TraceAndBurn, todoLv: 4),
            Nd("2", NodeType.IOP, NodeColor.Orange, 5, "Terminal",       IceType.TraceAndDump, 5),
            Nd("3", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",   IceType.Access,   6, IceType.TarPit,   6),
            Nd("4", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",    IceType.BlackIce, 5),
            Nd("5", NodeType.SPU, NodeColor.Orange, 4, "Building Maint", IceType.BlackIce, 5),
            Nd("6", NodeType.DS,  NodeColor.Green,  4, "Outdated Files", IceType.Blaster,  4),
            Nd("7", NodeType.DS,  NodeColor.Green,  5, "System Files",   IceType.Killer,   5),
            // Node 8: Access+BlackIce → TODO
            Nd("8", NodeType.DS,  NodeColor.Red,    5, "Financial Data", IceType.Access,   6, todoIce: IceType.BlackIce,    todoLv: 4),
            Nd("9", NodeType.DS,  NodeColor.Red,    5, "Security Files", IceType.Killer,   6, IceType.TarPaper, 6),
            Nd("A", NodeType.SAN, NodeColor.Orange, 4, "Hlywd Corr Fac", IceType.Blaster,  4),
            // Node B: Barrier+BlackIce → TODO
            Nd("B", NodeType.CPU, NodeColor.Red,    6, "Hlywd Corr Fac", IceType.Barrier,  6, todoIce: IceType.BlackIce,    todoLv: 4),
            Nd("C", NodeType.DS,  NodeColor.Orange, 5, "Mngmnt Files",   IceType.Access,   5, IceType.TarPit,   5),
            // Node D: Barrier+BlackIce → TODO
            Nd("D", NodeType.SM,  NodeColor.Red,    6, "Maglocks",       IceType.Barrier,  5, todoIce: IceType.BlackIce,    todoLv: 4),
            // Node E: Barrier+BlackIce → TODO
            Nd("E", NodeType.SPU, NodeColor.Red,    5, "Security",       IceType.Barrier,  6, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("F", NodeType.DS,  NodeColor.Orange, 4, "Prisoner Files", IceType.Access,   5, IceType.TarPaper, 5),
            Nd("G", NodeType.IOP, NodeColor.Red,    5, "Matrix Jack",    IceType.BlackIce, 6),
        ],
        Edges =
        [
            ("1","5"),("2","3"),("3","5"),("3","6"),("3","7"),("3","A"),("3","E"),
            ("4","5"),("5","B"),("6","7"),("8","C"),("9","E"),("A","E"),("B","C"),
            ("B","D"),("B","F"),("C","E"),("E","G")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 20 — Mitsuhama (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System20() => new()
    {
        SystemNumber = 20, Name = "Mitsuhama", Difficulty = "expert", CorporationName = "Mitsuhama",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Orange, 4, "Mitsuhama",       IceType.Access,      4),
            Nd("2", NodeType.SM,  NodeColor.Orange, 4, "Maglocks",        IceType.Blaster,     4),
            Nd("3", NodeType.IOP, NodeColor.Red,    6, "Matrix Jack",     IceType.BlackIce,    5),
            Nd("4", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",    IceType.Access,      6),
            Nd("5", NodeType.SPU, NodeColor.Orange, 4, "Building Maint",  IceType.TraceAndDump, 5),
            // Node 6: Access+TraceAndDump → TODO
            Nd("6", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",    IceType.Access,      5, todoIce: IceType.TraceAndDump, todoLv: 4),
            Nd("7", NodeType.DS,  NodeColor.Orange, 4, "Outdated Files",  IceType.Access,      4),
            Nd("8", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.TraceAndDump, 4),
            // Node 9: Barrier+BlackIce → TODO
            Nd("9", NodeType.SPU, NodeColor.Red,    6, "Security",        IceType.Barrier,     7, todoIce: IceType.BlackIce,    todoLv: 5),
            // Node A: Access+TraceAndDump → TODO
            Nd("A", NodeType.IOP, NodeColor.Red,    5, "Matrix Jack",     IceType.Access,      5, todoIce: IceType.TraceAndDump, todoLv: 6),
            Nd("B", NodeType.DS,  NodeColor.Orange, 6, "Security Files",  IceType.Killer,      6),
            Nd("C", NodeType.SM,  NodeColor.Orange, 6, "Elevator Cntrl",  IceType.Access,      6, IceType.TarPaper, 6),
            // Node D: Access+BlackIce → TODO
            Nd("D", NodeType.SPU, NodeColor.Red,    5, "Executive Area",  IceType.Access,      6, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("E", NodeType.SM,  NodeColor.Orange, 6, "Cameras",         IceType.Blaster,     7, IceType.TarPaper, 7),
            Nd("F", NodeType.IOP, NodeColor.Orange, 6, "Terminal",        IceType.Barrier,     5, IceType.TarPit,   5),
            Nd("G", NodeType.DS,  NodeColor.Red,    6, "Financial Data",  IceType.BlackIce,    6, IceType.TarPaper, 6),
            // Node H: Barrier+BlackIce → TODO
            Nd("H", NodeType.DS,  NodeColor.Red,    6, "Confidntl Data",  IceType.Barrier,     7, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("I", NodeType.SM,  NodeColor.Red,    5, "Alert Control",   IceType.BlackIce,    5, IceType.TarPit,   5),
            Nd("J", NodeType.CPU, NodeColor.Red,    7, "Mitsuhama",       IceType.BlackIce,    6, IceType.TarPit,   6),
            Nd("K", NodeType.DS,  NodeColor.Red,    5, "Mngmnt Files",    IceType.Barrier,     6, IceType.TarPit,   6),
            Nd("L", NodeType.DS,  NodeColor.Red,    5, "Project Files",   IceType.Access,      6, IceType.TarPit,   6),
            // Node M: Barrier+Killer → TODO
            Nd("M", NodeType.SPU, NodeColor.Red,    6, "Research",        IceType.Barrier,     6, todoIce: IceType.Killer,      todoLv: 5),
            // Node N: Access+TraceAndDump → TODO
            Nd("N", NodeType.SM,  NodeColor.Orange, 4, "Automtd. Equip",  IceType.Access,      5, todoIce: IceType.TraceAndDump, todoLv: 6),
            Nd("O", NodeType.IOP, NodeColor.Orange, 5, "Matrix Jack",     IceType.Access,      5, IceType.TarPaper, 5),
            Nd("P", NodeType.DS,  NodeColor.Orange, 5, "System Files",    IceType.TraceAndDump, 5),
            Nd("Q", NodeType.DS,  NodeColor.Orange, 6, "Competition",     IceType.Access,      5, IceType.TarPaper, 5),
        ],
        /*Edges =
        [
            ("1","4"),("1","5"),("2","4"),("3","9"),("4","5"),("4","6"),("5","8"),
	*/
        Edges =
        [
            ("1","4"),("2","5"),("3","9"),("4","5"),("4","7"),("4","8"),("4","B"),
            ("5","6"),("5","8"),("5","C"),("5","E"),("6","C"),("6","D"),("6","J"),
            ("9","A"),("9","D"),("9","H"),("D","F"),("D","G"),("D","J"),("D","M"),
            ("F","M"),("I","J"),("J","K"),("J","L"),("K","L"),("M","N"),("M","O"),
            ("M","P"),("M","Q"),("P","Q")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 21 — Renraku (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System21() => new()
    {
        SystemNumber = 21, Name = "Renraku", Difficulty = "expert", CorporationName = "Renraku",
        Nodes =
        [
            Nd("1", NodeType.SAN, NodeColor.Orange, 4, "Renraku",         IceType.TraceAndDump, 5),
            // Node 2: Access+BlackIce → TODO
            Nd("2", NodeType.DS,  NodeColor.Red,    6, "Security Files",  IceType.Access,      7, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("3", NodeType.SM,  NodeColor.Green,  5, "HVAC Systems",    IceType.TraceAndDump, 6),
            Nd("4", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.Killer,      4),
            // Node 5: Barrier+TraceAndDump → TODO
            Nd("5", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",     IceType.Barrier,     5, todoIce: IceType.TraceAndDump, todoLv: 4),
            // Node 6: Barrier+BlackIce → TODO
            Nd("6", NodeType.SPU, NodeColor.Red,    7, "Security",        IceType.Barrier,     5, todoIce: IceType.BlackIce,    todoLv: 5),
            // Node 7: Barrier+Killer → TODO
            Nd("7", NodeType.SM,  NodeColor.Red,    5, "Alert Control",   IceType.Barrier,     6, todoIce: IceType.Killer,      todoLv: 6),
            // Node 8: Access+TraceAndBurn → TODO
            Nd("8", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",    IceType.Access,      6, todoIce: IceType.TraceAndBurn, todoLv: 7),
            Nd("9", NodeType.IOP, NodeColor.Red,    6, "Matrix Jack",     IceType.Barrier,     6, IceType.TarPit,   6),
            // Node A: Access+TraceAndBurn → TODO
            Nd("A", NodeType.SPU, NodeColor.Orange, 5, "Building Maint",  IceType.Access,      6, todoIce: IceType.TraceAndBurn, todoLv: 6),
            Nd("B", NodeType.SM,  NodeColor.Red,    5, "Cameras",         IceType.Access,      7, IceType.TarPit,   7),
            Nd("C", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.TraceAndBurn, 6),
            // Node D: Barrier+BlackIce → TODO
            Nd("D", NodeType.SPU, NodeColor.Red,    5, "Executive Area",  IceType.Barrier,     5, todoIce: IceType.BlackIce,    todoLv: 4),
            Nd("E", NodeType.DS,  NodeColor.Green,  5, "Outdated Files",  IceType.Blaster,     4),
            Nd("F", NodeType.SPU, NodeColor.Orange, 5, "Marketing",       IceType.Access,      5, IceType.TarPaper, 5),
            // Node G: Barrier+BlackIce → TODO
            Nd("G", NodeType.SPU, NodeColor.Red,    5, "Research",        IceType.Barrier,     7, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("H", NodeType.DS,  NodeColor.Red,    6, "Financial Data",  IceType.Killer,      5, IceType.TarPaper, 5),
            // Node I: Access+TraceAndBurn → TODO
            Nd("I", NodeType.SM,  NodeColor.Orange, 4, "Satellite Feed",  IceType.Access,      5, todoIce: IceType.TraceAndBurn, todoLv: 6),
            Nd("J", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.Barrier,     5, IceType.TarPaper, 5),
            Nd("K", NodeType.IOP, NodeColor.Orange, 6, "Matrix Jack",     IceType.TraceAndBurn, 5),
            Nd("L", NodeType.IOP, NodeColor.Red,    4, "Terminal",        IceType.Killer,      6),
            // Node M: Access+BlackIce → TODO
            Nd("M", NodeType.DS,  NodeColor.Orange, 6, "Mngmnt Files",    IceType.Access,      7, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("N", NodeType.DS,  NodeColor.Green,  6, "System Files",    IceType.Access,      5),
            Nd("O", NodeType.CPU, NodeColor.Red,    7, "Renraku",         IceType.Barrier,     7, IceType.BlackIce, 6), // Barrier+BlackIce (both non-Tar)
            Nd("P", NodeType.DS,  NodeColor.Red,    6, "Project Files",   IceType.Barrier,     6, todoIce: IceType.BlackIce,    todoLv: 4),
            Nd("Q", NodeType.DS,  NodeColor.Orange, 6, "Competition",     IceType.Access,      5, IceType.TarPaper, 5),
            Nd("R", NodeType.SM,  NodeColor.Red,    5, "Maglocks",        IceType.Blaster,     7),
            Nd("S", NodeType.DS,  NodeColor.Red,    5, "Confidntl Data",  IceType.Blaster,     6, IceType.TarPit,   6),
            Nd("T", NodeType.DS,  NodeColor.Red,    5, "Project Files",   IceType.BlackIce,    6),
        ],
        Edges =
        [
            ("1","8"),("2","6"),("3","A"),("4","8"),("5","8"),("6","7"),("6","9"),("6","C"),
            ("6","G"),("8","A"),("8","B"),("8","C"),("8","F"),("A","B"),("A","C"),("A","D"),
            ("A","E"),("A","F"),("B","C"),("B","F"),("C","F"),("C","G"),("D","H"),("D","M"),
            ("F","I"),("F","J"),("F","O"),("G","K"),("G","L"),("G","N"),("G","Q"),("N","Q"),
            ("N","T"),("O","P"),("O","R"),("O","S"),("P","S")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 22 — Fuchi (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System22() => new()
    {
        SystemNumber = 22, Name = "Fuchi", Difficulty = "expert", CorporationName = "Fuchi",
        Nodes =
        [
            // Node 1: Access+TraceAndDump → TODO
            Nd("1", NodeType.DS,  NodeColor.Green,  6, "System Files",    IceType.Access,      5, todoIce: IceType.TraceAndDump, todoLv: 4),
            Nd("2", NodeType.SM,  NodeColor.Green,  5, "HVAC Systems",    IceType.Blaster,     4),
            Nd("3", NodeType.SM,  NodeColor.Orange, 7, "Cameras",         IceType.TraceAndBurn, 6),
            Nd("4", NodeType.SPU, NodeColor.Red,    6, "Security",        IceType.BlackIce,    5, IceType.TarPaper, 5),
            // Node 5: Access+BlackIce → TODO
            Nd("5", NodeType.DS,  NodeColor.Red,    6, "Security Files",  IceType.Access,      5, todoIce: IceType.BlackIce,    todoLv: 4),
            Nd("6", NodeType.SPU, NodeColor.Orange, 5, "Office Mangmnt",  IceType.BlackIce,    6),
            // Node 7: Access+TraceAndBurn → TODO
            Nd("7", NodeType.IOP, NodeColor.Orange, 6, "Terminal",        IceType.Access,      6, todoIce: IceType.TraceAndBurn, todoLv: 6),
            // Node 8: Access+TraceAndBurn → TODO
            Nd("8", NodeType.SM,  NodeColor.Red,    5, "Alert Control",   IceType.Access,      5, todoIce: IceType.TraceAndBurn, todoLv: 6),
            // Node 9: Access+BlackIce → TODO
            Nd("9", NodeType.IOP, NodeColor.Red,    5, "Matrix Jack",     IceType.Access,      6, todoIce: IceType.BlackIce,    todoLv: 6),
            Nd("A", NodeType.IOP, NodeColor.Red,    5, "Matrix Jack",     IceType.BlackIce,    6),
            // Node B: Access+BlackIce → TODO
            Nd("B", NodeType.SPU, NodeColor.Red,    6, "Research",        IceType.Access,      6, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("C", NodeType.SPU, NodeColor.Red,    5, "Marketing",       IceType.BlackIce,    6),
            Nd("D", NodeType.SM,  NodeColor.Red,    5, "Elevator Cntrl",  IceType.Barrier,     5, IceType.TarPaper, 5),
            // Node E: Access+Blaster → TODO
            Nd("E", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Access,      6, todoIce: IceType.Blaster,     todoLv: 4),
            Nd("F", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.TraceAndDump, 5),
            // Node G: Barrier+BlackIce → TODO
            Nd("G", NodeType.SM,  NodeColor.Orange, 4, "Satellite Feed",  IceType.Barrier,     5, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("H", NodeType.SPU, NodeColor.Red,    5, "Executive Area",  IceType.TraceAndBurn, 5),
            Nd("I", NodeType.DS,  NodeColor.Red,    5, "Simsense Files",  IceType.Barrier,     6, IceType.TarPit,   6),
            Nd("J", NodeType.SAN, NodeColor.Orange, 5, "Fuchi"),
            Nd("K", NodeType.DS,  NodeColor.Orange, 5, "Financial Data",  IceType.Access,      4, IceType.TarPit,   4),
            Nd("L", NodeType.SPU, NodeColor.Orange, 6, "Office Mangmnt",  IceType.BlackIce,    6),
            Nd("M", NodeType.SM,  NodeColor.Orange, 6, "Maglocks",        IceType.TraceAndBurn, 5),
            // Node N: Barrier+BlackIce → TODO
            Nd("N", NodeType.CPU, NodeColor.Red,    7, "Fuchi",           IceType.Barrier,     6, todoIce: IceType.BlackIce,    todoLv: 6),
            Nd("O", NodeType.DS,  NodeColor.Green,  4, "Outdated Files",  IceType.Killer,      5),
            // Node P: Access+BlackIce → TODO
            Nd("P", NodeType.DS,  NodeColor.Red,    6, "Mngmnt Files",    IceType.Access,      6, todoIce: IceType.BlackIce,    todoLv: 6),
        ],
        Edges =
        [
            ("1","4"),("1","5"),("1","6"),("2","6"),("2","E"),("3","4"),("4","5"),("4","8"),
            ("4","B"),("6","7"),("6","E"),("7","C"),("8","B"),("9","B"),("A","H"),("B","D"),
            ("B","I"),("C","E"),("C","F"),("C","G"),("C","L"),("D","N"),("E","F"),("E","J"),
            ("E","L"),("E","O"),("F","L"),("G","H"),("H","M"),("H","N"),("K","N"),("L","M"),
            ("M","N"),("N","P")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 23 — Lone Star (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System23() => new()
    {
        SystemNumber = 23, Name = "Lone Star", Difficulty = "expert", CorporationName = "Lone Star",
        Nodes =
        [
            Nd("1", NodeType.SM,  NodeColor.Red,    4, "Cameras",         IceType.Access,   6, IceType.TarPit,   6),
            Nd("2", NodeType.DS,  NodeColor.Red,    5, "Financial Data",  IceType.Access,   7, IceType.TarPaper, 7),
            // Node 3: Access+BlackIce → TODO
            Nd("3", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Access,   5, todoIce: IceType.BlackIce, todoLv: 4),
            // Node 4: Barrier+Blaster → TODO
            Nd("4", NodeType.SPU, NodeColor.Red,    6, "Executive Area",  IceType.Barrier,  7, todoIce: IceType.Blaster,  todoLv: 6),
            Nd("5", NodeType.SM,  NodeColor.Red,    5, "Alert Control",   IceType.Killer,   5),
            // Node 6: Access+TraceAndDump → TODO
            Nd("6", NodeType.SPU, NodeColor.Red,    6, "Security",        IceType.Access,   6, todoIce: IceType.TraceAndDump, todoLv: 5),
            Nd("7", NodeType.IOP, NodeColor.Orange, 6, "Matrix Jack",     IceType.Killer,   6),
            Nd("8", NodeType.DS,  NodeColor.Orange, 6, "Case Files",      IceType.Barrier,  6, IceType.TarPaper, 6),
            // Node 9: Barrier+BlackIce → TODO
            Nd("9", NodeType.DS,  NodeColor.Red,    5, "Mngmnt Files",    IceType.Barrier,  7, todoIce: IceType.BlackIce, todoLv: 5),
            Nd("A", NodeType.IOP, NodeColor.Red,    5, "Matrix Jack",     IceType.Killer,   6, IceType.TarPit,   6),
            // Node B: Barrier+Blaster → TODO
            Nd("B", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Barrier,  6, todoIce: IceType.Blaster,  todoLv: 4),
            Nd("C", NodeType.IOP, NodeColor.Orange, 5, "Terminal",        IceType.Barrier,  6),
            // Node D: Access+TraceAndDump → TODO
            Nd("D", NodeType.SPU, NodeColor.Orange, 5, "Research",        IceType.Access,   6, todoIce: IceType.TraceAndDump, todoLv: 5),
            Nd("E", NodeType.CPU, NodeColor.Red,    7, "Lone Star",       IceType.Barrier,  7, IceType.TarPit,   7),
            Nd("F", NodeType.SM,  NodeColor.Orange, 5, "Automtd. Equip",  IceType.Access,   6),
            Nd("G", NodeType.DS,  NodeColor.Red,    6, "Security Files",  IceType.Blaster,  6, IceType.TarPit,   6),
            Nd("H", NodeType.SM,  NodeColor.Orange, 4, "Elevator Cntrl",  IceType.Barrier,  5),
            Nd("I", NodeType.SM,  NodeColor.Red,    4, "Maglocks",        IceType.Killer,   6),
            Nd("J", NodeType.DS,  NodeColor.Green,  5, "Outdated Files",  IceType.Blaster,  4),
            // Node K: Access+Blaster → TODO
            Nd("K", NodeType.DS,  NodeColor.Green,  5, "System Files",    IceType.Access,   5, todoIce: IceType.Blaster,  todoLv: 3),
            Nd("L", NodeType.IOP, NodeColor.Orange, 4, "Terminal",        IceType.Killer,   5),
            Nd("M", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Access,   7),
            // Node N: Access+Killer → TODO
            Nd("N", NodeType.SPU, NodeColor.Red,    5, "Security",        IceType.Access,   7, todoIce: IceType.Killer,   todoLv: 5),
            Nd("O", NodeType.SPU, NodeColor.Red,    6, "Security",        IceType.Blaster,  6, IceType.TarPaper, 6),
            // Node P: Access+TraceAndDump → TODO
            Nd("P", NodeType.SPU, NodeColor.Green,  5, "Data Routing",    IceType.Access,   5, todoIce: IceType.TraceAndDump, todoLv: 5),
            Nd("Q", NodeType.SAN, NodeColor.Orange, 5, "Lone Star",       IceType.Access,   5),
            // Node R: Barrier+Blaster → TODO
            Nd("R", NodeType.DS,  NodeColor.Red,    4, "Prisoner Files",  IceType.Barrier,  6, todoIce: IceType.Blaster,  todoLv: 6),
            // Node S: Access+BlackIce → TODO
            Nd("S", NodeType.SPU, NodeColor.Orange, 4, "Building Maint",  IceType.Access,   6, todoIce: IceType.BlackIce, todoLv: 6),
            Nd("T", NodeType.SM,  NodeColor.Red,    4, "Alert Control",   IceType.Blaster,  6),
            Nd("U", NodeType.DS,  NodeColor.Orange, 4, "Legal Files",     IceType.Blaster,  5),
            Nd("V", NodeType.SM,  NodeColor.Orange, 4, "Automtd. Equip",  IceType.TraceAndBurn, 5),
        ],
        Edges =
        [
            ("1","4"),("2","4"),("2","6"),("3","7"),("3","B"),("4","6"),("4","E"),
            ("5","6"),("6","A"),("6","B"),("6","O"),("7","D"),("8","D"),("9","E"),
            ("9","G"),("A","B"),("A","O"),("B","C"),("B","O"),("B","P"),("D","F"),
            ("D","H"),("D","M"),("E","G"),("E","N"),("H","M"),("I","N"),("J","P"),
            ("K","S"),("L","M"),("L","S"),("M","Q"),("M","S"),("N","O"),("N","R"),
            ("O","T"),("O","U"),("P","S"),("S","V")
        ]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // System 24 — Ares (expert)
    // ═════════════════════════════════════════════════════════════════════════
    private static SystemDefinition System24() => new()
    {
        SystemNumber = 24, Name = "Ares", Difficulty = "expert", CorporationName = "Ares",
        Nodes =
        [
            Nd("1", NodeType.DS,  NodeColor.Red,    6, "Legal Files",     IceType.BlackIce,    6),
            Nd("2", NodeType.DS,  NodeColor.Orange, 6, "Mngmnt Files",    IceType.Blaster,     5),
            Nd("3", NodeType.IOP, NodeColor.Orange, 5, "Terminal",        IceType.Killer,      6),
            // Node 4: Access+BlackIce → TODO
            Nd("4", NodeType.SPU, NodeColor.Red,    6, "Office Mangmnt",  IceType.Access,      6, todoIce: IceType.BlackIce,    todoLv: 6),
            // Node 5: Barrier+Blaster → TODO
            Nd("5", NodeType.SPU, NodeColor.Orange, 4, "Data Routing",    IceType.Barrier,     5, todoIce: IceType.Blaster,     todoLv: 5),
            Nd("6", NodeType.SPU, NodeColor.Red,    5, "Retail Control",  IceType.Barrier,     6, IceType.TarPit,   6),
            Nd("7", NodeType.DS,  NodeColor.Red,    4, "System Files",    IceType.Killer,      6, IceType.TarPaper, 6),
            Nd("8", NodeType.SM,  NodeColor.Green,  4, "HVAC Systems",    IceType.Blaster,     5),
            // Node 9: Barrier+BlackIce → TODO
            Nd("9", NodeType.IOP, NodeColor.Orange, 5, "Matrix Jack",     IceType.Barrier,     6, todoIce: IceType.BlackIce,    todoLv: 5),
            Nd("A", NodeType.DS,  NodeColor.Red,    6, "Security Files",  IceType.BlackIce,    6, IceType.TarPaper, 6),
            // Node B: Access+Blaster → TODO
            Nd("B", NodeType.SM,  NodeColor.Orange, 6, "Automtd. Equip",  IceType.Access,      6, todoIce: IceType.Blaster,     todoLv: 5),
            Nd("C", NodeType.DS,  NodeColor.Green,  4, "Outdated Files",  IceType.TraceAndDump, 5),
            Nd("D", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",    IceType.TraceAndBurn, 5),
            Nd("E", NodeType.SPU, NodeColor.Red,    5, "Research",        IceType.Killer,      6, IceType.TarPit,   6),
            Nd("F", NodeType.IOP, NodeColor.Orange, 4, "Matrix Jack",     IceType.TraceAndBurn, 6),
            Nd("G", NodeType.SM,  NodeColor.Orange, 5, "Elevator Cntrl",  IceType.Killer,      6),
            Nd("H", NodeType.SM,  NodeColor.Green,  4, "Automtd. Equip",  IceType.Barrier,     4),
            Nd("I", NodeType.DS,  NodeColor.Red,    5, "Project Files",   IceType.Blaster,     7, IceType.TarPit,   7),
            Nd("J", NodeType.CPU, NodeColor.Red,    7, "Ares",            IceType.BlackIce,    6, IceType.TarPit,   6),
            // Node K: Barrier+BlackIce → TODO
            Nd("K", NodeType.DS,  NodeColor.Red,    6, "Confidntl Data",  IceType.Barrier,     6, todoIce: IceType.BlackIce,    todoLv: 6),
            // Node L: Access+Killer → TODO
            Nd("L", NodeType.SM,  NodeColor.Orange, 6, "Cameras",         IceType.Access,      7, todoIce: IceType.Killer,      todoLv: 6),
            Nd("M", NodeType.IOP, NodeColor.Green,  5, "Terminal",        IceType.Killer,      4),
            Nd("N", NodeType.SM,  NodeColor.Red,    5, "Alert Control",   IceType.Killer,      5),
            Nd("O", NodeType.SPU, NodeColor.Red,    6, "Security",        IceType.Barrier,     6, IceType.TarPit,   6),
            Nd("P", NodeType.SPU, NodeColor.Orange, 5, "Data Routing",    IceType.Blaster,     5),
            Nd("Q", NodeType.SAN, NodeColor.Orange, 4, "Ares",            IceType.Blaster,     5, IceType.TarPaper, 5),
            Nd("R", NodeType.SPU, NodeColor.Red,    6, "Security",        IceType.Blaster,     6, IceType.TarPit,   6),
            Nd("S", NodeType.IOP, NodeColor.Red,    5, "Matrix Jack",     IceType.TraceAndBurn, 5),
            Nd("T", NodeType.SM,  NodeColor.Red,    4, "Security Cntrl",  IceType.Access,      5),
            Nd("U", NodeType.SM,  NodeColor.Red,    7, "Maglocks",        IceType.Barrier,     5),
        ],
        Edges =
        [
            ("1","4"),("2","5"),("3","6"),("4","5"),("4","8"),("4","9"),("4","J"),
            ("5","6"),("5","9"),("5","A"),("6","7"),("6","B"),("6","E"),("8","J"),
            ("B","E"),("C","D"),("D","E"),("D","H"),("D","O"),("E","F"),("E","H"),
            ("E","I"),("G","J"),("J","K"),("J","R"),("L","O"),("M","P"),("N","R"),
            ("O","P"),("O","T"),("O","U"),("P","Q"),("P","U"),("R","S")
        ]
    };
}
