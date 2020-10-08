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
let rand=System.Random()
let mutable k = 0
let topology="2d grid"
let algo = "gossip"
let mutable actorRef = select "akka://MySystem/user/" system


let pickNeighbor (neighbors:_ list, numberOfNeighbors: int, i : int)  =
    let mutable index=(rand.Next()%numberOfNeighbors)
    let mutable randomNum=neighbors.[index]
    //printfn "random num=%i" randomNum
    while index = 0 || randomNum =i || randomNum=0 do
        index<-(rand.Next()%numberOfNeighbors)
        randomNum<-neighbors.[index]


    //randomNum<-neighbors.[randomNum]
    //printfn "%i Sending to %i" i randomNum
    let num_string= string randomNum
    let pathToActor="akka://MySystem/user/" + num_string
    //printfn "%s" pathToActor
    actorRef <- select pathToActor system
    actorRef<! 1
()



let Actor i j (mailbox: Actor<_>) =
    let mutable counter=0
    let mutable proceed=true
    let mutable neighbors = []
    let mutable numberOfNeighbors=0
    neighbors <- [-1] |> List.append neighbors
    //printfn "i=%i" i
    match topology with
        | "full"->
            for x = 1 to numOfNodes do
                if x <> i then
                    neighbors <- [x] |> List.append neighbors
        | "line"->
            if i=1 then
                neighbors <- [i+1] |> List.append neighbors
            else if i= numOfNodes then
                neighbors <- [i-1] |> List.append neighbors

            else
                neighbors <- [i-1] |> List.append neighbors
                neighbors <- [i+1] |> List.append neighbors

        | "2d grid"->
            if (i+numOfNodes) <= (numOfNodes*numOfNodes) then
                neighbors <- [i+numOfNodes] |> List.append neighbors
            if (i-numOfNodes) > 0 then
                neighbors <- [i-numOfNodes] |> List.append neighbors
            if ((i-1) > 0 && (i-1)%numOfNodes<>0) then
                neighbors <- [i-1] |> List.append neighbors
            if ((i+1) <= (numOfNodes*numOfNodes) && (((i+1)%numOfNodes)<>1))  then
                neighbors <- [i+1] |> List.append neighbors


        | _ -> ()
    



    let numberOfNeighbors= neighbors.Length
    
    
    
    



    let rec loop n = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | 1 ->
            counter<-counter+1
            if counter = 10 then
                proceed<-false
                printfn "Counter is 10 for %i" i
                k<-k+1
            if counter < 10 then  
                async {
                    while proceed do
                        do! Async.Sleep 100
                        pickNeighbor (neighbors,numberOfNeighbors,i)
                } |> Async.StartImmediate 

                return! loop ()

        | _ -> 
            printfn "HERE"
        
    }
    loop ()













let Master i j (mailbox: Actor<_>) =
    let mutable actorRef =
        Actor -1 algo
        |>spawn system "Actor"
    let mutable actorID = 0

    match topology with 
    |"full"->
        for i = 1 to numOfNodes do
            let actorName=string i
            match algo with
            |"gossip"->
                actorRef <-
                    Actor i algo
                    |> spawn system actorName
                //printfn "%i Actor Created" i



            |"push sum"->
                actorRef <-
                    Actor i algo
                    |> spawn system actorName
                ()
                //printfn "%i Actor Created" i
    |"line"->
        for i = 1 to numOfNodes do
            let actorName=string i
            match algo with
            |"gossip"->
                actorRef <-
                    Actor i algo
                    |> spawn system actorName
               // printfn "%i Actor Created" i



            |"push sum"->
                actorRef <-
                    Actor i algo
                    |> spawn system actorName
                ()
               // printfn "%i Actor Created" i
    |"2d grid"->
        for i= 1 to numOfNodes do
            for j=1 to numOfNodes do
                actorID<-actorID+1
                let actorName = string actorID
                printfn "%i Actor Created" actorID
                match algo with
                |"gossip"-> 
                    actorRef <-
                        Actor actorID algo
                        |> spawn system actorName
                    printfn "%i Actor Created" i

                |"push sum"->
                    actorRef <-
                        Actor actorID algo
                        |> spawn system actorName


    let num_string= string (rand.Next()%(numOfNodes*numOfNodes))
    let pathToActor="akka://MySystem/user/" + num_string
    let randomActor = select pathToActor system
    randomActor <! 1




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

#time "on"


printf "value of k=%i" k