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
    public bool ConfirmEjects { get; set; } = true;

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
                    Description = Plugin.Instance.Translation.CalibrateDistributor
                },
                new Task
                {
                    Name = TaskName.ChartCourse, RoomName = RoomName.Navigation, Type = TaskType.Short,
                    Description = Plugin.Instance.Translation.CharCourse
                },
                new Task
                {
                    Name = TaskName.CleanO2Filter, RoomName = RoomName.O2, Type = TaskType.Short,
                    Description = Plugin.Instance.Translation.CleanO2Filter
                },
                /*new Task
                {
                    Name = TaskName.EmptyChute, RoomName = RoomName.O2, Type = TaskType.Long, IsVisual = true,
                    Description = Plugin.Instance.Translation.EmptyChute
                },
                new Task
                {
                    Name = TaskName.EmptyChute, RoomName = RoomName.Storage, Type = TaskType.Long, IsVisual = true,
                    Description = Plugin.Instance.Translation.EmptyChute
                },
                new Task { Name = Skeld.TaskName.InspectSample, RoomName = Skeld.RoomName.MedBay, Type = TaskType.Long, Description = "Inspect Sample" }, Special task
                new Task
                {
                    Name = TaskName.PrimeShields, RoomName = RoomName.Shields, Type = TaskType.Common, IsVisual = true,
                    Description = Plugin.Instance.Translation.PrimeShields
                },*/
                new Task
                {
                    Name = TaskName.StartReactor, RoomName = RoomName.Reactor, Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.StartReactor
                },
                /*new Task
                {
                    Name = TaskName.SubmitScan, RoomName = RoomName.MedBay, Type = TaskType.Long, IsVisual = true,
                    Description = Plugin.Instance.Translation.SubmitScan
                },*/
                new Task
                {
                    Name = TaskName.FixWiring,
                    RoomName = RoomName.Electrical,
                    Type = TaskType.Common,
                    Description = Plugin.Instance.Translation.FixWiring,
                    MaxStageTask = 3,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Storage, Type = TaskType.Common,
                            Description = Plugin.Instance.Translation.FixWiring
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Admin, Type = TaskType.Common,
                            Description = Plugin.Instance.Translation.FixWiring
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Navigation, Type = TaskType.Common,
                            Description = Plugin.Instance.Translation.FixWiring
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Cafeteria, Type = TaskType.Common,
                            Description = Plugin.Instance.Translation.FixWiring
                        },
                        new StageTask
                        {
                            Name = TaskName.FixWiring, RoomName = RoomName.Security, Type = TaskType.Common,
                            Description = Plugin.Instance.Translation.FixWiring
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.AlignEngineOutput,
                    RoomName = RoomName.UpperEngine,
                    Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.AlignEngineOutput,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.AlignEngineOutput, RoomName = RoomName.LowerEngine, Type = TaskType.Long,
                            Description = Plugin.Instance.Translation.AlignEngineOutput
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.AlignEngineOutput,
                    RoomName = RoomName.LowerEngine,
                    Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.AlignEngineOutput,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.AlignEngineOutput, RoomName = RoomName.UpperEngine, Type = TaskType.Long,
                            Description = Plugin.Instance.Translation.AlignEngineOutput
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.DivertPower,
                    RoomName = RoomName.Electrical,
                    Type = TaskType.Short,
                    Description = Plugin.Instance.Translation.DivertPowerto,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Communications,
                            Type = TaskType.Short, Description = Plugin.Instance.Translation.AcceptDivertPower
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.LowerEngine, Type = TaskType.Short,
                            Description = Plugin.Instance.Translation.AcceptDivertPower
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Navigation, Type = TaskType.Short,
                            Description = Plugin.Instance.Translation.AcceptDivertPower
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.O2, Type = TaskType.Short,
                            Description = Plugin.Instance.Translation.AcceptDivertPower
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Security, Type = TaskType.Short,
                            Description = Plugin.Instance.Translation.AcceptDivertPower
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Shields, Type = TaskType.Short,
                            Description = Plugin.Instance.Translation.AcceptDivertPower
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.UpperEngine, Type = TaskType.Short,
                            Description = Plugin.Instance.Translation.AcceptDivertPower
                        },
                        new StageTask
                        {
                            Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Weapons, Type = TaskType.Short,
                            Description = Plugin.Instance.Translation.AcceptDivertPower
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Cafeteria,
                    Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.DownloadData,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = Plugin.Instance.Translation.UploadData
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Communications,
                    Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.DownloadData,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = Plugin.Instance.Translation.UploadData
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Electrical,
                    Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.DownloadData,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = Plugin.Instance.Translation.UploadData
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.UploadData,
                    RoomName = RoomName.Navigation,
                    Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.DownloadData,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = Plugin.Instance.Translation.UploadData
                        }
                    ]
                },
                new Task
                {
                    Name = TaskName.AcceptDivertedPower,
                    RoomName = RoomName.Weapons,
                    Type = TaskType.Long,
                    Description = Plugin.Instance.Translation.DownloadData,
                    StageTasks =
                    [
                        new StageTask
                        {
                            Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                            Description = Plugin.Instance.Translation.UploadData
                        }
                    ]
                }
            ]
        }
    };
}