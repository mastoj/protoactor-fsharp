// Learn more about F# at http://fsharp.org

open System
open Proto
open Proto.FSharp
open chat.messages
open Proto.Remote
open Grpc.Core
open Proto

type Message =
    | Connect of Connect
    | SayRequest of SayRequest
    | NickRequest of NickRequest
    | SystemMessage of SystemMessage
    | Unknown of obj

[<EntryPoint>]
let main argv =
    Serialization.RegisterFileDescriptor(ChatReflection.Descriptor)
    Remote.Start("127.0.0.1", 8000)

    let (|IsMessage|_|) (msg:obj) =
        match msg with
        | :? Connect as m -> Some(Connect m)
        | :? SayRequest as m -> Some(SayRequest m)
        | :? NickRequest as m -> Some(NickRequest m)
        | _ -> None

    let mapMsg (msg:obj) =
        printfn "Am I here?"
        match msg with
        | IsMessage m -> 
            printfn "IsMessage"
            m
        | IsSystemMessage m -> 
            printfn "SystemMessage"
            SystemMessage m
        | _ -> 
            printfn "Unknown"
            Unknown msg

    let handleMessage (msg: Message) (clients) =
        printfn "Am I here to?"
        let sayTo clients msg =
            clients |> List.iter (fun c -> msg >! c)

        let clients' =
            match msg with
            | Connect req ->
                printfn "client %A connected" req.Sender
                let msg = Connected()
                msg.Message <- "Welcome!"
                let pid = Proto.PID(req.Sender.Address, req.Sender.Id)
                msg >! pid
                let sender = Proto.PID(req.Sender.Address, req.Sender.Id)
                sender :: clients
            | SayRequest req ->
                let msg = SayResponse()
                msg.UserName <- req.UserName
                msg.Message <- req.Message
                msg |> sayTo clients
                clients
            | NickRequest req ->
                let msg = NickResponse()
                msg.OldUserName <- req.OldUserName
                msg.NewUserName <- req.NewUserName
                msg |> sayTo clients
                clients
            | x -> 
                printfn "Uknown request: %A" x
                clients
        clients'

    Actor.withState (mapMsg >> handleMessage) [] |> Actor.initProps |> Actor.spawnNamed "chatserver" |> ignore
    printfn "Started"
    System.Console.ReadLine() |> ignore

    0 // return an integer exit code
