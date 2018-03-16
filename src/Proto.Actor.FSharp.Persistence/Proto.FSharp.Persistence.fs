namespace Proto.FSharp

open Proto
open System.Threading.Tasks
open System

namespace Avalanchain.Core

open Proto.FSharp

[<RequireQualifiedAccess>]
module Persistence = 
    open Proto
    open Proto.Persistence
    open Proto.Persistence.SnapshotStrategies

    let private applyEvent recoverEvent replayEvent persistedEvent state (evt: Event) =
        match evt with
        | :? RecoverEvent as e -> 
            match e.Data with 
            | :? 'Event as event -> recoverEvent state evt.Index event 
            | data -> failwithf "Unsupported recover event type: '%A'" (data.GetType())
        | :? ReplayEvent as e -> 
            match e.Data with 
            | :? 'Event as event -> replayEvent state evt.Index event 
            | data -> failwithf "Unsupported replay event type: '%A'" (data.GetType())
        | :? PersistedEvent as e -> 
            match e.Data with 
            | :? 'Event as event -> persistedEvent state evt.Index event 
            | data -> failwithf "Unsupported persisted event type: '%A'" (data.GetType())
        | e -> failwithf "Unhandled event: '%A'" e

    let private applySnapshot recoverSnapshot persistedSnapshot updateState (snapshot: Snapshot) =
        match snapshot with
        | :? RecoverSnapshot as rs ->
            match recoverSnapshot with 
            | Some recS -> match rs.State with
                            | :? 'State as st -> recS snapshot.Index st |> updateState
                            | _ -> failwithf "Unsupported snapshot type: '%A'" (snapshot.State.GetType())
            | None -> ()
        | :? PersistedSnapshot as ps -> 
            match persistedSnapshot with 
            | Some perS -> match ps.State with
                            | :? 'State as st -> perS snapshot.Index st
                            | _ -> failwithf "Unsupported snapshot type: '%A'" (snapshot.State.GetType())
            | None -> ()
        | _ -> printfn "Unsupported snapshot type: '%A'" (snapshot.State.GetType())

    let private systemHandler (persistence: Persistence) (_: IContext) sm: Async<unit> = 
        match sm with 
        | Started _ -> async { do! persistence.RecoverStateAsync() |> Async.AwaitTask }
        | _ -> async {()}

    let private handler (persistence: Persistence) processCommand state (ctx: IContext) (cmd: 'Command): Async<unit> = 
        let res = processCommand state persistence.Index cmd
        match res with
        | Ok evt -> async { do! persistence.PersistEventAsync evt |> Async.AwaitTask 
                            if isNull(ctx.Sender) |> not then ctx.Sender <! Ok() }
        | Error e -> async { e >! ctx.Sender }

    [<RequireQualifiedAccess>]
    module CommandSourcingAndSnapshotting = 
        let persistDetailed 
            (eventStore: IEventStore) 
            (snapshotStore: ISnapshotStore) 
            (processCommand: 'State -> int64 -> 'Command -> Result<'Event, 'CommandError>)
            (recoverEvent: 'State -> int64 -> 'Event -> 'State) 
            (replayEvent: 'State -> int64 -> 'Event -> 'State)
            (persistedEvent: 'State -> int64 -> 'Event -> 'State)
            (recoverSnapshot: (int64 -> 'State -> 'State) option) 
            (persistedSnapshot: (int64 -> 'State -> unit) option) 
            (snapshotStrategy: ISnapshotStrategy)
            (persistentID: string) 
            (initialState: 'State)
            = 
            let mutable state = initialState

            let persistence = 
                Persistence.WithEventSourcingAndSnapshotting(
                    eventStore, 
                    snapshotStore, 
                    persistentID,
                    System.Action<_>(fun evt -> state <- applyEvent recoverEvent replayEvent persistedEvent state evt), 
                    System.Action<_>(applySnapshot recoverSnapshot persistedSnapshot (fun snap -> state <- snap)),
                    snapshotStrategy, 
                    fun () -> state :> obj)

            Actor.create3Async (systemHandler persistence) (handler persistence processCommand state)

        let persist
            (provider: IProvider) 
            (processCommand: 'State -> int64 -> 'Command -> Result<'Event, 'CommandError>)
            (onEvent: 'State -> int64 -> 'Event -> 'State) 
            (log: string -> unit) 
            (snapshotStrategy: ISnapshotStrategy)
            (persistentID: string)
            (initialState: 'State)
            = 
            persistDetailed 
                provider 
                provider
                processCommand
                onEvent
                onEvent
                onEvent
                (Some (fun _ s -> log (sprintf "Snapshot recovered for '%s'" persistentID); s))
                (Some (fun _ _ -> log (sprintf "Snapshot persisted for '%s'" persistentID)))
                snapshotStrategy
                persistentID
                initialState 
            
        let persistLight
            (provider: IProvider) 
            (processCommand: 'State -> int64 -> 'Command -> Result<'Event, 'CommandError>)
            (onEvent: 'State -> int64 -> 'Event -> 'State) 
            (persistentID: string)
            (initialState: 'State)
            = 
            persist
                provider 
                processCommand
                onEvent
                ignore
                (IntervalStrategy 100)
                persistentID
                initialState             

    [<RequireQualifiedAccess>]
    module EventSourcingAndSnapshotting = 
        let persistDetailed
            (eventStore: IEventStore) 
            (snapshotStore: ISnapshotStore) 
            (recoverEvent: 'State -> int64 -> 'Event -> 'State) 
            (replayEvent: 'State -> int64 -> 'Event -> 'State)
            (persistedEvent: 'State -> int64 -> 'Event -> 'State)
            (recoverSnapshot: (int64 -> 'State -> 'State) option) 
            (persistedSnapshot: (int64 -> 'State -> unit) option) 
            (snapshotStrategy: ISnapshotStrategy)
            (persistentID: string)
            (initialState: 'State)
            = 
            CommandSourcingAndSnapshotting.persistDetailed 
                eventStore 
                snapshotStore
                (fun _ _ cmd -> Ok cmd)
                recoverEvent
                replayEvent
                persistedEvent
                recoverSnapshot
                persistedSnapshot
                snapshotStrategy
                persistentID
                initialState

        let persist
            (provider: IProvider) 
            (onEvent: 'State -> int64 -> 'Event -> 'State) 
            (log: string -> unit) 
            (snapshotStrategy: ISnapshotStrategy)
            (persistentID: string)
            (initialState: 'State)
            = 
            CommandSourcingAndSnapshotting.persist 
                provider 
                (fun _ _ cmd -> Ok cmd)
                onEvent
                log
                snapshotStrategy
                persistentID
                initialState

        let persistLight
            (provider: IProvider) 
            (onEvent: 'State -> int64 -> 'Event -> 'State) 
            (persistentID: string)
            (initialState: 'State)
            = 
            CommandSourcingAndSnapshotting.persistLight 
                provider 
                (fun _ _ cmd -> Ok cmd)
                onEvent
                persistentID
                initialState

    [<RequireQualifiedAccess>]
    module Snapshotting = 
        let persist 
            (snapshotStore: ISnapshotStore) 
            (recoverSnapshot: (int64 -> 'State -> 'State) option) 
            (persistedSnapshot: (int64 -> 'State -> unit) option) 
            (persistentID: string) 
            = 
            let persistence = 
                Persistence.WithSnapshotting(
                    snapshotStore, 
                    persistentID,
                    System.Action<_>(applySnapshot recoverSnapshot persistedSnapshot ignore))

            Actor.create3Async (systemHandler persistence) (handler persistence (fun _ _ e -> Ok e) None)

    [<RequireQualifiedAccess>]
    module CommandSourcing = 
        let persist 
            (eventStore: IEventStore) 
            (processCommand: 'State -> int64 -> 'Command -> Result<'Event, 'CommandError>)
            (persistedEvent: 'State -> int64 -> 'Event -> 'State)
            (persistentID: string) 
            (initialState: 'State)
            = 
            let mutable state = initialState

            let persistence = 
                Persistence.WithEventSourcing(
                    eventStore, 
                    persistentID,
                    System.Action<_>(fun evt -> state <- applyEvent (fun s _ _ -> s) (fun s _ _ -> s) persistedEvent state evt))

            Actor.create3Async (systemHandler persistence) (handler persistence processCommand state)           

    [<RequireQualifiedAccess>]
    module EventSourcing = 
        let persist
            (eventStore: IEventStore) 
            (persistedEvent: 'State -> int64 -> 'Event -> 'State)
            (persistentID: string)
            (initialState: 'State)
            = 
            CommandSourcing.persist 
                eventStore 
                (fun _ _ cmd -> Ok cmd)
                persistedEvent
                persistentID
                initialState

        let persistLight
            (eventStore: IEventStore) 
            (persistentID: string)
            = 
            persist 
                eventStore 
                (fun s _ _ -> s) 
                persistentID
                None                

    let getEvents<'T> (handler: 'T -> unit) (persistentID: string) (indexStart: int64) (indexEnd: int64) (eventStore: IEventStore) =
        eventStore.GetEventsAsync (persistentID, indexStart, indexEnd,
            System.Action<_>(fun o ->   if not (isNull o) then 
                                            match o with
                                            | o when isNull o -> ()
                                            | :? 'T as oo -> handler oo
                                            | _ -> failwithf "Unsupported event type: '%s'" (o.GetType().Name)
                                        else ())) |> Async.AwaitTask

