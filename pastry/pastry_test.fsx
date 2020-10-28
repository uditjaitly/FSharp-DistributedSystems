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
    | Gossip of string
    | GossipSelf of string

let Node numOfNodes numOfReq nodeList (mailbox: Actor<_>) = 
    let temp=ceil(Math.Log((float)numOfNodes,4.0))
    let routingTable=Array2D.init 3 3
    let IDrange=  (int) (Math.Pow(4.0,temp))

    actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with 
        | (a,b) -> 
            
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
let Master (mailbox: Actor<_>) =
    let temp=ceil(Math.Log((float)numOfNodes,4.0))
    let IDrange=  (int) (Math.Pow(4.0,temp))
    let mutable nodeList= Array.create (numOfNodes+1) -1
    let numForInit=800
    let mutable initialNodeList= Array.create (numForInit+1) -1
    
  

    for i =1 to numForInit do
        let base4List=intToDigits (rand.Next()%IDrange)
        let base4String=List.fold (fun str x -> str + x.ToString()) "" (base4List)
        let base4Int= int base4String
        Array.set initialNodeList i base4Int
        actorsIDSet<- actorsIDSet.Add(base4Int)
    printfn "%A" initialNodeList
    printfn "%i" actorsIDSet.Count
    for i=1 to numForInit do
        let actorName= string nodeList.[i]
        let actorRef =
            Node numOfNodes numOfReq nodeList.[i]
            |>spawn system actorName
        ()



    let rec listen() =
        actor {
            let! message = mailbox.Receive()
            match message with
            | Initialize start -> 
                for i = 0 to numForInit do
                    let actorName=string nodeList.[i]
                    let pathToActor="akka://MySystem/user/" + actorName
                    let selectedActor= select pathToActor system
                    ()


            return! listen()
        }
    listen()
