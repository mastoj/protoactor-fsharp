open System
open Proto.FSharp

let echoActor() =
    
    let pid = Actor.create (printfn "Hello from actor: %A") |> Actor.initProps |> Actor.spawn
    "Hello world" >! pid

let echoWithStateActor() =
    let handler msg state =
        let state' = sprintf "%s %s" (state.ToString()) (msg.ToString())
        printfn "Current state: %A" state'
        state'
    let pid = Actor.withState handler "" |> Actor.initProps |> Actor.spawn
    "Hello world" >! pid
    "Hello world again" >! pid

let echoWithResponse() =
    let handler (context: Proto.IContext) (msg: obj) =
        match msg with
        | :? String as s -> sprintf "You said: %s" s >! context.Sender
        | x -> printfn "Unhandled message: %A" x
    let pid = Actor.create2 handler |> Actor.initProps |> Actor.spawn
    "Hello world" >? pid |> Async.RunSynchronously |> printfn "Response: %A"

let echoWithResponseAndState() =
    let handler (context: Proto.IContext) (msg: obj) state =
        let state' =
            match msg with
            | :? String as s ->
                let state' = sprintf "%s %s" (state.ToString()) (msg.ToString())
                sprintf "Current state: %A" state' >! context.Sender
                state'
            | x -> state
        state'
    let pid = Actor.withState2 handler "" |> Actor.initProps |> Actor.spawn
    "Hello world" >? pid |> Async.RunSynchronously |> printfn "Response: %A"
    "Hello world again" >? pid |> Async.RunSynchronously |> printfn "Response: %A"

[<EntryPoint>]
let main argv =
    echoActor()
    echoWithStateActor()
    echoWithResponse()
    echoWithResponseAndState()

    Console.ReadLine() |> ignore
    0 // return an integer exit code
