namespace ElectrumSharp

open System

module ElectrumClient =
    
    let StratumServer (fqdn: string) (port: uint32) (marshallingOptions: MarshallingOptions): StratumClient =
        let jsonRpcClient = new JsonRpcTcpClient(fqdn, port)
        let stratumClient = new StratumClient(jsonRpcClient, marshallingOptions)
        stratumClient

    let GetBalances (scriptHashes: List<string>) (stratumServer: Async<StratumClient>) = async {
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
        let! stratumClient = stratumServer
        let rec innerGetBalances (scriptHashes: List<string>) (result: BlockchainScriptHashGetBalanceInnerResult) =
            async {
                match scriptHashes with
                | scriptHash::otherScriptHashes ->
                    let! balanceHash = stratumClient.BlockchainScriptHashGetBalance scriptHash
                    
                    return! 
                        innerGetBalances
                            otherScriptHashes
                            {
                                result with
                                    Unconfirmed = result.Unconfirmed + balanceHash.Result.Unconfirmed
                                    Confirmed = result.Confirmed + balanceHash.Result.Confirmed
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

    let GetUnspentTransactionOutputs scriptHash (stratumServer: Async<StratumClient>) = async {
        let! stratumClient = stratumServer
        let! unspentListResult = stratumClient.BlockchainScriptHashListUnspent scriptHash
        return unspentListResult.Result
    }

    let GetBlockchainTransaction txHash (stratumServer: Async<StratumClient>) = async {
        let! stratumClient = stratumServer
        let! blockchainTransactionResult = stratumClient.BlockchainTransactionGet txHash
        return blockchainTransactionResult.Result
    }

    // DON'T DELETE, used in external projects
    let GetBlockchainTransactionIdFromPos (height: UInt32) (txPos: UInt32) (stratumServer: Async<StratumClient>) = async {
        let! stratumClient = stratumServer
        let! blockchainTransactionResult = stratumClient.BlockchainTransactionIdFromPos height txPos
        return blockchainTransactionResult.Result
    }

    let EstimateFee (numBlocksTarget: int) (stratumServer: Async<StratumClient>): Async<decimal> = async {
        let! stratumClient = stratumServer
        let! estimateFeeResult = stratumClient.BlockchainEstimateFee numBlocksTarget
        let amountPerKB = estimateFeeResult.Result
        return amountPerKB
    }

    let BroadcastTransaction (transactionInHex: string) (stratumServer: Async<StratumClient>) = async {
        let! stratumClient = stratumServer
        let! blockchainTransactionBroadcastResult = stratumClient.BlockchainTransactionBroadcast transactionInHex
        return blockchainTransactionBroadcastResult.Result
    }

