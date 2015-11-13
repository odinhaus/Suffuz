using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Diagnostics
{
    /// <summary>
    /// Put the counter names here to ensure that they are easy to manage
    /// </summary>
    public static class PerformanceCounterNames
    {
        /// <summary>
        /// The number of all device connections including zones and desktop connections
        /// </summary>
        public const String NUMBEROFDEVICECONNECTIONS_NAME = "# Device Connections";
        /// <summary>
        /// The number of all device connections including zones and desktop connections - help
        /// </summary>
        public const String NUMBEROFDEVICECONNECTIONS_HELP = "The number of all device connections";
        /// <summary>
        /// The receive queue depth
        /// </summary>
        public const String NUMBEROFQUEUEDDEVICEMESSAGESRECEIVE_NAME = "# Queued Device Messages - Receive";
        /// <summary>
        /// The receive queue depth - help
        /// </summary>
        public const String NUMBEROFQUEUEDDEVICEMESSAGESRECEIVE_HELP = "The receive queue depth";

        public const string TCP_NUMBEROFMESSAGESSENT_NAME = "# Messages - Sent";
        public const string TCPNUMBEROFMESSAGESSENT_HELP = "The total number of messages sent on all connections";

        public const string TCP_RateOfMessagesSent_NAME = "TCP: Rate of Message - Sent";
        public const string TCP_RateOfMessagesSent_HELP = "The average rate of messages sent on all connections";

        public const string TCP_NUMBEROFMESSAGESRECEIVED_NAME = "TCP: # Messages - Received";
        public const string TCP_NUMBEROFMESSAGESRECEIVED_HELP = "The total number of messages received on all connections";

        public const string TCP_RateOfMessagesReceived_NAME = "TCP: Rate of Messages - Received";
        public const string TCP_RateOfMessagesReceived_HELP = "The average rate of messages received on all connections";

        public const string TCP_NUMBEROFBYTESSENT_NAME = "TCP: # Bytes - Sent";
        public const string TCP_NUMBEROFBYTESSENT_HELP = "The total number of bytes sent on all connections";

        public const string TCP_NUMBEROFBYTESRECEIVED_NAME = "TCP: # Bytes - Received";
        public const string TCP_NUMBEROFBYTESRECEIVED_HELP = "The total number of bytes received on all connections";

        public const string TCP_NUMBEROFBYTESTOTAL_NAME = "TCP: # Bytes - Total";
        public const string TCP_NUMBEROFBYTESTOTAL_HELP = "The total number of bytes sent and received on all connections";

        public const string TCP_RateOfBytesSent_NAME = "TCP: Rate of Bytes - Sent";
        public const string TCP_RateOfBytesSent_HELP = "The average rate of bytes sent on all connections";

        public const string TCP_RateOfBytesReceived_NAME = "TCP: Rate of Bytes - Received";
        public const string TCP_RateOfBytesReceived_HELP = "The average rate of bytes received on all connections";

        public const string TCP_RateOfBytesTotal_NAME = "TCP: Rate of Bytes - Total";
        public const string TCP_RateOfBytesTotal_HELP = "The average rate of bytes sent and received on all connections";

        public const string UDP_NUMBEROFMESSAGESSENT_NAME = "UDP: # Messages - Sent";
        public const string UDPNUMBEROFMESSAGESSENT_HELP = "The total number of messages sent on all connections";

        public const string UDP_RateOfMessagesSent_NAME = "UDP: Rate of Message - Sent";
        public const string UDP_RateOfMessagesSent_HELP = "The average rate of messages sent on all connections";

        public const string UDP_NUMBEROFMESSAGESRECEIVED_NAME = "UDP: # Messages - Received";
        public const string UDP_NUMBEROFMESSAGESRECEIVED_HELP = "The total number of messages received on all connections";

        public const string UDP_RateOfMessagesReceived_NAME = "UDP: Rate of Messages - Received";
        public const string UDP_RateOfMessagesReceived_HELP = "The average rate of messages received on all connections";

        public const string UDP_NUMBEROFBYTESSENT_NAME = "UDP: # Bytes - Sent";
        public const string UDP_NUMBEROFBYTESSENT_HELP = "The total number of bytes sent on all connections";

        public const string UDP_NUMBEROFBYTESRECEIVED_NAME = "UDP: # Bytes - Received";
        public const string UDP_NUMBEROFBYTESRECEIVED_HELP = "The total number of bytes received on all connections";

        public const string UDP_NUMBEROFBYTESTOTAL_NAME = "UDP: # Bytes - Total";
        public const string UDP_NUMBEROFBYTESTOTAL_HELP = "The total number of bytes sent and received on all connections";

        public const string UDP_RateOfBytesSent_NAME = "UDP: Rate of Bytes - Sent";
        public const string UDP_RateOfBytesSent_HELP = "The average rate of bytes sent on all connections";

        public const string UDP_RateOfBytesReceived_NAME = "UDP: Rate of Bytes - Received";
        public const string UDP_RateOfBytesReceived_HELP = "The average rate of bytes received on all connections";

        public const string UDP_RateOfBytesTotal_NAME = "UDP: Rate of Bytes - Total";
        public const string UDP_RateOfBytesTotal_HELP = "The average rate of bytes sent and received on all connections";

        public const string Realtime_RawInsertRate_NAME = "Rate of Fields Inserted (Uncompressed)";
        public const string Realtime_RawInsertRate_HELP = "The average rate of field values being inserted on all Objects without compression";

        public const string Realtime_RawReadRate_NAME = "Rate of Fields Read (Uncompressed)";
        public const string Realtime_RawReadRate_HELP = "The average rate of field values being read from disk";

        public const string Realtime_HueristicReadJumps_NAME = "Hueristic Read Jump Size";
        public const string Realtime_HueristicReadJumps_HELP = "The number of estimated storage jumps to apply during read";

        public const string Realtime_HueristicReadVarianceThreshold_NAME = "Hueristic Read Variance Threshold";
        public const string Realtime_HueristicReadVarianceThreshold_HELP = "The minimum variance threshold to enable hueristic reading";

        public const string Realtime_HueristicReadVariance_NAME = "Hueristic Read Variance (Current Value)";
        public const string Realtime_HueristicReadVariance_HELP = "The current hueristic reading variance";

        public const string Realtime_HueristicReadAverageTime_NAME = "Hueristic Read Average Time (Current Value)";
        public const string Realtime_HueristicReadAverageTime_HELP = "The current hueristic reading average time between samples";

        public const string Realtime_RawReadCount_NAME = "Count of Fields Read (Uncompressed)";
        public const string Realtime_RawReadCount_HELP = "The total count of field values read from disk";

        public const string Realtime_RawInsertRateCompressed_NAME = "Rate of Fields Inserted (Compressed)";
        public const string Realtime_RawInsertRateCompressed_HELP = "The average rate of field values being inserted on all Objects with compression (actual disk rate)";

        public const string Realtime_RawInsertBytesRateCompressed_NAME = "Rate of Bytes Inserted (Compressed)";
        public const string Realtime_RawInsertBytesRateCompressed_HELP = "The average rate of values in bytes being inserted on all Objects with compression (actual disk rate)";

        public const string Realtime_Compression_NAME = "Average Compression over all IO Points";
        public const string Realtime_Compression_HELP = "The average compression ratio across all IO Points in all Objects";

        public const string Realtime_DataBytesOnDisk_NAME = "Total number of bytes written to disk";
        public const string Realtime_DataBytesOnDisk_HELP = "The total bytes written to disk for all Objects";

        public const string Realtime_DataBytesInserted_NAME = "Total number of bytes submitted to be inserted per second";
        public const string Realtime_DataBytesInserted_HELP = "The total bytes submitted for insert for all Objects";

        public const string Realtime_DataBytesRead_NAME = "Total number of bytes read per second";
        public const string Realtime_DataBytesRead_HELP = "The total bytes read from disk per second";

        public const string Realtime_WriteCacheDepth_NAME = "Total number of inserts in the write cache queue";
        public const string Realtime_WriteCacheDepth_HELP = "The total number of inserts in the write cache queue for all Objects";

        public const string Realtime_CompressionRatio_NAME = "Current Compression Ratio";
        public const string Realtime_CompressionRatio_HELP = "The ratio of total fields written to disk vs the total fields submitted to be written to disk (Compressed / Uncompressed) expressed as 0% - 100%";

        public const string Realtime_CompressionRatioBase_NAME = "Current Compression Ratio Base";
        public const string Realtime_CompressionRatioBase_HELP = "The ratio of total fields written to disk vs the total fields submitted to be written to disk (Compressed / Uncompressed) expressed as 0% - 100%";


        /// <summary>
        /// The average time in milliseconds it takes to serialize a binary object
        /// </summary>
        public const String BINARYSERIALIZATIONTIME_NAME = "Binary Serialization Time";
        /// <summary>
        /// The average time in milliseconds it takes to serialize a binary object - help
        /// </summary>
        public const String BINARYSERIALIZATIONTIME_HELP = "The average time in milliseconds it takes to serialize a binary object";
        /// <summary>
        /// The number of desktop connections to this CSGO server
        /// </summary>
        public const String REGISTEREDUSERS_NAME = "Registered Users";
        /// <summary>
        /// The number of desktop connections to this CSGO server - help
        /// </summary>
        public const String REGISTEREDUSERS_HELP = "The number of desktop connections to this CSGO server";
    }
}
