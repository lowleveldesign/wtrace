namespace LowLevelDesign.WTrace

type Process = {
    Pid : int32
    ParentPid : int32
    ProcessName : string
    ImageFileName : string
    CommandLine : string
    ExtraInfo : string
    StartTime : Qpc
    ExitTime : Qpc
    ExitStatus : int32
}

