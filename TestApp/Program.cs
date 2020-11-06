﻿using System;
using System.Net;
using STUN;
using STUN.Attributes;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!STUNUtils.TryParseHostAndPort("stun.schlund.de:3478", out IPEndPoint stunEndPoint))
                throw new Exception("Failed to resolve STUN server address");

            STUNClient.ReceiveTimeout = 500;
            STUNQueryFullResult queryResult;
            try
            {
                queryResult = STUNClient.TryQuery(stunEndPoint, STUNQueryType.ExactNAT, true, NATTypeDetectionRFC.Rfc5780);
            }
            catch (STUNQueryErrorException ex)
            {
                throw new Exception("Query Error: " + ex.Error.ToString());
            }
            catch (STUNServerErrorException ex)
            {
                throw new Exception($"Server Error ({ex.ErrorCode}): {ex.ErrorPhrase}");
            }

            Console.WriteLine("PublicEndPoint: {0}", queryResult.PublicEndPoint);
            Console.WriteLine("LocalEndPoint: {0}", queryResult.LocalEndPoint);
            Console.WriteLine("NAT Type: {0}", queryResult.NATType);
        }
    }
}
