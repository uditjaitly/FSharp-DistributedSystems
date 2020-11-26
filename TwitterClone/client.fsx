#load "global.fsx"
#load "server.fsx"
#r "nuget: System.Data.SqlClient" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open Global
open Server
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics


let pathToServer="akka://MySystem/user/server"
let serverRef=select pathToServer Global.GlobalVar.system

module Users=
    let numUsers=10
    let numSubscribers=5
    let input=System.Environment.GetCommandLineArgs()
    let k=10
    



    let User userName numTweets numSubscribe (mailbox: Actor<_>) =
        ////////////// REGISTER USER ///////
        printfn "USER CREATED %A" serverRef
        serverRef<! Server.RegisterUser 1
        let rec listen() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()

                match message with
                | _ ->
                    printfn "User has been registered"
                return! listen()
                        
            }
        listen()