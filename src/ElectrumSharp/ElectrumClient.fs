namespace ElectrumSharp

open System

module ElectrumClient =
    
    let GetClient (fqdn: string) (port: uint32) (timeout: TimeSpan): StratumClient =
        let jsonRpcClient = new JsonRpcTcpClient(fqdn, port)
        let stratumClient = new StratumClient(jsonRpcClient, timeout)
        stratumClient

    let GetBalances (scriptHashes: List<string>) (stratumClient: Async<StratumClient>) = async {
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

    let GetUnspentTransactionOutputs scriptHash (stratumClient: Async<StratumClient>) = async {
        let! stratumClient = stratumClient
        let! unspentListResult = stratumClient.BlockchainScriptHashListUnspent scriptHash
        return unspentListResult
    }

    let GetBlockchainTransaction txHash (stratumClient: Async<StratumClient>) = async {
        let! stratumClient = stratumClient
        let! blockchainTransactionResult = stratumClient.BlockchainTransactionGet txHash
        return blockchainTransactionResult
    }

    // DON'T DELETE, used in external projects
    let GetBlockchainTransactionIdFromPos (height: UInt32) (txPos: UInt32) (stratumClient: Async<StratumClient>) = async {
        let! stratumClient = stratumClient
        let! blockchainTransactionResult = stratumClient.BlockchainTransactionIdFromPos height txPos
        return blockchainTransactionResult
    }

    let EstimateFee (numBlocksTarget: int) (stratumClient: Async<StratumClient>): Async<decimal> = async {
        let! stratumClient = stratumClient
        let! estimateFeeResult = stratumClient.BlockchainEstimateFee numBlocksTarget
        let amountPerKB = estimateFeeResult
        return amountPerKB
    }

    let BroadcastTransaction (transactionInHex: string) (stratumClient: Async<StratumClient>) = async {
        let! stratumClient = stratumClient
        let! blockchainTransactionBroadcastResult = stratumClient.BlockchainTransactionBroadcast transactionInHex
        return blockchainTransactionBroadcastResult
    }

