module MessageHelpers
open ProtoActorDemo.Messages
open Proto.FSharp

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

let newSubmitWork (pid: Proto.PID) data =
    let submitWork = SubmitWork()
    submitWork.Data <- data
    submitWork.Pid <- toMessagePid pid
    submitWork

let newSubmitResult result =
    let submitResult = SubmitResult()
    submitResult.Result <- result
    submitResult

let newSubmitExpectedResultCount count =
    let submitExpectedResultCount = new SubmitExpectedResultCount()
    submitExpectedResultCount.Count <- count
    submitExpectedResultCount

type Message =
    | RequestWork of RequestWork
    | SubmitWork of SubmitWork
    | SubmitExpectedResultCount of SubmitExpectedResultCount
    | SubmitResult of SubmitResult
    | SystemMessage of SystemMessage
    | Unknown of obj

let (|IsMessage|_|) (msg: obj) =
    match msg with
    | :? RequestWork as m -> Some(RequestWork m)
    | :? SubmitWork as m -> Some(SubmitWork m)
    | :? SubmitResult as m -> Some(SubmitResult m)
    | :? SubmitExpectedResultCount as m -> Some(SubmitExpectedResultCount m)
    | _ -> None

let mapMsg (msg: obj) =
    match msg with
    | IsMessage m -> m
    | IsSystemMessage m -> SystemMessage m
    | _ -> Unknown msg
