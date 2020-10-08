#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics

//////////////////////////////Initialization and input processing/////////////////////////////////////
let system = System.create "MySystem" (Configuration.defaultConfig())
let input=System.Environment.GetCommandLineArgs()
let n=int input.[3]
let k1=int input.[4]
let bigint (x:int) = bigint(x)
let mutable k= k1 |> bigint
let mutable inc = 0


/////////////////////////Algorithm that computes the perfect square problem///////////////////////////
let check message = 
    let mutable res=0 |> bigint
    let mutable msg = message |> bigint
    let mutable i=msg
    let mutable increment = 1 |> bigint
    while (i<(msg+k)) do
        res <- res+ (i*i)
        i<- i + increment
    let mutable mid= 1 |> bigint
    let mutable midSquare = 1 |> bigint
    let one = 1 |> bigint
    let two = 2 |> bigint
    let perfect_square number = 
        let rec binary_search (lowLimit:bigint, highLimit:bigint) =
            mid <- ((highLimit + lowLimit) / two)
            midSquare <- mid * mid
            
            if lowLimit > highLimit then false
            elif number = midSquare then true
            else if number < midSquare then binary_search (lowLimit,(mid-increment))
            else binary_search ((mid + increment),highLimit)
        binary_search (one,res)

    let k = perfect_square res
    if k = true then printfn "%i" message

////////////////////////////////////Worker Actor/////////////////////////////////////////////////////
let Slave (mailbox: Actor<_>) = 
    actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with 
        | (a,b) -> 
            for i=a to b do
                check i
        sender <! 1
    }


////////////////////Supervisor actor that spawns worker actors///////////////////////////////////////

let Master (mailbox: Actor<_>) =
    let actors = Array.create (11) (spawn system "Slave" Slave)
    {1..10} |> Seq.iter (fun f ->
        actors.[f] <- spawn system (string f) Slave
        ()
    )


    /////////////////////////////Work assigned to worker actors/////////////////////////////////////
    let work_per_actor=n/10
    let mutable starting_range= 0
    let mutable ending_range = 0
    for i=1 to 10 do
        starting_range <- 1+(work_per_actor*(i-1))
        if i<10 then
            ending_range <- i*work_per_actor
            actors.[i] <! (starting_range,ending_range)            
        else
            ending_range <- n
            actors.[i] <! (starting_range,ending_range)    
    let rec listen() =
        actor {
            let! message = mailbox.Receive()
            match message with
            | 1 -> 
                inc<-inc+1 
            
              
            return! listen()
        } 
    listen()



let boss = spawn system "master" Master
let mutable r=0
while inc<9 do
    r<-r+1
#time "off"