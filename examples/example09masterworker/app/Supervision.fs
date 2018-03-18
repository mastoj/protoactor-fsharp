module Supervision
open Proto.FSharp
open Proto

let run() =
    let rand = new System.Random()

    let createChild childId =
        let handler _ =
            let rec inner cnt =
                if cnt = 5 then 
                    childId 
                    |> sprintf "Child %d: I don't want to play anymore" 
                    |> exn 
                    |> raise
                else 
                    printfn "Child %d: Playing with you %d" childId cnt
                    System.Threading.Thread.Sleep(rand.Next(2000))
                    inner (cnt+1)
            inner 0

        Actor.create handler |> Actor.initProps
    
    let masterHandler (context:IContext) (msg:obj) =
        match msg with
        | :? Started ->
            [1 .. 2] |> List.iter (createChild >> context.Spawn >> ignore)
        | x -> printfn "Master: %A" x

    Actor.create2 masterHandler |> Actor.initProps |> Actor.spawn |> ignore