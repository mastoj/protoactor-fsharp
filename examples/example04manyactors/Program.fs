open System
open Proto.FSharp

[<EntryPoint>]
let main argv =
    let createAndTell x =
        async {
            Actor.create ignore
            |> Actor.initProps
            |> Actor.spawn
            <! (sprintf "Hello actor %i" x)
        }
    let numberOfMessages = 1000000
    let startTime = DateTime.Now
    let res =
        Seq.init numberOfMessages id
        |> Seq.map createAndTell
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

    let endTime = DateTime.Now
    let duration = (endTime - startTime)
    printfn "%A" duration
    printfn "Messages per second %f" ((float numberOfMessages) / (float duration.TotalSeconds))
    Console.ReadLine() |> ignore
    0 // return an integer exit code
