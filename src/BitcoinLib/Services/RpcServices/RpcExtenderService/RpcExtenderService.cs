﻿// Copyright (c) 2014 George Kimionis
// Distributed under the GPLv3 software license, see the accompanying file LICENSE or http://opensource.org/licenses/GPL-3.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BitcoinLib.Auxiliary;
using BitcoinLib.ExceptionHandling.RpcExtenderService;
using BitcoinLib.ExtensionMethods;
using BitcoinLib.Requests.CreateRawTransaction;
using BitcoinLib.Responses;
using BitcoinLib.Responses.SharedComponents;
using BitcoinLib.RPC.Specifications;
using BitcoinLib.Services.Coins.Base;

namespace BitcoinLib.Services
{
    public partial class CoinService
    {
        //  Note: This will return funky results if the address in question along with its private key have been used to create a multisig address with unspent funds
        public Decimal GetAddressBalance(String inWalletAddress, Int32 minConf, Boolean validateAddressBeforeProcessing)
        {
            if (validateAddressBeforeProcessing)
            {
                ValidateAddressResponse validateAddressResponse = ValidateAddress(inWalletAddress);

                if (!validateAddressResponse.IsValid)
                {
                    throw new GetAddressBalanceException(String.Format("Address {0} is invalid!", inWalletAddress));
                }

                if (!validateAddressResponse.IsMine)
                {
                    throw new GetAddressBalanceException(String.Format("Address {0} is not an in-wallet address!", inWalletAddress));
                }
            }

            List<ListUnspentResponse> listUnspentResponses = ListUnspent(minConf, 9999999, new List<String>
                {
                    inWalletAddress
                });

            return listUnspentResponses.Any() ? listUnspentResponses.Sum(x => x.Amount) : 0;
        }

        public String GetImmutableTxId(String txId, Boolean getSha256Hash)
        {
            GetRawTransactionResponse response = GetRawTransaction(txId, 1);
            String text = response.Vin.First().TxId + "|" + response.Vin.First().Vout + "|" + response.Vout.First().Value;
            return getSha256Hash ? Hashing.GetSha256(text) : text;
        }

        //  Get a rough estimate on fees for non-free txs, depending on the total number of tx inputs and outputs
        [Obsolete("Please don't use this method to calculate tx fees, its purpose is to provide a rough estimate only")]
        public Decimal GetMinimumNonZeroTransactionFeeEstimate(Int16 numberOfInputs = 1, Int16 numberOfOutputs = 1)
        {
            CreateRawTransactionRequest rawTransactionRequest = new CreateRawTransactionRequest(new List<CreateRawTransactionInput>(numberOfInputs), new Dictionary<String, Decimal>(numberOfOutputs));

            for (Int16 i = 0; i < numberOfInputs; i++)
            {
                rawTransactionRequest.AddInput(new CreateRawTransactionInput { TxId = "dummyTxId" + i.ToString(CultureInfo.InvariantCulture), Vout = i });
            }

            for (Int16 i = 0; i < numberOfOutputs; i++)
            {
                rawTransactionRequest.AddOutput(new CreateRawTransactionOutput { Address = "dummyAddress" + i.ToString(CultureInfo.InvariantCulture), Amount = i + 1 });
            }

            return GetTransactionFee(rawTransactionRequest, false, true);
        }

        public Dictionary<String, String> GetMyPublicAndPrivateKeyPairs()
        {
            const Int16 secondsToUnlockTheWallet = 30;
            Dictionary<String, String> keyPairs = new Dictionary<String, String>();
            WalletPassphrase(Parameters.WalletPassword, secondsToUnlockTheWallet);
            List<ListReceivedByAddressResponse> myAddresses = (this as ICoinService).ListReceivedByAddress(0, true);

            foreach (ListReceivedByAddressResponse listReceivedByAddressResponse in myAddresses)
            {
                ValidateAddressResponse validateAddressResponse = ValidateAddress(listReceivedByAddressResponse.Address);

                if (validateAddressResponse.IsMine && validateAddressResponse.IsValid && !validateAddressResponse.IsScript)
                {
                    String privateKey = DumpPrivKey(listReceivedByAddressResponse.Address);
                    keyPairs.Add(validateAddressResponse.PubKey, privateKey);
                }
            }

            WalletLock();
            return keyPairs;
        }

        //  Note: As RPC's gettransaction works only for in-wallet transactions this had to be extended so it will work for every single transaction.
        public DecodeRawTransactionResponse GetPublicTransaction(String txId)
        {
            String rawTransaction = GetRawTransaction(txId, 0).Hex;
            return DecodeRawTransaction(rawTransaction);
        }

        [Obsolete("Please use EstimateFee() instead. You can however keep on using this method until the network fully adjusts to the new rules on fee calculation")]
        public Decimal GetTransactionFee(CreateRawTransactionRequest transaction, Boolean checkIfTransactionQualifiesForFreeRelay, Boolean enforceMinimumTransactionFeePolicy)
        {
            if (checkIfTransactionQualifiesForFreeRelay && IsTransactionFree(transaction))
            {
                return 0;
            }

            Decimal transactionSizeInBytes = GetTransactionSizeInBytes(transaction);
            Decimal transactionFee = ((transactionSizeInBytes / Parameters.FreeTransactionMaximumSizeInBytes) + (transactionSizeInBytes % Parameters.FreeTransactionMaximumSizeInBytes == 0 ? 0 : 1)) * Parameters.FeePerThousandBytesInCoins;

            if (transactionFee.GetNumberOfDecimalPlaces() > Parameters.CoinsPerBaseUnit.GetNumberOfDecimalPlaces())
            {
                transactionFee = Math.Round(transactionFee, Parameters.CoinsPerBaseUnit.GetNumberOfDecimalPlaces(), MidpointRounding.AwayFromZero);
            }

            if (enforceMinimumTransactionFeePolicy && Parameters.MinimumTransactionFeeInCoins != 0 && transactionFee < Parameters.MinimumTransactionFeeInCoins)
            {
                transactionFee = Parameters.MinimumTransactionFeeInCoins;
            }

            return transactionFee;
        }

        public GetRawTransactionResponse GetRawTxFromImmutableTxId(String rigidTxId, Int32 listTransactionsCount, Int32 listTransactionsFrom, Boolean getRawTransactionVersbose, Boolean rigidTxIdIsSha256)
        {
            List<ListTransactionsResponse> allTransactions = (this as ICoinService).ListTransactions("*", listTransactionsCount, listTransactionsFrom);

            return (from listTransactionsResponse in allTransactions
                    where rigidTxId == GetImmutableTxId(listTransactionsResponse.TxId, rigidTxIdIsSha256)
                    select GetRawTransaction(listTransactionsResponse.TxId, getRawTransactionVersbose ? 1 : 0)).FirstOrDefault();
        }

        public Decimal GetTransactionPriority(CreateRawTransactionRequest transaction)
        {
            if (transaction.Inputs.Count == 0)
            {
                return 0;
            }

            List<ListUnspentResponse> unspentInputs = (this as ICoinService).ListUnspent(0).ToList();
            Decimal sumOfInputsValueInBaseUnitsMultipliedByTheirAge = transaction.Inputs.Select(input => unspentInputs.First(x => x.TxId == input.TxId)).Select(unspentResponse => (unspentResponse.Amount * Parameters.OneCoinInBaseUnits) * unspentResponse.Confirmations).Sum();
            return sumOfInputsValueInBaseUnitsMultipliedByTheirAge / GetTransactionSizeInBytes(transaction);
        }

        public Decimal GetTransactionPriority(IList<ListUnspentResponse> transactionInputs, Int32 numberOfOutputs)
        {
            if (transactionInputs.Count == 0)
            {
                return 0;
            }

            return transactionInputs.Sum(input => input.Amount * Parameters.OneCoinInBaseUnits * input.Confirmations) / GetTransactionSizeInBytes(transactionInputs.Count, numberOfOutputs);
        }

        //  Note: Be careful when using GetTransactionSenderAddress() as it just gives you an address owned by someone who previously controlled the transaction's outputs
        //  which might not actually be the sender (e.g. for e-wallets) and who may not intend to receive anything there in the first place. 
        [Obsolete("Please don't use this method in production enviroment, it's for testing purposes only")]
        public String GetTransactionSenderAddress(String txId)
        {
            String rawTransaction = GetRawTransaction(txId, 0).Hex;
            DecodeRawTransactionResponse decodedRawTransaction = DecodeRawTransaction(rawTransaction);
            List<Vin> transactionInputs = decodedRawTransaction.Vin;
            String rawTransactionHex = GetRawTransaction(transactionInputs[0].TxId, 0).Hex;
            DecodeRawTransactionResponse inputDecodedRawTransaction = DecodeRawTransaction(rawTransactionHex);
            List<Vout> vouts = inputDecodedRawTransaction.Vout;
            return vouts[0].ScriptPubKey.Addresses[0];
        }

        public Int32 GetTransactionSizeInBytes(CreateRawTransactionRequest transaction)
        {
            return GetTransactionSizeInBytes(transaction.Inputs.Count, transaction.Outputs.Count);
        }

        public Int32 GetTransactionSizeInBytes(Int32 numberOfInputs, Int32 numberOfOutputs)
        {
            return numberOfInputs * Parameters.TransactionSizeBytesContributedByEachInput
                   + numberOfOutputs * Parameters.TransactionSizeBytesContributedByEachOutput
                   + Parameters.TransactionSizeFixedExtraSizeInBytes
                   + numberOfInputs;
        }
        
        public Boolean IsInWalletTransaction(String txId)
        {
            //  Note: This might not be efficient if iterated, consider caching ListTransactions' results.
            return (this as ICoinService).ListTransactions(null, Int32.MaxValue).Any(listTransactionsResponse => listTransactionsResponse.TxId == txId);
        }

        public Boolean IsTransactionFree(CreateRawTransactionRequest transaction)
        {
            return transaction.Outputs.Any(x => x.Value < Parameters.FreeTransactionMinimumOutputAmountInCoins)
                   && GetTransactionSizeInBytes(transaction) < Parameters.FreeTransactionMaximumSizeInBytes
                   && GetTransactionPriority(transaction) > Parameters.FreeTransactionMinimumPriority;
        }

        public Boolean IsTransactionFree(IList<ListUnspentResponse> transactionInputs, Int32 numberOfOutputs, Decimal minimumAmountAmongOutputs)
        {
            return minimumAmountAmongOutputs < Parameters.FreeTransactionMinimumOutputAmountInCoins
                   && GetTransactionSizeInBytes(transactionInputs.Count, numberOfOutputs) < Parameters.FreeTransactionMaximumSizeInBytes
                   && GetTransactionPriority(transactionInputs, numberOfOutputs) > Parameters.FreeTransactionMinimumPriority;
        }

        public Boolean IsWalletEncrypted()
        {
            return !Help(RpcMethods.walletlock.ToString()).Contains("unknown command");
        }
    }
}