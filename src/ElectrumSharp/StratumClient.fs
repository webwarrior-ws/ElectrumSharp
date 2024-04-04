namespace ElectrumSharp

open System

type BlockchainScriptHashGetBalanceResult =
    {
        Confirmed: Int64
        Unconfirmed: Int64
    }

type BlockchainScriptHashListUnspentResult =
    {
        TxHash: string
        TxPos: int
        Value: Int64
        Height: Int64
    }

type StratumClient (jsonRpcClient: JsonRpcTcpClient, timeout: TimeSpan) =

    member self.BlockchainScriptHashGetBalance (scriptHash: string): Async<BlockchainScriptHashGetBalanceResult> =
        jsonRpcClient.Request<BlockchainScriptHashGetBalanceResult> 
            "blockchain.scripthash.get_balance" 
            [| scriptHash |] 
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
        let! serverProtocolVersionResponse = 
            jsonRpcClient.Request<array<string>> 
                "server.version" 
                [| clientName; protocolVersion.ToString() |] 
                timeout

        return StratumClient.CreateVersion(serverProtocolVersionResponse.[1])
    }

    member self.BlockchainScriptHashListUnspent (scriptHash: string): Async<array<BlockchainScriptHashListUnspentResult>> =
        jsonRpcClient.Request<array<BlockchainScriptHashListUnspentResult>> 
            "blockchain.scripthash.listunspent" 
            [| scriptHash |] 
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
