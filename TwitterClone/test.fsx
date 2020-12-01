open System

let mutable registry :Map<int,bool>=Map.empty

registry<-registry.Add(1,false)
registry<-registry.Add(1,true)
printfn "%A" registry
