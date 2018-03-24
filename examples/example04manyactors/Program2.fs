open System
open Proto.FSharp
open Proto

type Msg =
    | Inc of int
    | Get

[<EntryPoint>]
let main argv =
    let mutable state = 0
    let handler (ctx: IContext) msg =
        match msg with
        | Inc _ -> state <- state + 1
        | Get -> ctx.Sender <! state

    let counter = Actor.create2 handler |> Actor.spawnProps

    let createAndTell x =
        Actor.create (fun msg -> counter <! msg)
        |> Actor.initProps
        |> Actor.spawn

    let numberOfMessages = 1000000
    let startTime = DateTime.Now
    async {
        Seq.init numberOfMessages id
        |> Seq.map createAndTell
        |> Seq.iteri (fun i pid -> pid <! Inc i)
        //|> Async.Parallel
        //|> Async.RunSynchronously
        //|> ignore
    } |> Async.Start
    
    async {
        let mutable count = -1
        while count < 1000000 do
            do! Async.Sleep 100
            let! (c: int) = counter <? Get
            printfn "%d" c
            count <- c
    } |> Async.RunSynchronously

    let endTime = DateTime.Now
    let duration = (endTime - startTime)
    printfn "%A" duration
    printfn "Messages per second %f" ((float numberOfMessages) / (float duration.TotalSeconds))
    Console.ReadLine() |> ignore
    0 // return an integer exit code
