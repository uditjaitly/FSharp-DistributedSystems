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
printfn "%A" input
let numOfNodes= int input.[3]
let topology=input.[4]
let algo = input.[5]
let failureNodes= int input.[6]
let rnd=System.Random()
let rand=System.Random()
let mutable k =decimal 0
let mutable actorRef = select "akka://MySystem/user/" system
let iCeil=int (Math.Ceiling(Math.Sqrt(float numOfNodes)))
let jFloor=int (Math.Ceiling(Math.Sqrt(float numOfNodes)))
let bossRef= select "akka://MySystem/user/master" system
let mutable c=0
let mutable b=System.Diagnostics.Stopwatch.StartNew()
let checkCounter = 
    if c >8 then
        printf "done"
let mutable selfLimit=500
let mutable proceed1=true
let mutable threshold = 0.8* float(numOfNodes-failureNodes)
let mutable pendingActors= [1..numOfNodes]

type Values={Values : List<decimal>}

type Command = 
    | ValuesForPush of Values
    | Gossip of string
    | GossipSelf of string

let mutable poisonActors = Array.create (numOfNodes+22) (0) 
let mutable tempStore=rand.Next()%numOfNodes
for b = 1 to failureNodes do
    tempStore<-rand.Next()%numOfNodes
    while poisonActors.[tempStore]=1 do
        tempStore<-rand.Next()%numOfNodes
    
    poisonActors.[tempStore]<-1
        
let mutable flag4=1
let checkIfPoisoned i ref=
    if poisonActors.[i]= 1 then
        system.Stop(ref)


let pickNeighbor (neighbors:_ list, numberOfNeighbors: int, i : int, s : decimal,w : decimal)  =
    let mutable index=(rand.Next()%numberOfNeighbors)
    let mutable randomNum=neighbors.[index]
    let mutable num_string= string randomNum
    let mutable pathToActor="akka://MySystem/user/" + num_string
    let mutable itr=1
    let mutable flag=0
    let mutable freeNeighbors=[]
    
    //printfn "random num=%i" randomNum
    if topology = "full" & algo = "gossip" then
        randomNum<-(rand.Next()%numOfNodes)
    else
        for i =1 to numberOfNeighbors-1 do
            if poisonActors.[neighbors.[i]]<>1 then
                randomNum<-neighbors.[index]
                flag4<-0

        if flag4=1 then

            while index = 0 || randomNum =i || randomNum=0 || poisonActors.[randomNum]=1 do
                index<-(rand.Next()%numberOfNeighbors)
                randomNum<-neighbors.[index]
    //printf "HERE IS THE RANDOM%i" randomNum
    num_string<- string randomNum
    pathToActor<-"akka://MySystem/user/" + num_string
    actorRef <- select pathToActor system
    if algo = "gossip" then
        actorRef<! Gossip "This is a very hot rumor"
    else if algo = "push-sum" && topology <> "full" then
        if List.contains randomNum pendingActors then
            num_string<- string randomNum
            pathToActor<-"akka://MySystem/user/" + num_string
        else
            while itr< numberOfNeighbors do
                if List.contains neighbors.[itr] pendingActors then
                    freeNeighbors <- [neighbors.[itr]] |> List.append freeNeighbors
                    flag<-1
                itr<-itr+1   
            if flag =1 then 
                num_string<- string freeNeighbors.[rand.Next()%freeNeighbors.Length]
                pathToActor<-"akka://MySystem/user/" + num_string
            if flag = 0 then
                num_string<- string (pendingActors.[rand.Next()%pendingActors.Length])
                pathToActor<-"akka://MySystem/user/" + num_string         

        actorRef <- select pathToActor system
        actorRef<! ValuesForPush {Values = [s;w]}
    else 
        num_string<- string (pendingActors.[rand.Next()%pendingActors.Length])
        pathToActor<-"akka://MySystem/user/" + num_string  
        actorRef <- select pathToActor system
        actorRef<! ValuesForPush {Values = [s;w]}

       

       
()



let Actor i j (mailbox: Actor<_>) =
    let mutable counter=0
    let mutable proceed=true
    let mutable neighbors = []
    let mutable numberOfNeighbors=0
    let mutable s= decimal i
    let mutable w= decimal 1
    let mutable lastRatio= decimal -1
    let mutable currentRatio= decimal 1
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

        | "2d-grid"->
            if (i+jFloor) <= (numOfNodes) then
                neighbors <- [i+jFloor] |> List.append neighbors
            if (i-jFloor) > 0 then
                neighbors <- [i-jFloor] |> List.append neighbors
            if ((i-1) > 0 && (i-1)%jFloor<>0) then
                neighbors <- [i-1] |> List.append neighbors
            if ((i+1) <= (numOfNodes) && (((i+1)%jFloor)<>1))  then
                neighbors <- [i+1] |> List.append neighbors
        | "imperfect-2d-grid"->
            if (i+jFloor) <= (numOfNodes) then
                neighbors <- [i+jFloor] |> List.append neighbors
            if (i-jFloor) > 0 then
                neighbors <- [i-jFloor] |> List.append neighbors
            if ((i-1) > 0 && (i-1)%jFloor<>0) then
                neighbors <- [i-1] |> List.append neighbors
            if ((i+1) <= (numOfNodes) && (((i+1)%jFloor)<>1))  then
                neighbors <- [i+1] |> List.append neighbors

            neighbors <- [rand.Next()%(numOfNodes)] |> List.append neighbors
        | _ -> ()
    


    let numberOfNeighbors= neighbors.Length
    let ss= string i
    let p="akka://MySystem/user/" + ss
    let selfRef= select p system
    //printf "selfRef%A" selfRef
    //printfn "Neighbors for actor=%i are %A" i neighbors
    //printfn "num of neighbors for actor=%i is %i" i numberOfNeighbors

    let rec loop n = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | Gossip g ->
            counter<-counter+1
            if counter = 10 then
                proceed<-false
                k<-k+ decimal 1
                //printfn "completed actor %i" i
                bossRef<! (-1,-1)
            else if counter = 1 then
                pickNeighbor (neighbors,numberOfNeighbors,i,s,w)
                selfRef<! GossipSelf "Why am i talking to myself"
            else if counter < 10 then
                pickNeighbor (neighbors,numberOfNeighbors,i,s,w)

            return! loop ()


        | GossipSelf sg ->
            selfGossipCounter<-selfGossipCounter+1
            if selfGossipCounter=selfLimit then
                selfGossipCounter<-0
                pickNeighbor (neighbors,numberOfNeighbors,i,s,w)

            selfRef<!GossipSelf "Why am i talking to myself"
            return! loop()


        | ValuesForPush {Values= values} ->
            s<-s+values.[0]
            w<-w+values.[1]
            s<-s/decimal 2.0
            w<-w/decimal 2.0
            currentRatio<-s/w
            currentRatio<-Math.Round(currentRatio,10)
            //currentRatio<-System.Math.Round (currentRatio,10)
            if lastRatio = currentRatio then
                k<-lastRatio
               // printfn "CURR=LAST FOR ACTOR=%i" i
               // printfn "%ALAST RATIO=" lastRatio
                roundCounter<-roundCounter+1
                if(roundCounter<3) then
                  //  k<-k+8
                    lastRatio<-currentRatio

                   // lastRatio<-System.Math.Round (lastRatio,10)
                    pickNeighbor(neighbors,numberOfNeighbors,i,s,w)
                    return! loop()
                if roundCounter = 3 then
                    c<-c+1
                   // printfn "DONE%i" i
                   // printfn "%ALAST RATIO=" lastRatio

                    bossRef <! (-1,-1)
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
                    if poisonActors.[i]=1 then
                        system.Stop(actorRef)
                        printfn "ACTOR STOPPED=%A" actorRef
                let mutable tempStore=rand.Next()%(numOfNodes)
                while poisonActors.[tempStore]=1 do
                    tempStore<-rand.Next()%(numOfNodes)
                let num_string= string (tempStore)
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                randomActor <! Gossip "This is a very hot rumor"
                b<-System.Diagnostics.Stopwatch.StartNew()



            |"push-sum"->
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
                randomActor <! ValuesForPush {Values = [decimal num_string;decimal 1]}
                b<-System.Diagnostics.Stopwatch.StartNew()

    |"line"->
        match algo with
            |"gossip"->
                for i = 1 to numOfNodes do
                    let actorName=string i
                    actorRef <-
                        Actor i algo
                        |> spawn system actorName
                    if poisonActors.[i]=1 then
                        system.Stop(actorRef)
                        printfn "ACTOR STOPPED=%A" actorRef
                let mutable tempStore=rand.Next()%(numOfNodes)
                while poisonActors.[tempStore]=1 do
                    tempStore<-rand.Next()%(numOfNodes)
                    //printfn "%i Actor Created" i

                let num_string= string (tempStore)
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                randomActor <! Gossip "This is a very hot rumor"
                b<-System.Diagnostics.Stopwatch.StartNew()




            |"push-sum"->
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
                randomActor <! ValuesForPush {Values = [decimal num_string;decimal 1]}
    |"2d-grid"->
        selfLimit<-100
        match algo with
            |"gossip"-> 
                for i= 1 to iCeil do
                    for j=1 to jFloor do
                        actorID<-actorID+1
                        let actorName = string actorID
                        if actorID<=numOfNodes then
                            printfn "%i Actor Created" actorID
                            actorRef <-
                                Actor actorID algo
                                |> spawn system actorName

                            if poisonActors.[actorID]=1 then
                                system.Stop(actorRef)
                                printfn "ACTOR STOPPED=%A" actorRef
                                pendingActors<-pendingActors |> List.filter ((<>) actorID)

                let mutable tempStore=rand.Next()%(numOfNodes)
                while poisonActors.[tempStore]=1 do
                    tempStore<-rand.Next()%(numOfNodes)
                let num_string= string (tempStore)
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                printfn "random chosen=%A" randomActor
                randomActor <! Gossip "This is a very hot rumor"
                b<-System.Diagnostics.Stopwatch.StartNew()

                 

            |"push-sum"->
                for i= 1 to iCeil do
                    for j=1 to jFloor do
                        actorID<-actorID+1
                        let actorName = string actorID
                        if actorID<=numOfNodes then
                            printfn "%i Actor Created" actorID
                            actorRef <-
                                Actor actorID algo
                                |> spawn system actorName
                let num_string= string (rand.Next()%(numOfNodes))
                let pathToActor="akka://MySystem/user/" + num_string
                let randomActor = select pathToActor system
                printf "%A" randomActor
                randomActor <! ValuesForPush {Values = [decimal num_string;decimal 1]}
    |"imperfect-2d-grid"->
        selfLimit<-500
        match algo with
                |"gossip"->
                    for i= 1 to iCeil do
                        for j=1 to jFloor do
                            actorID<-actorID+1
                            let actorName = string actorID
                            if actorID<=numOfNodes then
                                printfn "%i Actor Created" actorID
                                actorRef <-
                                    Actor actorID algo
                                    |> spawn system actorName
                                if poisonActors.[actorID]=1 then
                                    system.Stop(actorRef)
                                    printfn "ACTOR STOPPED=%A" actorRef
                    let mutable tempStore=rand.Next()%(numOfNodes)
                    while poisonActors.[tempStore]=1 do
                        tempStore<-rand.Next()%(numOfNodes)
                    let num_string= string (tempStore)
                    let pathToActor="akka://MySystem/user/" + num_string
                    let randomActor = select pathToActor system
                    randomActor <! Gossip "This is a very hot rumor"
                    b<-System.Diagnostics.Stopwatch.StartNew()


                |"push-sum"->
                    for i= 1 to iCeil do
                        for j=1 to jFloor do
                            actorID<-actorID+1
                            let actorName = string actorID
                            if actorID<=numOfNodes then
                                printfn "%i Actor Created" actorID
                                actorRef <-
                                    Actor actorID algo
                                    |> spawn system actorName
                    let num_string= string (rand.Next()%(numOfNodes))
                    let pathToActor="akka://MySystem/user/" + num_string
                    let randomActor = select pathToActor system

                    randomActor <! ValuesForPush {Values = [decimal num_string;decimal 1]}
                    b<-System.Diagnostics.Stopwatch.StartNew()



    




    let rec listen() =
        actor {
            let! message = mailbox.Receive()
            let sender=mailbox.Sender()
            match message with
            | (-1,-1) -> 
                c<-c+1
                if c>=int threshold then
                    b.Stop()
                    printfn "Done="
                    printf "%A" (b.Elapsed.TotalMilliseconds)
                    proceed1<-false
                    ()


            return! listen()
        } 
    listen()
()



let boss = 
    Master -1 algo
    |> spawn system "master"

printfn "number of compeleted actors=%i" c

while proceed1 do
    Async.Sleep 10
    if c>=numOfNodes then
        proceed1<-false
 