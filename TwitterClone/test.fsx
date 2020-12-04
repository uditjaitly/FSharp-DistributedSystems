open System

let mutable registry :Map<int,List<string>>=Map.empty

let l=["dsaf" ; "dsaff"]
registry<-registry.Add(1,l)
registry<-registry.Add(1,[])
printfn "%A" registry
