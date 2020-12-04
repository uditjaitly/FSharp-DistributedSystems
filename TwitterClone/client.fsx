#load "global.fsx"
#load "server.fsx"
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: System.Data.SqlClient" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open Global
open Server
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Diagnostics
open MathNet.Numerics.Distributions


let pathToServer="akka://MySystem/user/server"
let serverRef=select pathToServer Global.GlobalVar.system
let rand=System.Random()
module Users=
    let numUsers=1000
    let numSubscribers=2
    let input=System.Environment.GetCommandLineArgs()
    let mutable k=0
    let mutable counterUserRT=0
    let mutable myFollowersC :Map<int,Set<int>>=Map.empty
    let mutable countUser=0
    let mutable afterDisconnection=false
    let mutable disconnected=0
    let maxFollowers=(int)(0.2*(float)numUsers)
    let followersArray=Array.create numUsers 0
    let zInstance=new Zipf(1.0,maxFollowers)
    zInstance.Samples(followersArray)
    printfn "%A" followersArray
    printfn "%i" followersArray.[87]
    

    let User userName numTweets numSubscribe numUsers (mailbox: Actor<_>) =

        let mutable setOfSubscriptions =  Set.empty<int>
        let mutable tweetCount=1
        let pSelf="akka://MySystem/user/" + string userName
        let selfRef= select pSelf Global.GlobalVar.system
        let mutable myFeedTweets=List.empty<string>
        





        ////////////// REGISTER USER ///////
        //printfn "USER CREATED %A" serverRef
        serverRef<! Server.RegisterUser userName

        ////////Genererate to Subscribe/////////////////
        let generateToSubscribe (userName:int,numTweets:int,numSubscribe:int) = 
            for i =1 to followersArray.[userName-1] do
                if i<>userName then
                    let genFollow=(rand.Next()%numUsers)+1
                    setOfSubscriptions<-setOfSubscriptions.Add(genFollow)
                    if not (myFollowersC.ContainsKey(genFollow)) then
                        let tempSet=Set.empty.Add(userName)
                        myFollowersC<-myFollowersC.Add(genFollow,tempSet)
                    else
                        let mutable tS=myFollowersC.[genFollow]
                        tS<-tS.Add(userName)
                        myFollowersC<-myFollowersC.Add(genFollow,tS)
            //countUser<-countUser+1
            //printfn "%A" myFollowersC.[1].Count
            serverRef<! Server.MyFollowing (setOfSubscriptions,myFollowersC,userName)
            if userName=numUsers then
                printfn "HERESA"
                serverRef<! Server.MyFollowers(myFollowersC,userName)



        ///////// Generate Tweet by User//////////////////
        let generateTweet(userName:int)=
            for i=1 to numTweets do
                let tweet= "number " + string tweetCount + " tweet from user " + string userName
                tweetCount<-tweetCount+1
                serverRef<! Server.Tweet (userName,tweet)

            /////Tweet with HashTag/////////
            let tweet="I am user " + (string userName) + " and I think that Distributed Systems are fascinating! #COP5616isgreat"
            serverRef<! Server.Tweet (userName,tweet)

            ///// Tweet with Mention/////////
            let mutable randUserPick=rand.Next()%numUsers
            while randUserPick=userName do
                randUserPick<-rand.Next()%numUsers+1
            let tweet="I would like to mention user @" +  string randUserPick +  " because i am testing my application"
            serverRef<! Server.Tweet (userName,tweet)

        let generateRetweet(userName:int)= 
            //////////Retweet////////////
            serverRef<! Server.Retweet userName  
            //////////Retweet one of the tweet from follower's list//////////////
        
        let queryByUsername(userName:int)=
            ////// Query tweets of a random subscriber///////
            let selectedSub= Set.toArray(setOfSubscriptions).[rand.Next()%setOfSubscriptions.Count]
            serverRef<! Server.QueryByUsername (userName,selectedSub)

        let queryByHashtag(hashtagString:string) = 
            serverRef<! Server.QueryByHashtag (userName,hashtagString)

        let getWallFeed(userName)=
                printfn "User %i feed is: %A- was following %i users" userName myFeedTweets followersArray.[userName-1]
            

  



        let rec listen() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()

                match message with
                | Server.SendUpdate "user registered" ->
                    printfn "User has been registered"
                    generateToSubscribe (userName, numTweets, numSubscribe)

                | Server.SendUpdate "updated my followers" ->
                    selfRef<!Server.GenerateTweet userName
                | Server.GenerateTweet userName->
                    generateTweet(userName)
                    System.Threading.Thread.Sleep(5000)
                    generateRetweet(userName)
                

                | Server.TweetUpdate (userName,tweet)->
                    let mutable temp=myFeedTweets
                    temp<- [tweet] |> List.append temp
                    myFeedTweets<-temp
                // | Server.SendUpdate "done tweeting"->
                //      queryByUsername(userName)
                | Server.SendUpdate "retweet complete for user"->
                    counterUserRT<-counterUserRT+1
                    if counterUserRT = (int) ((float)numUsers*0.95) then
                        let chosenUser=88
                        let path="akka://MySystem/user/" + string chosenUser
                        let chosenUserRef=select path Global.GlobalVar.system
                        chosenUserRef<!Server.SelectedUserToObserve chosenUser
                    printfn "%i" counterUserRT
                    
                    if counterUserRT = (int) ((float) numUsers * 1.4) then
                        let chosenUser=88
                        
                        serverRef<!Server.Login chosenUser

                        
      
                | Server.SelectedUserToObserve chosenUser ->
                    queryByUsername (userName)
                | Server.QueryReplyOfUsername (userName,subUserName,queryData)->
                    //printfn "User %i queried User %i tweets which are:%A" userName subUserName queryData
                    queryByHashtag ("#COP5616isgreat")
                | Server.QueryReplyOfHashtag (userName,hashtagString,queryData)->
                    //printfn "User %i queried hashtag %s and results are:%A" userName hashtagString queryData
                    //System.Threading.Thread.Sleep(4000)
                    getWallFeed(userName)
                    //System.Threading.Thread.Sleep(2000)
                    serverRef<!Server.WallUpdated userName
                | Server.DisconnectMe userName->
                    //printfn "User disconnected is=%i" userName
                    disconnected<-disconnected+1
                    if disconnected>=numUsers/2 then
                       
                        serverRef<!Server.TweetAliveUsers

                | Server.TweetUpdateAfterLogin (userName,pendingTweets)->
                    let mutable temp=myFeedTweets
                    temp<- pendingTweets |> List.append temp
                    myFeedTweets<-temp
                    selfRef<!SelectedUserToObserve userName

                    
                        
                





                return! listen()

            }
        listen()