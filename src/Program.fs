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
    | Unknown

let (|IsMessage|_|) (msg: obj) =
    match msg with
    | :? SetAge as m -> Some(SetAge m)
    | :? SetName as m -> Some(SetName m)
    | _ -> None

let test2() =
    let handler s m = 
        printfn "Received: %A" m
        let s' = s + "." + m
        printfn "State: %A" s'
        s'

    let pid =
        Actor.withState handler ""
        |> Actor.initProps
        |> Actor.spawn
    pid <! "proto"
    pid <! "actor"

    let pid3 = Actor.create (printfn "Msg: %A") |> Actor.initProps |> Actor.spawn
    [ 1 .. 100 ] |> List.iter (fun i -> pid3 <! i)

    let mapMsg (msg: obj) =
        match msg with
        | IsMessage m -> m
        | _ -> Unknown

    let handler4 (msg: Messages) state = 
        let state' : Person = 
            match msg with
            | SetName m -> 
                printfn "SetName: %A" m
                { state with Name = m.Name }
            | SetAge m -> 
                printfn "SetAge: %A" m
                { state with Age = m.Age }
            | Unknown -> 
                printfn "unknown %A" msg
                state
        printfn "Current state: %A" state'
        state'

    let pid4 =
        Actor.withState (mapMsg >> handler4) { Name = "John"; Age = 0}
        |> Actor.initProps 
        |> Actor.spawn

    pid4 <! { Name = "tomas" }
    pid4 <! { Age = 35 }
    pid4 <! "this isn't handled"
    ()

[<EntryPoint>]
let main argv =
    test2()
    System.Console.ReadLine() |> ignore
    0
