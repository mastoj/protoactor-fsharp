module Master
open MessageHelpers
open Proto.FSharp
open Proto.Remote

module Sink =
    type SinkState = {
        ExpectedNumberOfResult: int option
        NumberOfResult: int
        TotalSum: float32
    }

    let sinkHandler message state =
        let state' =
            match message with
            | SubmitExpectedResultCount m -> {state with ExpectedNumberOfResult = Some (m.Count)}
            | SubmitResult m -> {state with TotalSum = state.TotalSum + m.Result; NumberOfResult = state.NumberOfResult + 1}
            | _ -> state
        match state'.ExpectedNumberOfResult with
        | Some x when x = state'.NumberOfResult -> 
            printfn "Average is: %A" (state'.TotalSum / (float32)state'.NumberOfResult)
        | None -> 
            printfn "Current sink state: %A" (state', message)
        | Some _ -> ()
        state'

    let createSink() =
        Actor.withState (mapMsg >> sinkHandler) {ExpectedNumberOfResult = None; NumberOfResult = 0; TotalSum = (float32)0.}
        |> Actor.initProps
        |> Actor.spawn

type MasterState = {
    RemainingWork: int list
    SinkPid: Proto.PID option
}

let setupSink expectedWorkLoad =
    let sinkPid = Sink.createSink()
    let expectedCountMsg = MessageHelpers.newSubmitExpectedResultCount expectedWorkLoad
    expectedCountMsg >! sinkPid
    sinkPid

let handler (context: Proto.IContext) message state =
    match message |> MessageHelpers.mapMsg with
    | SystemMessage (SystemMessage.Started _) ->
        printfn "Starting master"
        let sinkPid = setupSink (state.RemainingWork.Length)
        printfn "Sink started"
        {state with SinkPid = Some sinkPid}
    | RequestWork m ->
        match state.RemainingWork with
        | [] -> state
        | x::rest ->
            let sinkPid = state.SinkPid |> Option.get
            let protoPid = m.Pid |> MessageHelpers.toProtoPid
            newSubmitWork sinkPid x >! protoPid
            {state with RemainingWork = rest}

let createMasterProps numberOfWork =
    Actor.withState2 handler {RemainingWork = [1 .. numberOfWork]; SinkPid = None} 
    |> Actor.initProps 

let createMaster = createMasterProps >> (Actor.spawn)

let startMaster numberOfWork =
    let props = createMasterProps numberOfWork
    Remote.RegisterKnownKind("MasterKind", props)
    createMaster numberOfWork |> ignore
    Cluster.startCluster "master"
