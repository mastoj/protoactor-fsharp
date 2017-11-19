open System
open Proto
open System.Threading.Tasks
open Proto.FSharp

type Message = {Text: string}
type Message2 = 
    | Text of string

let test1() = 
    let handleMessage (msg: obj) = 
        match msg with
        | :? Message2 as m ->
            match m with
            | Text t -> printfn "This could work: %s" t

    let pid = 
        simpleProducer handleMessage
        |> fromProducer
        |> spawn

    [1 .. 1000]
    |> List.map (fun i -> Text (sprintf "Tomas %d" i))
    |> List.iter (tell pid)

    //Proto.createActor handleMessage // Actor.FromProducer(fun () -> (new MyActor() :> IActor))
    // let pid = Actor.Spawn(props)
    // pid.Tell({Text = "Tomas"})
    // pid.Tell(Text "Tomas")

module ProtoFSharp = 
    type IO<'T> =
        | Input

    type Actor<'Message> = 
        abstract Receive : unit -> IO<'Message>


    type Cont<'In, 'Out> = 
        | Func of ('In -> Cont<'In, 'Out>)
        | Return of 'Out

    /// The builder for actor computation expression.
    type ProtoBuilder() = 
    
        /// Binds the next message.
        member __.Bind(m : IO<'In>, f : 'In -> _) = Func(fun m -> f m)
    
        /// Binds the result of another actor computation expression.
        member this.Bind(x : Cont<'In, 'Out1>, f : 'Out1 -> Cont<'In, 'Out2>) : Cont<'In, 'Out2> = 
            match x with
            | Func fx -> Func(fun m -> this.Bind(fx m, f))
            | Return v -> f v
    
        member __.ReturnFrom(x) = x
        member __.Return x = Return x
        member __.Zero() = Return()
    
        member this.TryWith(f : unit -> Cont<'In, 'Out>, c : exn -> Cont<'In, 'Out>) : Cont<'In, 'Out> = 
            try 
                true, f()
            with ex -> false, c ex
            |> function 
            | true, Func fn -> Func(fun m -> this.TryWith((fun () -> fn m), c))
            | _, v -> v
    
        member this.TryFinally(f : unit -> Cont<'In, 'Out>, fnl : unit -> unit) : Cont<'In, 'Out> = 
            try 
                match f() with
                | Func fn -> Func(fun m -> this.TryFinally((fun () -> fn m), fnl))
                | r -> 
                    fnl()
                    r
            with ex -> 
                fnl()
                reraise()
    
        member this.Using(d : #IDisposable, f : _ -> Cont<'In, 'Out>) : Cont<'In, 'Out> = 
            this.TryFinally((fun () -> f d), 
                            fun () -> 
                                if d <> null then d.Dispose())
    
        member this.While(condition : unit -> bool, f : unit -> Cont<'In, unit>) : Cont<'In, unit> = 
            if condition() then 
                match f() with
                | Func fn -> 
                    Func(fun m -> 
                        fn m |> ignore
                        this.While(condition, f))
                | v -> this.While(condition, f)
            else Return()
    
        member __.For(source : 'Iter seq, f : 'Iter -> Cont<'In, unit>) : Cont<'In, unit> = 
            use e = source.GetEnumerator()
        
            let rec loop() = 
                if e.MoveNext() then 
                    match f e.Current with
                    | Func fn -> 
                        Func(fun m -> 
                            fn m |> ignore
                            loop())
                    | r -> loop()
                else Return()
            loop()
    
        member __.Delay(f : unit -> Cont<_, _>) = f
        member __.Run(f : unit -> Cont<_, _>) = f()
        member __.Run(f : Cont<_, _>) = f
    
        member this.Combine(f : unit -> Cont<'In, _>, g : unit -> Cont<'In, 'Out>) : Cont<'In, 'Out> = 
            match f() with
            | Func fx -> Func(fun m -> this.Combine((fun () -> fx m), g))
            | Return _ -> g()
    
        member this.Combine(f : Cont<'In, _>, g : unit -> Cont<'In, 'Out>) : Cont<'In, 'Out> = 
            match f with
            | Func fx -> Func(fun m -> this.Combine(fx m, g))
            | Return _ -> g()
    
        member this.Combine(f : unit -> Cont<'In, _>, g : Cont<'In, 'Out>) : Cont<'In, 'Out> = 
            match f() with
            | Func fx -> Func(fun m -> this.Combine((fun () -> fx m), g))
            | Return _ -> g
    
        member this.Combine(f : Cont<'In, _>, g : Cont<'In, 'Out>) : Cont<'In, 'Out> = 
            match f with
            | Func fx -> Func(fun m -> this.Combine(fx m, g))
            | Return _ -> g

    let proto = ProtoBuilder()

open ProtoFSharp

module Spawn =
    open Proto
    let spawn (createActor) =
        let inline startAsPlainTask (work : Async<unit>) = Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)


        let producer () =
    // public interface IActor
    // {
    //     Task ReceiveAsync(IContext context);
    // }
            let mutable state = createActor { new Actor<'T1> with member this.Receive() = Input }
            { new IActor with
                member this.ReceiveAsync(context: IContext) =
                    async {
                        match state with
                        | Func f ->
                            match context.Message with
                            | :? 'T1 as msg -> 
                                state <- f msg
                            | _ -> ()
                    } |> startAsPlainTask
            }

        let producerFunc = System.Func<_>(producer)
        let props = Actor.FromProducer(producerFunc)
        Actor.spawn props


let test2() =
    let proton = (fun (mailbox: Actor<string>) ->
            let rec loop state = proto {
                let! message = mailbox.Receive()
                printfn "Received: %A" message
                // handle an incoming message
                let state' = state + "." + message
                printfn "State: %A" state'
                return! loop state'
            }
            loop "")

    // use system = System.create "my-system" (Configuration.load())
    let pid = Spawn.spawn proton // spawn system "my-actor" proton
    pid <! "proto"
    pid <! "actor"
    ()


[<EntryPoint>]
let main argv =
    test2()


    System.Console.ReadLine() |> ignore
    0
