open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Net

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open System.Text
open FSharp.Json

 let system = System.create "MySystem" (Configuration.defaultConfig())
type Command=
    | RegisterUser of int*string*WebSocket
    | SendUpdate of string
    | MyFollowing of Set<int>*int
    | MyFollowers of Set<int>*int
    | HandleTweet of int*string
    | HandleLogin of int*string
    | GetWallFeed of int
    | Tweet of int*string
    | HandleLogout of int
    | UpdateFollow of int*int
    | GenerateTweet of int
    | Retweet of int*int
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
let pathToSelf="akka://MySystem/user/server"
let userRef=select pathToSelf system
let join (p:Map<'a,'b>) (q:Map<'a,'b>) = 
    Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])
let mutable registry :Map<int,bool>=Map.empty
let mutable numberAndPassword :Map<int,string>=Map.empty 
let mutable numberAndWebsocket :Map<int,WebSocket>=Map.empty
let mutable tweetIDAndTweet :Map<int,string>=Map.empty
let mutable userNumbers: List<int>=List.empty
let mutable webSockets: List<WebSocket>=List.empty
let mutable iAmFollowing :Map<int,Set<int>>=Map.empty
let mutable myFollowers :Map<int,Set<int>>=Map.empty
let mutable tweets :Map<int,List<string>>=Map.empty
let mutable hashTags :Map<string,List<string>>=Map.empty
let mutable queryData :List<string>=List.empty
let mutable pendingTweets : Map<int, List<string>>= Map.empty
let mutable disconnectedUsers=0
let mutable tweetID=100

let wsForSending (webSocket : WebSocket, res:String)=
  printfn "%s" res

  let mutable sendMessage=res
  let byteMessage=
    sendMessage
    |>System.Text.Encoding.ASCII.GetBytes
    |>ByteSegment
  webSocket.send Text byteMessage true

let Server numUsers (mailbox: Actor<_>) =
    let mutable initComplete=false
    printfn "SERVER STARTED"
//////////////Update My followers map//////
    let updateMyFollowers  (userNameIsFollowing:Set<int>,userNameToFollow:int)=
        let t=userNameIsFollowing.MaximumElement
        let tempSet=Set.empty.Add(userNameToFollow)
        if (myFollowers.ContainsKey(t)) then
          let mutable temp=myFollowers.[t]
          temp<-temp.Add(userNameToFollow)
          myFollowers<-myFollowers.Add(t,temp)
        else
          myFollowers<-myFollowers.Add(t,tempSet)
        printfn "%A" myFollowers
        //printfn "%A" myFollowers.[1].Count
        
       


//////////Update My following map//////////
    let updateIAmFollowing (userName:int,subs:Set<int>) =
        if iAmFollowing.ContainsKey(userName) then
          let mutable temp=iAmFollowing.[userName]
          temp<-temp.Add(subs.MaximumElement)
          iAmFollowing<-iAmFollowing.Add(userName,temp)
        else
          iAmFollowing<-iAmFollowing.Add(userName,subs)
        //printfn "%A" iAmFollowing.[userName]
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
        printfn "%A" tweets
        

        
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

        
    /////////////HANDLE QUERY BY USERNAME///////////////
    let handleQueryByUsername(userNumber:int,userNumToSearch:int)=
        let mutable res=""
        if(tweets.ContainsKey(userNumToSearch)) then
            let t1=String.Concat(tweets.[userNumToSearch])
            res<-sprintf "Tweets for %i are %s" userNumToSearch  t1
        else
            res<-sprintf "No tweets found"
        Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))
    
    /////////////HANDLE QUERY BY HASHTAG//////
    let handleQueryByHashtag(userNumber:int,hashtagToSearch:string)=
      let mutable res=""
      if(hashTags.ContainsKey(hashtagToSearch)) then
            let t1=String.Concat(hashTags.[hashtagToSearch])
            res<-sprintf "Tweets having %s are %s" hashtagToSearch t1
      else
            res<-sprintf "No tweets found"
      Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))


    let handleRegister(userNumber:int,password:string,webSocket:WebSocket)=
      let mutable res=""
      if(numberAndPassword.ContainsKey(userNumber)) then
          res<- "USER NUMBER ALREADY REGISTERED"
      else
               numberAndPassword<-numberAndPassword.Add(userNumber,password)
            //    registry<-registry.Add(int userNumber,true)
               numberAndWebsocket<-numberAndWebsocket.Add(userNumber,webSocket)
               res <-  "SIGN UP SUCCESSFULL "

      Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))


    let handleLogin(userNumber:int,password:string) =
      let mutable res=""
      if numberAndPassword.ContainsKey(userNumber) then
        if numberAndPassword.[userNumber]=password then
          registry<-registry.Add(int userNumber,true)
                
          res <-  "LOGIN SUCCESSFULL: Tweets of users it follows are=" 
          if iAmFollowing.ContainsKey(userNumber) then
            for sub in iAmFollowing.[userNumber] do
              res<-sprintf "%s" (res+System.String.Concat(tweets.[sub]))

        else
          res <- sprintf "INCORRECT PASSWORD " 
      else
        res <- sprintf "USERNUMBER NOT REGISTERED " 
      Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))

    let handleFollow(userNumber:int,userNumberToFollow:int)=
      let mutable res=""
      if(numberAndWebsocket.ContainsKey(userNumberToFollow)) then
        let mutable fSet :Set<int>=Set.empty.Add(userNumberToFollow)
        
    
        userRef<!MyFollowing (fSet,userNumber)
        userRef<!MyFollowers (fSet,userNumber)
        res<-sprintf "FOLLOWED SUCCESSFULLY"
      else
        res<-sprintf "USERNUMBER TO FOLLOW DOES NOT EXIST"
      Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))

    let handleTweet(userNumber:int,tweet:string)=
      let mutable res=""
      let modifiedTweet=sprintf "{TweetStart ID=%i} %s {TweetEnd}" tweetID tweet
            
      tweetIDAndTweet<-tweetIDAndTweet.Add(tweetID,modifiedTweet)
      tweetID<-tweetID+1
      res <- sprintf "Tweet sent"
      userRef<? Tweet(userNumber,modifiedTweet)

      System.Threading.Thread.Sleep(100)
      Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))
      if myFollowers.ContainsKey(userNumber) then
        for sub in myFollowers.[userNumber] do
          if registry.[sub]=true then /////////////IF USER IS ONLINE////////////
            res<-sprintf "User %i tweeted %s" userNumber modifiedTweet
            Async.RunSynchronously(wsForSending(numberAndWebsocket.[sub],res))
            ()

    let handleRetweet(userNumber:int,retweetID:int)=
      let mutable res=""
      if(tweetIDAndTweet.ContainsKey(retweetID)) then
            let tweetToRetweet=tweetIDAndTweet.[retweetID]
            let modifiedRetweet=sprintf "{TweetStart ID=%i} %s {--Retweet TweetEnd}" tweetID tweetToRetweet
            tweetID<-tweetID+1
            tweetIDAndTweet<-tweetIDAndTweet.Add(tweetID,modifiedRetweet)
            res <- sprintf "Tweet sent " 
            userRef<? Tweet(userNumber,modifiedRetweet)
            Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))
      else
            res <- sprintf "Tweet by ID not found"    
            Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))

    let handleWallFeed (userNumber:int)=
      let mutable res=""
      if(tweets.ContainsKey(userNumber)) then
            let tOne= tweets.[userNumber]
            let stringTweets=System.String.Concat(tOne)
            res<-sprintf "MY TWEETS %s" stringTweets
      else
            res<-sprintf "No Tweets Yet"
      Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],res))


      
    
    let rec listen() =
            actor {
                
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match message with 
                | RegisterUser (user,password,webSocket)->
                     handleRegister(user,password,webSocket)
                     
                     //printfn "%A" registry
                     return! listen()
                | HandleLogin (userNumber,password)->
                  handleLogin(userNumber,password)
                
                | UpdateFollow (userNumber,userNumberToFollow)->
                  handleFollow(userNumber,userNumberToFollow)

                | MyFollowing (setOfSubscriptions,userName) ->
                    
                        //let k= updateMyFollowers(userName,myFollowersC) 
                        let m=updateIAmFollowing(userName,setOfSubscriptions)
                        ()
                        //sender<! SendUpdate "updated my following"
                | MyFollowers (userNameIsFollowing,userNameToFollow) ->
                        updateMyFollowers(userNameIsFollowing,userNameToFollow)
                        // for i = 1 to numUsers do
                        //         let pathUser="akka://MySystem/user/" + string i
                        //         //let userRef=select pathUser Global.GlobalVar.system
                        //         userRef<! SendUpdate "updated my followers"

                | HandleTweet (userNumber,tweet)->
                  handleTweet(userNumber,tweet)
                  
                |Tweet (userName,tweet) ->
                    updateTweetRecord(userName,tweet)
                | Retweet (userName,retweetID)->
                    handleRetweet(userName,retweetID)
                | QueryByUsername (userNumber,userNumToSearch) ->
                    handleQueryByUsername(userNumber,userNumToSearch)
                    
                | QueryByHashtag (userName,hashtagString)->
                    handleQueryByHashtag (userName,hashtagString)
                | GetWallFeed (userName) ->
                  handleWallFeed(userName)

                | HandleLogout(userNumber) ->
                  registry<-registry.Add(int userNumber,false)
                  Async.RunSynchronously(wsForSending(numberAndWebsocket.[userNumber],"Logged out"))

                | DisconnectMe userName ->
                    registry<-registry.Add(userName,false)
                    disconnectedUsers<-disconnectedUsers+1
                    printfn "%A" registry
                    if(disconnectedUsers>=numUsers/2) then
                        //printfn "%i" disconnectedUsers
                        sender<! SendUpdate "disconnection of half users completed"
                | Login userName->
                    registry<-registry.Add(userName,true)
                    
      
                










                return! listen()
            }
    listen()







let serverRef=
  Server 1000
  |> spawn system "server"







let ws (webSocket : WebSocket) (context: HttpContext) =
  
  let mutable userNumber= -1
  socket {
    
    let mutable loop = true

    while loop do
      
      let! msg = webSocket.read()
      printfn "%A" webSocket.send
      match msg with
      
      | (Text, data, true) ->

        let mutable response=""
        

        let str = Encoding.UTF8.GetString data 
        
        //printfn "%A" values
        if str.Contains("register") then                                    ////////////////REGISTRATION////////////////////
          let values = str.Split '+'
          if values.Length>1 && values.[0]="register"  then
            
            userNumber<-values.[3] |> int
            let password=values.[4]
            
            ()
            serverRef<! RegisterUser (userNumber,password,webSocket)
             


        else if str.Contains("login") then
          let values = str.Split '+'
          if values.Length>1 then
            userNumber<-values.[1] |> int
            let password=values.[2]
            serverRef<! HandleLogin (userNumber,password)
             



        else if str.Contains("searchUser") then
          let values=str.Split '+'
          let userNameToSearch=values.[1]|> int
          serverRef<!QueryByUsername(userNumber, userNameToSearch)
          

        else if str.Contains("hashtagTweet") then
          let values=str.Split '+'
          let hashtagToSearch=values.[1]
          serverRef<!QueryByHashtag(userNumber,hashtagToSearch)
          

        else if str.Contains("follow") then
          let values=str.Split '+'
          if values.Length>1 then
            let userNumberToFollow=values.[1] |> int
            
            serverRef<!UpdateFollow (userNumber,userNumberToFollow)
            

        else if str.Contains("tweet") then
          let values=str.Split '+'
          if values.Length>1 then
            let userNum=userNumber
            let tweet=values.[2]
            serverRef<! HandleTweet (userNum,tweet)
            
            
            
            
                
            

            //response<-sprintf "USERNUMBER TO FOLLOW DOES NOT EXIST %s" stringTweets





        else if str.Contains("rt") then
          let values=str.Split '+'
          let retweetID=values.[1]|>int
          serverRef<! Retweet (userNumber,retweetID)
          

        else if str.Contains("ShowWallfeed") then
          let values = str.Split '+'
          //let userNum=values.[1] |> int
          let userNum=userNumber
          serverRef<!GetWallFeed userNum
          
          
        else if str.Contains("Logout") then
          serverRef<!HandleLogout userNumber

        // let byteResponse =
        //   response
        //   |> System.Text.Encoding.ASCII.GetBytes
        //   |> ByteSegment

        // // the `send` function sends a message back to the client
        // do! webSocket.send Text byteResponse true



      | _ -> ()
    }



let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    // path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
    NOT_FOUND "Found no handlers." ]

[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0