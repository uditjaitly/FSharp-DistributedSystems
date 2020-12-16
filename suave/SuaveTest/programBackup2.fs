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
    | RegisterUser of int
    | SendUpdate of string
    | MyFollowing of Set<int>*int
    | MyFollowers of Set<int>*int
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
    //let handleLogIn(userName:int)=
        // if pendingTweets.ContainsKey(userName) then
        //     let pathToUser="akka://MySystem/user/"+ string userName
        //     //let userRef=select pathToUser Global.GlobalVar.system
        //     userRef<! TweetUpdateAfterLogin (userName,pendingTweets.[userName])
        //     pendingTweets<-pendingTweets.Add(userName,[])

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

                |Tweet (userName,tweet) ->
                    updateTweetRecord(userName,tweet)
                | Retweet username->
                    //doRetweet(username)
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
                    
      
                










                return! listen()
            }
    listen()


let mutable tweetID=100




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
            if(numberAndPassword.ContainsKey(userNumber)) then
               response<-sprintf "USER NUMBER ALREADY REGISTERED"
            else
               numberAndPassword<-numberAndPassword.Add(userNumber,password)
            //    registry<-registry.Add(int userNumber,true)
               numberAndWebsocket<-numberAndWebsocket.Add(userNumber,webSocket)
               response <- sprintf "SIGN UP SUCCESSFULL " 


        else if str.Contains("login") then
          let values = str.Split '+'
          if values.Length>1 then
            userNumber<-values.[1] |> int
            let password=values.[2]

            if numberAndPassword.ContainsKey(userNumber) then
              if numberAndPassword.[userNumber]=password then
                registry<-registry.Add(int userNumber,true)
                
                response <- sprintf "LOGIN SUCCESSFULL: Tweets of users it follows are=" 
                if iAmFollowing.ContainsKey(userNumber) then
                  for sub in iAmFollowing.[userNumber] do
                    response<-sprintf "%s" (response+System.String.Concat(tweets.[sub]))

              else
                response <- sprintf "INCORRECT PASSWORD " 
            else
              response <- sprintf "USERNUMBER NOT REGISTERED " 



        else if str.Contains("searchUser") then
          let values=str.Split '+'
          let userNameToSearch=values.[1]|> int
          if(tweets.ContainsKey(userNameToSearch)) then
            let t1=String.Concat(tweets.[userNameToSearch])
            response<-sprintf "Tweets for %i are %s" userNameToSearch  t1
          else
            response<-sprintf "No tweets found"

        else if str.Contains("hashtagTweet") then
          let values=str.Split '+'
          let hashtagToSearch=values.[1]
          if(hashTags.ContainsKey(hashtagToSearch)) then
            let t1=String.Concat(hashTags.[hashtagToSearch])
            response<-sprintf "Tweets having %s are %s" hashtagToSearch t1
          else
            response<-sprintf "No tweets found"

        else if str.Contains("follow") then
          let values=str.Split '+'
          if values.Length>1 then
            let userNumberToFollow=values.[1] |> int
            let userNum=values.[2] |> int
            if(numberAndWebsocket.ContainsKey(userNumberToFollow)) then
              let mutable fSet :Set<int>=Set.empty.Add(userNumberToFollow)
              serverRef<!MyFollowing (fSet,userNum)
              serverRef<!MyFollowers (fSet,userNum)
              response<-sprintf "FOLLOWED SUCCESSFULLY"
            else
              response<-sprintf "USERNUMBER TO FOLLOW DOES NOT EXIST"

        else if str.Contains("tweet") then
          let values=str.Split '+'
          if values.Length>1 then
            let userNum=userNumber
            let tweet=values.[2]
            let modifiedTweet=sprintf "{TweetStart ID=%i} %s {TweetEnd}" tweetID tweet
            
            tweetIDAndTweet<-tweetIDAndTweet.Add(tweetID,modifiedTweet)
            tweetID<-tweetID+1
            response <- sprintf "Tweet sent %s" str
            serverRef<? Tweet(userNum,modifiedTweet)
            
            
            System.Threading.Thread.Sleep(100)
            
            if myFollowers.ContainsKey(userNum) then
              for sub in myFollowers.[userNum] do
                if registry.[sub]=true then /////////////IF USER IS ONLINE////////////
                  response<-sprintf "User %i tweeted %s" userNum modifiedTweet
                  let byteResponse=
                    response
                    |>System.Text.Encoding.ASCII.GetBytes
                    |>ByteSegment
                  do! numberAndWebsocket.[sub].send Text byteResponse true
                
            

            //response<-sprintf "USERNUMBER TO FOLLOW DOES NOT EXIST %s" stringTweets





        else if str.Contains("rt") then
          let values=str.Split '+'
          let retweetID=values.[1]|>int
          
          if(tweetIDAndTweet.ContainsKey(retweetID)) then
            let tweetToRetweet=tweetIDAndTweet.[retweetID]
            let modifiedRetweet=sprintf "{TweetStart ID=%i} %s {--Retweet TweetEnd}" tweetID tweetToRetweet
            tweetID<-tweetID+1
            tweetIDAndTweet<-tweetIDAndTweet.Add(tweetID,modifiedRetweet)
            response <- sprintf "Tweet sent %s" str
            serverRef<? Tweet(userNumber,modifiedRetweet)
          else
            response <- sprintf "Tweet by ID not found"

        else if str.Contains("ShowWallfeed") then
          let values = str.Split '+'
          //let userNum=values.[1] |> int
          let userNum=userNumber
          if(tweets.ContainsKey(userNum)) then
            let tOne= tweets.[userNum]
            let stringTweets=System.String.Concat(tOne)
            response<-sprintf "MY TWEETS %s" stringTweets
          else
            response<-sprintf "No Tweets Yet"
          
        else if str.Contains("Logout") then
          registry<-registry.Add(int userNumber,false)

        let byteResponse =
          response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment

        // the `send` function sends a message back to the client
        do! webSocket.send Text byteResponse true



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