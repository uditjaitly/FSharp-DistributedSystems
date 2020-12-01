#load "global.fsx"
#load "server.fsx"
#r "nuget: System.Data.SqlClient" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open Global
open Server
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics


let pathToServer="akka://MySystem/user/server"
let serverRef=select pathToServer Global.GlobalVar.system
let rand=System.Random()
module Users=
    let numUsers=10
    let numSubscribers=5
    let input=System.Environment.GetCommandLineArgs()
    let k=10
    let mutable counterUserRT=0
   


    let User userName numTweets numSubscribe numUsers (mailbox: Actor<_>) =

        let mutable setOfSubscriptions =  Set.empty<int>
        let pSelf="akka://MySystem/user/" + string userName
        let selfRef= select pSelf Global.GlobalVar.system
        let mutable myFeedTweets=List.empty<string>
        





        ////////////// REGISTER USER ///////
        printfn "USER CREATED %A" serverRef
        serverRef<! Server.RegisterUser userName

        ////////Genererate to Subscribe/////////////////
        let generateToSubscribe (userName:int,numTweets:int,numSubscribe:int) = 
            for i =1 to numSubscribe do
                if i<>userName then
                    setOfSubscriptions<-setOfSubscriptions.Add(i)
            serverRef<! Server.MyFollowing (setOfSubscriptions,userName)

        ///////// Generate Tweet by User//////////////////
        let generateTweet(userName:int)=
            for i=1 to numTweets do
                let tweet= "number " + string i + " tweet from user " + string userName
                serverRef<! Server.Tweet (userName,tweet)

            /////Tweet with HashTag/////////
            let tweet="I am user " + (string userName) + " and I think that Distributed Systems are fascinating! #COP5616isgreat"
            serverRef<! Server.Tweet (userName,tweet)

            ///// Tweet with Mention/////////
            let mutable randUserPick=rand.Next()%numUsers
            while randUserPick=userName do
                randUserPick<-rand.Next()%numUsers
            let tweet="I would like to mention user @" +  string randUserPick +  " because i am testing my application"
            serverRef<! Server.Tweet (userName,tweet)

            System.Threading.Thread.Sleep(5000)
            //////////Retweet////////////
            serverRef<! Server.Retweet userName  //////////Retweet one of the tweet from follower's list//////////////
        
        let queryByUsername(userName:int)=
            ////// Query tweets of a random subscriber///////
            let selectedSub= Set.toArray(setOfSubscriptions).[rand.Next()%setOfSubscriptions.Count]
            serverRef<! Server.QueryByUsername (userName,selectedSub)

        let queryByHashtag(hashtagString:string) = 
            serverRef<! Server.QueryByHashtag (userName,hashtagString)

       

        let rec listen() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()

                match message with
                | Server.SendUpdate "user registered" ->
                    printfn "User has been registered"
                    generateToSubscribe (userName, numTweets, numSubscribe)

                | Server.SendUpdate "updated my following" ->
                    generateTweet(userName)
                | Server.TweetUpdate (userName,tweet)->
                    let mutable temp=myFeedTweets
                    temp<- [tweet] |> List.append temp
                    myFeedTweets<-temp
                | Server.SendUpdate "done tweeting"->
                    queryByUsername(userName)
                | Server.SendUpdate "retweet complete for user"->
                    counterUserRT<-counterUserRT+1
                    if counterUserRT = numUsers then
                        queryByUsername (userName)
                | Server.QueryReplyOfUsername (userName,subUserName,queryData)->
                    //printfn "User %i queried User %i tweets which are:%A" userName subUserName queryData
                    queryByHashtag ("#COP5616isgreat")
                | Server.QueryReplyOfHashtag (userName,hashtagString,queryData)->
                    //printfn "User %i queried hashtag %s and results are:%A" userName hashtagString queryData
                    printfn "User %i feed is: %A" userName myFeedTweets
                    


                return! listen()
                        
            }
        listen()