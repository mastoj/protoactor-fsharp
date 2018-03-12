Dev: [![CircleCI](https://circleci.com/gh/mastoj/protoactor-fsharp/tree/dev.svg?style=shield)](https://circleci.com/gh/mastoj/protoactor-fsharp)
Master: [![CircleCI](https://circleci.com/gh/mastoj/protoactor-fsharp.svg?style=shield)](https://circleci.com/gh/mastoj/protoactor-fsharp)

# FSharp wrapper on top of proto.actor

Functional languages usually fits quite well with the actor framework, so having
a good support for F# in proto.actor make sense.

Proto.actor: https://github.com/AsynkronIT/protoactor-dotnet/

## Getting started

Most of the examples below is from the `example01basicactors` example in the
`examples` folder. There are also some other examples in the `examples` folder
if you need it.

### Simple actor

After you have added `Proto.FSharp` from nuget it is super easy to get started,
so let us start our first actor:

    let pid = Actor.create (printfn "Hello from actor: %A") |> Actor.initProps |> Actor.spawn
    "Hello world" >! pid

The first line spawns and actor and returns the `pid`, on the next we send a
message to `pid`.

### Responding actor

If you want to reply back to the sender you need to use another helper to create
the actor:

    let handler (context: Proto.IContext) (msg: obj) =
        match msg with
        | :? String as s -> sprintf "You said: %s" s >! context.Sender
        | x -> printfn "Unhandled message: %A" x

    let pid = Actor.create2 handler |> Actor.initProps |> Actor.spawn
    "Hello world" >? pid |> Async.RunSynchronously |> printfn "Response: %A"

`Actor.create2` expects a function that takes `IContext` in addition to the
message as input. When you have the `IContext` you can just send a message back
to `IContext.Sender`, or use `IContext.Repond(...)`.

### Simple actor with state

There is also a simple helper that you can use if you want to create an actor
that has state.

    let handler msg state =
        let state' = sprintf "%s %s" (state.ToString()) (msg.ToString())
        printfn "Current state: %A" state'
        state'

    let pid = Actor.withState handler "" |> Actor.initProps |> Actor.spawn
    "Hello world" >! pid
    "Hello world again" >! pid

`Actor.withState` creates an actor with an initial state that you pass in after
the handler function. The returning state from that function will be passed as
argument to the next message handling.

### Responding actor with state

This example combines all of the above to one slightly more complex example.

    let handler (context: Proto.IContext) (msg: obj) state =
        let state' =
            match msg with
            | :? String as s ->
                let state' = sprintf "%s %s" (state.ToString()) (msg.ToString())
                sprintf "Current state: %A" state' >! context.Sender
                state'
            | x -> state
        state'
    let pid = Actor.withState2 handler "" |> Actor.initProps |> Actor.spawn
    "Hello world" >? pid |> Async.RunSynchronously |> printfn "Response: %A"
    "Hello world again" >? pid |> Async.RunSynchronously |> printfn "Response: %A"

Here we use the `Actor.withState2` to both have state, and also have access to
the `IContext` so we can send a message back to the sender.

### Taking more control

If you need to take even more control and the helpers `Actor.create`,
`Actor.create2`, `Actor.withState` and `Actor.withState2` isn't enough you can
use the `proto { ... }` computation expression do control how you receive a
message. The following example is the same as above, but here we use `proto`
instead.

    let handler (actor: Actor<obj>) =
        let rec loop state =
            proto {
                let! (ctx, msg) = actor.Receive()
                let state' =
                    match msg with
                    | :? String as s ->
                        let state' = sprintf "%s %s" (state.ToString()) (msg.ToString())
                        sprintf "Current state: %A" state' >! ctx.Sender
                        state'
                    | x -> state
                return! loop state'
            }
        loop ""

    let pid = handler |> Actor.initProps |> Actor.spawn
    "Hello world" >? pid |> Async.RunSynchronously |> printfn "Response (comp): %A"
    "Hello world again" >? pid |> Async.RunSynchronously |> printfn "Response (comp): %A"

It is not much more complicated, but by using the `proto` computation expression
we can now wait for messages with `actor.Receive`. This approach also makes it
easier to create some initial state or do other setup steps in the actor.

## Cluster

No helpers has been implemented around the cluster at the moment, since it is
easy to use the regular .NET API works fine at the moment.
