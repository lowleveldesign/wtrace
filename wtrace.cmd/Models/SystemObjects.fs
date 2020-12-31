namespace LowLevelDesign.WTrace

open System

type Process = {
    Pid : int32
    ParentPid : int32
    ProcessName : string
    ImageFileName : string
    CommandLine : string
    ExtraInfo : string
    StartTime : DateTime
    ExitTime : DateTime
    ExitStatus : int32
}

