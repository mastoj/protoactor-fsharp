// Learn more about F# at http://fsharp.org

open System
open Proto
open Proto.FSharp
open chat.messages
open Proto.Remote


[<EntryPoint>]
let main argv =
    Serialization.RegisterFileDescriptor(ChatReflection.Descriptor)
    Remote.Start("127.0.0.1", 0);
    let server = Proto.PID("127.0.0.1:8000", "chatserver")

    let handleMessage (ctx: IContext) = 
        match ctx.Message with
        | :? Connected as req -> 
            printfn "%A" req.Message
        | :? SayResponse as req ->
            printfn "%s %s" req.UserName req.Message
        | :? NickResponse as req ->
            printfn "%s is now %s" req.OldUserName req.NewUserName
        | _ -> printfn "Unknown message: %A" ctx.Message
        Actor.Done

    handleMessage
    |> fromFunc
    |> spawn
    |> (fun pid -> 
            let msg = Connect()
            msg.Sender <- chat.messages.PID()
            msg.Sender.Address <- pid.Address
            msg.Sender.Id <- pid.Id
            msg)
    |> tell server

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
            server <! msg
            nick <- msg.NewUserName
            readLine()
        else
            let msg = SayRequest()
            msg.UserName <- nick
            msg.Message <- line
            server <! msg
            readLine()

    readLine()
    printfn "Hello World from F#!"
    0 // return an integer exit code
