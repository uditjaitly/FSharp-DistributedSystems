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
let topology="line 2d grid"
let algo = "gossip"
let mutable actorRef = select "akka://MySystem/user/" system
let round (x:float,d:float) =
    let rounded = Math.Round(d)
    if rounded < x then x + Math.Pow(10.0,-d) else rounded
let bossRef= select "akka://MySystem/user/master" system
let mutable c=0
let mutable b=System.Diagnostics.Stopwatch.StartNew()
let checkCounter = 
    if c >8 then
        printf "done"

let mutable pendingActors= [1..1000]

let pickNeighbor (neighbors:_ list, numberOfNeighbors: int, i : int, s : float,w : float)  =
    let mutable index=(rand.Next()%numberOfNeighbors)
    let mutable randomNum=neighbors.[index]
    let mutable num_string= string randomNum
    let mutable pathToActor="akka://MySystem/user/" + num_string
    let mutable itr=1
    let mutable flag=0
    //printfn "random num=%i" randomNum
    while index = 0 || randomNum =i || randomNum=0 do
        index<-(rand.Next()%numberOfNeighbors)
        randomNum<-neighbors.[index]

    num_string<- string randomNum
    pathToActor<-"akka://MySystem/user/" + num_string
    actorRef <- select pathToActor system
    if algo = "gossip" then
        actorRef<! (-1.0,-1.0)
    else if algo = "push sum" then
        if List.contains randomNum pendingActors then
            num_string<- string randomNum
            pathToActor<-"akka://MySystem/user/" + num_string
        else
            while itr< numberOfNeighbors && flag=0 do
                if List.contains neighbors.[itr] pendingActors then
                    num_string<- string neighbors.[itr]
                    pathToActor<-"akka://MySystem/user/" + num_string
                    flag<-1
                itr<-itr+1    
            if flag = 0 then
                num_string<- string (pendingActors.[rand.Next()%pendingActors.Length])
                pathToActor<-"akka://MySystem/user/" + num_string         

        actorRef <- select pathToActor system
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
    let mutable selfGossipCounter=0
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
    let ss= string i
    let p="akka://MySystem/user/" + ss
    let selfRef= select p system
    
    
    



    let rec loop n = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        

        match message with
        | (-1.0,-1.0) ->
            counter<-counter+1
            if counter = 10 then
                proceed<-false
                k<-k+1
                //printfn "completed actor %i" i
                bossRef<! (-1,-1)
            else if counter = 1 then
                pickNeighbor (neighbors,numberOfNeighbors,i,s,w)
                selfRef<!(1.0,1.0)
            return! loop ()
            

            
        | (1.0,1.0) ->
            selfGossipCounter<-selfGossipCounter+1
            if selfGossipCounter = 1000 then
                selfGossipCounter<-0
                pickNeighbor (neighbors,numberOfNeighbors,i,s,w)

            selfRef<!(1.0,1.0)
            return! loop()
            

        | (a ,b ) ->
            s<-s+a
            w<-w+b
            s<-s/2.0
            w<-w/2.0
            currentRatio<-s/w
            //currentRatio<-System.Math.Round (currentRatio,10)
            if lastRatio = currentRatio then
                k<-k+1
               // printfn "CURR=LAST FOR ACTOR=%i" i
                //printfn "%fLAST RATIO=" lastRatio
                roundCounter<-roundCounter+1
                if(roundCounter<3) then
                  //  k<-k+8
                    lastRatio<-currentRatio
                   // lastRatio<-System.Math.Round (lastRatio,10)
                    pickNeighbor(neighbors,numberOfNeighbors,i,s,w)
                    return! loop()
                if roundCounter = 3 then
                    c<-c+1
                    printf "DONE%i" i
                    printfn "%fLAST RATIO=" lastRatio

                    sender <! (-1,-1)
                    pendingActors<-pendingActors |> List.filter ((<>) i)
                    pickNeighbor(neighbors,numberOfNeighbors,i,s,w)
                    return! loop()
                else

                    pickNeighbor(neighbors,numberOfNeighbors,i,s,w)
                    return! loop()

            else
                lastRatio<-currentRatio

               // lastRatio<-System.Math.Round (lastRatio,10)
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
                    //printfn "%i Actor Created" i
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
                            //printfn "%i Actor Created" actorID
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
                         //   printfn "%i Actor Created" actorID
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
            | (-1,-1) -> 
                c<-c+1
                if c>=1000 then
                    b.Stop()

                    printf "%A" (b.Elapsed.TotalMilliseconds)
                    printf "Done"


            return! listen()
        } 
    listen()
()


let boss = 
    Master -1 algo
    |> spawn system "master"


printf "%A" boss
#time "on"


printf "value of k=%i" k
printf "value of c=%i" c
printf "presentActors=%A" pendingActors
