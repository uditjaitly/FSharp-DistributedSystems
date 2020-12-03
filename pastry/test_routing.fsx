open System
let inline charToInt c = int c - int '0'

//let mutable nodeList = 
let mutable routingTable = 
        [| for i in 0 .. (int 8-1) do 
            yield [| for i in 0 ..8 do yield "-1" |] 
        |] |> array2D


let nodeList= [|"12348976";"12893476";"82345678";"18345677"|]


let fillRoutingTable (nodeList: string[], nodeID: string) = 
        for i=0 to (nodeList.Length-1) do
            let nodeStr=string nodeList.[i]
            let nodeIDStr=string nodeID
            let mutable k = 0
            let mutable intAt1= (nodeStr.[k]|>charToInt)
            let mutable intAt2= (nodeIDStr.[k]|>charToInt)
            let gg= nodeIDStr.Length
     
            while ( k < nodeIDStr.Length-1 && k<>nodeStr.Length-1 && intAt1 = intAt2 ) do
                k<-k+1
                intAt1<- (nodeStr.[k]|>charToInt)
                intAt2<- (nodeIDStr.[k]|>charToInt)
                ()



            printf "%i" k
            if routingTable.[k,nodeStr.[k]|>charToInt] = "-1" then
                routingTable.[k, nodeStr.[k]|>charToInt] <- nodeList.[i]


fillRoutingTable(nodeList,"12555555")

printfn "%A" routingTable