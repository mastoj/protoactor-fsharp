// Learn more about F# at http://fsharp.org

open System
open Proto
open Proto.FSharp
open chat.messages
open Proto.Remote

[<EntryPoint>]
let main argv =
    Serialization.RegisterFileDescriptor(ChatReflection.Descriptor)
    Remote.Start("127.0.0.1", 8000)

    let mutable clients = []

    // let toPid (pid: chat.messages. = 

    let sayTo (clients: chat.messages.PID list) msg =
        clients
        |> List.map (fun a -> Proto.PID(a.Address, a.Id))
        |> List.iter (fun pid -> pid <! msg)

    let actor (ctx: Proto.IContext) = 
        match ctx.Message with
        | :? Connect as req ->
            printfn "client %A connected" req.Sender
            clients <- req.Sender :: clients
            let msg = Connected()
            msg.Message <- "Welcome!"
            let pid = Proto.PID(req.Sender.Address, req.Sender.Id)
            pid <! msg
            //            msg |> tell req.Sender
        | :? SayRequest as req ->
            let msg = SayResponse()
            msg.UserName <- req.UserName
            msg.Message <- req.Message
            msg |> sayTo clients
        | :? NickRequest as req ->
            let msg = NickResponse()
            msg.OldUserName <- req.OldUserName
            msg.NewUserName <- req.NewUserName
            msg |> sayTo clients
        | _ -> printfn "Uknown request: %A" ctx.Message
        Actor.Done

    actor
    |> fromFunc
    |> spawnNamed "chatserver"
    |> ignore

    System.Console.ReadLine() |> ignore


    0 // return an integer exit code
