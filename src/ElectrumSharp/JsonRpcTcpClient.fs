namespace ElectrumSharp

open System
open System.Net
open System.Net.Sockets
open System.Runtime.Serialization
open System.Text

open StreamJsonRpc

[<AutoOpen>]
module Helpers =
    type TimeoutOrResult<'T> =
        | Timeout
        | Result of 'T
    
    let withTimeout (timeout: TimeSpan) (job: Async<_>) = async {
        let read = async {
            let! value = job
            return value |> Result |> Some
        }

        let delay = async {
            do! Async.Sleep (int timeout.TotalMilliseconds)
            return Some Timeout
        }

        let! result = Async.Choice([read; delay])
        match result with
        | Some x -> return x
        | None -> return Timeout
    }

    let unwrapTimeout timeoutMsg job = async {
        let! maybeRes = job
        match maybeRes with
        | Timeout ->
            let timeoutEx = TimeoutException(timeoutMsg)
            return raise timeoutEx
        | Result res ->
            return res
    }

type PascalCaseToSnakeCaseNamingPolicy() = 
    inherit Json.JsonNamingPolicy()

    static let capitalizedWordRegex = RegularExpressions.Regex "[A-Z][a-z0-9]*"

    override self.ConvertName name =
        let evaluator (regexMatch: RegularExpressions.Match) =
            let lowercase = regexMatch.Value.ToLower()
            if regexMatch.Index = 0 then lowercase else "_" + lowercase
        capitalizedWordRegex.Replace(name, Text.RegularExpressions.MatchEvaluator evaluator)

type JsonRpcTcpClient (host: string, port: uint32) =
    member __.Host with get() = host

    member __.Request<'TResult> (method: string) (args: array<obj>) (timeout: TimeSpan): Async<'TResult> = async {
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
                    printfn "Disconnected (Reason=%A): %s" 
                        disconnectedEventArgs.Reason 
                        disconnectedEventArgs.Description)

        rpcClient.Disconnected.AddHandler disconnectHandler

        rpcClient.StartListening()

        let! response =
            rpcClient.InvokeAsync<'TResult>(method, args) 
            |> Async.AwaitTask
            |> withTimeout timeout
            |> unwrapTimeout "RPC call timed out"

        rpcClient.Disconnected.RemoveHandler disconnectHandler

        return response
    }
