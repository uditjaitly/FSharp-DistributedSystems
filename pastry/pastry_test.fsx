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

type Command = 
    | Initialize of String
    | ConnectionInit of int[] 
    | GossipSelf of string
    | LeafSmaller of Set<int>
    | LeafGreater of Set<int>
    | RoutingTable of int[,]
let inline charToInt c = int c - int '0'

let Node numOfNodes numOfReq nodeID (mailbox: Actor<_>) = 
    let temp=ceil(Math.Log((float)numOfNodes,4.0))
    
    let L=8
    let mutable routingTable = 
        [| for i in 0 .. (int temp-1) do 
            yield [| for i in 0 ..3 do yield -1 |] 
        |] |> array2D
    let IDrange=  (int) (Math.Pow(4.0,temp))
    let mutable leafGreater= Set.empty.Add(1)
    let mutable leafSmaller= Set.empty.Add(1)
    leafGreater<-leafGreater.Remove(1)
    leafSmaller<-leafSmaller.Remove(1)


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

 
    let fillRoutingTable (nodeList: int[], nodeID: int) = 
        for i=1 to (nodeList.Length-1) do
            let nodeStr=string nodeList.[i]
            let nodeIDStr=string nodeID
            let mutable k = 0
           
            while ((nodeStr.[k]|>charToInt) = (nodeIDStr.[k]|>charToInt)) && ((k <> nodeIDStr.Length)) do
                k<-k+1
            //if routingTable.[k,nodeStr.[k]|>charToInt] = -1 then
              //  routingTable.[k, nodeStr.[k]|>charToInt] <- nodeList.[i]
                
            
                
    ()
            
            
            


    actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with 
        | ConnectionInit initialNodeList -> 
            
            createLeafSet(initialNodeList,nodeID)
            fillRoutingTable(initialNodeList,nodeID)
            if nodeID=10122 then
                sender<! LeafSmaller leafSmaller
                sender<! LeafGreater leafGreater
                sender<! RoutingTable routingTable
            
        sender <! 1
    }



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
    let mutable nodeList= Array.create (numOfNodes+1) -1
    let numForInit=800
    let mutable initialNodeList= Array.create (numForInit+1) -1

  

    for i =1 to numForInit do
        let base4List=intToDigits i
        let base4String=List.fold (fun str x -> str + x.ToString()) "" (base4List)
        let base4Int= int base4String
        Array.set initialNodeList i base4Int
        actorsIDSet<- actorsIDSet.Add(base4Int)

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
            match message with
            | Initialize start -> 
                for i = 1 to numForInit do
                    let actorName=string initialNodeList.[i]
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


            return! listen()
        }
    listen()
()

let boss = 
    Master 1 1 1
    |> spawn system "master"
boss<! Initialize "start the program"
