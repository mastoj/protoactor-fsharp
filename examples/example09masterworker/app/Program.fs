// Learn more about F# at http://fsharp.org

open System
open Proto.FSharp
open Proto.Mailbox
open Proto.FSharp.Core
open Proto.Remote
open ProtoActorDemo.Messages

let testLocal() =
    let masterPid = Master.createMaster 100

    Worker.createWorkerMonitor 5 (Worker.requestWork masterPid) |> ignore


[<EntryPoint>]
let main argv =
    printfn "Registrating protos"
    Serialization.RegisterFileDescriptor(ProtoActorDemo.Messages.MessagesReflection.Descriptor)

    let argList = argv |> Array.toList

    match argList with
    | [] | "hello"::_ -> HelloProtoActor.hello()
    | "local"::_ | "l" :: _ -> testLocal()
    | "m"::count::_ -> Master.startMaster (Int32.Parse count) |> ignore
    | "w"::count::_ -> Worker.startWorker (Int32.Parse count) |> ignore
    | _ -> printfn "Unkown argument: %A" argList

    // let masterPid = Master.createMaster 30
    // let workerPids = [1 .. 10] |> List.map (fun i -> Worker.createWorker masterPid i)

    // printfn "Hello World from F#!"
    System.Threading.Thread.Sleep Int32.MaxValue
    0 // return an integer exit code



// Parent node....