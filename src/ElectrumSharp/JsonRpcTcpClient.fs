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

type JsonRpcTcpClient (host: string, port: uint32) =
    member __.Host with get() = host

    member __.Request<'TResult> (method: string) (args: array<obj>) (timeout: TimeSpan): Async<'TResult> = async {
        use tcpClient = new TcpClient(host, int port)
        
        let rpcClient : JsonRpc = JsonRpc.Attach(tcpClient.GetStream())

        let! res = rpcClient.InvokeAsync<'TResult>(method, args) |> Async.AwaitTask
        return res
    }
