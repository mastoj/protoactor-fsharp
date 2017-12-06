open System
open Proto
open Proto.Remote
open Proto.Cluster
open Proto.Cluster.Consul
open Proto.FSharp
open Messages

let startCluster() =
    let consulConfigure = Action<Consul.ConsulClientConfiguration>(fun c -> c.Address <- Uri("http://consul:8500/"))
    printfn "Connecting to cluster"
    Cluster.Start("MyCluster", "node2", 12000, ConsulProvider(ConsulProviderOptions(), consulConfigure))
    printfn "Connected to cluster"

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
        | x -> printfn "Seems like we are getting something: %A" x
    
    let props = Actor.create2 handler |> Actor.initProps
    Remote.RegisterKnownKind("HelloKind", props)

[<EntryPoint>]
let main argv =
    printfn "Starting"
    System.Threading.Thread.Sleep(5000)
    doStuff()
    try
        startCluster()
    with
    | x -> printfn "Something went wrong: %A" x
    printfn "Are we getting this far"
    Console.ReadLine() |> printfn "This is the line: |%s|"
    System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
    printfn "Did we read a line?"
    0 // return an integer exit code
