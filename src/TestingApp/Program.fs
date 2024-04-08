open ElectrumSharp

open System

let serverAddress = "electrum.ltc.xurious.com"
let serverPort = 50001u
let timeout = TimeSpan.FromSeconds 3.0

let stratumClient = ElectrumClient.GetClient serverAddress serverPort timeout

async {
    let! version = stratumClient.ServerVersion "geewallet" (Version "1.4")
    printfn "Version: %A" version
    let txHash = "ecbe69b199b55f59f6053d9e9b1b45306d62d2f4acf0759da074c3c74137ce72"
    let! transaction = ElectrumClient.GetBlockchainTransaction txHash (async { return stratumClient })
    printfn "transaction:\n%s" transaction
    let! feeEstimate = stratumClient.BlockchainEstimateFee 6
    printfn "Fee estimate:\n%f" feeEstimate
    let scripthash = "e3645b4d105b3e442ae8bb9dd18603c9496e210b8d2a31f12b488ee5f32e5281"
    let! listUnspent = stratumClient.BlockchainScriptHashListUnspent scripthash
    printfn "Result of blockchain.scripthash.listunspent:\n%A" listUnspent
}
|> Async.RunSynchronously