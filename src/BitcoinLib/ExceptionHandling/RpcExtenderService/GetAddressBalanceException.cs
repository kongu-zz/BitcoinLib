﻿// Copyright (c) 2014 George Kimionis
// Distributed under the GPLv3 software license, see the accompanying file LICENSE or http://opensource.org/licenses/GPL-3.0

using System;

namespace BitcoinLib.ExceptionHandling.RpcExtenderService
{
    public class GetAddressBalanceException : Exception
    {
        public GetAddressBalanceException()
        {
        }

        public GetAddressBalanceException(String customMessage) : base(customMessage)
        {
        }

        public GetAddressBalanceException(String customMessage, Exception exception) : base(customMessage, exception)
        {
        }
    }
}