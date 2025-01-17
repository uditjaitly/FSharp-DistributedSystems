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
let numOfNodes=33
let rnd=System.Random()
let rand=System.Random()
let mutable k = 0
let topology="2d grid"
let algo = "gossip"
let mutable inc=0
let mutable actorRef = select "akka://MySystem/user/" system
let mutable b=System.Diagnostics.Stopwatch.StartNew()
let round (x:float,d:float) =
    let rounded = Math.Round(d)
    if rounded < x then x + Math.Pow(10.0,-d) else rounded
let bossRef= select "akka://MySystem/user/master" system

let pickNeighbor (neighbors:_ list, numberOfNeighbors: int, i : int, s : float,w : float)  =
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
    if algo = "gossip" then
        actorRef<! (-1.0,-1.0)
    else if algo = "push sum" then
        actorRef<! (s,w)
()



let Actor i j (mailbox: Actor<_>) =
    let mutable counter=0
    let mutable proceed=true
    let mutable neighbors = []
    let mutable numberOfNeighbors=0
    let mutable s= float i
    let mutable w= float 1
    let mutable lastRatio= float -1
    let mutable currentRatio= float 1
    let mutable roundCounter=0
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

        | "imperfect 2d grid"->
            if (i+numOfNodes) <= (numOfNodes*numOfNodes) then
                neighbors <- [i+numOfNodes] |> List.append neighbors
            if (i-numOfNodes) > 0 then
                neighbors <- [i-numOfNodes] |> List.append neighbors
            if ((i-1) > 0 && (i-1)%numOfNodes<>0) then
                neighbors <- [i-1] |> List.append neighbors
            if ((i+1) <= (numOfNodes*numOfNodes) && (((i+1)%numOfNodes)<>1))  then
                neighbors <- [i+1] |> List.append neighbors
            
            neighbors <- [rand.Next()%(numOfNodes*numOfNodes)] |> List.append neighbors
        | _ -> ()
    



    let numberOfNeighbors= neighbors.Length
    
    
    
    



    let rec loop n = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | (-1.0,-1.0) ->
            counter<-counter+1
            if counter = 10 then
                proceed<-false
                printfn "Counter is 10 for %i" i
                k<-k+1
                bossRef<! (1.0,1.0)
            if counter = 1 then
                async {
                    while proceed do
                        do! Async.Sleep 500
                        pickNeighbor (neighbors,numberOfNeighbors,i,s,w)
                } |> Async.StartImmediate
            else
                pickNeighbor (neighbors,numberOfNeighbors,i,s,w)

            return! loop ()








        | (a,b) ->
            s<-s+a
            w<-w+b
            s<-s/2.0
            w<-w/2.0
            currentRatio<-s/w
            currentRatio<-System.Math.Round (currentRatio,10)
            if lastRatio = currentRatio then
                printfn "%fLAST RATIO=" lastRatio
                roundCounter<-roundCounter+1
                if(roundCounter<3) then
                  //  k<-k+8
                    lastRatio<-currentRatio
                    lastRatio<-System.Math.Round (lastRatio,10)
                    pickNeighbor(neighbors,numberOfNeighbors,i,s,w)
                    
                    return! loop()
                if roundCounter = 3 then
                    printf "DONE"
                    sender <! "DONE"
            else
                
                //k<-k+1
                lastRatio<-currentRatio
                lastRatio<-System.Math.Round (lastRatio,10)
                pickNeighbor(neighbors,numberOfNeighbors,i,s,w)
                return! loop()

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
        
            match algo with
            |"gossip"->
                for i = 1 to numOfNodes do
                    let actorName=string i
                    actorRef <-
                        Actor i algo
                        |> spawn system actorName
                    //printfn "%i Actor Created" i

                let num_string= string (rand.Next()%(numOfNodes))
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                randomActor <! (-1.0,-1.0)
                b<-System.Diagnostics.Stopwatch.StartNew()



            |"push sum"->
                for i = 1 to numOfNodes do
                    let actorName=string i
                    actorRef <-
                        Actor i algo
                        |> spawn system actorName
                    ()
                    printfn "%i Actor Created" i
                let num_string= string (rand.Next()%(numOfNodes))
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                randomActor <! (float (num_string),1.0)
    |"line"->
        match algo with
            |"gossip"->
                for i = 1 to numOfNodes do
                    let actorName=string i
                    actorRef <-
                        Actor i algo
                        |> spawn system actorName
                    //printfn "%i Actor Created" i

                let num_string= string (rand.Next()%(numOfNodes))
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                randomActor <! (-1.0,-1.0)
                b<-System.Diagnostics.Stopwatch.StartNew()




            |"push sum"->
                for i = 1 to numOfNodes do
                    let actorName=string i
                    actorRef <-
                        Actor i algo
                        |> spawn system actorName
                    ()
                    printfn "%i Actor Created" i
                let num_string= string (rand.Next()%(numOfNodes))
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                randomActor <! (float (num_string),1.0)
    |"2d grid"->
        
                match algo with
                |"gossip"-> 
                    for i= 1 to numOfNodes do
                        for j=1 to numOfNodes do
                            actorID<-actorID+1
                            let actorName = string actorID
                            printfn "%i Actor Created" actorID
                            actorRef <-
                                Actor actorID algo
                                |> spawn system actorName
                    let num_string= string (rand.Next()%(numOfNodes*numOfNodes))
                    let pathToActor="akka://MySystem/user/" + num_string
                    let randomActor = select pathToActor system
                    randomActor <! (-1.0,-1.0)
                 

                |"push sum"->
                    for i= 1 to numOfNodes do
                        for j=1 to numOfNodes do
                            actorID<-actorID+1
                            let actorName = string actorID
                            printfn "%i Actor Created" actorID
                            actorRef <-
                                Actor actorID algo
                                |> spawn system actorName
                    let num_string= string (rand.Next()%(numOfNodes*numOfNodes))
                    let pathToActor="akka://MySystem/user/" + num_string
                    let randomActor = select pathToActor system
                    randomActor <! (float (num_string),1.0)
    |"imperfect 2d grid"->
        match algo with
                |"gossip"-> 
                    for i= 1 to numOfNodes do
                        for j=1 to numOfNodes do
                            actorID<-actorID+1
                            let actorName = string actorID
                            printfn "%i Actor Created" actorID
                            actorRef <-
                                Actor actorID algo
                                |> spawn system actorName
                    let num_string= string (rand.Next()%(numOfNodes*numOfNodes))
                    let pathToActor="akka://MySystem/user/" + num_string
                    let randomActor = select pathToActor system
                    randomActor <! (-1.0,-1.0)
                 

                |"push sum"->
                    for i= 1 to numOfNodes do
                        for j=1 to numOfNodes do
                            actorID<-actorID+1
                            let actorName = string actorID
                            printfn "%i Actor Created" actorID
                            actorRef <-
                                Actor actorID algo
                                |> spawn system actorName
                    let num_string= string (rand.Next()%(numOfNodes*numOfNodes))
                    let pathToActor="akka://MySystem/user/" + num_string
                    let randomActor = select pathToActor system
                    randomActor <! (float (num_string),1.0)


    




    let rec listen() =
        actor {
            let! message = mailbox.Receive()
            match message with
            | (1.0,1.0) -> 
                inc<-inc+1 
                if(inc>=1085) then
                    b.Stop()
                    printfn "%f" b.Elapsed.TotalMilliseconds


            return! listen()
        } 
    listen()
()


let boss = 
    Master -1 algo
    |> spawn system "master"

#time "on"


printf "value of k=%i" k
printf "value of c=%i" inc