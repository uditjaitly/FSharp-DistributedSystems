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
let rnd=System.Random()
type Gossip_full = Gossip_full of a : int
let rand=System.Random()
let mutable k = 0
let topology="full"
let algo = "gossip"

let Actor i j (mailbox: Actor<_>) =
    let mutable counter=0
    let mutable actorRef = select "akka://MySystem/user/" system
    (*let neighbors = Array.create 10
    Array.set neighbors -1
    match topology with
        | "full"->
            for x = 1 to numOfNodes do
                if x!=i then
                    Array.set neighbors x
        | "line"->
            if i=1 then
                Array.set neighbors (i+1)
            else if i= numOfNodes then
                Array.set neighbors (i-1)
            else
                Array.set neighbors (i-1)
                Array.set neighbors (i+1)

    *)


    let rec loop n = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        let mutable randomNum=(rand.Next()%10)
        //printfn "random num=%i" randomNum
        while randomNum = 0 || randomNum =i do
            randomNum<-(rand.Next()%11)
        //printfn "%i Sending to %i" i randomNum
        let num_string= string randomNum
        let pathToActor="akka://MySystem/user/" + num_string
        printfn "%s" pathToActor
        actorRef <- select pathToActor system
        actorRef<! 1
        match message with
        | 1 ->
            counter<-counter+1
            if counter = 10 then
                printf "Counter is 10 for %i" i
            k<-k+1
            if counter < 10 then
                return! loop ()
            
        | _ ->
            printfn "HERE"

        //sender <! 1
    }
    loop ()











let Master i j (mailbox: Actor<_>) =
    let mutable actorRef =
        Actor -1 algo
        |>spawn system "Actor"


    match topology with 
    |"full"->
        for i = 1 to 10 do
            let actorName=string i
            match algo with
            |"gossip"->
                actorRef <-
                    Actor i algo
                    |> spawn system actorName
                printfn "%i Actor Created" i



            |"push sum"->
                actorRef <-
                    Actor i algo
                    |> spawn system actorName
                ()
                printfn "%i Actor Created" i

        actorRef<! 1





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


let boss = 
    Master -1 algo
    |> spawn system "master"




printf "value of k=%i" k