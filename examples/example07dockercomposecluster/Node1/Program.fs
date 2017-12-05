open System
open Proto.Cluster
open Proto.Cluster.Consul
open Proto.FSharp
open System.Diagnostics
open Proto
open Proto.Remote
open Messages
open Messages
open System

let startCluster() =
    let consulConfigure = Action<Consul.ConsulClientConfiguration>(fun c -> c.Address <- Uri("http://consul:8500"))
    printfn "Connecting to cluster"
    Cluster.Start("MyCluster", "node1", 12001, ConsulProvider(ConsulProviderOptions(), consulConfigure))
    printfn "Connected to cluster"

let doStuff() = 
    let rec getPid() = 
        printfn "==> Getting PID"
        let (pid, sc) = Cluster.GetAsync("MyCluster", "HelloKind").Result.ToTuple()
        if sc <> ResponseStatusCode.OK then getPid()
        else pid
    let pid = getPid()
    printfn "==> Getting data: %A" pid
    let res = HelloRequest() >? pid |> Async.RunSynchronously
    printfn "==> Result: %A" res

let retry f maxRetries sleepTime =
    let rec inner retryCount =
        if retryCount > maxRetries then ()
        else
            try
                f()
            with
            | x -> 
                printfn "===> Something went wrong: %A" x
                System.Threading.Thread.Sleep(sleepTime * 1000)
                inner (retryCount+1)
    inner 1

[<EntryPoint>]
let main argv =
    printfn "===> Starting"
    System.Threading.Thread.Sleep(5000)
    Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor)
    let start() =
        startCluster()
        doStuff()
        Console.ReadLine() |> ignore
    retry start 3 3
    // with
    // | x -> printfn "Something went wrong: %A" x
    0 // return an integer exit code
