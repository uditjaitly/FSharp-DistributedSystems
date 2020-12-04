open System
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
//#r "nuget: MathNet.Numerics.Distributions"
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
//open MathNet.Numerics.Distributions
open System.Diagnostics
module GlobalVar=
    let system = System.create "MySystem" (Configuration.defaultConfig())

