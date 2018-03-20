namespace Proto.FSharp

open Proto
open System.Threading.Tasks
open System

module Async = 
    let inline startAsPlainTask (work : Async<unit>) = Async.StartAsTask work :> Task 

module System = 
    let toFunc<'a> f = Func<'a>(f)
    let toFunc2<'a, 'b> f = Func<'a, 'b>(f)

[<AutoOpen>]
module Core =
    type SystemMessage =
        | AutoReceiveMessage of AutoReceiveMessage
        | Terminated of Terminated
        | Restarting of Restarting
        | Failure of Failure
        | Watch of Watch
        | Unwatch of Unwatch
        | Restart of Restart
        | Stop of Stop
        | Started of Started
        | ReceiveTimeout of ReceiveTimeout
        | Continuation of Continuation

    let (|IsSystemMessage|_|) (msg:obj) = 
        match msg with
        | :? AutoReceiveMessage as m -> Some(AutoReceiveMessage m)
        | :? Terminated as m -> Some(Terminated m)
        | :? Restarting as m -> Some(Restarting m)
        | :? Failure as m -> Some(Failure m)
        | :? Watch as m -> Some(Watch m)
        | :? Unwatch as m -> Some(Unwatch m)
        | :? Restart as m -> Some(Restart m)
        | :? Stop as m -> Some(Stop m)
        | :? Started as m -> Some(Started m)
        | :? ReceiveTimeout as m -> Some(ReceiveTimeout m)
        | :? Continuation as m -> Some(Continuation m)
        | _ -> None

    type Decider = PID -> Exception -> SupervisorDirective

    type SupervisionStrategy =
        | DefaultStrategy
        | AllForOneStrategy of decider:Decider * maxNrOfRetries:int * withinTimeSpan:TimeSpan option
        | OneForOneStrategy of decider:Decider * maxNrOfRetries:int * withinTimeSpan:TimeSpan option
        | ExponentialBackoffStrategy of backoffWindow:TimeSpan * initialBackoff:TimeSpan

    type FSharpActor<'Message, 'State>(systemMessageHandler: IContext -> SystemMessage -> 'State -> Async<'State>, handler: IContext -> 'Message -> 'State -> Async<'State>, initialState: 'State) = 
        let mutable state = initialState
        interface IActor with
            member this.ReceiveAsync(context: IContext) =
                async {
                    match context.Message with
                    | IsSystemMessage msg ->
                        try
                            let! state' = systemMessageHandler context msg state 
                            state <- state'
                        with
                        | x -> 
                            printfn "Failed to execute actor: %A" x
                            raise x
                    | :? 'Message as msg ->
                        try
                            let! state' = handler context msg state 
                            state <- state'
                        with
                        | x -> 
                            printfn "Failed to execute actor: %A" x
                            raise x
                    | _ -> ()                
                } |> Async.startAsPlainTask


[<RequireQualifiedAccess>]
module Actor =
    let inline spawn (props: Props) = Actor.Spawn(props)

    let inline spawnPrefix prefix (props: Props) = Actor.SpawnPrefix(props, prefix)

    let inline spawnNamed name (props: Props) = Actor.SpawnNamed(props, name)

    let inline initProps (producer: unit -> IActor) =
        let producerFunc = System.Func<_>(producer)
        Actor.FromProducer(producerFunc)

    let inline spawnProps p = p |> initProps |> spawn

    let inline spawnPropsPrefix prefix = initProps >> spawnPrefix prefix

    let inline spawnPropsNamed name = initProps >> spawnNamed name


    let inline withState3Async (systemMessageHandler: IContext -> SystemMessage -> 'State -> Async<'State>) (handler: IContext -> 'Message -> 'State -> Async<'State>) (initialState: 'State) =
        fun () -> new FSharpActor<'Message, 'State>(systemMessageHandler, handler, initialState) :> IActor

    let inline withState2Async (handler: IContext -> 'Message -> 'State -> Async<'State>) (initialState: 'State) =
        withState3Async (fun _ _ s -> async { return s }) handler  initialState

    let inline withStateAsync (handler: 'Message -> 'State -> Async<'State>) (initialState: 'State) =
        withState2Async (fun _ m s -> handler m s) initialState

    let inline create3Async (systemMessageHandler: IContext -> SystemMessage -> Async<unit>) (handler: IContext -> 'Message -> Async<unit>) =
        withState3Async (fun context message _ -> systemMessageHandler context message) (fun context message _ -> handler context message) ()

    let inline create2Async (handler: IContext -> 'Message -> Async<unit>) =
        withState2Async (fun context message _ -> handler context message) ()

    let inline createAsync (handler: 'Message -> Async<unit>) =
        withState2Async (fun _ m _ -> handler m) ()

    let inline withState2 (handler: IContext -> 'Message -> 'State -> 'State) (initialState: 'State) = 
        withState2Async (fun ctx msg state -> async { return handler ctx msg state }) initialState 

    let inline withState (handler: 'Message -> 'State -> 'State) (initialState: 'State) =
        withStateAsync (fun msg state -> async { return handler msg state }) initialState 

    let inline create (handler: 'Message -> unit) = createAsync (fun msg -> async { return handler msg })  

    let inline create2 (handler: IContext -> 'Message -> unit) = create2Async (fun ctx msg -> async { return handler ctx msg })


[<RequireQualifiedAccess>]
module Props = 
    open System

    let inline newProps() = Props()

    let inline withProducer producer (props: Props) = 
        props.WithProducer(producer)

    let inline withDispatcher dispatcher (props: Props) = 
        props.WithDispatcher(dispatcher)

    let inline withMailbox mailbox (props: Props) = 
        props.WithMailbox(mailbox)

    let inline withChildSupervisorStrategy supervisorStrategy (props: Props) =
        let strategy =
            match supervisorStrategy with
            | DefaultStrategy -> Supervision.DefaultStrategy
            | OneForOneStrategy (decider, maxRetries, withinTimeSpan) ->
                let withinTimeSpanNullable =
                    match withinTimeSpan with
                    | None -> Nullable<TimeSpan>()
                    | Some timeSpan -> Nullable<TimeSpan>(timeSpan)
                Proto.OneForOneStrategy(Proto.Decider(decider), maxRetries, withinTimeSpanNullable) :> ISupervisorStrategy
            | AllForOneStrategy (decider, maxRetries, withinTimeSpan) -> 
                let withinTimeSpanNullable =
                    match withinTimeSpan with
                    | None -> Nullable<TimeSpan>()
                    | Some timeSpan -> Nullable<TimeSpan>(timeSpan)
                Proto.AllForOneStrategy(Proto.Decider(decider), maxRetries, withinTimeSpanNullable) :> ISupervisorStrategy
            | ExponentialBackoffStrategy (backoffWindow, initialBackoff) ->
                Proto.ExponentialBackoffStrategy(backoffWindow, initialBackoff) :> ISupervisorStrategy
        props.WithChildSupervisorStrategy(strategy)

    let inline withReceiveMiddleware (middleware: Receive -> Receive) (props: Props) =
        props.WithReceiveMiddleware([|toFunc2(middleware)|])

    let inline withReceiveMiddlewares (middlewares: (Receive -> Receive) list) (props: Props) =
        middlewares 
        |> List.map toFunc2
        |> Array.ofList
        |> (fun arr -> props.WithReceiveMiddleware(arr))

    let inline withSenderMiddleware (middleware: Sender -> Sender) (props: Props) =
        props.WithSenderMiddleware([|toFunc2(middleware)|])

    let inline withSenderMiddlewares (middlewares: (Sender -> Sender) list) (props: Props) =
        middlewares 
        |> List.map toFunc2
        |> Array.ofList
        |> (fun arr -> props.WithSenderMiddleware(arr))

    let inline withSpawner spawner (props: Props) = 
        props.WithSpawner(spawner)

[<AutoOpen>]
module Pid = 
    let inline tell (pid: PID) msg = 
        pid.Tell(msg)

    let inline ask (pid: PID) msg = 
        pid.RequestAsync(msg) |> Async.AwaitTask

    let inline (<!) (pid: PID) msg = tell pid msg
    let inline (>!) msg (pid: PID) = tell pid msg
    let inline (<?) (pid: PID) msg = ask pid msg
    let inline (>?) msg (pid: PID) = ask pid msg

