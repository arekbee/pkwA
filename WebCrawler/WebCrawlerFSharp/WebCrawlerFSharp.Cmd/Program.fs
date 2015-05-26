
open System
open System.Text.RegularExpressions
open System.Net
open System.IO
open System.Collections.Concurrent
open System.Collections
open System.Collections.Generic
open HtmlAgilityPack



//#region Agents

let Gate = 1

type Message =
        | Url of string option
        | Done
        | Mailbox of MailboxProcessor<Message>
        | Stop

//#endregion 


let fetchHtml (url : string) =
        try
            let req = WebRequest.Create(url) :?> HttpWebRequest
            req.UserAgent <- "Mozilla/5.0 (Windows; U; MSIE 9.0; Windows NT 9.0; en-US)"
            req.Timeout <- 5000
            use resp = req.GetResponse()
            let content = resp.ContentType
            let isHtml = Regex("html").IsMatch(content)
            match isHtml with
            | true -> use stream = resp.GetResponseStream()
                      use reader = new StreamReader(stream)
                      let html = reader.ReadToEnd()
                      Some html
            | false -> None
        with
        | _ -> None


let getDomain (url :string) :string = 
        let req = WebRequest.Create(url) :?> HttpWebRequest
        req.Host






let extractLinks (html : string) (url : string) =
        let domain = getDomain url
        let doc = new HtmlDocument()
        doc.LoadHtml(html)
        let nodes = doc.DocumentNode.SelectNodes("//div/div/table/tbody/tr/td[@class=\"notrwd\"]/a")
        let links =
            [
                for x in nodes  do
                    yield x.Attributes.[0].Value

            ] |> Seq.distinct |> Seq.toList
        links



let collectLinks (url : string) =
        let html = fetchHtml url
        match html with
        | Some x -> extractLinks x url
        | None -> []




let parseHtmlDetails html = 
        let doc = new HtmlDocument()
        doc.LoadHtml(html) 
        let dic = new Dictionary<string,string>()

        for groupCols in doc.DocumentNode.SelectNodes("//div[@class=\"kom_karta_tab\"]") do
                     for row in groupCols.ChildNodes do
                            let col = row.ChildNodes
                            if col.Count > 0 then
                                dic.Add( col.[1].InnerText , col.[3].InnerText) 
        
        for groupCols in doc.DocumentNode.SelectNodes("//div[@class=\"kom_karta_tab2\"]") do
             for row in groupCols.ChildNodes do
                    let col = row.ChildNodes
                    if col.Count > 0 then
                        dic.Add( col.[3].InnerText , col.[5].InnerText) 

        for groupCols in doc.DocumentNode.SelectNodes("//div/div/table/tbody/tr")  do
                    let col = groupCols.ChildNodes
                    if col.Count > 0 then
                        dic.Add( col.[3].InnerText , col.[5].InnerText) 

        dic

     

           
[<EntryPoint>]
let main argv = 
    printfn "%A" argv


    let urlCollector =
           MailboxProcessor.Start(fun y ->

               let rec loop count (urls :Set<string>) =
                   async {
                       let! msg = y.TryReceive(6000)
                       match msg with
                           | Some message ->
                               match message with
                               | Url u ->
                                   match u with
                                       | Some url when urls.Contains(url) |> not -> 
                                            
                                            let newSet = urls.Add(url)
                                            return! loop count newSet
                                       | None -> return! loop count urls
                               
                               | _ -> printfn "TEST"
                           | None -> printfn "Test2"
                   }
               loop 1 Set.empty)

    let scraper id = 
           MailboxProcessor.Start(fun inbox ->
               let rec loop() =
                   async {
                       let! msg = inbox.Receive()
                       match msg with
                       | Url x ->
                           match x with
                           | Some url -> 
                                   let datas = parseHtmlDetails url

                                   return! loop()

                       }
               loop())


    let crawler id =
           MailboxProcessor.Start(fun inbox ->
               let rec loop() =
                   async {
                       let! msg = inbox.Receive()
                       match msg with
                       | Url x ->
                           match x with
                           | Some url -> 
                                   let links = collectLinks url
                                   for link in links do
                                       urlCollector.Post <| Url (Some link)
                                   return! loop()
                           | None ->
                                     return! loop()
                       | _ -> urlCollector.Post Done
                              printfn "Agent %d is done." id
                              (inbox :> IDisposable).Dispose()
                       }
               loop())




    let crawlers = 
              [
                  for i in 1 .. Gate do
                      yield crawler i
              ]


    //let html = fetchHtml "http://prezydent2015.pkw.gov.pl/327_protokol_komisji_obwodowej/22373"
    //let details = parseHtmlDetails html.Value


    //let url = "http://prezydent2015.pkw.gov.pl/325_Ponowne_glosowanie/260101"
    //let html2 = fetchHtml url
    //let links = extractLinks html2.Value url





//   let crawl (url : string) limit =
//       let q = ConcurrentQueue<string>()
//       // Holds crawled URLs.
//       let set : Set<string> = Set.empty
//       let t = true
//       let f = false
//    
//       let supervisor =
//           MailboxProcessor.Start(fun x ->
//               let rec loop (set : Set<string>) (run : bool) =
//                   async {
//                       let! msg = x.Receive()
//                       match msg with
//                       | Mailbox(mailbox) -> 
//                           let count = set.Count
//                           if count < limit - 1 && run then 
//                               let url = q.TryDequeue()
//                               match url with
//                               | true, str -> if not (set.Contains str) then
//                                                   let set'= set.Add str
//                                                   mailbox.Post <| Url(Some str)
//                                                   return! loop set' run
//                                               else
//                                                   mailbox.Post <| Url None
//                                                   return! loop set run
//    
//                               | _ -> mailbox.Post <| Url None
//                                      return! loop set run
//                           else
//                               mailbox.Post Stop
//                               return! loop set run
//                       | Stop -> return! loop set f
//                       | _ -> printfn "Supervisor is done."
//                              (x :> IDisposable).Dispose()
//                   }
//               loop set t)
//
//       let urlCollector =
//           MailboxProcessor.Start(fun y ->
//               let rec loop count =
//                   async {
//                       let! msg = y.TryReceive(6000)
//                       match msg with
//                       | Some message ->
//                           match message with
//                           | Url u ->
//                               match u with
//                               | Some url -> q.Enqueue url
//                                             return! loop count
//                               | None -> return! loop count
//                           | _ ->
//                               match count with
//                               | Gate -> supervisor.Post Done
//                                         (y :> IDisposable).Dispose()
//                                         printfn "URL collector is done."
//                               | _ -> return! loop (count + 1)
//                       | None -> supervisor.Post Stop
//                                 return! loop count
//                   }
//               loop 1)
//       
//       /// Initializes a crawling agent.
//       
//    
//       // Spawn the crawlers.
// 
//       
//
//       // Post the first messages.
//       crawlers.Head.Post <| Url (Some url)
//       crawlers.Tail |> List.iter (fun ag -> ag.Post <| Url None)
//
//
//   crawl "http://news.google.com" 50

    System.Console.ReadKey() |> ignore
    0 // return an integer exit code

