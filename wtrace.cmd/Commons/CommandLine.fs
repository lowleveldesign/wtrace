module LowLevelDesign.WTrace.CommandLine

open  System.Text.RegularExpressions

// from http://fssnip.net/8g/title/Yet-another-commandline-parser by Gennady Loskutov
// with my modifications

// parse command using regex
// if matched, return (command name, command value) as a tuple
let (|Command|_|) (s : string) =
    let r = new Regex(@"^(?:-{1,2}|\/)(?<command>\w+)[=:]*(?<value>.*)$", RegexOptions.IgnoreCase)
    let m = r.Match(s)
    if m.Success then Some (m.Groups.["command"].Value.ToLower(), m.Groups.["value"].Value)
    else None

// take a sequence of argument values
// map them into a (name,value) tuple
// scan the tuple sequence and put command name into all subsequent tuples without name
// discard the initial ("","") tuple
// group tuples by name 
// convert the tuple sequence into a map of (name,value seq)
let parseArgs (flags : seq<string>) (args : seq<string>) =
    args 
    |> Seq.map (fun i -> 
                    match i with
                    | Command (n, v) -> 
                        if v.Length = 0 && flags |> Seq.contains(n) then 
                            (n, "<flag>") // flag
                        else (n, v) // command
                    | _ -> ("", i) // data
                  )
    |> Seq.scan (fun (sn, sv) (n, v) -> 
                    if n.Length > 0 then (n, v) 
                    else if sv.Length > 0 then ("", v) else (sn, v)) ("", "")
    |> Seq.skip 1
    |> Seq.groupBy (fun (n, _) -> n)
    |> Seq.map (fun (n, s) -> (n, s |> Seq.map (fun (_, v) -> v) |> Seq.filter (fun i -> i.Length > 0) |> Seq.toList))
    |> Map.ofSeq

