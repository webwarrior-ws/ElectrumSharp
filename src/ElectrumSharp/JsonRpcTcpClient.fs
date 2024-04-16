namespace ElectrumSharp

open System
open System.Net.Sockets
open System.Runtime.Serialization
open System.Text

open Fsdk.FSharpUtil
open StreamJsonRpc

type PascalCaseToSnakeCaseNamingPolicy() = 
    inherit Json.JsonNamingPolicy()

    static let capitalizedWordRegex = RegularExpressions.Regex "[A-Z][a-z0-9]*"

    override self.ConvertName name =
        let evaluator (regexMatch: RegularExpressions.Match) =
            let lowercase = regexMatch.Value.ToLower()
            if regexMatch.Index = 0 then lowercase else "_" + lowercase
        capitalizedWordRegex.Replace(name, Text.RegularExpressions.MatchEvaluator evaluator)

type CommunticationFailedException =
    inherit Exception

    new(message: string, innerException: Exception) = { inherit Exception(message, innerException) }
    new(message: string) = { inherit Exception(message) }
    new() = { inherit Exception() }

type JsonRpcTcpClient (host: string, port: uint32) =
    member __.Host with get() = host

    member val Logger: string -> unit = Console.WriteLine with get, set

    member self.Request<'TResult> (method: string) (args: array<obj>) (timeout: TimeSpan): Async<'TResult> = async {
        try
            use tcpClient = new TcpClient()
            do! tcpClient.ConnectAsync(host, int port) |> Async.AwaitTask

            let networkStream = tcpClient.GetStream()
            use formatter = new SystemTextJsonFormatter()
            use handler = new NewLineDelimitedMessageHandler(networkStream, networkStream, formatter)
            formatter.JsonSerializerOptions.PropertyNamingPolicy <- PascalCaseToSnakeCaseNamingPolicy()
            use rpcClient = new JsonRpc(handler)

            let disconnectHandler =
                new EventHandler<JsonRpcDisconnectedEventArgs>(
                    fun  _ disconnectedEventArgs ->
                        sprintf "Disconnected (Reason=%A): %s" 
                            disconnectedEventArgs.Reason 
                            disconnectedEventArgs.Description
                        |> self.Logger)
        
            rpcClient.Disconnected.AddHandler disconnectHandler

            rpcClient.StartListening()

            let! response =
                rpcClient.InvokeAsync<'TResult>(method, args) 
                |> Async.AwaitTask
                |> WithTimeout timeout

            rpcClient.Disconnected.RemoveHandler disconnectHandler

            match response with
            | Some value -> return value
            | None -> return raise (CommunticationFailedException "RPC call timed out")
        with
        | exn ->
            match Fsdk.FSharpUtil.FindException<RemoteRpcException> exn with
            | Some remoteRpcExn -> return raise <| CommunticationFailedException(remoteRpcExn.Message, remoteRpcExn)
            | None -> 
                match Fsdk.FSharpUtil.FindException<SocketException> exn with
                | Some socketExn -> return raise <| CommunticationFailedException(socketExn.Message, socketExn)
                | None -> return raise exn
    }
