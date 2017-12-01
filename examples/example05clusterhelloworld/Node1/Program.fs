open System
open Proto.Cluster
open Proto.Cluster.Consul
open Proto.FSharp
open System.Diagnostics
open Proto
open Proto.Remote
open Messages
open Messages

let startConsul() =
    printfn "Starting consul"
    let psi = ProcessStartInfo("consul", "agent -server -bootstrap -data-dir /tmp/consul -bind=127.0.0.1 -ui")
    psi.CreateNoWindow <- true
    let p = Process.Start(psi)
    printfn "Consul started"
    p

let startCluster() =
    Cluster.Start("MyCluster", "127.0.0.1", 12000, ConsulProvider(ConsulProviderOptions()))

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

[<EntryPoint>]
let main argv =
    let p = startConsul()
    try
        startCluster()
        doStuff()
        Console.ReadLine() |> ignore
    with
    | x -> ()
//    p.Kill()
    0 // return an integer exit code
