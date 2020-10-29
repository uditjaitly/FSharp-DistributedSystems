// Learn more about F# at http://fsharp.org

#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Security.Cryptography
let y = SHA256.Create()
let mutable nodeId=""
let numOfNodes=100
let nodelist=1

let ByteToHex bytes = 
    bytes 
    |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
    |> String.concat System.String.Empty

let mutable k="s"
for i=1 to 10 do
    k<-(string) i 
    let b = y.ComputeHash(System.Text.Encoding.ASCII.GetBytes k)
    //printf "%A" b
    nodeId <- ByteToHex b
    nodeId <- nodeId.[0..8]
    let n = Convert.ToUInt64(nodeId,16)
    printfn "%i" n
    ()



let Slave (mailbox: Actor<_>) =
    actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with
        | (a,b) ->

        sender <! 1
    }


let NodeRef =
    Slave actorID algo
    |> spawn system actorName
