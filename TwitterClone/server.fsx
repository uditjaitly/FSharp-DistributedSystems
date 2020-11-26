#load "global.fsx"

#r "nuget: System.Data.SqlClient" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
open Global
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

type Command=
    | RegisterUser of int

module HostingServer=
    let mutable registry = Set.empty<int>
    
    let Server (mailbox: Actor<_>) =
        printfn "SERVER STARTED"
        
        let rec listen() =
            actor {
                
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match message with 
                | RegisterUser ints->
                    // registry<-registry.Add(2)
                     sender<! "user registered"
                     return! listen()
                   
                |_-> printfn "Unknown case"

                return! listen()
            }
        listen()