
module LowLevelDesign.WTrace.Tests.Commons

open NUnit.Framework
open FsUnit
open LowLevelDesign.WTrace.Events

[<Test>]
let TestRollingCache () =
    let cache = DataCache<string, string>(4)

    let keyValues = 
        seq { 1..4 }
        |> Seq.map (fun i -> (sprintf "k%d" i, sprintf "v%d" i))

    keyValues
    |> Seq.iter cache.Add

    keyValues
    |> Seq.iter (fun (k, v) -> cache.[k] = v |> should be True)

    // adding for the second time should not change anything
    keyValues
    |> Seq.iter cache.Add

    keyValues
    |> Seq.iter (fun (k, v) -> cache.[k] = v |> should be True)

    // adding one new element should replace the oldest one
    cache.Add("k5", "v5")
    cache.ContainsKey("k1") |> should be False

    cache.Remove("k5") |> ignore
    cache.ContainsKey("k5") |> should be False

