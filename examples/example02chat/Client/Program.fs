// Learn more about F# at http://fsharp.org

open System
open Proto
open chat.messages
open Proto.Remote
open Proto.Mailbox
open Proto.FSharp

type Message =
    | Connected of Connected
    | SayResponse of SayResponse
    | NickResponse of NickResponse
    | SystemMessage of SystemMessage
    | Unknown of obj

[<EntryPoint>]
let main argv =
    Serialization.RegisterFileDescriptor(ChatReflection.Descriptor)
    Remote.Start("127.0.0.1", 0);
    let server = Proto.PID("127.0.0.1:8000", "chatserver")

    let (|IsMessage|_|) (msg:obj) =
        match msg with
        | :? Connected as m -> Some(Connected m)
        | :? SayResponse as m -> Some(SayResponse m)
        | :? NickResponse as m -> Some(NickResponse m)
        | _ -> None

    let mapMsg (msg:obj) =
        match msg with
        | IsMessage m -> m
        | IsSystemMessage m -> SystemMessage m
        | _ -> Unknown msg

    let handleMessage (msg: Message) =
        match msg with
        | Connected req -> printfn "%A" req.Message
        | SayResponse req ->
            printfn "%s %s" req.UserName req.Message
        | NickResponse req ->
            printfn "%s is now %s" req.OldUserName req.NewUserName
        | SystemMessage _ -> ()
        | Unknown _ -> ()

    let pid = Actor.create (mapMsg >> handleMessage) |> Actor.initProps |> Actor.spawn
    let tellServer (pid: Proto.PID) (server:Proto.PID) =
        let msg = Connect()
        msg.Sender <- chat.messages.PID()
        msg.Sender.Address <- pid.Address
        msg.Sender.Id <- pid.Id
        msg >! server
    tellServer pid server

    let mutable nick = "Alex"

    let rec readLine() = 
        let line = System.Console.ReadLine()
        if line = "/exit" then ()
        else if line.StartsWith("/nick")
        then
            let arr = line.Split([|' '|])
            let msg = NickRequest()
            msg.OldUserName <- nick
            msg.NewUserName <- arr.[1]
            msg >! server
            nick <- msg.NewUserName
            readLine()
        else
            let msg = SayRequest()
            msg.UserName <- nick
            msg.Message <- line
            msg >! server
            readLine()

    readLine()
    printfn "Hello World from F#!"
    0 // return an integer exit code
