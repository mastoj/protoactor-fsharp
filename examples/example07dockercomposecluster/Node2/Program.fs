open System
open Proto
open Proto.Remote
open Proto.Cluster
open Proto.Cluster.Consul
open Proto.FSharp
open Messages

let startCluster() =
    Cluster.Start("MyCluster", "127.0.0.1", 12000, ConsulProvider(ConsulProviderOptions()))

let doStuff() =
    Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor)
    let handler (context:Proto.IContext) (msg:obj) =
        printfn "Some one called: %A" (msg.GetType())
        match msg with
        | :? HelloRequest ->
            printfn "Someone is saying hello %A" (context.Sender)
            let response = HelloResponse()
            response.Message <- "Hello from node2"
            context.Respond(response)
            printfn "Did we send response?"
        | _ -> ()
    
    let props = Actor.create2 handler |> Actor.initProps
    Remote.RegisterKnownKind("HelloKind", props)

[<EntryPoint>]
let main argv =
    doStuff()
    startCluster()
    Console.ReadLine() |> ignore
    0 // return an integer exit code
