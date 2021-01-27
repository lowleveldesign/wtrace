module LowLevelDesign.WTrace.CommandLine

open  System.Text.RegularExpressions

// from http://fssnip.net/8g/title/Yet-another-commandline-parser by Gennady Loskutov
// with my modifications

// parse command using regex
// if matched, return (command name, command value) as a tuple
let (|Command|_|) (s : string) =
    let r = new Regex(@"^(?:-{1,2}|\/)(?<command>\?|\w+)[=:]*(?<value>.*)$", RegexOptions.IgnoreCase)
    let m = r.Match(s)
    if m.Success then Some (m.Groups.["command"].Value.ToLower(), m.Groups.["value"].Value)
    else None

let parseArgs (flags : seq<string>) (args : seq<string>) =
    args 
    |> Seq.scan (fun (sn : string, sv) arg ->
                    match arg with
                    | Command (n, v) when sn.Length <> 0 ->
                        // parse the command only if it's a command and we haven't
                        // passed the first free argument
                        if v.Length = 0 && flags |> Seq.contains(n) then 
                            (n, "<flag>") // flag
                        elif n.Length = 0 then (sn, v)
                        else (n, v)
                    | v when sn.Length <> 0 && sv.Length <> 0 -> ("", v)
                    | v -> (sn, v)) ("<empty>", "<empty>")
    |> Seq.skip 1
    |> Seq.groupBy (fun (n, _) -> n)
    |> Seq.map (fun (n, s) -> (n, s |> Seq.map (fun (_, v) -> v) |> Seq.filter (fun i -> i.Length > 0) |> Seq.toList))
    |> Map.ofSeq

