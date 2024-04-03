namespace ElectrumSharp

open System
open System.ComponentModel

type BlockchainScriptHashGetBalanceInnerResult =
    {
        Confirmed: Int64;
        Unconfirmed: Int64;
    }

type BlockchainScriptHashListUnspentInnerResult =
    {
        TxHash: string;
        TxPos: int;
        Value: Int64;
        Height: Int64;
    }

type RpcErrorCode =
    // see https://gitlab.com/nblockchain/geewallet/issues/110
    | ExcessiveResourceUsage = -101

    // see https://gitlab.com/nblockchain/geewallet/issues/117
    | ServerBusy = -102

    // see git commit msg of 0aba03a8291daa526fde888d0c02a789abe411f2
    | InternalError = -32603

    // see https://gitlab.com/nblockchain/geewallet/issues/112
    | UnknownMethod = -32601

type StratumClient (jsonRpcClient: JsonRpcTcpClient, timeout: TimeSpan) =

    member self.BlockchainScriptHashGetBalance address: Async<BlockchainScriptHashGetBalanceInnerResult> =
        jsonRpcClient.Request<BlockchainScriptHashGetBalanceInnerResult> 
            "blockchain.scripthash.get_balance" 
            [| address |] 
            timeout

    static member private CreateVersion(versionStr: string): Version =
        let correctedVersion =
            if (versionStr.EndsWith("+")) then
                versionStr.Substring(0, versionStr.Length - 1)
            else
                versionStr
        try
            Version(correctedVersion)
        with
        | exn -> raise(Exception("Electrum Server's version disliked by .NET Version class: " + versionStr, exn))

    member self.ServerVersion (clientName: string) (protocolVersion: Version) : Async<Version> = async {
        let! serverProtocolVersion = 
            jsonRpcClient.Request<string> 
                "server.version" 
                [| clientName; protocolVersion.ToString() |] 
                timeout

        return StratumClient.CreateVersion(serverProtocolVersion)
    }

    member self.BlockchainScriptHashListUnspent address: Async<array<BlockchainScriptHashListUnspentInnerResult>> =
        jsonRpcClient.Request<array<BlockchainScriptHashListUnspentInnerResult>> 
            "blockchain.scripthash.listunspent" 
            [| address |] 
            timeout

    member self.BlockchainTransactionGet txHash: Async<string> =
        jsonRpcClient.Request<string> 
            "blockchain.transaction.get" 
            [| txHash |] 
            timeout

    // DON'T DELETE, used in external projects
    member self.BlockchainTransactionIdFromPos height txPos: Async<string> =
        jsonRpcClient.Request<string> 
            "blockchain.transaction.id_from_pos" 
            [| height :> obj; txPos :> obj |] 
            timeout

    // NOTE: despite Electrum-X official docs claiming that this method is deprecated... it's not! go read the official
    //       non-shitcoin forked version of the docs: https://electrumx-spesmilo.readthedocs.io/en/latest/protocol-methods.html#blockchain-estimatefee
    member self.BlockchainEstimateFee (numBlocksTarget: int): Async<decimal> =
        jsonRpcClient.Request<decimal> 
            "blockchain.estimatefee" 
            [| numBlocksTarget |] 
            timeout

    member self.BlockchainTransactionBroadcast txInHex: Async<string> =
        jsonRpcClient.Request<string> 
            "blockchain.transaction.broadcast" 
            [| txInHex |] 
            timeout
