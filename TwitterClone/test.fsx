open System

let mutable iAmFollowing :Map<int,Set<int>>=Map.empty

let k=Set.empty.Add(1).Add(2).Add(3)

iAmFollowing<-iAmFollowing.Add(1,k)
iAmFollowing<-k
let mutable m=k
m<-m.Add(55)
iAmFollowing<-iAmFollowing.Add(1,m)
printfn "%A" iAmFollowing
iAmFollowing.ContainsKey(1)
iAmFollowing.[1]=m