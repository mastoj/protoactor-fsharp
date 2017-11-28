open System
open Proto.FSharp

type Person = 
    {
        Age: int
        Name: string
    }

type SetName = { Name : string }
type SetAge = { Age : int }

type Messages =
    | SetName of SetName
    | SetAge of SetAge
    | SystemMessage of SystemMessage
    | Unknown of obj

let (|IsMessage|_|) (msg: obj) =
    match msg with
    | :? SetAge as m -> Some(SetAge m)
    | :? SetName as m -> Some(SetName m)
    | _ -> None

[<EntryPoint>]
let main argv =
    let mapMsg (msg: obj) =
        match msg with
        | IsMessage m -> m
        | IsSystemMessage m -> SystemMessage m
        | _ -> Unknown msg

    let handler msg state = 
        let state' : Person = 
            match msg with
            | SetName m -> 
                printfn "SetName: %A" m
                { state with Name = m.Name }
            | SetAge m -> 
                printfn "SetAge: %A" m
                { state with Age = m.Age }
            | SystemMessage m ->
                printfn "System message: %A" m
                state
            | Unknown m -> 
                printfn "unknown %A" m
                state
        printfn "Current state: %A" state'
        state'

    let pid =
        Actor.withState (mapMsg >> handler) { Name = "John"; Age = 0}
        |> Actor.initProps 
        |> Actor.spawn

    { Age = 35 } >! pid
    { Name = "Tomas" } >! pid

    Console.ReadLine() |> ignore
    0 // return an integer exit code
