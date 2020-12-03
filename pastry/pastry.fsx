#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics

//////////////////////////////Initialization and input processing/////////////////////////////////////
let system = System.create "MySystem" (Configuration.defaultConfig())
let input=System.Environment.GetCommandLineArgs()
let numOfNodes= int input.[3]
let numOfReq= int input.[4]
let mutable avghops=0.0
let mutable count=0
let rand = System.Random()
let numForInit=int (float (0.9*float(numOfNodes)))
let mutable randomNum=0

type RouteMessage = {RouteMessage:List<int>}
type TableRow={TableRow:List<int>}
type Command = 
    | Initialize of String
    | ConnectionInit of int[] 
    | GossipSelf of string
    | LeafSmaller of Set<int>
    | LeafGreater of Set<int>
    | InitDone of int
    | RoutingTable of int[,]
    | StartRouting of int[]
    | Route of RouteMessage
    | NodeJoin
    | NewNodeJoin of int
    | UpdateTableRow of TableRow
    | Here of String
    | LeafUpdate of Set<int>
    | UpdateSelf of int
    | Received of int[]
    | JoinDone of string
    | SignalStartRouting of string
    | JoinEnd of string
    | PrintInfo of string
let inline charToInt c = int c - int '0'
let mutable counter=0
let toDecimal (nodeStr: string) = 
    let len=nodeStr.Length
    let mutable power=1
    let mutable num=0
    let mutable i=(len-1)
    while i >= 0 do
        num<-num+ ((nodeStr.[i]|>charToInt) * power)
        power<-power*4
        i<-i-1
    num


/////////////EACH NODE IN THE NETWORK //////////////////////////

let Node numOfNodes numOfReq nodeID (mailbox: Actor<_>) = 
    let temp=ceil(Math.Log((float)numOfNodes,4.0))
    let L=8
    let mutable routingTable = 
        [| for i in 0 .. (int temp) do 
            yield [| for i in 0 ..3 do yield -1 |] 
        |] |> array2D
    let mutable updateNested = 0
    let IDrange=  (int) (Math.Pow(4.0,temp))
    let mutable leafGreater= Set.empty.Add(1)
    let mutable leafSmaller= Set.empty.Add(1)
    let mutable myNodeList= Array.create (numOfNodes+1) -1  
    let mutable hops =0

    leafGreater<-leafGreater.Remove(1)
    leafSmaller<-leafSmaller.Remove(1)
    let actName=string nodeID
    let p="akka://MySystem/user/" +  actName
    let selfRef= select p system
    //////////////CREATES LEAF SET FOR NODES/////////////////
    let createLeafSet (nodeList: int[], nodeID: int) = 
        for i = 1 to (nodeList.Length-1) do
            let isLessThanID = nodeID > nodeList.[i]
            match isLessThanID with
            | true->
                if leafSmaller.Contains(nodeList.[i]) = false then
                    if leafSmaller.Count < 4 then
                        leafSmaller<-leafSmaller.Add(nodeList.[i])
                    else if leafSmaller.MinimumElement < nodeList.[i] then
                        leafSmaller<-leafSmaller.Remove(leafSmaller.MinimumElement)
                        leafSmaller<-leafSmaller.Add(nodeList.[i])
                   
            | false->
                if leafGreater.Contains(nodeList.[i]) = false then
                    if leafGreater.Count < 4 then
                        leafGreater<-leafGreater.Add(nodeList.[i])
                    else if leafGreater.MaximumElement > nodeList.[i] then
                        leafGreater<-leafGreater.Remove(leafGreater.MaximumElement)
                        leafGreater<-leafGreater.Add(nodeList.[i])
    /////////////////CHECKS IF DESTINATION IS PRESENT IN LEAF SET///////////////
    let foundInLeaf (message:List<int>)=
        let key=message.[0]
        let hops=message.[1]

        let mutable flag=0
        let keyDecimal= toDecimal ((string) key)
        let nodeIDDecimal=toDecimal((string)nodeID)
        let mutable closestNode = (-1)
        let mutable min=99999
        ()
        if ( leafSmaller.Count <> 0 && keyDecimal>=toDecimal (string leafSmaller.MinimumElement) && keyDecimal<=nodeIDDecimal ) then
            for leaf in leafSmaller do 
                let t = Math.Abs(toDecimal (string leaf) - keyDecimal)
                if( t<min) then
                    closestNode<-leaf
                    min<-t
                    
                    
                 

        else if (leafGreater.Count <> 0 && keyDecimal<=toDecimal (string leafGreater.MaximumElement) && keyDecimal>=nodeIDDecimal ) then
            for leaf in leafGreater do
                let t = Math.Abs(toDecimal(string leaf) - keyDecimal)
                if(t<min) then
                    closestNode<-leaf
                    min<-t
                    

        if min<>99999 && Math.Abs(nodeIDDecimal-keyDecimal)<=min then 
            count<-count+1
            avghops<-(float) hops+avghops
            flag<-1

            
        else if min <> 99999 && Math.Abs(nodeIDDecimal-keyDecimal)>min then
            let actorName=string closestNode
            flag<-1
            let pathToActor="akka://MySystem/user/" + actorName
            let nextNode= select pathToActor system
            nextNode<! Route {RouteMessage = [key;hops+1;1]} 
            

        if flag = 1 then 
            true
        else 
            false


    ///////////////CHECKS AND FORWARDS BY LOOKING AT THE ROUTING TABLE//////////////////
    let foundInRoutingTable(message:List<int>)=
        let key=message.[0]
        let nodeStr=string key
        let nodeIDStr= string nodeID
        let hops=message.[1]
        let mutable k = 0
       
        let mutable intAt1= (nodeStr.[k]|>charToInt)
        let mutable intAt2= (nodeIDStr.[k]|>charToInt)
        let gg= nodeIDStr.Length
        ()
        while ( k < nodeIDStr.Length-1 && k<>nodeStr.Length-1 && intAt1 = intAt2 ) do
            k<-k+1
            intAt1<- (nodeStr.[k]|>charToInt)
            intAt2<- (nodeIDStr.[k]|>charToInt)
            ()
        if routingTable.[k,nodeStr.[k]|>charToInt] <> (-1) then
            let actorName=string routingTable.[k,nodeStr.[k]|>charToInt]
            let pathToActor="akka://MySystem/user/" + actorName
            let nextNode= select pathToActor system
            nextNode<! Route {RouteMessage = [key;hops+1;0]}
            true
        else
            false




    ///////////CREATES ROUTING TABLE FOR A NODE AND ADD'S ENTRIES BY DOING PREFIX MATCHING///////////////////////
    let fillRoutingTable (nodeList: int[], nodeID: int) = 
        for i=1 to (nodeList.Length-1) do
            let nodeStr=string nodeList.[i]
            let nodeIDStr=string nodeID
            let mutable k = 0
            let mutable intAt1= (nodeStr.[k]|>charToInt)
            let mutable intAt2= (nodeIDStr.[k]|>charToInt)
            let gg= nodeIDStr.Length
            ()
            while ( k < nodeIDStr.Length-1 && k<>nodeStr.Length-1 && intAt1 = intAt2 ) do
                k<-k+1
                intAt1<- (nodeStr.[k]|>charToInt)
                intAt2<- (nodeIDStr.[k]|>charToInt)
                ()
                 

            if routingTable.[k,nodeStr.[k]|>charToInt] = -1 then
                routingTable.[k, nodeStr.[k]|>charToInt] <- nodeList.[i]

    ()

    let rec loop n = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with 
        ///////////////////// INITIALIZES THE NETWORK FOR 90% NODES. EACH NODE HAS IT'S OWN LEAF SET AND ROUTING TABLE CREATED////////////////////
        | ConnectionInit initialNodeList -> 
            let initialNodeList = initialNodeList|>Array.filter((<>)nodeID)
            myNodeList<-initialNodeList
            createLeafSet(initialNodeList,nodeID)
            fillRoutingTable(initialNodeList,nodeID)
            for i=0 to int temp do
                let nodeIDStr=string nodeID
                if i<=nodeIDStr.Length-1 then
                    routingTable.[i, (nodeIDStr.[i])|>charToInt]<-(-1)
            if nodeID=10000000 then
                sender<! LeafSmaller leafSmaller
                sender<! LeafGreater leafGreater
                sender<! RoutingTable routingTable

            sender <! InitDone nodeID
            return! loop()

        | NewNodeJoin key ->
            selfRef<!Route{RouteMessage=[key;hops;-1]}
            return! loop()


        | StartRouting arr -> 
            for i=1 to numOfReq do
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(i|>float),mailbox.Self,Route{RouteMessage = [arr.[rand.Next()%numForInit+1]+randomNum;0;0]})

            return! loop()
        | Route {RouteMessage=message}->
            if message.[2]= (-1) then         ///////////////CREATES AND UPDATES NETWORK INFO WHEN A NEW NODE JOINS/////////////////
                let nodeStr=string message.[0]
                let hops=message.[1]
                let nodeIDStr=string nodeID
                let mutable k = 0
                let mutable intAt1= (nodeStr.[k]|>charToInt)
                let mutable intAt2= (nodeIDStr.[k]|>charToInt)
                let gg= nodeIDStr.Length
                ()
                while ( k < nodeIDStr.Length-1 && k<>nodeStr.Length-1 && intAt1 = intAt2 ) do
                    k<-k+1
                    intAt1<- (nodeStr.[k]|>charToInt)
                    intAt2<- (nodeIDStr.[k]|>charToInt)
                    ()
                if k>0 then
                    if hops= 0 then 
                        for b = 0 to k do
                            let actorName= nodeStr
                            let pathToActor="akka://MySystem/user/" + actorName
                            let selectedActor= select pathToActor system 
                            let x =[routingTable.[b,0];routingTable.[b,1];routingTable.[b,2];routingTable.[b,3];b]
                            selectedActor<!UpdateTableRow{TableRow=x}
                            sender<!Here "here"




                let key=message.[0]
                let hops=message.[1]
                let mutable flag=0
                let keyDecimal= toDecimal ((string) key)
                let nodeIDDecimal=toDecimal((string)nodeID)
                let mutable closestNode = (-1)
                let mutable min=99999
                ()
                if ( leafSmaller.Count <> 0 && keyDecimal>=toDecimal (string leafSmaller.MinimumElement) && keyDecimal<=nodeIDDecimal ) then
                    for leaf in leafSmaller do 
                        let t = Math.Abs(toDecimal (string leaf) - keyDecimal)
                        if( t<min) then
                            closestNode<-leaf
                            min<-t


                else if (leafGreater.Count <> 0 && keyDecimal<=toDecimal (string leafGreater.MaximumElement) && keyDecimal>=nodeIDDecimal ) then
                    for leaf in leafGreater do
                        let t = Math.Abs(toDecimal(string leaf) - keyDecimal)
                        if(t<min) then
                            closestNode<-leaf
                            min<-t


                if min<>99999 && Math.Abs(nodeIDDecimal-keyDecimal)<=min then 
                    let actorName=string key
                    let leafs= Set.union leafGreater leafSmaller
                    let pathToActor="akka://MySystem/user/" + actorName
                    let nextNode= select pathToActor system
                    nextNode <! LeafUpdate leafs
                    count<-count+1
                    avghops<-(float) hops+avghops
                    flag<-1

                    //////IMPLEMENT CODE TO UPDATE MASTER//////
                    
                else if min <> 99999 && Math.Abs(nodeIDDecimal-keyDecimal)>min then
                    let actorName=string closestNode
                    flag<-1
                    let pathToActor="akka://MySystem/user/" + actorName
                    let nextNode= select pathToActor system
                    nextNode<! Route {RouteMessage = [key;hops+1;-1]} 

                if flag = 0 then
                    
                    if leafGreater.Count<4 && leafGreater.Count>0 && keyDecimal>(toDecimal (string leafGreater.MinimumElement)) then
                        let actorName=string leafGreater.MaximumElement
                        let pathToActor="akka://MySystem/user/" + actorName
                        let nextNode= select pathToActor system
                        nextNode<!Route {RouteMessage = [key;hops+1;-1]} 
                    else if leafSmaller.Count<4 && leafSmaller.Count>0 && keyDecimal<(toDecimal (string leafSmaller.MinimumElement)) then
                        let actorName=string leafSmaller.MinimumElement
                        let pathToActor="akka://MySystem/user/" + actorName
                        let nextNode= select pathToActor system
                        nextNode<!Route {RouteMessage = [key;hops+1;-1]} 
                    else if (leafSmaller.Count =0 && keyDecimal<nodeIDDecimal) || (leafGreater.Count=0 && keyDecimal>nodeIDDecimal) then /// NEAREST DESTINATION
                        let mutable leafs=Set.union leafGreater leafSmaller
                        leafs<-leafs.Add(nodeID)
                        let actorName=string key
                        let pathToActor="akka://MySystem/user/" + actorName
                        let nextNode= select pathToActor system
                        ()

                        nextNode <! LeafUpdate leafs
                    else if routingTable.[k,nodeStr.[k]|>charToInt] <> (-1) then
            
                        let actorName=string routingTable.[k,nodeStr.[k]|>charToInt]
                        let pathToActor="akka://MySystem/user/" + actorName
                        let nextNode= select pathToActor system
                        nextNode<! Route {RouteMessage = [key;hops+1;-1]}




            else                                        //////////////PERFORMS ROUTING FOR A REQUEST////////////////
                if foundInLeaf(message) then
                    counter<-counter+1

                else if foundInRoutingTable(message) then
                    counter<-counter+1

                else
                    counter<-counter+1


            return! loop()


        | LeafUpdate leafs ->                                               /////////UPDATE LEAFS OF NODES OF EXISTING NETWORK WHEN NEW NODE JOINS////////////
            let list sa =  sa |> Set.fold (fun se sacc -> sacc::se) [] |> List.rev
            let leafList= list leafs
            let leafsArray= leafList |> Seq.toArray
            sender<! Received leafsArray

            createLeafSet(leafsArray,nodeID)

            for i in leafSmaller do
                updateNested<-updateNested+1
                let actorName=string i
                let pathToActor="akka://MySystem/user/" + actorName
                let selectedActor= select pathToActor system 
                selectedActor<!UpdateSelf nodeID
            for i in leafGreater do
                updateNested<-updateNested+1
                let actorName=string i
                let pathToActor="akka://MySystem/user/" + actorName
                let selectedActor= select pathToActor system 
                selectedActor<!UpdateSelf nodeID
            for i=0 to int temp do
                for j=0 to 3 do
                    if routingTable.[i,j] <> -1 then
                        updateNested<-updateNested+1
                        let actorName=string routingTable.[i,j]
                        let pathToActor="akka://MySystem/user/" + actorName
                        let selectedActor= select pathToActor system
                        selectedActor<! UpdateSelf nodeID
            
            for i=0 to int temp do
                let nodeIDStr=string nodeID
                if i<=nodeIDStr.Length-1 then
                    routingTable.[i, (nodeIDStr.[i])|>charToInt]<-(-1)
           
            return! loop()

        | UpdateTableRow {TableRow=tableRow} ->
            let rowNum=tableRow.[4]
            for i=0 to 3 do
                if routingTable.[rowNum,i] = (-1) then
                    routingTable.[rowNum,i] <- tableRow.[i]
            return! loop()

        | UpdateSelf id -> 
            let isLessThanID = nodeID >  id
            match isLessThanID with
                | true->
                    if leafSmaller.Contains(id) = false then
                        if leafSmaller.Count < 4 then
                            leafSmaller<-leafSmaller.Add(id)
                        else if leafSmaller.MinimumElement < id then
                            leafSmaller<-leafSmaller.Remove(leafSmaller.MinimumElement)
                            leafSmaller<-leafSmaller.Add(id)

                | false->
                    if leafGreater.Contains(id) = false then
                        if leafGreater.Count < 4 then
                            leafGreater<-leafGreater.Add(id)
                        else if leafGreater.MaximumElement > id then
                            leafGreater<-leafGreater.Remove(leafGreater.MaximumElement)
                            leafGreater<-leafGreater.Add(id)
            let nodeStr=string id
            let nodeIDStr=string nodeID
            let mutable k = 0
            let mutable intAt1= (nodeStr.[k]|>charToInt)
            let mutable intAt2= (nodeIDStr.[k]|>charToInt)
            let gg= nodeIDStr.Length
            ()
            while ( k < nodeIDStr.Length-1 && k<>nodeStr.Length-1 && intAt1 = intAt2 ) do
                k<-k+1
                intAt1<- (nodeStr.[k]|>charToInt)
                intAt2<- (nodeIDStr.[k]|>charToInt)
                ()

            if routingTable.[k, (nodeIDStr.[k])|>charToInt]= (-1) then
                routingTable.[k, (nodeIDStr.[k])|>charToInt]<-id
            sender<! JoinDone "done"
            return! loop()


        |JoinDone d->     /////////////MASTER IS SENT A MESSAGE WHEN A NEW NODE COMPLETELY JOINS AND UPDATE THE NETWORK////////////
            updateNested<-updateNested-1
            if updateNested=0 then
                let aName= "master"
                let pp="akka://MySystem/user/" +  aName
                let bossref= select pp system
                bossref<! InitDone nodeID
            return! loop()
        |PrintInfo p->

            return! loop()
        |_-> 
            counter<-counter+1
            return! loop()


        return! loop()
    }
    loop()



let intToDigits value =
    let rec loop num digits =
        let q = num / 4
        let r = num % 4
        if q = 0 then 
            r :: digits 
        else 
            loop q (r :: digits)

    loop value []
let mutable actorsIDSet=Set.empty.Add(1)

let Master i j k (mailbox: Actor<_>) =
    let temp=ceil(Math.Log((float)numOfNodes,4.0))
    let IDrange=  (int) (Math.Pow(4.0,temp))
    let IDrangeEnd= (int) (Math.Pow(4.0,temp+1.0))
    let mutable nodeList= Array.create (numOfNodes+1) -1
    let mutable initDoneFor=0
    let mutable initialNodeList= Array.create (numForInit+1) -1
    let mutable secondNodeList= Array.create (numOfNodes-numForInit+1) -1
    let mutable itrSecond=0
    let mutable ii=1
    let mutable valAdd=0
    ()
    for i =1 to (numOfNodes-1) do            
        let base4List=intToDigits (IDrange+valAdd)
        let base4String=List.fold (fun str x -> str + x.ToString()) "" (base4List)
        let base4Int= int base4String
        if i <= numForInit then
            Array.set initialNodeList i base4Int
        else
            Array.set secondNodeList ii base4Int
            ii<-ii+1
        actorsIDSet<- actorsIDSet.Add(base4Int)
        valAdd<-valAdd+3

    for i=1 to numForInit do                  //////////////////INITAL NODES FROM WHICH NETWORK IS DEFINED/////////////////
        let actorName= string initialNodeList.[i]
        let actorRef =
            Node numOfNodes numOfReq initialNodeList.[i]
            |>spawn system actorName
        ()
    for i=1 to (secondNodeList.Length-2) do               
        let actorName= string secondNodeList.[i]
        let actorRef =
            Node numOfNodes numOfReq secondNodeList.[i]
            |>spawn system actorName
        ()

    let actName= "master"
    let p="akka://MySystem/user/" +  actName
    let selfRef= select p system

    let rec listen() =
        actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
      
            match message with
            | Initialize start -> 
                for i = 1 to numForInit do
                    let actorName=string initialNodeList.[i]
                    let pathToActor="akka://MySystem/user/" + actorName
                    let selectedActor= select pathToActor system
                    selectedActor<! ConnectionInit initialNodeList
            | NodeJoin ->                ////////// EACH NODE OF THE REMAINING 10% JOINS THE NETWORK ONE BY ONE/////////////
                if itrSecond=1 then
                    let selectedID= string initialNodeList.[numForInit]
                    let pathToActor="akka://MySystem/user/" + selectedID
                    let selectedActor= select pathToActor system 
                    selectedActor<! NewNodeJoin secondNodeList.[itrSecond]
                    ()
                else 
                    let selectedID= string secondNodeList.[itrSecond-1]
                    let pathToActor="akka://MySystem/user/" + selectedID
                    let selectedActor= select pathToActor system 
                    selectedActor<! NewNodeJoin secondNodeList.[itrSecond]
                    ()



            | LeafSmaller leafSmaller -> 
                printfn "Smaller Leaf%A" leafSmaller
            | LeafGreater leafGreater ->
                printfn "Greater Leaf%A" leafGreater
            | RoutingTable routingTable->
                printfn "routing Table%A" routingTable
            | InitDone nodeID->
                initDoneFor<-initDoneFor+1

                if(initDoneFor=numForInit) then
                    if initDoneFor >= (numOfNodes-1) then
                        selfRef<!SignalStartRouting "start"
                    else
                        itrSecond<-itrSecond+1
                        selfRef<!NodeJoin
                if initDoneFor>numForInit then
                    let actorName=string nodeID
                    let pathToActor="akka://MySystem/user/" + actorName
                    let selectedActor= select pathToActor system
                    selectedActor<!PrintInfo "print"
                    if initDoneFor=(numOfNodes-2) then

                        selfRef<!SignalStartRouting "start"
                    else
                        itrSecond<-itrSecond+1
                        selfRef<!NodeJoin 


            | SignalStartRouting sg->           /////////////SIGNAL TO START ROUTING//////////////

                for i=1 to numForInit do
                    let actorName=string initialNodeList.[i]
                    let path= "akka://MySystem/user/" + actorName
                    let selectedActor= select path system
                    let key=initialNodeList.[rand.Next()%numForInit+1]
                    selectedActor<!StartRouting initialNodeList
                System.Threading.Thread.Sleep(2000)
                for i=1 to (numOfNodes-numForInit-1) do
                    let actorName=string secondNodeList.[i]
                    let path= "akka://MySystem/user/" + actorName
                    let selectedActor= select path system
                    let key=initialNodeList.[rand.Next()%numForInit+1]

                    selectedActor<!StartRouting initialNodeList

            return! listen()
        }
    listen()
()

let boss = 
    Master 1 1 1
    |> spawn system "master"
boss<! Initialize "start the program"


let totalReq= float numOfNodes * float numOfReq
let threshold= 0.95 * float numOfNodes * float numOfReq

while float count < threshold do
    System.Threading.Thread.Sleep(100)
    if float count >= threshold then
        printfn "Average Hops For %i Nodes and %i Requests =%f" numOfNodes numOfReq (avghops/totalReq)
       

