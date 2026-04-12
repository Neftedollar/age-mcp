#r "bin/Debug/net10.0/FsMcp.Core.dll"
#r "bin/Debug/net10.0/FsMcp.Server.dll"
#r "bin/Debug/net10.0/TypeShape.dll"

open FsMcp.Core
open FsMcp.Core.Validation
open FsMcp.Server
open System.Threading.Tasks

type TestArgs = { graph_name: string }
type OptArgs = { dummy: string option }

let td1 = TypedTool.define<TestArgs> "test" "test" (fun _ -> Task.FromResult(Ok [])) |> unwrapResult
let td2 = TypedTool.define<OptArgs> "test2" "test2" (fun _ -> Task.FromResult(Ok [])) |> unwrapResult

printfn "=== TestArgs schema ==="
match td1.InputSchema with
| Some s -> printfn "%s" (s.GetRawText())
| None -> printfn "None"

printfn "\n=== OptArgs schema ==="
match td2.InputSchema with
| Some s -> printfn "%s" (s.GetRawText())
| None -> printfn "None"
