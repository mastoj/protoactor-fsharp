open System
open Proto
open System.Threading.Tasks
open Proto.FSharp

type Message = {Text: string}
type Message2 = 
    | Text of string

[<EntryPoint>]
let main argv =
    let handleMessage msg = 
        match msg with
        | Text t -> printfn "This could work: %s" t

    let pid = 
        simpleProducer handleMessage
        |> fromProducer
        |> spawn

    [1 .. 1000]
    |> List.map (fun i -> Text (sprintf "Tomas %d" i))
    |> List.iter (tell pid)

    //Proto.createActor handleMessage // Actor.FromProducer(fun () -> (new MyActor() :> IActor))
    // let pid = Actor.Spawn(props)
    // pid.Tell({Text = "Tomas"})
    // pid.Tell(Text "Tomas")
    System.Console.ReadLine() |> ignore
    0
