// Learn more about F# at http://fsharp.org
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics
let system = System.create "MySystem" (Configuration.defaultConfig())
let input=System.Environment.GetCommandLineArgs()
let numOfNodes=10

type Gossip_full = Gossip_full of a : int
let rand=System.Random()
let topology="full"
let algo="gossip"
let mutable k = 0
let test = 

let Actor id algoType (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        printf "Here"
        match message with 
        | (a,b) ->
            match id with
            |1->
               


        sender <! 1
    }
    loop()










let Master(mailbox: Actor<_>) =

    for i=1 to 10 do
        let actorName="Actor%i" + string i
        
        let actorRef=
            Actor i 0
            |> spawn system actorName
        printfn "%i Actor Created" i
        k<-k+1

   (*
    let mutable actorRef =
        Actor -1 0
        |>spawn system "Actor"


    match topology with 
    |"full"->
        for i = 1 to 10 do
         
            k<-k+1
            match algo with
            |"gossip"->
                actorRef <-
                    Actor i 0
                    |> spawn system actorName
                printfn "%i Actor Created" i
                k<-k+1

            |"push sum"->
                actorRef <-
                    Actor i 1
                    |> spawn system actorName
                ()
                printfn "%i Actor Created" i

*)


    let rec listen() =
        actor {
            let! message = mailbox.Receive()
            match message with
            | 1 -> 
                //inc<-inc+1 

              
            return! listen()
        } 
    listen()
()



let boss = spawn system "master" Master


while k<1 do
    printf "value of k=%i" k