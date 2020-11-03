#time "on"
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
let numOfNodes=10000
let numOfReq=10
let mutable avghops=0.0
let mutable count=0
type RouteMessage = {RouteMessage:List<int>}
type Command = 
    | Initialize of String
    | ConnectionInit of int[] 
    | GossipSelf of string
    | LeafSmaller of Set<int>
    | LeafGreater of Set<int>
    | InitDone of int
    | RoutingTable of int[,]
    | StartRouting of int
    | Route of RouteMessage
    | NodeJoin
let inline charToInt c = int c - int '0'

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




let Node numOfNodes numOfReq nodeID (mailbox: Actor<_>) = 
    let temp=ceil(Math.Log((float)numOfNodes,4.0))
    let L=8
    let mutable routingTable = 
        [| for i in 0 .. (int temp) do 
            yield [| for i in 0 ..3 do yield -1 |] 
        |] |> array2D
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

    let foundInLeaf (message:List<int>)=
        let key=message.[0]
        let hops=message.[1]
        //printfn "HERE"
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
                 //   printfn "INHERE"
                    
                    
                 

        else if (keyDecimal<=toDecimal (string leafGreater.MaximumElement) && keyDecimal>=nodeIDDecimal && leafGreater.Count <> 0) then
            for leaf in leafGreater do
                let t = Math.Abs(toDecimal(string leaf) - keyDecimal)
                if(t<min) then
                    closestNode<-leaf
                    min<-t
                  //  printfn "INHERE"
                    

        if min<>99999 && Math.Abs(nodeIDDecimal-keyDecimal)<=min then 
            printfn "DESTINATION REACHED IN HOPS %i" hops
            count<-count+1
            avghops<-(float) hops+avghops
            flag<-1

            //////IMPLEMENT CODE TO UPDATE MASTER//////
            
        else if min <> 99999 && Math.Abs(nodeIDDecimal-keyDecimal)>min then
            //printfn "HERE"
            let actorName=string closestNode
            flag<-1
            let pathToActor="akka://MySystem/user/" + actorName
            let nextNode= select pathToActor system
            printfn "Message of destination %i received by %i to be sent to %i" key nodeID closestNode
            nextNode<! Route {RouteMessage = [key;hops+1]} 
            

        if flag = 1 then 
            true
        else 
            false



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
            printfn "Message of destination %i received by %i to be sent to %i via routing table" key nodeID routingTable.[k,nodeStr.[k]|>charToInt]
            nextNode<! Route {RouteMessage = [key;hops+1]} 
            true
        else
            false




 
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
                 

            //printf "%i" k
            if routingTable.[k,nodeStr.[k]|>charToInt] = -1 then
                 routingTable.[k, nodeStr.[k]|>charToInt] <- nodeList.[i]

    ()

    let rec loop n = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with 
        | ConnectionInit initialNodeList -> 
            let initialNodeList = initialNodeList|>Array.filter((<>)nodeID)
            myNodeList<-initialNodeList
            createLeafSet(initialNodeList,nodeID)
            //printfn "%A" leafSmaller
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
        | StartRouting key -> 

            selfRef <! Route {RouteMessage = [key;hops]}
            return! loop()
        | Route {RouteMessage=message}->
            if foundInLeaf(message) then
           //     printfn "Here"// DESTINATION REACHED/////
                printfn "%i= count" count
                
            else if foundInRoutingTable(message) then
                printfn "sent through routing table"
            else
                printfn "Not found"
            return! loop()

        
        |_-> printfn "HHere"


        sender <! 1
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

let rand = System.Random()
let Master i j k (mailbox: Actor<_>) =
    let temp=ceil(Math.Log((float)numOfNodes,4.0))
    let IDrange=  (int) (Math.Pow(4.0,temp))
    let IDrangeEnd= (int) (Math.Pow(4.0,temp+1.0))
    let mutable nodeList= Array.create (numOfNodes+1) -1
    let numForInit=9000
    let mutable initialNodeList= Array.create (numForInit+1) -1

  
    let mutable valAdd=0
    ()
    for i =1 to numForInit do
        let base4List=intToDigits (IDrange+valAdd)
        let base4String=List.fold (fun str x -> str + x.ToString()) "" (base4List)
        let base4Int= int base4String
        Array.set initialNodeList i base4Int
        actorsIDSet<- actorsIDSet.Add(base4Int)
        valAdd<-valAdd+3

    printfn "%A" initialNodeList
    printfn "%i" actorsIDSet.Count
    for i=1 to numForInit do                  ///Spawn all nodes, however, only numForInit will participate for now/////
        let actorName= string initialNodeList.[i]
        let actorRef =
            Node numOfNodes numOfReq initialNodeList.[i]
            |>spawn system actorName
        printfn "Actor Created with ID = %s" actorName
        ()



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
                    //printf "%A" initialNodeList
                    selectedActor<! ConnectionInit initialNodeList
            | NodeJoin ->
                let actorName=string (IDrange+valAdd*9000)
                let pathToActor="akka://MySystem/user/" + actorName
                let selectedActor= select pathToActor system
                //printf "%A" initialNodeList
                selectedActor<! ConnectionInit initialNodeList

            | LeafSmaller leafSmaller -> 
                printfn "Smaller Leaf%A" leafSmaller
            | LeafGreater leafGreater ->
                printfn "Greater Leaf%A" leafGreater
            | RoutingTable routingTable->
                printfn "routing Table%A" routingTable
            | InitDone nodeID->
                    sender<!StartRouting initialNodeList.[rand.Next()%numForInit+1]
                    printfn "%A" sender
                   // let actorName=string nodeID
                //    let path= "akka://MySystem/user/" + actorName
               //     let selectedActor= select path system
              //      let key=initialNodeList.[rand.Next()/numForInit]
              

    
                    //selectedActor<!StartRouting key

            return! listen()
        }
    listen()
()

let boss = 
    Master 1 1 1
    |> spawn system "master"
boss<! Initialize "start the program"

printfn "%i= count" count
printfn "avghops= %f" (avghops/7000.0)