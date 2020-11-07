using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using STUN.Attributes;
using System.IO;

namespace STUN
{
    public class STUNQueryErrorException : Exception
    {
        public STUNQueryErrorException(STUNQueryError error) : base()
        {
            this.Error = error;
        }

        public STUNQueryError Error { get; private set; }
    }

    public class STUNServerErrorException : Exception
    {
        public STUNServerErrorException(STUNErrorCodes error, string errorPhrase) : base(errorPhrase)
        {
            this.ErrorCode = error;
            this.ErrorPhrase = errorPhrase;
        }

        public STUNErrorCodes ErrorCode { get; private set; }

        public string ErrorPhrase { get; private set; }
    }

    /// <summary>
    /// Implements a STUN client.
    /// </summary>
    public static class STUNClient
    {
        /// <summary>
        /// Period of time in miliseconds to wait for server response.
        /// </summary>
        public static int ReceiveTimeout = 5000;

        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        /// <param name="closeSocket">
        /// Set to true if created socket should closed after the query
        /// else <see cref="STUNQueryResult.Socket"/> will leave open and can be used.
        /// </param>
        public static Task<STUNQueryResult> QueryAsync(IPEndPoint server, STUNQueryType queryType, bool closeSocket)
        {
            return Task.Run(() => Query(server, queryType, closeSocket));
        }

        /// <param name="socket">A UDP <see cref="Socket"/> that will use for query. You can also use <see cref="UdpClient.Client"/></param>
        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        public static Task<STUNQueryResult> QueryAsync(Socket socket, IPEndPoint server, STUNQueryType queryType,
            NATTypeDetectionRFC natTypeDetectionRfc)
        {
            return Task.Run(() => Query(socket, server, queryType, natTypeDetectionRfc));
        }

        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        /// <param name="closeSocket">
        /// Set to true if created socket should closed after the query
        /// else <see cref="STUNQueryResult.Socket"/> will leave open and can be used.
        /// </param>
        public static Task<STUNQueryFullResult> TryQueryAsync(IPEndPoint server, STUNQueryType queryType, bool closeSocket)
        {
            return Task.Run(() => QueryInternal(server, queryType, closeSocket));
        }

        /// <param name="socket">A UDP <see cref="Socket"/> that will use for query. You can also use <see cref="UdpClient.Client"/></param>
        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        public static Task<STUNQueryFullResult> TryQueryAsync(Socket socket, IPEndPoint server, STUNQueryType queryType,
            NATTypeDetectionRFC natTypeDetectionRfc)
        {
            return Task.Run(() => QueryInternal(socket, server, queryType, natTypeDetectionRfc));
        }

        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        /// <param name="closeSocket">
        /// Set to true if created socket should closed after the query
        /// else <see cref="STUNQueryResult.Socket"/> will leave open and can be used.
        /// </param>
        public static STUNQueryResult Query(IPEndPoint server, STUNQueryType queryType, bool closeSocket,
            NATTypeDetectionRFC natTypeDetectionRfc = NATTypeDetectionRFC.Rfc3489)
        {
            var result = QueryInternal(server, queryType, closeSocket, natTypeDetectionRfc);
            if (result.QueryError != STUNQueryError.None)
                throw new STUNQueryErrorException(result.QueryError);
            if (result.ServerError != STUNErrorCodes.None)
                throw new STUNServerErrorException(result.ServerError, result.ServerErrorPhrase);

            return result;
        }

        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        /// <param name="closeSocket">
        /// Set to true if created socket should closed after the query
        /// else <see cref="STUNQueryResult.Socket"/> will leave open and can be used.
        /// </param>
        public static STUNQueryFullResult TryQuery(IPEndPoint server, STUNQueryType queryType, bool closeSocket,
            NATTypeDetectionRFC natTypeDetectionRfc = NATTypeDetectionRFC.Rfc3489)
        {
            return QueryInternal(server, queryType, closeSocket, natTypeDetectionRfc);
        }

        private static STUNQueryFullResult QueryInternal(IPEndPoint server, STUNQueryType queryType, bool closeSocket,
            NATTypeDetectionRFC natTypeDetectionRfc = NATTypeDetectionRFC.Rfc3489)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint bindEndPoint = new IPEndPoint(IPAddress.Any, 0);
            socket.Bind(bindEndPoint);

            var result = QueryInternal(socket, server, queryType, natTypeDetectionRfc);

            if (closeSocket)
            {
                socket.Dispose();
                result.Socket = null;
            }

            return result;
        }

        /// <param name="socket">A UDP <see cref="Socket"/> that will use for query. You can also use <see cref="UdpClient.Client"/></param>
        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        /// <param name="natTypeDetectionRfc">Rfc algorithm type</param>
        public static STUNQueryResult Query(Socket socket, IPEndPoint server, STUNQueryType queryType,
            NATTypeDetectionRFC natTypeDetectionRfc)
        {
            var result = QueryInternal(socket, server, queryType, natTypeDetectionRfc);
            if (result.QueryError != STUNQueryError.None)
                throw new STUNQueryErrorException(result.QueryError);
            if (result.ServerError != STUNErrorCodes.None)
                throw new STUNServerErrorException(result.ServerError, result.ServerErrorPhrase);

            return result;
        }

        /// <param name="socket">A UDP <see cref="Socket"/> that will use for query. You can also use <see cref="UdpClient.Client"/></param>
        /// <param name="server">Server address</param>
        /// <param name="queryType">Query type</param>
        /// <param name="natTypeDetectionRfc">Rfc algorithm type</param>
        public static STUNQueryFullResult TryQuery(Socket socket, IPEndPoint server, STUNQueryType queryType,
            NATTypeDetectionRFC natTypeDetectionRfc)
        {
            return QueryInternal(socket, server, queryType, natTypeDetectionRfc);
        }

        private static STUNQueryFullResult QueryInternal(Socket socket, IPEndPoint server, STUNQueryType queryType,
            NATTypeDetectionRFC natTypeDetectionRfc)
        {
            if (natTypeDetectionRfc == NATTypeDetectionRFC.Rfc3489)
            {
                return STUNRfc3489.Query(socket, server, queryType, ReceiveTimeout);
            }

            if (natTypeDetectionRfc == NATTypeDetectionRFC.Rfc5780)
            {
                return STUNRfc5780.Query(socket, server, queryType, ReceiveTimeout);
            }

            throw new Exception($"Unexpected RFC type {natTypeDetectionRfc}");
        }
    }
}