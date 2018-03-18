module HelloProtoActor
open Proto.FSharp

let hello() =
    let pid1 = 
        Actor.create (printfn "Hello from actor1: %A") 
        |> Actor.initProps 
        |> Actor.spawn
    let pid2 = 
        Actor.create (fun x -> printfn "Hello from actor2: %A" x; (sprintf "I was called with %A" x) >! pid1) 
            |> Actor.initProps 
            |> Actor.spawn

    [ 1 .. 100 ]
    |> List.iter (tell pid2)
