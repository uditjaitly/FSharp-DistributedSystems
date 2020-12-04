#load "global.fsx"

#r "nuget: System.Data.SqlClient" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
open Global
open Akka.Actor
open Akka.Configuration
open Akka.FSharp


let rand=System.Random()
type Command=
    | RegisterUser of int
    | SendUpdate of string
    | MyFollowing of Set<int>*Map<int,Set<int>>*int
    | MyFollowers of Map<int,Set<int>>*int
    | Tweet of int*string
    | GenerateTweet of int
    | Retweet of int
    | QueryByUsername of int*int
    | QueryReplyOfUsername of int*int*List<string>
    | QueryByHashtag of int*string
    | QueryReplyOfHashtag of int*string*List<string>
    | TweetUpdate of int*string
    | Wallfeed of int*List<string>
    | DisconnectMe of int
    | Login of int
    | TweetUpdateAfterLogin of int*List<string>
    | SelectedUserToObserve of int
    | WallUpdated of int
    | TweetAliveUsers
module HostingServer=
    let join (p:Map<'a,'b>) (q:Map<'a,'b>) = 
        Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])
    let mutable registry :Map<int,bool>=Map.empty
    let mutable iAmFollowing :Map<int,Set<int>>=Map.empty
    let mutable myFollowers :Map<int,Set<int>>=Map.empty
    let mutable tweets :Map<int,List<string>>=Map.empty
    let mutable hashTags :Map<string,List<string>>=Map.empty
    let mutable queryData :List<string>=List.empty
    let mutable pendingTweets : Map<int, List<string>>= Map.empty
    let mutable disconnectedUsers=0
    
    let Server numUsers (mailbox: Actor<_>) =
        let mutable initComplete=false
        printfn "SERVER STARTED"
    //////////////Update My followers map//////
        let updateMyFollowers  (username:int,myFollowersC:Map<int,Set<int>>)=
            myFollowers<-myFollowersC
            //printfn "%A" myFollowers.[1].Count

           


    //////////Update My following map//////////
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
            /////////////HANDLE MENTIONS////////////////
            if tweet.IndexOf "@" <> -1 then
                let mutable i=tweet.IndexOf "@"
                let starting=i
                while i<tweet.Length && tweet.[i]<> ' ' do
                    i<-i+1
                let mentionedUser=int tweet.[starting+1..i-1]
                if registry.ContainsKey(mentionedUser) then /////IF MENTIONED USER IS REGISTERED////////
                    if not(tweets.ContainsKey(mentionedUser)) then
                        let temp=[tweet]
                        tweets<-tweets.Add(mentionedUser,temp)
                    else
                        let mutable temp=tweets.[mentionedUser]
                        temp<-[tweet] |> List.append temp
                        tweets<-tweets.Add(mentionedUser,temp)

            //////////Send tweet to the people who are following me and are online///////////////
            if myFollowers.ContainsKey(userName) then
                for sub in myFollowers.[userName] do
                    if registry.[sub]=true then
                        let pathToUser="akka://MySystem/user/"+ string sub
                        let userRef=select pathToUser Global.GlobalVar.system
                        userRef<! TweetUpdate (userName,tweet)
                    else 
                        //printfn "HERESAY"
                        if not(pendingTweets.ContainsKey(sub)) then              ///If not online store in pending tweets/////
                            let temp=[tweet]
                            pendingTweets<-pendingTweets.Add(sub,temp)
                        else
                            let mutable temp=pendingTweets.[sub]
                            temp<-[tweet] |> List.append temp
                            pendingTweets<-pendingTweets.Add(sub,temp)
                    //printfn "%i" sub
            //printfn "%A" tweets
            //printfn "%A" hashTags

        ///////////////HANDLE RETWEETS////////////////
        let doRetweet(username:int) =
            if iAmFollowing.ContainsKey(username) && iAmFollowing.[username].Count>0 then
                
                let followSet=iAmFollowing.[username]
                let followArray=Set.toArray(followSet)
                
                let selectedUserToRt=followArray.[rand.Next()%followArray.Length]
                if(tweets.ContainsKey(selectedUserToRt)) then
                    let userTweets=tweets.[selectedUserToRt]

                    let selectedTweetToRt=userTweets.[rand.Next()%userTweets.Length]
                    let modifiedRt=selectedTweetToRt + " ---Retweet by user " + string username
                    if myFollowers.ContainsKey(username) then
                        for sub in myFollowers.[username] do
                            if registry.[sub]=true then                         //////// Check if the user is online, if yes, send tweet///
                                let pathToUser="akka://MySystem/user/"+ string sub
                                let userRef=select pathToUser Global.GlobalVar.system
                                userRef<! TweetUpdate (username,modifiedRt)
                            else
                                
                                if not(pendingTweets.ContainsKey(sub)) then              ///If not online store in pending tweets/////
                                    let temp=[modifiedRt]
                                    pendingTweets<-pendingTweets.Add(sub,temp)
                                else
                                    let mutable temp=pendingTweets.[sub]
                                    temp<-[modifiedRt] |> List.append temp
                                    pendingTweets<-pendingTweets.Add(sub,temp)









                    if not(tweets.ContainsKey(username)) then
                                let temp=[modifiedRt]
                                tweets<-tweets.Add(username,temp)
                             else
                                let mutable temp=tweets.[username]
                                temp<-[modifiedRt] |> List.append temp
                                tweets<-tweets.Add(username,temp)
            //printfn "%A" tweets

        /////////////HANDLE QUERY BY USERNAME///////////////
        let handleQueryByUsername(myUsername:int,subUsername:int)=
            if tweets.ContainsKey(subUsername) then
                queryData<-tweets.[subUsername]
        
        /////////////HANDLE QUERY BY HASHTAG//////
        let handleQueryByHashtag(userName:int,hashtagString:string)=
            if hashTags.ContainsKey(hashtagString) then
                queryData<-hashTags.[hashtagString]
            else
                queryData<-["No tweets found containing the specified hashtag"]

        //////////////HANDLE LOGIN////////////////
        let handleLogIn(userName:int)=
            if pendingTweets.ContainsKey(userName) then
                let pathToUser="akka://MySystem/user/"+ string userName
                let userRef=select pathToUser Global.GlobalVar.system
                userRef<! TweetUpdateAfterLogin (userName,pendingTweets.[userName])
                pendingTweets<-pendingTweets.Add(userName,[])

        let rec listen() =
            actor {
                
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match message with 
                | RegisterUser user->
                     registry<-registry.Add(user,true)

                     sender<! SendUpdate "user registered"
                     //printfn "%A" registry
                     return! listen()
                     
                   
                | MyFollowing (setOfSubscriptions,myFollowersC,userName) ->
                    
                        //let k= updateMyFollowers(userName,myFollowersC) 
                        let m=updateIAmFollowing(userName,setOfSubscriptions)
                        ()
                        //sender<! SendUpdate "updated my following"
                | MyFollowers (myFollowersC,userName) ->
                        myFollowers<-myFollowersC
                        for i = 1 to numUsers do
                                let pathUser="akka://MySystem/user/" + string i
                                let userRef=select pathUser Global.GlobalVar.system
                                userRef<! SendUpdate "updated my followers"

                |Tweet (userName,tweet) ->
                    updateTweetRecord(userName,tweet)
                | Retweet username->
                    doRetweet(username)
                    sender<! SendUpdate "retweet complete for user"
                | QueryByUsername (myUsername,subUsername) ->
                    handleQueryByUsername(myUsername,subUsername)
                    sender<! QueryReplyOfUsername (myUsername,subUsername,queryData)
                | QueryByHashtag (userName,hashtagString)->
                    handleQueryByHashtag (userName,hashtagString)
                    sender<! QueryReplyOfHashtag(userName,hashtagString,queryData)
                | DisconnectMe userName ->
                    registry<-registry.Add(userName,false)
                    disconnectedUsers<-disconnectedUsers+1
                    printfn "%A" registry
                    if(disconnectedUsers>=numUsers/2) then
                        //printfn "%i" disconnectedUsers
                        sender<! SendUpdate "disconnection of half users completed"
                | Login userName->
                    registry<-registry.Add(userName,true)
                    
                    printfn "User which logged in is :%i" userName
                    handleLogIn(userName)
                    printfn "%A" registry
                | WallUpdated userName->
                    printfn "ack received"
                    
                    for i = 1 to numUsers do
                        if i%2 = 0 then
                            let pathUser="akka://MySystem/user/" + string i
                            let userRef=select pathUser Global.GlobalVar.system
                            userRef<! DisconnectMe i
                            registry<-registry.Add(i,false)
                | TweetAliveUsers ->
                    for i=1 to numUsers do
                        if i%2<>0 then
                            let pathUser="akka://MySystem/user/" + string i
                            let userRef=select pathUser Global.GlobalVar.system
                            userRef<!GenerateTweet i










                return! listen()
            }
        listen()
