module Cluster

open Proto.Cluster
open Proto.Cluster.Consul
open System

let startCluster nodeName =
    let consulConfigure = Action<Consul.ConsulClientConfiguration>(fun c -> c.Address <- Uri("http://consul:8500/"))
    printfn "Connecting to cluster"
    Cluster.Start("FiresideChatCluster", nodeName, 12000, ConsulProvider(ConsulProviderOptions(), consulConfigure))
    printfn "Connected to cluster"

