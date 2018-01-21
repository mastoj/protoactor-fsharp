// Learn more about F# at http://fsharp.org

open System
open Proto.FSharp
open ProtoActorDemo.Messages
open Proto.Mailbox
open Proto.FSharp.Core

module MessageHelpers =

    let toMessagePid (pid: Proto.PID) =
        let messagePid = PID()
        messagePid.Address <- pid.Address
        messagePid.Id <- pid.Id
        messagePid

    let toProtoPid (pid: PID) =
        Proto.PID(pid.Address, pid.Id)

    let newRequestWork (pid: Proto.PID) = 
        let requestWork = RequestWork()
        requestWork.Pid <- toMessagePid pid
        requestWork

    let newSubmitWorkRequest (pid: Proto.PID) (correlationId: Guid) data =
        let submitWorkRequest = SubmitWorkRequest()
        submitWorkRequest.CorrelationId <- correlationId.ToString()
        submitWorkRequest.Data <- data
        submitWorkRequest.Pid <- toMessagePid pid
        submitWorkRequest

    let newFinishedWorkResponse (pid: Proto.PID) (correlationId: Guid) result =
        let finishedWorkResponse = FinishedWorkResponse()
        finishedWorkResponse.CorrelationId <- correlationId.ToString()
        finishedWorkResponse.Result <- result
        finishedWorkResponse.Pid <- toMessagePid pid
        finishedWorkResponse

    type Messages =
        | RequestWork of RequestWork
        | SubmitWorkRequest of SubmitWorkRequest
        | FinishedWorkResponse of FinishedWorkResponse
        | SystemMessage of SystemMessage
        | Unknown of obj

    let (|IsMessage|_|) (msg: obj) =
        match msg with
        | :? RequestWork as m -> Some(RequestWork m)
        | :? SubmitWorkRequest as m -> Some(SubmitWorkRequest m)
        | :? FinishedWorkResponse as m -> Some(FinishedWorkResponse m)
        | _ -> None

    let mapMsg (msg: obj) =
        match msg with
        | IsMessage m -> m
        | IsSystemMessage m -> SystemMessage m
        | _ -> Unknown msg

module Master =
    open MessageHelpers

    type MasterState = {
        JobQueue: int list
        SubmittedJob: Map<Guid, int>
        FinishedJobs: Map<Guid, int>
        StartTime: DateTime
        Sink: PID option
    }

    let handler (context:Proto.IContext) message state =
        let submitJob (pid: ProtoActorDemo.Messages.PID) state =
            match state.JobQueue with
            | x::rest -> 
                let correlationId = Guid.NewGuid()
                let request = newSubmitWorkRequest (state.Sink |> Option.get |> toProtoPid) correlationId x
                request >! (toProtoPid pid)
                {state with JobQueue = rest; SubmittedJob = state.SubmittedJob |> Map.add correlationId x}
            | [] -> state

        match mapMsg message with
        | RequestWork m -> submitJob (m.Pid) state
        | FinishedWorkResponse m ->
            let key = Guid.Parse(m.CorrelationId)
            if state.SubmittedJob |> Map.tryFind key |> Option.isSome
            then 
                let state' = {state with FinishedJobs = state.FinishedJobs |> Map.add key m.Result}
                if state'.FinishedJobs.Count = state'.SubmittedJob.Count && state.JobQueue |> List.isEmpty
                then 
                    let result = state'.FinishedJobs |> Map.toSeq |> Seq.sumBy snd
                    printfn "JobQueue finished with total result: %A" result
                    printfn "Duration %A" (DateTime.Now - state.StartTime)
                    state'
                else
                    state' |> submitJob m.Pid
            else state
        | SystemMessage (SystemMessage.Started _) ->
            printfn "YOLO"
            let sinkProtoPid = Actor.create (printfn "Sink: %A") |> Actor.initProps |> Actor.spawn
            let sinkPid = PID()
            printfn "YOLO 2"
            sinkPid.Address <- sinkProtoPid.Address
            sinkPid.Id <- sinkProtoPid.Id
            {state with Sink = Some sinkPid }
        | _ -> state


    let createMaster workLoad =
        printfn "Creating master"
        Actor.withState2 handler ({JobQueue = (List.init workLoad id); SubmittedJob = Map.empty; FinishedJobs = Map.empty; StartTime = DateTime.Now; Sink = None})
        |> Actor.initProps
        |> Actor.spawn

module Worker =
    open MessageHelpers
    let createWorker (masterPid: Proto.PID) workerId =
        let handler (context: Proto.IContext) message =
            match mapMsg message with
            | SubmitWorkRequest m ->
                printfn "%d Got some work: %A" workerId m.Data
                System.Threading.Thread.Sleep 1000
                let result = (m.Data |> float |> Math.Sqrt |> int)
                newFinishedWorkResponse context.Self (Guid.Parse(m.CorrelationId)) result
                >! (toProtoPid m.Pid)
                newRequestWork (context.Self) >! masterPid
            | SystemMessage (SystemMessage.Started _) ->
                printfn "Worker started: %A" workerId
                newRequestWork (context.Self) >! masterPid
            | _ -> printfn "Should never happen"

        Actor.create2 handler
        |> Actor.initProps
        |> Actor.spawn

open MessageHelpers
[<EntryPoint>]
let main argv =

    let masterPid = Master.createMaster 20
    let workerPids = [1 .. 10] |> List.map (fun i -> Worker.createWorker masterPid i)

    printfn "Hello World from F#!"
    System.Threading.Thread.Sleep Int32.MaxValue
    0 // return an integer exit code



// Parent node....