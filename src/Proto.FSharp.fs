module Proto.FSharp

open Proto

module System = 
    open System

    let toFunc<'a> f = Func<'a>(f)
    let toFunc2<'a, 'b> f = Func<'a, 'b>(f)

[<AutoOpen>]
module Props = 
    open System

    let newProps() = Props()

    let withProducer producer (props: Props) = 
        props.WithProducer(producer)

    let withDispatcher dispatcher (props: Props) = 
        props.WithDispatcher(dispatcher)

    let withMailbox mailbox (props: Props) = 
        props.WithMailbox(mailbox)

    let withChildSupervisorStrategy supervisorStrategy (props: Props) =
        props.WithChildSupervisorStrategy(supervisorStrategy)

    let withReceiveMiddleware (middleware: Receive -> Receive) (props: Props) =
        props.WithReceiveMiddleware([|toFunc2(middleware)|])

    let withReceiveMiddlewares (middlewares: (Receive -> Receive) list) (props: Props) =
        middlewares 
        |> List.map toFunc2
        |> Array.ofList
        |> (fun arr -> props.WithReceiveMiddleware(arr))

    let withSenderMiddleware (middleware: Sender -> Sender) (props: Props) =
        props.WithSenderMiddleware([|toFunc2(middleware)|])

    let withSenderMiddlewares (middlewares: (Sender -> Sender) list) (props: Props) =
        middlewares 
        |> List.map toFunc2
        |> Array.ofList
        |> (fun arr -> props.WithSenderMiddleware(arr))

    let withSpawner spawner (props: Props) = 
        props.WithSpawner(spawner)

[<AutoOpen>]
module Actor =
    open System

    let spawn (props: Props) = Actor.Spawn(props)

    let spawnPrefix prefix (props: Props) = Actor.SpawnPrefix(props, prefix)

    let spawnNamed name (props: Props) = Actor.SpawnNamed(props, name)

    let simpleProducer (f: obj -> unit) =
        fun () ->
            { new IActor with 
                member this.ReceiveAsync(context: IContext) = 
                    let msg = context.Message
                    match msg with
                    | :? Proto.Started
                    | :? Proto.Restarting 
                    | :? Proto.Stopping -> 
                        printfn "Internal message: %A" msg
                    | x -> f x
                    Actor.Done }

    let fromProducer producer = Actor.FromProducer(toFunc(producer))

    let fromFunc receive =
        Receive(receive)
        |> Actor.FromFunc

[<AutoOpen>]
module Pid = 
    let tell (pid: PID) msg = 
        pid.Tell(msg)
    
    let (<!) (pid: PID) msg = tell pid msg