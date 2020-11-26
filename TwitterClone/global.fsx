open System
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics
module GlobalVar=
    let system = System.create "MySystem" (Configuration.defaultConfig())

