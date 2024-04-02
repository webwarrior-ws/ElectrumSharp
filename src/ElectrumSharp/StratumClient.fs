namespace ElectrumSharp

open System
open System.ComponentModel

open Newtonsoft.Json

type MarshallingOptions =
    {
        PascalCase2LowercasePlusUnderscoreConversionSettings: JsonSerializerSettings
        IsValidJson: string -> bool
    }

// can't make this type below private, or else Newtonsoft.Json will serialize it incorrectly
type Request =
    {
        Id: int;
        Method: string;
        Params: seq<obj>;
    }

type ServerVersionResult =
    {
        Id: int;
        Result: array<string>;
    }

type BlockchainScriptHashGetBalanceInnerResult =
    {
        Confirmed: Int64;
        Unconfirmed: Int64;
    }
type BlockchainScriptHashGetBalanceResult =
    {
        Id: int;
        Result: BlockchainScriptHashGetBalanceInnerResult
    }

type BlockchainScriptHashListUnspentInnerResult =
    {
        TxHash: string;
        TxPos: int;
        Value: Int64;
        Height: Int64;
    }
type BlockchainScriptHashListUnspentResult =
    {
        Id: int;
        Result: array<BlockchainScriptHashListUnspentInnerResult>
    }

type BlockchainTransactionGetResult =
    {
        Id: int;
        Result: string;
    }

// DON'T DELETE, used in external projects
type BlockchainTransactionIdFromPosResult =
    {
        Id: int
        Result: string
    }

type BlockchainEstimateFeeResult =
    {
        Id: int;
        Result: decimal;
    }

type BlockchainTransactionBroadcastResult =
    {
        Id: int;
        Result: string;
    }

type ErrorInnerResult =
    {
        Message: string;
        Code: int;
    }

type ErrorResult =
    {
        Id: int;
        Error: ErrorInnerResult;
    }

type ErrorResultWithStringError =
    {
        Id: int
        Error: string
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

type StratumClient (jsonRpcClient: JsonRpcTcpClient, marshallingOptions: MarshallingOptions) =

    let Serialize (req: Request): string =
        JsonConvert.SerializeObject(req, Formatting.None,
                                    marshallingOptions.PascalCase2LowercasePlusUnderscoreConversionSettings)

    // TODO: add 'T as incoming request type, leave 'R as outgoing response type
    member private self.Request<'R> (jsonRequest: string): Async<'R*string> = async {
        let! rawResponse = jsonRpcClient.Request jsonRequest
        return (StratumClient.Deserialize<'R> rawResponse marshallingOptions, rawResponse)
    }

    static member private DeserializeInternal<'T> (result: string) (marshallingOptions: MarshallingOptions): 'T =
        let resultTrimmed = result.Trim()
        (*
        let maybeError: Choice<ErrorResult, ErrorResultWithStringError> =
            let raiseDeserializationError (ex: Exception) =
                raise <| Exception(sprintf "Failed deserializing JSON response (to check for error) '%s' to type '%s'"
                                                   resultTrimmed typedefof<'T>.FullName, ex)
            try
                JsonConvert.DeserializeObject<ErrorResult>(resultTrimmed,
                                                           marshallingOptions.PascalCase2LowercasePlusUnderscoreConversionSettings)
                |> Choice1Of2
            with
            | :? JsonSerializationException ->
                try
                    JsonConvert.DeserializeObject<ErrorResultWithStringError>(resultTrimmed,
                                                               marshallingOptions.PascalCase2LowercasePlusUnderscoreConversionSettings)
                    |> Choice2Of2
                with
                | ex ->
                    raiseDeserializationError ex
            | ex ->
                raiseDeserializationError ex

        match maybeError with
        | Choice1Of2 errorResult when (not (Object.ReferenceEquals(errorResult, null))) && (not (Object.ReferenceEquals(errorResult.Error, null)))  ->
            raise <| ElectrumServerReturningErrorInJsonResponseException(errorResult.Error.Message, Some errorResult.Error.Code)
        | Choice2Of2 errorResultWithStringError when (not (Object.ReferenceEquals(errorResultWithStringError, null))) && (not (String.IsNullOrWhiteSpace errorResultWithStringError.Error)) ->
            raise <| ElectrumServerReturningErrorInJsonResponseException(errorResultWithStringError.Error, None)
        | _ -> ()
        *)
        let failedDeserMsg = sprintf "Failed deserializing JSON response '%s' to type '%s'"
                                      resultTrimmed typedefof<'T>.FullName
        let deserializedValue =
            try
                JsonConvert.DeserializeObject<'T>(resultTrimmed,
                                                  marshallingOptions.PascalCase2LowercasePlusUnderscoreConversionSettings)
            with
            (*
            | :? Newtonsoft.Json.JsonSerializationException as serEx ->
                let newEx = ElectrumServerReturningImproperJsonResponseException(failedDeserMsg, serEx)
#if !DEBUG
                Infrastructure.ReportWarning newEx
                |> ignore<bool>
#endif
                raise newEx
            *)
            | ex -> raise <| Exception(failedDeserMsg, ex)

        if Object.ReferenceEquals(deserializedValue, null) then
            failwithf "Failed deserializing JSON response '%s' to type '%s' (result was null)"
                      resultTrimmed typedefof<'T>.FullName

        deserializedValue

    // TODO: should this validation actually be part of JsonRpcSharp?
    static member public Deserialize<'T> (result: string) (marshallingOptions: MarshallingOptions): 'T =
        StratumClient.DeserializeInternal result marshallingOptions

    member self.BlockchainScriptHashGetBalance address: Async<BlockchainScriptHashGetBalanceResult> =
        let obj = {
            Id = 0;
            Method = "blockchain.scripthash.get_balance";
            Params = [address]
        }
        let json = Serialize obj

        async {
            let! resObj,_ = self.Request<BlockchainScriptHashGetBalanceResult> json
            return resObj
        }

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
        let obj = {
            Id = 0;
            Method = "server.version";
            Params = [clientName; protocolVersion.ToString()]
        }
        // this below serializes to:
        //  (SPrintF2 "{ \"id\": 0, \"method\": \"server.version\", \"params\": [ \"%s\", \"%s\" ] }"
        //      CURRENT_ELECTRUM_FAKED_VERSION PROTOCOL_VERSION)
        let json = Serialize obj
        let! resObj, rawResponse = self.Request<ServerVersionResult> json

        if Object.ReferenceEquals (resObj, null) then
            failwithf "resObj is null?? raw response was %s" rawResponse

        if Object.ReferenceEquals (resObj.Result, null) then
            failwithf "resObj.Result is null?? raw response was %s" rawResponse

        // resObj.Result.[0] is e.g. "ElectrumX 1.4.3"
        // e.g. "1.1"
        let serverProtocolVersion = resObj.Result.[1]

        return StratumClient.CreateVersion(serverProtocolVersion)
    }

    member self.BlockchainScriptHashListUnspent address: Async<BlockchainScriptHashListUnspentResult> =
        let obj = {
            Id = 0;
            Method = "blockchain.scripthash.listunspent";
            Params = [address]
        }
        let json = Serialize obj
        async {
            let! resObj,_ = self.Request<BlockchainScriptHashListUnspentResult> json
            return resObj
        }

    member self.BlockchainTransactionGet txHash: Async<BlockchainTransactionGetResult> =
        let obj = {
            Id = 0;
            Method = "blockchain.transaction.get";
            Params = [txHash]
        }
        let json = Serialize obj
        async {
            let! resObj,_ = self.Request<BlockchainTransactionGetResult> json
            return resObj
        }

    // DON'T DELETE, used in external projects
    member self.BlockchainTransactionIdFromPos height txPos: Async<BlockchainTransactionIdFromPosResult> =
        let obj = {
            Id = 0;
            Method = "blockchain.transaction.id_from_pos";
            Params = [height :> obj; txPos :> obj]
        }
        let json = Serialize obj
        async {
            let! resObj,_ = self.Request<BlockchainTransactionIdFromPosResult> json
            return resObj
        }

    // NOTE: despite Electrum-X official docs claiming that this method is deprecated... it's not! go read the official
    //       non-shitcoin forked version of the docs: https://electrumx-spesmilo.readthedocs.io/en/latest/protocol-methods.html#blockchain-estimatefee
    member self.BlockchainEstimateFee (numBlocksTarget: int): Async<BlockchainEstimateFeeResult> =
        let obj = {
            Id = 0;
            Method = "blockchain.estimatefee";
            Params = [numBlocksTarget]
        }
        let json = Serialize obj

        async {
            let! resObj,_ = self.Request<BlockchainEstimateFeeResult> json
            return resObj
        }

    member self.BlockchainTransactionBroadcast txInHex: Async<BlockchainTransactionBroadcastResult> =
        let obj = {
            Id = 0;
            Method = "blockchain.transaction.broadcast";
            Params = [txInHex]
        }
        let json = Serialize obj

        async {
            let! resObj,_ = self.Request<BlockchainTransactionBroadcastResult> json
            return resObj
        }
