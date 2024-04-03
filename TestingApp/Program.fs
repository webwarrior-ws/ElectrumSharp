open ElectrumSharp

open System

let serverAddress = "electrum.ltc.xurious.com"
let serverPort = 50001u
let timeout = TimeSpan.FromSeconds 3.0

let stratumClient = ElectrumClient.StratumServer serverAddress serverPort timeout

async {
    let! version = stratumClient.ServerVersion "geewallet" (Version "1.4")
    printfn "Version: %A" version
    let txHash = "ecbe69b199b55f59f6053d9e9b1b45306d62d2f4acf0759da074c3c74137ce72"
    let! transaction = ElectrumClient.GetBlockchainTransaction txHash (async { return stratumClient })
    printfn "transaction:\n%s" transaction
}
|> Async.RunSynchronously