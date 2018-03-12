open System
open Proto
open Proto.FSharp
open System

let testSupervision strategy =
    printfn "Will test with strategy %A" strategy
    printfn "Press enter"
    Console.ReadLine() |> ignore
    let spawnChild (context: Proto.IContext) id =
        let childName = sprintf "child %i %s" id (System.Guid.NewGuid().ToString())

        let handler (ctx: IContext) (msg: obj) =
            printfn "Child actor pid: %A" ctx.Self
            match msg with
            | :? string -> raise (exn("I'm dying: " + childName))
            | _ -> printfn "(%s) Hello from child: %A" childName msg

        let props = Actor.create2 handler |> Actor.initProps
        let childPID = context.SpawnNamed(props, childName)
        printfn "Spawned child: '%s' with PID: '%A'" childName childPID

    let parentHandler (ctx: IContext) (msg: obj) =
        printfn "(Parent) Message: %A" msg
        match msg with
        | :? string as message when message = "kill" ->
            printfn "Will kill someone"
            let children = ctx.Children
            let childToKill = children |> Seq.head
            "die" >! childToKill
        | :? Proto.Started ->
            [ 1 .. 3 ] |> List.iter (spawnChild ctx)
        | _ -> printfn "Some other message: %A" msg

    let pid =
        parentHandler 
        |> Actor.create2
        |> Actor.initProps 
        |> Props.withChildSupervisorStrategy strategy
        |> Actor.spawnNamed (sprintf "parent %s" (System.Guid.NewGuid().ToString()))

    Console.ReadLine() |> ignore

    "kill" >! pid


[<EntryPoint>]
let main argv =
    testSupervision DefaultStrategy
    testSupervision (AllForOneStrategy((fun _ _ -> SupervisorDirective.Restart), 10, Some (TimeSpan.FromSeconds(10.))))

    Console.ReadLine() |> ignore
    0 // return an integer exit code
