#r "nuget:MathNet.Numerics.FSharp"
open System
open MathNet.Numerics.Distributions
let mutable registry :Map<int,List<string>>=Map.empty

let l=["dsaf" ; "dsaff"]
registry<-registry.Add(1,l)
registry<-registry.Add(1,[])
printfn "%A" registry

let array=Array.create 100 0
let z=new Zipf(1.0,9)
z.Samples(array)