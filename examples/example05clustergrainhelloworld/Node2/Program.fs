open System
open Proto.Cluster
open Proto.Cluster.Consul
open Proto.FSharp
open System.Diagnostics
open Proto
open Proto.Remote
open Messages
open Proto.Remote
open Messages
open Proto.FSharp

let startCluster() =
    Cluster.Start("MyCluster", "127.0.0.1", 12001, ConsulProvider(ConsulProviderOptions()))

let doStuff() =
    Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor)
    let handler (mailbox:Actor<obj>) (msg:obj) =
        printfn "Some one called: %A" (msg.GetType())
        match msg with
        | :? HelloRequest ->
            printfn "Someone is saying hello %A" (mailbox.Sender())
            let response = HelloResponse()
            response.Message <- "Hello from node2"
            mailbox.CurrentContext().Respond(response)
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
