namespace Proto.FSharp

open Proto
open System.Threading.Tasks
open System

module Async = 
    let inline startAsPlainTask (work : Async<unit>) = Async.StartAsTask work :> Task 

module System = 
    let inline toFunc<'a> f = Func<'a>(f)
    let inline toFunc2<'a, 'b> f = Func<'a, 'b>(f)

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

    type FSharp3AsyncActor<'Message, 'State>(handler: IContext -> obj -> 'State -> Async<'State>, initialState: 'State) = 
        let mutable state = initialState
        interface IActor with
            member this.ReceiveAsync(context: IContext) =
                async {
                    try
                        let! state' = handler context context.Message state 
                        state <- state'
                    with
                    | x -> 
                        printfn "Failed to execute actor: %A" x
                        raise x
                } |> Async.startAsPlainTask

    type FSharp3Actor<'Message, 'State>(handler: IContext -> obj -> 'State -> 'State, initialState: 'State) = 
        let mutable state = initialState
        interface IActor with
            member this.ReceiveAsync(context: IContext) =
                async {
                    try
                        let state' = handler context context.Message state  
                        state <- state'
                    with
                    | x -> 
                        printfn "Failed to execute actor: %A" x
                        raise x
                } |> Async.startAsPlainTask

[<RequireQualifiedAccess>]
module Actor =
    let inline private composeAsyncHandlers (systemMessageHandler: IContext -> SystemMessage -> 'State -> Async<'State>) (handler: IContext -> 'Message -> 'State -> Async<'State>) =
        fun context (msg: obj) state ->  match msg with
                                            | IsSystemMessage msg -> systemMessageHandler context msg state 
                                            | :? 'Message as msg -> handler context msg state
                                            | _ -> async { return state }

    let inline private composeHandlers (systemMessageHandler: IContext -> SystemMessage -> 'State -> 'State) (handler: IContext -> 'Message -> 'State -> 'State) =
        fun context (msg: obj) state ->  match msg with
                                            | IsSystemMessage msg -> systemMessageHandler context msg state 
                                            | :? 'Message as msg -> handler context msg state
                                            | _ -> state 

    let inline spawn (props: Props) = Actor.Spawn(props)

    let inline spawnPrefix prefix (props: Props) = Actor.SpawnPrefix(props, prefix)

    let inline spawnNamed name (props: Props) = Actor.SpawnNamed(props, name)

    let inline initProps (producer: unit -> IActor) = Actor.FromProducer(System.Func<_>(producer))

    let inline spawnProps p = p |> initProps |> spawn

    let inline spawnPropsPrefix prefix = initProps >> spawnPrefix prefix

    let inline spawnPropsNamed name = initProps >> spawnNamed name


    let inline withState3Async (systemMessageHandler: IContext -> SystemMessage -> 'State -> Async<'State>) (handler: IContext -> 'Message -> 'State -> Async<'State>) (initialState: 'State) =
        fun () -> new FSharp3AsyncActor<'Message, 'State>((composeAsyncHandlers systemMessageHandler handler), initialState) :> IActor

    let inline withObjState2Async (handler: IContext -> obj -> 'State -> Async<'State>) (initialState: 'State) = // TODO: decide on the name and create corresponding create()s
        withState3Async handler handler initialState
    
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

    let inline withState3 (systemMessageHandler: IContext -> SystemMessage -> 'State -> 'State) (handler: IContext -> 'Message -> 'State -> 'State) (initialState: 'State) =
        fun () -> new FSharp3Actor<'Message, 'State>((composeHandlers systemMessageHandler handler), initialState) :> IActor

    let inline withState2 (handler: IContext -> 'Message -> 'State -> 'State) (initialState: 'State) =
        withState3 (fun _ _ s -> s) handler  initialState

    let inline withObjState2 (handler: IContext -> obj -> 'State -> 'State) (initialState: 'State) = // TODO: decide on the name and create corresponding create()s
        withState3 handler handler initialState

    let inline withState (handler: 'Message -> 'State -> 'State) (initialState: 'State) =
        withState2 (fun _ m s -> handler m s) initialState

    let inline create3 (systemMessageHandler: IContext -> SystemMessage -> unit) (handler: IContext -> 'Message -> unit) =
        withState3 (fun context message _ -> systemMessageHandler context message) (fun context message _ -> handler context message) ()

    let inline create2 (handler: IContext -> 'Message -> unit) =
        withState2 (fun context message _ -> handler context message) ()

    let inline create (handler: 'Message -> unit) =
        withState2 (fun _ m _ -> handler m) ()


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

