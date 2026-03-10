namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Global developer / debug settings.
///
/// Flip <see cref="DevMode"/> to <c>true</c> to restore the "everything goes"
/// behaviour that was the previous default:
///   • Full matrix map is visible at all times.
///   • Travel and node actions are not blocked by live ICE.
///
/// When <c>false</c> (normal / production mode):
///   • Fog of war: only the current node and already-visited nodes appear on
///     the map; unvisited nodes are hidden.
///   • ICE must be fully defeated before the decker can travel to an adjacent
///     node or execute any node action at the current node.
/// </summary>
public static class DevSettings
{
    public const bool DevMode = false;
}
