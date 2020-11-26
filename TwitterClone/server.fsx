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
    | MyFollowing of Set<int>*int
    | Tweet of int*string
module HostingServer=
    let mutable registry = Set.empty<int>
    let mutable iAmFollowing :Map<int,Set<int>>=Map.empty
    let mutable tweets :Map<int,List<string>>=Map.empty
    let mutable hashTags :Map<string,List<string>>=Map.empty
    let Server (mailbox: Actor<_>) =
        printfn "SERVER STARTED"
        
    //////////Update following list//////////
        let updateIAmFollowing (userName:int,subs:Set<int>) =
            
            iAmFollowing<-iAmFollowing.Add(userName,subs)
            //printfn "%A" iAmFollowing
    //////////Update Tweet Record//////////////
        let updateTweetRecord(userName:int,tweet:string) =
            if not (tweets.ContainsKey(userName)) then
                let temp=[tweet]
                tweets<-tweets.Add(userName,temp)

            else
                let mutable temp=tweets.[userName]
                temp<- [tweet] |> List.append temp
                tweets<-tweets.Add(userName,temp)
            //////////////////HANDLE HASHTAGS//////////////////
            if tweet.IndexOf "#" <> -1 then
                let mutable i=tweet.IndexOf "#"
                let starting=i
                while i<tweet.Length && tweet.[i]<> ' ' do
                    i<-i+1
                let hashTag=tweet.[starting..i-1]
                if not (hashTags.ContainsKey(hashTag)) then
                    hashTags<-hashTags.Add(hashTag,[tweet])
                else
                    let mutable temp=hashTags.[hashTag]
                    temp<-[tweet] |> List.append temp
                    hashTags<-hashTags.Add(hashTag,temp)


            //printfn "%A" tweets
            printfn "%A" hashTags



        let rec listen() =
            actor {
                
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match message with 
                | RegisterUser user->
                     registry<-registry.Add(user)
                     sender<! "user registered"
                     printfn "%A" registry
                     return! listen()
                     
                   
                | MyFollowing (setOfSubscriptions,userName) ->
                    updateIAmFollowing(userName,setOfSubscriptions)
                    sender<! "updated my following"
                |Tweet (userName,tweet) ->
                    updateTweetRecord(userName,tweet)


                return! listen()
            }
        listen()