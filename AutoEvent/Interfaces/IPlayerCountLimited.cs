namespace AutoEvent.Interfaces;

/// <summary>
///     Events that have a maximum player limit should implement this interface.
///     The <c>ev run</c> command will refuse to start the event when the player count exceeds
///     <see cref="MaxPlayers" />.
/// </summary>
public interface IPlayerCountLimited
{
    /// <summary>Maximum number of players allowed to participate.</summary>
    int MaxPlayers { get; }
}
