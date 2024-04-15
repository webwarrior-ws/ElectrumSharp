namespace ElectrumSharp

open System

open StreamJsonRpc

type IncompatibleProtocolException(message) =
    inherit CommunticationFailedException(message)

type ServerTooNewException(message) =
    inherit IncompatibleProtocolException(message)

type ServerTooOldException(message) =
    inherit IncompatibleProtocolException(message)

module Electrum =
    
    let CreateClient (fqdn: string) (port: uint32) (timeout: TimeSpan) (walletName: string): Async<ElectrumClient> =
        let jsonRpcClient = new JsonRpcTcpClient(fqdn, port)
        let stratumClient = new ElectrumClient(jsonRpcClient, timeout)
        
        // last version of the protocol [1] as of electrum's source code [2] at the time of
        // writing this... actually this changes relatively rarely (one of the last changes
        // was for 2.4 version [3] (changes documented here[4])
        // [1] https://electrumx-spesmilo.readthedocs.io/en/latest/protocol.html
        // [2] https://github.com/spesmilo/electrum/blob/master/lib/version.py
        // [3] https://github.com/spesmilo/electrum/commit/118052d81597eff3eb636d242eacdd0437dabdd6
        // [4] https://electrumx-spesmilo.readthedocs.io/en/latest/protocol-changes.html
        let PROTOCOL_VERSION_SUPPORTED = Version "1.4"

        async {
            let! versionSupportedByServer =
                try
                    stratumClient.ServerVersion walletName PROTOCOL_VERSION_SUPPORTED
                with
                | :? RemoteInvocationException as ex ->
                    if (ex.ErrorCode = 1 && ex.Message.StartsWith "unsupported protocol version" &&
                                            ex.Message.EndsWith (PROTOCOL_VERSION_SUPPORTED.ToString())) then

                        // FIXME: even if this ex is already handled to ignore the server, we should report to sentry as WARN
                        raise <| ServerTooNewException(sprintf "Version of server rejects our client version (%s)"
                                                               (PROTOCOL_VERSION_SUPPORTED.ToString()))
                    else
                        reraise()
            if versionSupportedByServer < PROTOCOL_VERSION_SUPPORTED then
                raise (ServerTooOldException (sprintf "Version of server is older (%s) than the client (%s)"
                                                      (versionSupportedByServer.ToString())
                                                      (PROTOCOL_VERSION_SUPPORTED.ToString())))
            return stratumClient
        }

    let GetBalances (scriptHashes: List<string>) (stratumClient: Async<ElectrumClient>) = async {
        // FIXME: we should rather implement this method in terms of:
        //        - querying all unspent transaction outputs (X) -> block heights included
        //        - querying transaction history (Y) -> block heights included
        //        - check the difference between X and Y (e.g. Y - X = Z)
        //        - query details of each element in Z to see their block heights
        //        - query the current blockheight (H) -> pick the highest among all servers queried
        //        -> having H, we now know which elements of X, Y, and Z are confirmed or not
        // Doing it this way has two advantages:
        // 1) We can configure GWallet with a number of confirmations to consider some balance confirmed (instead
        //    of trusting what "confirmed" means from the point of view of the Electrum Server)
        // 2) and most importantly: we could verify each of the transactions supplied in X, Y, Z to verify their
        //    integrity (in a similar fashion as Electrum Wallet client already does), to not have to trust servers*
        //    [ see https://www.youtube.com/watch?v=hjYCXOyDy7Y&feature=youtu.be&t=1171 for more information ]
        // * -> although that would be fixing only half of the problem, we also need proof of completeness
        let! stratumClient = stratumClient
        let rec innerGetBalances (scriptHashes: List<string>) (result: BlockchainScriptHashGetBalanceResult) =
            async {
                match scriptHashes with
                | scriptHash::otherScriptHashes ->
                    let! balanceHash = stratumClient.BlockchainScriptHashGetBalance scriptHash
                    
                    return! 
                        innerGetBalances
                            otherScriptHashes
                            {
                                result with
                                    Unconfirmed = result.Unconfirmed + balanceHash.Unconfirmed
                                    Confirmed = result.Confirmed + balanceHash.Confirmed
                            }
                | [] ->
                    return result
            }

        return!
            innerGetBalances
                scriptHashes
                {
                    Unconfirmed = 0L
                    Confirmed = 0L
                }
    }

    let GetUnspentTransactionOutputs scriptHash (stratumClient: Async<ElectrumClient>) = async {
        let! stratumClient = stratumClient
        let! unspentListResult = stratumClient.BlockchainScriptHashListUnspent scriptHash
        return unspentListResult
    }

    let GetBlockchainTransaction txHash (stratumClient: Async<ElectrumClient>) = async {
        let! stratumClient = stratumClient
        let! blockchainTransactionResult = stratumClient.BlockchainTransactionGet txHash
        return blockchainTransactionResult
    }

    // DON'T DELETE, used in external projects
    let GetBlockchainTransactionIdFromPos (height: UInt32) (txPos: UInt32) (stratumClient: Async<ElectrumClient>) = async {
        let! stratumClient = stratumClient
        let! blockchainTransactionResult = stratumClient.BlockchainTransactionIdFromPos height txPos
        return blockchainTransactionResult
    }

    let EstimateFee (numBlocksTarget: int) (stratumClient: Async<ElectrumClient>): Async<decimal> = async {
        let! stratumClient = stratumClient
        let! estimateFeeResult = stratumClient.BlockchainEstimateFee numBlocksTarget
        let amountPerKB = estimateFeeResult
        return amountPerKB
    }

    let BroadcastTransaction (transactionInHex: string) (stratumClient: Async<ElectrumClient>) = async {
        let! stratumClient = stratumClient
        let! blockchainTransactionBroadcastResult = stratumClient.BlockchainTransactionBroadcast transactionInHex
        return blockchainTransactionBroadcastResult
    }

