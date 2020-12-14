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

let join (p:Map<'a,'b>) (q:Map<'a,'b>) = 
    Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])
let mutable registry :Map<int,bool>=Map.empty
let mutable numberAndPassword :Map<int,string>=Map.empty 
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
        // if myFollowers.ContainsKey(userName) then
        //     for sub in myFollowers.[userName] do
        //         if registry.[sub]=true then
        //             let pathToUser="akka://MySystem/user/"+ string sub
        //             //let userRef=select pathToUser Global.GlobalVar.system
        //             userRef<! TweetUpdate (userName,tweet)
        //         else 
        //             //printfn "HERESAY"
        //             if not(pendingTweets.ContainsKey(sub)) then              ///If not online store in pending tweets/////
        //                 let temp=[tweet]
        //                 pendingTweets<-pendingTweets.Add(sub,temp)
        //             else
        //                 let mutable temp=pendingTweets.[sub]
        //                 temp<-[tweet] |> List.append temp
        //                 pendingTweets<-pendingTweets.Add(sub,temp)
                //printfn "%i" sub
        //printfn "%A" tweets
        //printfn "%A" hashTags

    // ///////////////HANDLE RETWEETS////////////////
    // let doRetweet(username:int) =
    //     if iAmFollowing.ContainsKey(username) && iAmFollowing.[username].Count>0 then
            
    //         let followSet=iAmFollowing.[username]
    //         let followArray=Set.toArray(followSet)
            
    //         //let selectedUserToRt=followArray.[rand.Next()%followArray.Length]
    //         if(tweets.ContainsKey(selectedUserToRt)) then
    //             let userTweets=tweets.[selectedUserToRt]

    //            // let selectedTweetToRt=userTweets.[rand.Next()%userTweets.Length]
    //             let modifiedRt=selectedTweetToRt + " ---Retweet by user " + string username
    //             if myFollowers.ContainsKey(username) then
    //                 for sub in myFollowers.[username] do
    //                     if registry.[sub]=true then                         //////// Check if the user is online, if yes, send tweet///
    //                         let pathToUser="akka://MySystem/user/"+ string sub
    //                     //    let userRef=select pathToUser Global.GlobalVar.system
    //                         userRef<! TweetUpdate (username,modifiedRt)
    //                     else
                            
    //                         if not(pendingTweets.ContainsKey(sub)) then              ///If not online store in pending tweets/////
    //                             let temp=[modifiedRt]
    //                             pendingTweets<-pendingTweets.Add(sub,temp)
    //                         else
    //                             let mutable temp=pendingTweets.[sub]
    //                             temp<-[modifiedRt] |> List.append temp
    //                             pendingTweets<-pendingTweets.Add(sub,temp)









                // if not(tweets.ContainsKey(username)) then
                //             let temp=[modifiedRt]
                //             tweets<-tweets.Add(username,temp)
                //          else
                //             let mutable temp=tweets.[username]
                //             temp<-[modifiedRt] |> List.append temp
                //             tweets<-tweets.Add(username,temp)
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
                     
                   
                | MyFollowing (setOfSubscriptions,myFollowersC,userName) ->
                    
                        //let k= updateMyFollowers(userName,myFollowersC) 
                        let m=updateIAmFollowing(userName,setOfSubscriptions)
                        ()
                        //sender<! SendUpdate "updated my following"
                | MyFollowers (myFollowersC,userName) ->
                        myFollowers<-myFollowersC
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
                    
                    printfn "User which logged in is :%i" userName
                    //handleLogIn(userName)
                    printfn "%A" registry
                










                return! listen()
            }
    listen()







let serverRef=
  Server 1000
  |> spawn system "server"



let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    // if `loop` is set to false, the server will stop receiving messages
    let mutable loop = true

    while loop do
      // the server will wait for a message to be received without blocking the thread
      let! msg = webSocket.read()
      printfn "%A" webSocket.send
      match msg with
      // the message has type (Opcode * byte [] * bool)
      //
      // Opcode type:
      //   type Opcode = Continuation | Text | Binary | Reserved | Close | Ping | Pong
      //
      // byte [] contains the actual message
      //
      // the last element is the FIN byte, explained later
      | (Text, data, true) ->
        // the message can be converted to a string
        let mutable response=""
        

        let str = Encoding.UTF8.GetString data //UTF8Encoding.UTF8.ToString data
        //let strNotJSON = Json.deseriaçlize<Foo> testjson
        
        //let values = str.Split '+'
        
        //printfn "%A" values
        if str.Contains("register") then
          let values = str.Split '+'
          if values.Length>1 && values.[0]="register"  then
            
            let userNumber=values.[3] |> int
            let password=values.[4]
            
            ()
            if(numberAndPassword.ContainsKey(userNumber)) then
               response<-sprintf "USER NUMBER ALREADY REGISTERED"
            else
               numberAndPassword<-numberAndPassword.Add(userNumber,password)
            //    registry<-registry.Add(int userNumber,true)
               response <- sprintf "SIGN UP SUCCESSFULL %s" str
        // else
        //   response <- sprintf "MARNEE MADE THIS response to %s" str
         

        // the response needs to be converted to a ByteSegment
        let byteResponse =
          response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment

        // the `send` function sends a message back to the client
        do! webSocket.send Text byteResponse true

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        // after sending a Close message, stop the loop
        loop <- false

      | _ -> ()
    }

/// An example of explictly fetching websocket errors and handling them in your codebase.
let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
   
   let exampleDisposableResource = { new IDisposable with member __.Dispose() = printfn "Resource needed by websocket connection disposed" }
   let websocketWorkflow = ws webSocket context
   
   async {
    let! successOrError = websocketWorkflow
    match successOrError with
    // Success case
    | Choice1Of2() -> ()
    // Error case
    | Choice2Of2(error) ->
        // Example error handling logic here
        printfn "Error: [%A]" error
        exampleDisposableResource.Dispose()
        
    return successOrError
   }

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    // path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    path "/websocketWithError" >=> handShake wsWithErrorHandling
    GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
    NOT_FOUND "Found no handlers." ]

[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0