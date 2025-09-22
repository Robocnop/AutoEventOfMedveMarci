using System.Collections.Generic;
using System.ComponentModel;
using AutoEvent.API;
using AutoEvent.Games.AmongUs.Features;
using AutoEvent.Games.AmongUs.Skeld;
using AutoEvent.Interfaces;
using CustomPlayerEffects;
using PlayerRoles;
using YamlDotNet.Serialization;

namespace AutoEvent.Games.AmongUs.Configs;

public class Config : EventConfig
{
    public Loadout Loadout { get; set; } = new()
    {
        Roles = new Dictionary<RoleTypeId, int> { { RoleTypeId.ClassD, 100 } },
        Items = [],
        Effects =
        [
            new EffectData { Type = nameof(FogControl), Duration = 0, Intensity = 2 },
            new EffectData { Type = nameof(Fade), Duration = 0, Intensity = 255 }
        ],
        InfiniteAmmo = AmmoMode.NoReloadInfiniteAmmo
    };

    [Description("The amount of Impostors that can spawn.")]
    public RoleCount Impostors { get; set; } = new() { MinimumPlayers = 1, MaximumPlayers = 3, PlayerPercentage = 10 };

    public float KillCooldown { get; set; } = 45f;
    public float KillDistance { get; set; } = 2f;

    public int EmergencyMeetings { get; set; } = 1;
    public int EmergencyCooldown { get; set; } = 0;
    public float VotingTime { get; set; } = 120f;
    public bool AnonymousVotes { get; set; } = false;
    public bool ConfirmEjects { get; set; } = false;

    public int CommonTasks { get; set; } = 1;
    public int LongTasks { get; set; } = 1;
    public int ShortTasks { get; set; } = 2;

    //todo: Special Tasks, Visual Tasks
    [YamlIgnore] public bool VisualTasks { get; set; } = true;

    [YamlIgnore]
    public Dictionary<string, List<Task>> Tasks { get; set; } = new()
    {
        {
            "Skeld",
            [
                new Task
                {
                    Name = TaskName.CalibrateDistributor, RoomName = RoomName.Electrical, Type = TaskType.Short,
                    Description = "Calibrate Distributor"
                },
                new Task
                {
                    Name = TaskName.ChartCourse, RoomName = RoomName.Navigation, Type = TaskType.Short,
                    Description = "Char Course"
                },
                new Task
                {
                    Name = TaskName.CleanO2Filter, RoomName = RoomName.O2, Type = TaskType.Short,
                    Description = "Clean O2 Filter"
                },
                /*new Task
                {
                    Name = TaskName.EmptyChute, RoomName = RoomName.O2, Type = TaskType.Long, IsVisual = true,
                    Description = "Empty Chute"
                },
                new Task
                {
                    Name = TaskName.EmptyChute, RoomName = RoomName.Storage, Type = TaskType.Long, IsVisual = true,
                    Description = "Empty Chute"
                },
                new Task { Name = Skeld.TaskName.InspectSample, RoomName = Skeld.RoomName.MedBay, Type = TaskType.Long, Description = "Inspect Sample" }, Special task
                new Task
                {
                    Name = TaskName.PrimeShields, RoomName = RoomName.Shields, Type = TaskType.Common, IsVisual = true,
                    Description = "Prime Shields"
                },*/
                new Task
                {
                    Name = TaskName.StartReactor, RoomName = RoomName.Reactor, Type = TaskType.Long,
                    Description = "Start Reactor"
                },
                /*new Task
                {
                    Name = TaskName.SubmitScan, RoomName = RoomName.MedBay, Type = TaskType.Long, IsVisual = true,
                    Description = "Submit Scan"
                },*/
                new Task
                {
                    Name = TaskName.FixWiring,
                    RoomName = RoomName.Electrical,
                    Type = TaskType.Common,
                    Description = "Fix Wiring",
                    MaxStageTask = 3,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Storage, Type = TaskType.Common,
                            Description = "Fix Wiring"
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Admin, Type = TaskType.Common,
                            Description = "Fix Wiring"
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Navigation, Type = TaskType.Common,
                            Description = "Fix Wiring"
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Cafeteria, Type = TaskType.Common,
                            Description = "Fix Wiring"
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Security, Type = TaskType.Common,
                            Description = "Fix Wiring"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.AlignEngineOutput,
                    RoomName = RoomName.UpperEngine,
                    Type = TaskType.Long,
                    Description = "Align Engine Output",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.AlignEngineOutput, RoomName = RoomName.LowerEngine, Type = TaskType.Long,
                            Description = "Align Engine Output"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.AlignEngineOutput,
                    RoomName = RoomName.LowerEngine,
                    Type = TaskType.Long,
                    Description = "Align Engine Output",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.AlignEngineOutput, RoomName = RoomName.UpperEngine, Type = TaskType.Long,
                            Description = "Align Engine Output"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.DivertPower,
                    RoomName = RoomName.Electrical,
                    Type = TaskType.Short,
                    Description = "Divert Power to %roomName%",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Communications,
                            Type = TaskType.Short, Description = "Accept Diverted Power"
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.LowerEngine, Type = TaskType.Short,
                            Description = "Accept Diverted Power"
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Navigation, Type = TaskType.Short,
                            Description = "Accept Diverted Power"
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.O2, Type = TaskType.Short,
                            Description = "Accept Diverted Power"
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Security, Type = TaskType.Short,
                            Description = "Accept Diverted Power"
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Shields, Type = TaskType.Short,
                            Description = "Accept Diverted Power"
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.UpperEngine, Type = TaskType.Short,
                            Description = "Accept Diverted Power"
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Weapons, Type = TaskType.Short,
                            Description = "Accept Diverted Power"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Cafeteria,
                    Type = TaskType.Long,
                    Description = "Download Data",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = "Upload Data"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Communications,
                    Type = TaskType.Long,
                    Description = "Download Data",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = "Upload Data"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Electrical,
                    Type = TaskType.Long,
                    Description = "Download Data",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = "Upload Data"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Navigation,
                    Type = TaskType.Long,
                    Description = "Download Data",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = "Upload Data"
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.AcceptDivertedPower,
                    RoomName = RoomName.Weapons,
                    Type = TaskType.Long,
                    Description = "Download Data",
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = "Upload Data"
                        }
                    ]
                }
            ]
        }
    };
}