namespace HashForge.Web.Components.Shared;

/// <summary>
/// The drawing for each icon, as SVG shapes on a 24x24 grid. These are fixed strings written here,
/// never user input, which is what makes them safe to emit as markup.
/// </summary>
public static class IconShapes
{
    private static readonly Dictionary<IconKind, string> Shapes = new()
    {
        [IconKind.Hashrate] = """<polyline points="3 12 7 12 10 5 14 19 17 12 21 12"/>""",
        [IconKind.Miners] = """<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20c0-3.6 2.9-6 6.5-6s6.5 2.4 6.5 6"/><path d="M16 4.6a3.5 3.5 0 0 1 0 6.8M21.5 20c0-2.6-1.5-4.6-3.7-5.5"/>""",
        [IconKind.Accepted] = """<circle cx="12" cy="12" r="9"/><polyline points="8 12.5 11 15.5 16.5 9"/>""",
        [IconKind.Rejected] = """<circle cx="12" cy="12" r="9"/><path d="M9 9l6 6M15 9l-6 6"/>""",
        [IconKind.Best] = """<polygon points="12 3 14.8 8.9 21 9.7 16.5 14.1 17.6 20.5 12 17.4 6.4 20.5 7.5 14.1 3 9.7 9.2 8.9"/>""",
        [IconKind.Effort] = """<path d="M4 17a8 8 0 1 1 16 0"/><path d="M12 17l4-6"/><circle cx="12" cy="17" r="1"/>""",
        [IconKind.Clock] = """<circle cx="12" cy="12" r="9"/><polyline points="12 7 12 12 15.5 14"/>""",
        [IconKind.Block] = """<path d="M12 3l8 4.5v9L12 21l-8-4.5v-9z"/><path d="M4 7.5l8 4.5 8-4.5M12 12v9"/>""",
        [IconKind.Difficulty] = """<path d="M5 20v-6M12 20V9M19 20V4"/>""",
        [IconKind.Network] = """<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c3 3.2 3 14.8 0 18M12 3c-3 3.2-3 14.8 0 18"/>""",
        [IconKind.Sync] = """<path d="M20 11a8 8 0 0 0-14.5-4M4 13a8 8 0 0 0 14.5 4"/><polyline points="5 3 5 7.5 9.5 7.5"/><polyline points="19 21 19 16.5 14.5 16.5"/>""",
        [IconKind.Storage] = """<ellipse cx="12" cy="6" rx="7.5" ry="3"/><path d="M4.5 6v12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3V6M4.5 12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3"/>""",
        [IconKind.Chain] = """<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>""",
        [IconKind.Connections] = """<path d="M9 3v5M15 3v5M6 8h12v3a6 6 0 0 1-12 0zM12 17v4"/>""",
        [IconKind.Port] = """<rect x="3" y="5" width="18" height="14" rx="2"/><polyline points="7 10 10 12.5 7 15"/><path d="M12.5 15H17"/>""",
    };

    public static string For(IconKind kind) => Shapes.GetValueOrDefault(kind, string.Empty);
}