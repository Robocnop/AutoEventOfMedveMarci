using AutoEvent.Interfaces;

namespace AutoEvent.Games.AmongUs.Configs;

public class Translation : EventTranslation
{
    public static string StartingEvent { get; set; } =
        "<b>{name}</b>\nThere are {time} second before the game starts. Selecting the <color=red><b>Impostors</b></color>.";

    public static string YouAreImpostor { get; set; } = "You are the <color=red>Impostor</color>!";
    public static string YouAreCrewmate { get; set; } = "You are a <color=#00FFFF>Crewmate</color>!";
    public static string ImpostorWin { get; set; } = "The <color=red>Impostors</color> have won!";
    public static string CrewmateWin { get; set; } = "The <color=#00FFFF>Crewmates</color> have won!";
    public string KilledByImpostor { get; set; } = "You were killed by an Impostor!";

    public string KillCooldown { get; set; } = "You can't kill for another {time} seconds!";
    public string TooFar { get; set; } = "You are too far away to kill!";

    public string VotingInfo { get; set; } =
        "You can vote by writing .vote <color> to the ClientConsole which you can open with ~ by default.\nYou can get the colors by opening the PlayerList with N.\n{time} seconds remaining.";

    public string MeetingCalled { get; set; } = "A meeting has been called by {player}!";
    public string DeathBodyReported { get; set; } = "A death body has been found by {player}!";
    public string EmergencyMeetingsReached { get; set; } = "You can't call any more emergency meetings!";
    public string MeetingCooldown { get; set; } = "You can't call a meeting for another {time} seconds!";
}