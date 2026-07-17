using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace atp_enterprise_app_wpf.Services.TestModules
{
    /// <summary>
    /// Error classification for communication tests.
    /// </summary>
    public enum CommErrorType
    {
        None,
        Timeout,
        DeviceDisconnected,
        DataCorruption,
        ProtocolMismatch,
        AccessDenied,
        HardwareFailure,
        Unknown
    }

    /// <summary>
    /// Shared communication statistics for active interface tests.
    /// </summary>
    public class CommStats
    {
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }
        public int RetryCount { get; set; }
        public int ErrorCount { get; set; }
        public CommErrorType LastErrorType { get; set; } = CommErrorType.None;
        public string LastErrorMessage { get; set; } = string.Empty;
        public bool IntegrityVerified { get; set; }
        public DateTime? LastSuccessfulTransaction { get; set; }
        public double ThroughputMbps { get; set; }

        public string ToSummary()
        {
            var sb = new StringBuilder();
            sb.Append($"Sent: {FormatBytes(BytesSent)} | Recv: {FormatBytes(BytesReceived)}");
            if (ThroughputMbps > 0) sb.Append($" | {ThroughputMbps:F2} Mbps");
            sb.Append($" | Integrity: {(IntegrityVerified ? "Verified" : "Not Verified")}");
            if (ErrorCount > 0) sb.Append($" | Errors: {ErrorCount} ({LastErrorType})");
            if (RetryCount > 0) sb.Append($" | Retries: {RetryCount}");
            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1048576) return $"{bytes / 1048576.0:F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }

    /// <summary>
    /// Shared checksum utilities for data integrity verification.
    /// </summary>
    public static class ChecksumUtil
    {
        public static uint ComputeCrc32(byte[] data, int length = -1)
        {
            int len = length < 0 ? data.Length : Math.Min(length, data.Length);
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < len; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
            }
            return ~crc;
        }

        public static uint ComputeAdler32(byte[] data, int length = -1)
        {
            int len = length < 0 ? data.Length : Math.Min(length, data.Length);
            uint a = 1, b = 0;
            for (int i = 0; i < len; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        public static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "");
        }

        /// <summary>
        /// Generates a deterministic test payload of the given size.
        /// Each byte is pattern-filled for integrity verification.
        /// </summary>
        public static byte[] GenerateTestPayload(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            for (int i = 0; i < sizeBytes; i++)
            {
                data[i] = (byte)((i * 7 + 13) % 256);
            }
            return data;
        }

        /// <summary>
        /// Verifies a test payload matches the expected deterministic pattern.
        /// </summary>
        public static bool VerifyTestPayload(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != (byte)((i * 7 + 13) % 256))
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Retry policy for communication tests.
    /// </summary>
    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public int RetryIntervalMs { get; set; } = 500;
        public int TimeoutMs { get; set; } = 5000;

        public static RetryPolicy Default => new();

        public async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation,
            CancellationToken token, Action<int>? onRetry = null)
        {
            Exception? lastException = null;
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        onRetry?.Invoke(attempt);
                        await Task.Delay(RetryIntervalMs, token);
                    }

                    using var timeoutCts = new CancellationTokenSource(TimeoutMs);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                    return await operation(linked.Token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw; // User-initiated cancellation, don't retry
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }
            throw lastException ?? new Exception("Operation failed after retries.");
        }
    }
}
