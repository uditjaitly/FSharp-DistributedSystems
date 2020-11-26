#load "global.fsx"
#load "server.fsx"
#load "client.fsx"
#time "on"
#r "nuget: System.Data.SqlClient" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
open Client
open Server
open Global
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics
let numUsers=10
let numSubscribers=5 

printfn "%i" Client.Users.numUsers
//let system = System.create "MySystem" (Configuration.defaultConfig())

let input=System.Environment.GetCommandLineArgs()

let Master (mailbox: Actor<_>) =

    ///// SPAWN SERVER INSTANCE NAMED SERVER/////////
    let serverRef=
        Server.HostingServer.Server 
        |> spawn Global.GlobalVar.system "server"
    System.Threading.Thread.Sleep(2000)

    ////// SPAWNING USERS////////////////////
    for i=1 to numUsers do
        let userName=string i
        let numTweets=5
        let numSubscribe=7
        let userName=string i
        let actorRef =
            Client.Users.User i numTweets numSubscribe numUsers
            |> spawn Global.GlobalVar.system userName
        ()
            
    let rec listen() =
        actor {
            let! message = mailbox.Receive()
            let sender=mailbox.Sender()
            match message with
            |_-> printfn "RECEIEVED"
            return! listen()
        } 
    listen()

let boss = 
    Master
    |> spawn Global.GlobalVar.system "master"