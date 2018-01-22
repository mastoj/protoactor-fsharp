module Worker
open MessageHelpers
open Proto.FSharp
open Proto
open System
open Proto.Mailbox
open ProtoActorDemo.Messages

// module Master = 
//     let sinkPid = Sink.createSink()

let handler workerId requestWork (context: Proto.IContext) message =
    match message |> MessageHelpers.mapMsg with
    | SubmitWork m ->
        printfn "%d Got some work: %A" workerId m
        System.Threading.Thread.Sleep 1000
        let result = m.Data |> float |> Math.Sqrt |> float32
        let resultDto = MessageHelpers.newSubmitResult result
        resultDto >! (m.Pid |> MessageHelpers.toProtoPid)
        requestWork (context.Self)
    | SystemMessage (SystemMessage.Started _) -> 
        requestWork (context.Self)

let createWorkerKind requestWork workerId =
    Actor.create2 (handler workerId requestWork) |> Actor.initProps


let createWorkerMonitor workerCount requestWork =
    let handler (context: Proto.IContext) message =
        match message |> MessageHelpers.mapMsg with
        | SystemMessage (SystemMessage.Started _) ->
            [ 1 .. workerCount ] |> List.iter (fun i  -> i |> createWorkerKind requestWork |> context.Spawn |> ignore)
        | _ -> ()

    Actor.create2 handler
    |> Actor.initProps
    |> Actor.Spawn

let requestWork masterPid workerPid =
    MessageHelpers.newRequestWork workerPid >! masterPid

let getMasterPid() = 
    let rec getPid() = 
        printfn "Trying to get pid again"
        let (pid, sc) = Proto.Cluster.Cluster.GetAsync("FiresideChatCluster", "MasterKind").Result.ToTuple()
        if sc <> Proto.Remote.ResponseStatusCode.OK then 
            printfn "Failed to get pid: %A" (pid, sc)
            System.Threading.Thread.Sleep(4000)
            getPid()
        else pid
    getPid()

let startWorker workerCount =
    let hostName = 
        match Environment.GetEnvironmentVariable("CUMPUTERNAME"), Environment.GetEnvironmentVariable("HOSTNAME") with
        | null, h -> h
        | h, _ -> h
    printfn "Hostname: %A" hostName

    for entry in Environment.GetEnvironmentVariables() |> Seq.cast<System.Collections.DictionaryEntry> do printfn "%A: %A" entry.Key entry.Value
    
    Cluster.startCluster (hostName) // + (System.Guid.NewGuid().ToString()))
    let masterPid = getMasterPid()
    createWorkerMonitor workerCount (requestWork masterPid)
