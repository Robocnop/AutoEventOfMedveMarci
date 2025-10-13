using AutoEvent.Interfaces;

namespace AutoEvent.Games.AmongUs.Configs;

public class Translation : EventTranslation
{
    public string StartingEvent { get; set; } =
        "<b>{name}</b>\nThere are {time} second before the game starts. Selecting the <color=red><b>Impostors</b></color>.";

    public string YouAreImpostor { get; set; } = "You are the <color=red>Impostor</color>!";
    public string YouAreCrewmate { get; set; } = "You are a <color=#00FFFF>Crewmate</color>!";
    public string ImpostorWin { get; set; } = "The <color=red>Impostors</color> have won!";
    public string CrewmateWin { get; set; } = "The <color=#00FFFF>Crewmates</color> have won!";
    public string KilledByImpostor { get; set; } = "You were killed by an Impostor!";

    public string KillCooldown { get; set; } = "You can't kill for another {time} seconds!";
    public string TooFar { get; set; } = "You are too far away to kill!";

    public string VotingInfo { get; set; } =
        "{reason}\nYou can vote by writing .vote <color> to the Console which you can open with ~ by default.\n{time} seconds remaining.";

    public string MeetingCalled { get; set; } = "A meeting has been called by {player}!";
    public string DeathBodyReported { get; set; } = "{deadPlayer}'s body found by {reportedPlayer}!";
    public string EmergencyMeetingsReached { get; set; } = "You can't call any more emergency meetings!";
    public string MeetingCooldown { get; set; } = "You can't call a meeting for another {time} seconds!";
    
    public string CalibrateDistributor { get; set; } = "Calibrate Distributor";
    public string CharCourse { get; set; } = "Chart Course";
    public string CleanO2Filter { get; set; } = "Clean O2 Filter";
    public string EmptyChute { get; set; } = "Empty Chute";
    public string PrimeShields { get; set; } = "Prime Shields";
    public string StartReactor { get; set; } = "Start Reactor";
    public string SubmitScan { get; set; } = "Submit Scan";
    public string FixWiring { get; set; } = "Fix Wiring";
    public string AlignEngineOutput { get; set; } = "Align Engine Output";
    public string DivertPowerto { get; set; } = "Divert Power to %roomName%";
    public string AcceptDivertPower { get; set; } = "Accept Divert Power";
    public string DownloadData { get; set; } = "Download Data";
    public string UploadData { get; set; } = "Upload Data";
}